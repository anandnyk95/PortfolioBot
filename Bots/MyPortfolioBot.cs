using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;

namespace PortfolioBots.Bots;

public class MyPortfoliBot : ActivityHandler
{
    private readonly BotState _conversationState;
    private readonly BotState _userState;
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _ragApiUrl;


    public MyPortfoliBot(ConversationState conversationState, UserState userState, HttpClient httpClient, IConfiguration config)
    {
        _conversationState = conversationState;
        _userState = userState;
        _httpClient = httpClient;

        var apiKey = config["GeminiApiKey"];
        var apiUrl = config["GeminiApiUrl"];
        _apiUrl = $"{apiUrl}{apiKey}";
        _ragApiUrl = config["RagApiUrl"];
    }

    // 1. THE MIDDLEWARE: Runs for every single event
    public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        // Let the base class act as the switchboard. 
        // If it's a message, it will automatically call OnMessageActivityAsync below.
        await base.OnTurnAsync(turnContext, cancellationToken);
        
        // Save state at the very end of the turn, regardless of what kind of activity it was
        await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
    }

    // 2. THE BUSINESS LOGIC: Only runs when a user sends text
    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var userQuery = turnContext.Activity.Text;
        Console.WriteLine($"---> Recruiter asked: {userQuery}");

        // Step 1 — Get raw answer from RAG
        var ragAnswer = await GetResumeContextAsync(userQuery);
        Console.WriteLine($"---> RAG Raw Answer: {ragAnswer}");

        // Step 2 — Beautify via Gemini
        var finalAnswer = await SummarizeWithGeminiAsync(userQuery, ragAnswer);

        // Step 3 — Send to user
        await turnContext.SendActivityAsync(MessageFactory.Text(finalAnswer), cancellationToken);
    }

    // 3. THE AI ENGINE
    private async Task<string> GetAiResponseAsync(string prompt)
    {
        try
        {
            var promptPathFile = System.IO.Path.Combine("Prompts", "SystemPrompt.txt");
            
            // Added the 'await' keyword so it reads the text, not the Task object!
            var systemPrompt = await System.IO.File.ReadAllTextAsync(promptPathFile);

            // GET RESUME CONTEXT FROM RAG
            var resumeContext = await GetResumeContextAsync(prompt);
            Console.WriteLine($"---> RAG Context: {resumeContext}");
            
            var agentInput = $"{systemPrompt}\n\nResume Context:\n{resumeContext}\n\nRecruiter's question: {prompt}";

            var payload = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = agentInput } } }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"\n---> GOOGLE API REJECTED THE REQUEST:");
                Console.WriteLine(responseJson);
                Console.WriteLine($"--------------------------------------\n");
                return $"AI Error: {response.StatusCode}. Check the VS Code terminal for the exact reason.";
            }

            using var doc = JsonDocument.Parse(responseJson);
            var aiText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return aiText ?? "I processed that, but my brain came up blank.";
        }
        catch (Exception ex)
        {
            return $"Error calling LLM: {ex.Message}";
        }
    }

    private async Task<string> GetResumeContextAsync(string question)
    {
        try
        {
            var payload = new { question };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_ragApiUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("answer").GetString() ?? "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RAG Error: {ex.Message}");
            return "";
        }
    }

    private async Task<string> SummarizeWithGeminiAsync(string question, string ragContext)
    {
        try
        {
            // Read prompt from file and inject values
            var promptPath = System.IO.Path.Combine("Prompts", "SummarizationPrompt.txt");
            var promptTemplate = await System.IO.File.ReadAllTextAsync(promptPath);
            var prompt = promptTemplate
            .Replace("{question}", question)
            .Replace("{ragContext}", ragContext);

            var payload = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"---> Gemini Error: {responseJson}");
                return ragContext; // fallback to raw RAG answer
            }

            using var doc = JsonDocument.Parse(responseJson);
            var aiText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return aiText ?? ragContext;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"---> Summarize Error: {ex.Message}");
            return ragContext; // fallback to raw RAG answer
        }
    }
}