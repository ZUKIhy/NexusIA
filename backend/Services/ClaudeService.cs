using System.Text;
using System.Text.Json;

namespace NexusBackend.Services;

public class ClaudeService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ClaudeService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public bool IsEnabled()
    {
        return (Environment.GetEnvironmentVariable("CLAUDE_ENABLED") ?? "false")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> AskAsync(string prompt, int maxTokens = 2048)
    {
        if (!IsEnabled())
            return null;

        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var model = Environment.GetEnvironmentVariable("CLAUDE_MODEL") ?? "claude-sonnet-4-6";

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Equals("coloque_sua_chave_aqui", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var payload = new
            {
                model,
                max_tokens = maxTokens,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);

            var response = await client.PostAsync(
                "https://api.anthropic.com/v1/messages",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("content", out var content))
            {
                foreach (var item in content.EnumerateArray())
                {
                    if (
                        item.TryGetProperty("type", out var type) &&
                        type.GetString() == "text" &&
                        item.TryGetProperty("text", out var text)
                    )
                    {
                        return text.GetString();
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
