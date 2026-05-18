namespace NexusBackend.Services;

public class AlertService
{
    private readonly ObsidianService _obsidian;

    public AlertService(ObsidianService obsidian)
    {
        _obsidian = obsidian;
        EnsureAlertFiles();
    }

    public void Record(string type, string title, string severity, string detail, string suggestedAction)
    {
        var entry = $"""

        ## {DateTime.Now:yyyy-MM-dd HH:mm:ss}
        Tipo: {type}
        Titulo: {title}
        Severidade: {severity}
        Detalhe: {detail}
        Acao sugerida: {suggestedAction}
        Status: aberto

        """;

        _obsidian.AppendToFile("07_Nexus/alert-log.md", entry);
        _obsidian.AppendToFile("07_Nexus/alerts.md", entry);
    }

    public IReadOnlyList<NexusAlert> GetRecentAlerts(int limit = 10)
    {
        var content = _obsidian.ReadFile("07_Nexus/alert-log.md");
        var sections = content.Split("\n## ", StringSplitOptions.RemoveEmptyEntries)
            .Where(section => section.Contains("Tipo:", StringComparison.OrdinalIgnoreCase))
            .Select(section => "## " + section.Trim())
            .Reverse()
            .Take(limit)
            .Select(ParseAlert)
            .ToList();

        return sections;
    }

    private void EnsureAlertFiles()
    {
        _obsidian.EnsureFile(
            "07_Nexus/alerts.md",
            """
            # Alertas do Nexus

            Alertas ativos e recentes do Nexus.
            """
        );

        _obsidian.EnsureFile(
            "07_Nexus/alert-rules.md",
            """
            # Regras de Alerta do Nexus

            ## Regras iniciais
            - Alertar se Home Assistant cair.
            - Alertar se internet cair.
            - Alertar se dispositivo critico ficar offline.
            - Alertar se novo dispositivo aparecer na rede.
            - Alertar se vault ficar inacessivel.
            """
        );

        _obsidian.EnsureFile(
            "07_Nexus/alert-log.md",
            """
            # Log de Alertas do Nexus
            """
        );
    }

    private static NexusAlert ParseAlert(string section)
    {
        return new NexusAlert
        {
            Time = ExtractHeading(section),
            Type = ExtractValue(section, "Tipo"),
            Title = ExtractValue(section, "Titulo"),
            Severity = ExtractValue(section, "Severidade"),
            Detail = ExtractValue(section, "Detalhe"),
            SuggestedAction = ExtractValue(section, "Acao sugerida"),
            Status = ExtractValue(section, "Status")
        };
    }

    private static string ExtractHeading(string section)
    {
        var firstLine = section.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return firstLine.Replace("##", "").Trim();
    }

    private static string ExtractValue(string section, string key)
    {
        foreach (var line in section.Split('\n'))
        {
            var prefix = key + ":";
            if (line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return line[(line.IndexOf(':') + 1)..].Trim();
        }

        return "";
    }
}

public class NexusAlert
{
    public string Time { get; set; } = "";
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Detail { get; set; } = "";
    public string SuggestedAction { get; set; } = "";
    public string Status { get; set; } = "";
}
