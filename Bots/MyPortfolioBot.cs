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

    public MyPortfoliBot(ConversationState conversationState, UserState userState, HttpClient httpClient, IConfiguration config)
    {
        _conversationState = conversationState;
        _userState = userState;
        _httpClient = httpClient;

        var apiKey = config["GeminiApiKey"];
        Console.WriteLine($"---> LOADED API KEY STARTS WITH: {apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0))}");
        var apiUrl = config["GeminiApiUrl"];

        _apiUrl = $"{apiUrl}?key={apiKey}";
        Console.WriteLine($"---> LOADED _apiUrl : {_apiUrl}");
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
        
        // Call the LLM
        var aiResponse = await GetAiResponseAsync(userQuery);
        
        // Send the response back to the user
        await turnContext.SendActivityAsync(MessageFactory.Text(aiResponse), cancellationToken);
    }

    // 3. THE AI ENGINE
    private async Task<string> GetAiResponseAsync(string prompt)
    {
        try
        {
            var promptPathFile = System.IO.Path.Combine("Prompts", "SystemPrompt.txt");
            
            // Added the 'await' keyword so it reads the text, not the Task object!
            var systemPrompt = await System.IO.File.ReadAllTextAsync(promptPathFile);
            
            var agentInput = $"{systemPrompt}\n\nRecruiter's question: {prompt}";

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
}