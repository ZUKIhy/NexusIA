using System.Text;
using System.Text.Json;

namespace NexusBackend.Services;

public class TelegramService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AlertService _alerts;
    private readonly NetworkMonitorService _network;
    private readonly TodayService _today;
    private readonly OpenAIService _openAI;
    private readonly OperationService _operation;
    private readonly IServiceProvider _serviceProvider;

    public TelegramService(
        IHttpClientFactory httpClientFactory,
        AlertService alerts,
        NetworkMonitorService network,
        TodayService today,
        OpenAIService openAI,
        OperationService operation,
        IServiceProvider serviceProvider)
    {
        _httpClientFactory = httpClientFactory;
        _alerts = alerts;
        _network = network;
        _today = today;
        _openAI = openAI;
        _operation = operation;
        _serviceProvider = serviceProvider;
    }

    public object GetStatus() => new
    {
        enabled = IsEnabled(),
        configured = IsConfigured(),
        chatConfigured = !string.IsNullOrWhiteSpace(GetChatId()),
        pollingEnabled = IsPollingEnabled(),
        commands = new[] { "/start", "/help", "/status", "/network", "/today", "/alerts", "/zabbix" }
    };

    public async Task<TelegramSendResult> SendMessageAsync(string message)
    {
        return await SendMessageToChatAsync(GetChatId(), message);
    }

    public async Task<TelegramSendResult> SendMessageToChatAsync(string chatId, string message)
    {
        if (!IsConfigured())
            return new TelegramSendResult(false, "Telegram nao configurado. Defina TELEGRAM_BOT_TOKEN e TELEGRAM_CHAT_ID.");

        if (string.IsNullOrWhiteSpace(chatId))
            return new TelegramSendResult(false, "chat_id nao informado.");

        var payload = JsonSerializer.Serialize(new
        {
            chat_id = chatId,
            text = message
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
        return await SendMessageAsync($"Nexus Alert\nSeveridade: {severity}\n{title}\n\n{detail}");
    }

    public async Task<IReadOnlyList<TelegramCommandResult>> GetCommandUpdatesAsync()
    {
        if (!IsConfigured())
            return Array.Empty<TelegramCommandResult>();

        var updates = await GetUpdatesAsync(null, CancellationToken.None);
        var results = new List<TelegramCommandResult>();

        foreach (var update in updates)
        {
            if (!update.Text.TrimStart().StartsWith('/'))
                continue;

            results.Add(await ExecuteCommandAsync(update.Text, update.ChatId));
        }

        return results;
    }

    public async Task<TelegramCommandResult> ExecuteCommandAsync(string command)
    {
        return await ExecuteCommandAsync(command, GetChatId());
    }

    public async Task<TelegramCommandResult> ExecuteCommandAsync(string command, string chatId)
    {
        var normalized = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? "";
        var answer = normalized switch
        {
            "/start" => BuildHelpMessage(),
            "/help" => BuildHelpMessage(),
            "/status" => "Nexus online. Use /today para briefing, /network para rede, /zabbix para monitoramento e /alerts para alertas.",
            "/network" => BuildNetworkMessage(await _network.CheckNetworkStatusAsync()),
            "/today" => (await _today.BuildAsync()).Summary,
            "/alerts" => BuildAlertsMessage(),
            "/zabbix" => await BuildZabbixMessageAsync(),
            _ => "Comando nao reconhecido. Use /status, /network, /today, /alerts ou /zabbix."
        };

        if (IsConfigured())
            await SendMessageToChatAsync(chatId, answer);

        return new TelegramCommandResult(command, answer);
    }

    public async Task<TelegramCommandResult> HandleIncomingMessageAsync(string chatId, string text)
    {
        if (!IsAllowedChat(chatId))
            return new TelegramCommandResult(text, "Chat nao autorizado.");

        if (text.TrimStart().StartsWith('/'))
            return await ExecuteCommandAsync(text, chatId);

        var answer = await BuildConversationalAnswerAsync(text);
        await SendMessageToChatAsync(chatId, answer);
        return new TelegramCommandResult(text, answer);
    }

    public async Task<IReadOnlyList<TelegramIncomingMessage>> GetUpdatesAsync(long? offset, CancellationToken cancellationToken)
    {
        if (!IsConfigured())
            return Array.Empty<TelegramIncomingMessage>();

        var parameters = new List<string>
        {
            "timeout=20",
            "allowed_updates=%5B%22message%22%5D"
        };

        if (offset.HasValue)
            parameters.Add($"offset={offset.Value}");

        var url = BuildBotUrl("getUpdates") + "?" + string.Join("&", parameters);
        var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(url, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return Array.Empty<TelegramIncomingMessage>();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("result", out var updates))
            return Array.Empty<TelegramIncomingMessage>();

        var messages = new List<TelegramIncomingMessage>();

        foreach (var update in updates.EnumerateArray())
        {
            var updateId = update.TryGetProperty("update_id", out var updateIdElement)
                ? updateIdElement.GetInt64()
                : 0;

            if (!update.TryGetProperty("message", out var message))
                continue;

            var text = message.TryGetProperty("text", out var textElement)
                ? textElement.GetString() ?? ""
                : "";

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var chatId = "";
            if (message.TryGetProperty("chat", out var chat) && chat.TryGetProperty("id", out var chatIdElement))
                chatId = chatIdElement.ToString();

            messages.Add(new TelegramIncomingMessage(updateId, chatId, text));
        }

        return messages;
    }

    public bool ShouldProcessExistingUpdates()
    {
        return (Environment.GetEnvironmentVariable("TELEGRAM_PROCESS_EXISTING_UPDATES") ?? "false")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPollingEnabled()
    {
        return (Environment.GetEnvironmentVariable("TELEGRAM_POLLING_ENABLED") ?? "true")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
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

    private async Task<string> BuildZabbixMessageAsync()
    {
        try
        {
            var zabbix = _serviceProvider.GetRequiredService<ZabbixService>();
            var report = await zabbix.GenerateReportAsync(4, saveToObsidian: false, notifyCritical: false);
            return $"{report.Summary}\n{report.Recommendation}";
        }
        catch
        {
            return "Nao consegui consultar o Zabbix agora.";
        }
    }

    private async Task<string> BuildConversationalAnswerAsync(string text)
    {
        var lower = text.ToLowerInvariant();

        if (lower.Contains("zabbix"))
            return await BuildZabbixMessageAsync();

        if (lower.Contains("rede") || lower.Contains("internet"))
            return BuildNetworkMessage(await _network.CheckNetworkStatusAsync());

        if (lower.Contains("alerta"))
            return BuildAlertsMessage();

        if (lower.Contains("briefing") || lower.Contains("hoje") || lower.Contains("dia"))
            return (await _today.BuildAsync()).Summary;

        var answer = await _openAI.AskAsync(text);
        return _operation.IsOperationMode()
            ? _operation.BuildOperationFrame(answer)
            : answer;
    }

    private static string BuildHelpMessage()
    {
        return """
        Nexus Telegram online.

        Comandos:
        /status - status rapido
        /today - briefing do dia
        /network - status da rede
        /zabbix - resumo Zabbix
        /alerts - alertas recentes

        Voce tambem pode falar comigo em texto normal por aqui.
        """;
    }

    private static bool IsEnabled() => (Environment.GetEnvironmentVariable("TELEGRAM_ENABLED") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);

    private static bool IsConfigured() => IsEnabled() &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")) &&
        !string.IsNullOrWhiteSpace(GetChatId());

    private static string GetChatId() => Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? "";

    private static bool IsAllowedChat(string chatId)
    {
        return !string.IsNullOrWhiteSpace(chatId) &&
            chatId.Equals(GetChatId(), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBotUrl(string method)
    {
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? "";
        return $"https://api.telegram.org/bot{Uri.EscapeDataString(token)}/{method}";
    }
}

public record TelegramSendResult(bool Success, string Message);

public record TelegramCommandResult(string Command, string Answer);

public record TelegramIncomingMessage(long UpdateId, string ChatId, string Text);
