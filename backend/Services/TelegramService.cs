using System.Text;
using System.Text.Json;

namespace NexusBackend.Services;

public class TelegramService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AlertService _alerts;
    private readonly NetworkMonitorService _network;
    private readonly TodayService _today;

    public TelegramService(IHttpClientFactory httpClientFactory, AlertService alerts, NetworkMonitorService network, TodayService today)
    {
        _httpClientFactory = httpClientFactory;
        _alerts = alerts;
        _network = network;
        _today = today;
    }

    public object GetStatus() => new
    {
        enabled = IsEnabled(),
        configured = IsConfigured(),
        chatConfigured = !string.IsNullOrWhiteSpace(GetChatId()),
        commands = new[] { "/status", "/network", "/today", "/alerts" }
    };

    public async Task<TelegramSendResult> SendMessageAsync(string message)
    {
        if (!IsConfigured())
            return new TelegramSendResult(false, "Telegram nao configurado. Defina TELEGRAM_BOT_TOKEN e TELEGRAM_CHAT_ID.");

        var payload = JsonSerializer.Serialize(new
        {
            chat_id = GetChatId(),
            text = message,
            parse_mode = "Markdown"
        });

        var client = _httpClientFactory.CreateClient();
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(BuildBotUrl("sendMessage"), content);
        var body = await response.Content.ReadAsStringAsync();

        return new TelegramSendResult(response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Mensagem enviada." : body);
    }

    public async Task<TelegramSendResult> SendAlertAsync(string title, string severity, string detail)
    {
        _alerts.Record("telegram", title, severity, detail, "Verificar alerta enviado pelo Telegram.");
        return await SendMessageAsync($"*Nexus Alert*\nSeveridade: {severity}\n{title}\n\n{detail}");
    }

    public async Task<IReadOnlyList<TelegramCommandResult>> GetCommandUpdatesAsync()
    {
        if (!IsConfigured())
            return Array.Empty<TelegramCommandResult>();

        var offset = Environment.GetEnvironmentVariable("TELEGRAM_UPDATES_OFFSET") ?? "";
        var url = BuildBotUrl("getUpdates") + (string.IsNullOrWhiteSpace(offset) ? "" : $"?offset={Uri.EscapeDataString(offset)}");
        var client = _httpClientFactory.CreateClient();
        var json = await client.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var results = new List<TelegramCommandResult>();

        if (!doc.RootElement.TryGetProperty("result", out var updates))
            return results;

        foreach (var update in updates.EnumerateArray())
        {
            var text = update.TryGetProperty("message", out var message) &&
                message.TryGetProperty("text", out var textElement)
                    ? textElement.GetString() ?? ""
                    : "";

            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith('/'))
                continue;

            results.Add(await ExecuteCommandAsync(text));
        }

        return results;
    }

    public async Task<TelegramCommandResult> ExecuteCommandAsync(string command)
    {
        var normalized = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? "";
        var answer = normalized switch
        {
            "/status" => "Nexus online. Use /today para briefing e /network para rede.",
            "/network" => BuildNetworkMessage(await _network.CheckNetworkStatusAsync()),
            "/today" => (await _today.BuildAsync()).Summary,
            "/alerts" => BuildAlertsMessage(),
            _ => "Comando nao reconhecido. Use /status, /network, /today ou /alerts."
        };

        if (IsConfigured())
            await SendMessageAsync(answer);

        return new TelegramCommandResult(command, answer);
    }

    private string BuildAlertsMessage()
    {
        var alerts = _alerts.GetRecentAlerts(5);
        if (alerts.Count == 0)
            return "Sem alertas recentes.";

        return string.Join("\n\n", alerts.Select(alert => $"{alert.Time} | {alert.Severity}\n{alert.Title}\n{alert.Detail}"));
    }

    private static string BuildNetworkMessage(NetworkStatusResult status)
    {
        return $"Rede: {status.Status}\nInternet: {(status.InternetOnline ? "online" : "offline")}\nMonitorados: {status.MonitoredDevices}\nOnline: {status.OnlineDevices}\nOffline: {status.OfflineDevices}\nDesconhecidos: {status.UnknownDevices.Count}";
    }

    private static bool IsEnabled() => (Environment.GetEnvironmentVariable("TELEGRAM_ENABLED") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);

    private static bool IsConfigured() => IsEnabled() &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")) &&
        !string.IsNullOrWhiteSpace(GetChatId());

    private static string GetChatId() => Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? "";

    private static string BuildBotUrl(string method)
    {
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? "";
        return $"https://api.telegram.org/bot{Uri.EscapeDataString(token)}/{method}";
    }
}

public record TelegramSendResult(bool Success, string Message);

public record TelegramCommandResult(string Command, string Answer);
