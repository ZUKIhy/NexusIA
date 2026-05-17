using System.Text;
using System.Text.Json;

namespace NexusBackend.Services;

public class OllamaService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OllamaService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public bool IsEnabled()
    {
        return (Environment.GetEnvironmentVariable("OLLAMA_ENABLED") ?? "false")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> AskAsync(string prompt)
    {
        if (!IsEnabled())
            return null;

        var url = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";
        var model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "nexus-qwen3b";

        try
        {
            var client = _httpClientFactory.CreateClient();

            var payload = new
            {
                model,
                prompt,
                stream = false,
                options = new
                {
                    num_ctx = 4096,
                    temperature = 0.3,
                    top_p = 0.9
                }
            };

            var json = JsonSerializer.Serialize(payload);

            var response = await client.PostAsync(
                $"{url}/api/generate",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("response", out var output))
                return output.GetString();

            return null;
        }
        catch
        {
            return null;
        }
    }
}
