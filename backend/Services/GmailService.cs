using System.Text.Json;

namespace NexusBackend.Services;

public class GmailService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GmailService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public object GetStatus() => new
    {
        enabled = IsEnabled(),
        configured = IsConfigured(),
        user = Environment.GetEnvironmentVariable("GMAIL_USER_ID") ?? "me"
    };

    public async Task<GmailBriefing> GetBriefingAsync()
    {
        if (!IsConfigured())
            return new GmailBriefing { Configured = false, Summary = "Gmail nao configurado." };

        var user = Environment.GetEnvironmentVariable("GMAIL_USER_ID") ?? "me";
        var query = Environment.GetEnvironmentVariable("GMAIL_BRIEFING_QUERY") ?? "newer_than:2d";
        var listUrl = $"https://gmail.googleapis.com/gmail/v1/users/{Uri.EscapeDataString(user)}/messages?maxResults=8&q={Uri.EscapeDataString(query)}";
        var client = _httpClientFactory.CreateClient();
        using var listResponse = await client.SendAsync(BuildRequest(listUrl));
        var listJson = await listResponse.Content.ReadAsStringAsync();

        if (!listResponse.IsSuccessStatusCode)
            return new GmailBriefing { Configured = true, Summary = "Falha ao consultar Gmail.", Error = listJson };

        using var listDoc = JsonDocument.Parse(listJson);
        var messages = new List<GmailMessageItem>();

        if (listDoc.RootElement.TryGetProperty("messages", out var items))
        {
            foreach (var item in items.EnumerateArray().Take(6))
            {
                var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var detailUrl = $"https://gmail.googleapis.com/gmail/v1/users/{Uri.EscapeDataString(user)}/messages/{Uri.EscapeDataString(id)}?format=metadata&metadataHeaders=Subject&metadataHeaders=From";
                using var detailResponse = await client.SendAsync(BuildRequest(detailUrl));
                var detailJson = await detailResponse.Content.ReadAsStringAsync();
                if (!detailResponse.IsSuccessStatusCode)
                    continue;

                messages.Add(ParseMessage(detailJson));
            }
        }

        return new GmailBriefing
        {
            Configured = true,
            Messages = messages,
            Summary = messages.Count == 0 ? "Nenhum email recente encontrado para o filtro configurado." : $"{messages.Count} email(s) recentes no resumo."
        };
    }

    private static HttpRequestMessage BuildRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GetAccessToken());
        return request;
    }

    private static GmailMessageItem ParseMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var result = new GmailMessageItem
        {
            Id = root.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            Snippet = root.TryGetProperty("snippet", out var snippet) ? snippet.GetString() ?? "" : ""
        };

        if (root.TryGetProperty("payload", out var payload) && payload.TryGetProperty("headers", out var headers))
        {
            foreach (var header in headers.EnumerateArray())
            {
                var name = header.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";
                var value = header.TryGetProperty("value", out var valueElement) ? valueElement.GetString() ?? "" : "";

                if (name.Equals("Subject", StringComparison.OrdinalIgnoreCase))
                    result.Subject = value;
                else if (name.Equals("From", StringComparison.OrdinalIgnoreCase))
                    result.From = value;
            }
        }

        return result;
    }

    private static bool IsEnabled() => (Environment.GetEnvironmentVariable("GMAIL_ENABLED") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);

    private static bool IsConfigured() => IsEnabled() && !string.IsNullOrWhiteSpace(GetAccessToken());

    private static string GetAccessToken() => Environment.GetEnvironmentVariable("GOOGLE_ACCESS_TOKEN") ?? "";
}

public class GmailBriefing
{
    public bool Configured { get; set; }
    public string Summary { get; set; } = "";
    public string Error { get; set; } = "";
    public List<GmailMessageItem> Messages { get; set; } = new();
}

public class GmailMessageItem
{
    public string Id { get; set; } = "";
    public string From { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Snippet { get; set; } = "";
}
