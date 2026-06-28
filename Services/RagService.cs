using System.Text;
using System.Text.Json;

public class RagService
{
    private readonly HttpClient _httpClient;
    private const string RagApiUrl = "http://localhost:8000/query";

    public RagService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> AskAsync(string question)
    {
        var payload = new { question };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(RagApiUrl, content);
        var responseJson = await response.Content.ReadAsStringAsync();

        var result = JsonSerializer.Deserialize<RagResponse>(responseJson);
        return result?.Answer ?? "Sorry, I could not find an answer.";
    }
}

public class RagResponse
{
    public string Answer { get; set; } = "";
}