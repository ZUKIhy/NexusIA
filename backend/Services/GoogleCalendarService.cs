using System.Text.Json;

namespace NexusBackend.Services;

public class GoogleCalendarService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleCalendarService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public object GetStatus() => new
    {
        enabled = IsEnabled(),
        configured = IsConfigured(),
        calendarId = GetCalendarId()
    };

    public async Task<CalendarBriefing> GetBriefingAsync()
    {
        if (!IsConfigured())
            return new CalendarBriefing { Configured = false, Summary = "Google Calendar nao configurado." };

        var now = DateTimeOffset.Now;
        var end = now.AddDays(1);
        var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(GetCalendarId())}/events" +
            $"?singleEvents=true&orderBy=startTime&timeMin={Uri.EscapeDataString(now.ToString("O"))}&timeMax={Uri.EscapeDataString(end.ToString("O"))}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GetAccessToken());

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new CalendarBriefing { Configured = true, Summary = "Falha ao consultar Google Calendar.", Error = json };

        using var doc = JsonDocument.Parse(json);
        var events = new List<CalendarEventItem>();

        if (doc.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray().Take(8))
            {
                events.Add(new CalendarEventItem
                {
                    Title = item.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "Sem titulo" : "Sem titulo",
                    StartsAt = ReadGoogleDate(item, "start"),
                    EndsAt = ReadGoogleDate(item, "end"),
                    Location = item.TryGetProperty("location", out var location) ? location.GetString() ?? "" : ""
                });
            }
        }

        return new CalendarBriefing
        {
            Configured = true,
            Events = events,
            Summary = events.Count == 0 ? "Nenhum evento nas proximas 24 horas." : $"{events.Count} evento(s) nas proximas 24 horas."
        };
    }

    private static string ReadGoogleDate(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
            return "";

        if (value.TryGetProperty("dateTime", out var dateTime))
            return dateTime.GetString() ?? "";

        return value.TryGetProperty("date", out var date) ? date.GetString() ?? "" : "";
    }

    private static bool IsEnabled() => (Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_ENABLED") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);

    private static bool IsConfigured() => IsEnabled() && !string.IsNullOrWhiteSpace(GetAccessToken());

    private static string GetAccessToken() => Environment.GetEnvironmentVariable("GOOGLE_ACCESS_TOKEN") ?? "";

    private static string GetCalendarId() => Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_ID") ?? "primary";
}

public class CalendarBriefing
{
    public bool Configured { get; set; }
    public string Summary { get; set; } = "";
    public string Error { get; set; } = "";
    public List<CalendarEventItem> Events { get; set; } = new();
}

public class CalendarEventItem
{
    public string Title { get; set; } = "";
    public string StartsAt { get; set; } = "";
    public string EndsAt { get; set; } = "";
    public string Location { get; set; } = "";
}
