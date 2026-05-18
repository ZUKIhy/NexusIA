namespace NexusBackend.Services;

public class TodayService
{
    private readonly ObsidianService _obsidian;
    private readonly TaskService _tasks;
    private readonly DocumentIndexService _index;
    private readonly OperationService _operation;
    private readonly HomeAssistantService _home;
    private readonly NetworkMonitorService _network;
    private readonly AlertService _alerts;

    public TodayService(
        ObsidianService obsidian,
        TaskService tasks,
        DocumentIndexService index,
        OperationService operation,
        HomeAssistantService home,
        NetworkMonitorService network,
        AlertService alerts)
    {
        _obsidian = obsidian;
        _tasks = tasks;
        _index = index;
        _operation = operation;
        _home = home;
        _network = network;
        _alerts = alerts;
    }

    public async Task<TodayBriefing> BuildAsync()
    {
        var index = _index.BuildIndex();
        var tasksText = _tasks.ReadAll();
        var openTasks = tasksText.Split('\n')
            .Where(line => line.TrimStart().StartsWith("- [ ]"))
            .Select(line => line.Trim())
            .Take(8)
            .ToList();
        var network = await _network.CheckNetworkStatusAsync();
        var homeOnline = await _home.PingAsync();
        var recentAlerts = _alerts.GetRecentAlerts(8).ToList();
        var reviewItems = index
            .Where(item => item.Status.Equals("revisar", StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .Select(item => new TodayReviewItem { Title = item.Title, Path = item.Path, Type = item.Type })
            .ToList();

        var briefing = new TodayBriefing
        {
            Date = DateTime.Now,
            NexusStatus = "online",
            OllamaEnabled = (Environment.GetEnvironmentVariable("OLLAMA_ENABLED") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            VaultDocuments = index.Count,
            OperationMode = _operation.IsOperationMode(),
            HomeAssistantOnline = homeOnline,
            Network = network,
            OpenTasks = openTasks,
            Alerts = recentAlerts,
            ReviewItems = reviewItems
        };

        briefing.Suggestion = BuildSuggestion(briefing);
        briefing.Summary = BuildSummary(briefing);
        return briefing;
    }

    private static string BuildSuggestion(TodayBriefing briefing)
    {
        if (!briefing.Network.InternetOnline)
            return "Verificar internet antes de iniciar tarefas que dependem de acesso externo.";

        if (briefing.Network.UnknownDevices.Count > 0)
            return "Abrir o painel Network e classificar os dispositivos desconhecidos.";

        if (!briefing.HomeAssistantOnline)
            return "Verificar Home Assistant antes de executar rotinas da casa.";

        if (briefing.Alerts.Count > 0)
            return "Revisar os alertas recentes e fechar o que ja foi resolvido.";

        if (briefing.ReviewItems.Count > 0)
            return "Revisar notas em status revisar antes de promover para base definitiva.";

        return "Comecar pelas tarefas abertas e manter o painel Today como ponto de controle.";
    }

    private static string BuildSummary(TodayBriefing briefing)
    {
        return $"Nexus online. Vault com {briefing.VaultDocuments} documentos. " +
            $"Rede {briefing.Network.Status}, internet {(briefing.Network.InternetOnline ? "online" : "offline")}, " +
            $"Home Assistant {(briefing.HomeAssistantOnline ? "online" : "offline")}. " +
            $"Tarefas abertas: {briefing.OpenTasks.Count}. Alertas recentes: {briefing.Alerts.Count}.";
    }
}

public class TodayBriefing
{
    public DateTime Date { get; set; }
    public string NexusStatus { get; set; } = "";
    public bool OllamaEnabled { get; set; }
    public int VaultDocuments { get; set; }
    public bool OperationMode { get; set; }
    public bool HomeAssistantOnline { get; set; }
    public NetworkStatusResult Network { get; set; } = new();
    public List<string> OpenTasks { get; set; } = new();
    public List<NexusAlert> Alerts { get; set; } = new();
    public List<TodayReviewItem> ReviewItems { get; set; } = new();
    public string Suggestion { get; set; } = "";
    public string Summary { get; set; } = "";
}

public class TodayReviewItem
{
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
    public string Type { get; set; } = "";
}
