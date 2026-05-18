using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusBackend.Services;

public class ZabbixService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ObsidianService _obsidian;
    private readonly TelegramService _telegram;
    private readonly AlertService _alerts;

    public ZabbixService(
        IHttpClientFactory httpClientFactory,
        ObsidianService obsidian,
        TelegramService telegram,
        AlertService alerts)
    {
        _httpClientFactory = httpClientFactory;
        _obsidian = obsidian;
        _telegram = telegram;
        _alerts = alerts;
        EnsureZabbixFiles();
    }

    public bool IsEnabled() => GetBool("ZABBIX_ENABLED");

    public bool IsConfigured()
    {
        return IsEnabled() &&
            !string.IsNullOrWhiteSpace(GetApiUrl()) &&
            (!string.IsNullOrWhiteSpace(GetApiToken()) ||
             (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZABBIX_USERNAME")) &&
              !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZABBIX_PASSWORD"))));
    }

    public async Task<ZabbixStatus> GetStatusAsync()
    {
        if (!IsEnabled())
            return new ZabbixStatus { Enabled = false, Configured = false, Status = "disabled", Message = "Zabbix desabilitado." };

        if (string.IsNullOrWhiteSpace(GetApiUrl()))
            return new ZabbixStatus { Enabled = true, Configured = false, Status = "missing_url", Message = "Defina ZABBIX_API_URL." };

        try
        {
            var version = await CallApiInfoVersionAsync();
            var configured = IsConfigured();

            return new ZabbixStatus
            {
                Enabled = true,
                Configured = configured,
                Status = "online",
                ApiUrl = RedactUrl(GetApiUrl()),
                Version = version,
                AuthMode = string.IsNullOrWhiteSpace(GetApiToken()) ? "user.login" : "api_token",
                Message = configured ? "Zabbix API acessivel." : "API acessivel, autenticacao pendente."
            };
        }
        catch (Exception ex)
        {
            return new ZabbixStatus
            {
                Enabled = true,
                Configured = IsConfigured(),
                Status = "offline",
                ApiUrl = RedactUrl(GetApiUrl()),
                Message = ex.Message
            };
        }
    }

    public async Task<IReadOnlyList<ZabbixHostSummary>> GetHostsAsync()
    {
        if (!IsConfigured())
            return Array.Empty<ZabbixHostSummary>();

        var parameters = new Dictionary<string, object?>
        {
            ["output"] = new[] { "hostid", "host", "name", "status", "available" },
            ["selectInterfaces"] = new[] { "interfaceid", "ip", "dns", "port", "type", "main" },
            ["selectGroups"] = new[] { "groupid", "name" },
            ["sortfield"] = "name",
            ["limit"] = GetInt("ZABBIX_HOST_LIMIT", 100)
        };

        var result = await CallWithAuthAsync("host.get", parameters);
        return result.EnumerateArray().Select(ParseHost).ToList();
    }

    public async Task<IReadOnlyList<ZabbixProblemSummary>> GetProblemsAsync(int? minimumSeverity = null, bool recent = false)
    {
        if (!IsConfigured())
            return Array.Empty<ZabbixProblemSummary>();

        var severities = BuildSeverityFilter(minimumSeverity);
        var parameters = new Dictionary<string, object?>
        {
            ["output"] = "extend",
            ["selectHosts"] = new[] { "hostid", "host", "name" },
            ["selectTags"] = "extend",
            ["selectAcknowledges"] = "extend",
            ["recent"] = recent,
            ["sortfield"] = new[] { "severity", "eventid" },
            ["sortorder"] = "DESC",
            ["limit"] = GetInt("ZABBIX_PROBLEM_LIMIT", 100)
        };

        if (severities.Length > 0)
            parameters["severities"] = severities;

        var result = await CallWithAuthAsync("problem.get", parameters);
        return result.EnumerateArray()
            .Select(ParseProblem)
            .OrderByDescending(problem => problem.Severity)
            .ThenByDescending(problem => problem.EventId)
            .ToList();
    }

    public async Task<ZabbixReport> GenerateReportAsync(int? minimumSeverity = null, bool saveToObsidian = true, bool notifyCritical = false)
    {
        var status = await GetStatusAsync();
        var problems = await GetProblemsAsync(minimumSeverity);
        var hosts = await GetHostsAsync();
        var report = new ZabbixReport
        {
            GeneratedAt = DateTime.Now,
            Status = status,
            HostsMonitored = hosts.Count,
            Problems = problems.ToList()
        };

        report.CriticalProblems = problems.Count(problem => problem.Severity >= 4);
        report.AcknowledgedProblems = problems.Count(problem => problem.Acknowledged);
        report.Summary = BuildReportSummary(report);
        report.Recommendation = BuildRecommendation(report);
        report.Markdown = BuildMarkdownReport(report);

        if (saveToObsidian)
            SaveReport(report);

        if (notifyCritical && report.CriticalProblems > 0)
            await NotifyCriticalAsync(report);

        return report;
    }

    public async Task<ZabbixAcknowledgeResult> AcknowledgeAsync(ZabbixAcknowledgeRequest request)
    {
        if (!IsConfigured())
            return new ZabbixAcknowledgeResult { Success = false, Message = "Zabbix nao configurado." };

        if (string.IsNullOrWhiteSpace(request.EventId))
            return new ZabbixAcknowledgeResult { Success = false, Message = "eventId nao informado." };

        var message = string.IsNullOrWhiteSpace(request.Message)
            ? "Reconhecido pelo Nexus. Analise em andamento."
            : request.Message.Trim();

        var action = 2 | 4;
        var parameters = new Dictionary<string, object?>
        {
            ["eventids"] = request.EventId.Trim(),
            ["action"] = action,
            ["message"] = message
        };

        var result = await CallWithAuthAsync("event.acknowledge", parameters);
        var updated = result.TryGetProperty("eventids", out var eventIds)
            ? eventIds.EnumerateArray().Select(item => item.GetString() ?? "").Where(id => !string.IsNullOrWhiteSpace(id)).ToList()
            : new List<string>();

        _obsidian.AppendToFile(
            "07_Nexus/zabbix-actions.md",
            $"\n\n## {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nAcao: acknowledge\nEventId: {request.EventId}\nMensagem: {message}\nResultado: {string.Join(", ", updated)}\n"
        );

        return new ZabbixAcknowledgeResult
        {
            Success = updated.Count > 0,
            EventIds = updated,
            Message = updated.Count > 0 ? "Evento reconhecido no Zabbix." : "Chamada concluida, mas sem eventids no retorno."
        };
    }

    private async Task NotifyCriticalAsync(ZabbixReport report)
    {
        var top = report.Problems.Where(problem => problem.Severity >= 4).Take(5).ToList();
        var detail = string.Join("\n", top.Select(problem => $"- {problem.SeverityName}: {problem.Name} ({problem.HostName})"));

        _alerts.Record(
            "zabbix",
            "Problemas criticos no Zabbix",
            "critical",
            detail,
            "Abrir o painel /zabbix, reconhecer o evento e iniciar modo operacao se necessario."
        );

        await _telegram.SendAlertAsync("Problemas criticos no Zabbix", "critical", detail);
    }

    private void SaveReport(ZabbixReport report)
    {
        var fileName = $"zabbix-report-{report.GeneratedAt:yyyyMMdd-HHmmss}.md";
        var path = $"07_Nexus/Zabbix/{fileName}";
        _obsidian.WriteFile(path, report.Markdown);
        _obsidian.AppendToFile(
            "07_Nexus/zabbix-history.md",
            $"\n- {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} | problemas: {report.Problems.Count} | criticos: {report.CriticalProblems} | arquivo: {path}\n"
        );
    }

    private static string BuildReportSummary(ZabbixReport report)
    {
        if (!report.Status.Configured)
            return "Zabbix ainda nao configurado no Nexus.";

        if (report.Problems.Count == 0)
            return $"Zabbix {report.Status.Status}. Nenhum problema ativo encontrado em {report.HostsMonitored} host(s).";

        return $"Zabbix {report.Status.Status}. {report.Problems.Count} problema(s) ativo(s), {report.CriticalProblems} critico(s), {report.AcknowledgedProblems} reconhecido(s).";
    }

    private static string BuildRecommendation(ZabbixReport report)
    {
        if (!report.Status.Configured)
            return "Configurar ZABBIX_API_URL e credenciais no .env.";

        if (report.CriticalProblems > 0)
            return "Priorizar severidades High/Disaster, reconhecer eventos em atendimento e registrar evidencias no modo operacao.";

        if (report.Problems.Any(problem => !problem.Acknowledged))
            return "Reconhecer problemas em atendimento e acompanhar tendencia de severidade.";

        return "Manter monitoramento ativo e revisar historico no Obsidian.";
    }

    private static string BuildMarkdownReport(ZabbixReport report)
    {
        var lines = new List<string>
        {
            "---",
            "type: zabbix-report",
            "tags:",
            "  - nexus",
            "  - zabbix",
            "  - monitoramento",
            "---",
            "",
            $"# Relatorio Zabbix - {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}",
            "",
            "## Resumo",
            report.Summary,
            "",
            "## Recomendacao",
            report.Recommendation,
            "",
            "## Indicadores",
            $"- Status: {report.Status.Status}",
            $"- Versao API: {report.Status.Version}",
            $"- Hosts monitorados: {report.HostsMonitored}",
            $"- Problemas ativos: {report.Problems.Count}",
            $"- Criticos: {report.CriticalProblems}",
            $"- Reconhecidos: {report.AcknowledgedProblems}",
            "",
            "## Problemas"
        };

        if (report.Problems.Count == 0)
        {
            lines.Add("- Nenhum problema ativo encontrado.");
        }
        else
        {
            foreach (var problem in report.Problems)
            {
                lines.Add($"- [{problem.SeverityName}] {problem.Name}");
                lines.Add($"  - EventId: {problem.EventId}");
                lines.Add($"  - Host: {problem.HostName}");
                lines.Add($"  - Desde: {problem.StartedAt:yyyy-MM-dd HH:mm:ss}");
                lines.Add($"  - Reconhecido: {(problem.Acknowledged ? "sim" : "nao")}");
                lines.Add($"  - Acao sugerida: {problem.SuggestedAction}");
            }
        }

        return string.Join("\n", lines) + "\n";
    }

    private async Task<string> CallApiInfoVersionAsync()
    {
        var result = await CallAsync("apiinfo.version", new { }, null);
        return result.ValueKind == JsonValueKind.String ? result.GetString() ?? "" : result.ToString();
    }

    private async Task<JsonElement> CallWithAuthAsync(string method, object parameters)
    {
        if (!string.IsNullOrWhiteSpace(GetApiToken()))
            return await CallAsync(method, parameters, GetApiToken());

        var authToken = await LoginAsync();

        try
        {
            return await CallAsync(method, parameters, authToken);
        }
        finally
        {
            await LogoutSafeAsync(authToken);
        }
    }

    private async Task<string> LoginAsync()
    {
        var username = Environment.GetEnvironmentVariable("ZABBIX_USERNAME") ?? "";
        var password = Environment.GetEnvironmentVariable("ZABBIX_PASSWORD") ?? "";
        var result = await CallAsync("user.login", new { username, password }, null);
        return result.GetString() ?? "";
    }

    private async Task LogoutSafeAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            await CallAsync("user.logout", Array.Empty<object>(), token);
        }
        catch
        {
            // Best-effort logout to avoid leaking sessions when user.login is used.
        }
    }

    private async Task<JsonElement> CallAsync(string method, object parameters, string? bearerToken)
    {
        var requestPayload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method,
            @params = parameters,
            id = 1
        }, JsonOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, GetApiUrl());
        request.Content = new StringContent(requestPayload, Encoding.UTF8, "application/json-rpc");

        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Zabbix HTTP {(int)response.StatusCode}: {json}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : "Erro Zabbix";
            var data = error.TryGetProperty("data", out var dataElement) ? dataElement.GetString() : "";
            throw new InvalidOperationException($"{message}: {data}".Trim(' ', ':'));
        }

        if (!root.TryGetProperty("result", out var result))
            throw new InvalidOperationException("Resposta Zabbix sem campo result.");

        return result.Clone();
    }

    private void EnsureZabbixFiles()
    {
        _obsidian.EnsureFile(
            "07_Nexus/zabbix-settings.md",
            """
            # Zabbix Settings

            O Nexus consulta o Zabbix via API JSON-RPC e salva relatorios operacionais aqui no vault.

            Variaveis:
            - ZABBIX_ENABLED
            - ZABBIX_API_URL
            - ZABBIX_API_TOKEN ou ZABBIX_USERNAME/ZABBIX_PASSWORD
            """
        );

        _obsidian.EnsureFile("07_Nexus/zabbix-history.md", "# Historico Zabbix\n");
        _obsidian.EnsureFile("07_Nexus/zabbix-actions.md", "# Acoes Zabbix\n");
    }

    private static ZabbixHostSummary ParseHost(JsonElement item)
    {
        var host = new ZabbixHostSummary
        {
            HostId = ReadString(item, "hostid"),
            Host = ReadString(item, "host"),
            Name = ReadString(item, "name"),
            Status = ReadInt(item, "status"),
            Available = ReadInt(item, "available")
        };

        if (item.TryGetProperty("interfaces", out var interfaces) && interfaces.ValueKind == JsonValueKind.Array)
        {
            host.Ip = interfaces.EnumerateArray()
                .Select(value => ReadString(value, "ip"))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }

        if (item.TryGetProperty("groups", out var groups) && groups.ValueKind == JsonValueKind.Array)
        {
            host.Groups = groups.EnumerateArray()
                .Select(group => ReadString(group, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
        }

        return host;
    }

    private static ZabbixProblemSummary ParseProblem(JsonElement item)
    {
        var severity = ReadInt(item, "severity");
        var clock = ReadLong(item, "clock");
        var problem = new ZabbixProblemSummary
        {
            EventId = ReadString(item, "eventid"),
            ObjectId = ReadString(item, "objectid"),
            Name = ReadString(item, "name"),
            Severity = severity,
            SeverityName = SeverityName(severity),
            StartedAt = DateTimeOffset.FromUnixTimeSeconds(clock <= 0 ? 0 : clock).LocalDateTime,
            Acknowledged = ReadInt(item, "acknowledged") == 1,
            Suppressed = ReadInt(item, "suppressed") == 1,
            SuggestedAction = SuggestedAction(severity)
        };

        if (item.TryGetProperty("hosts", out var hosts) && hosts.ValueKind == JsonValueKind.Array)
        {
            var host = hosts.EnumerateArray().FirstOrDefault();
            if (host.ValueKind == JsonValueKind.Object)
                problem.HostName = ReadString(host, "name");
        }

        if (string.IsNullOrWhiteSpace(problem.HostName))
            problem.HostName = "host nao informado";

        if (item.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            problem.Tags = tags.EnumerateArray()
                .Select(tag => $"{ReadString(tag, "tag")}={ReadString(tag, "value")}".Trim('='))
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToList();
        }

        return problem;
    }

    private static int[] BuildSeverityFilter(int? minimumSeverity)
    {
        if (!minimumSeverity.HasValue)
            return Array.Empty<int>();

        var min = Math.Clamp(minimumSeverity.Value, 0, 5);
        return Enumerable.Range(min, 6 - min).ToArray();
    }

    private static string SuggestedAction(int severity)
    {
        return severity switch
        {
            >= 5 => "Acionar atendimento imediato, reconhecer evento e registrar incidente.",
            4 => "Priorizar analise, validar impacto no servico e reconhecer em atendimento.",
            3 => "Verificar tendencia e correlacionar com outros alertas.",
            2 => "Acompanhar e registrar se houver recorrencia.",
            _ => "Monitorar."
        };
    }

    private static string SeverityName(int severity)
    {
        return severity switch
        {
            0 => "Not classified",
            1 => "Information",
            2 => "Warning",
            3 => "Average",
            4 => "High",
            5 => "Disaster",
            _ => "Unknown"
        };
    }

    private static string ReadString(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
            return "";

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }

    private static int ReadInt(JsonElement item, string property)
    {
        var raw = ReadString(item, property);
        return int.TryParse(raw, out var value) ? value : 0;
    }

    private static long ReadLong(JsonElement item, string property)
    {
        var raw = ReadString(item, property);
        return long.TryParse(raw, out var value) ? value : 0;
    }

    private static bool GetBool(string key)
    {
        return (Environment.GetEnvironmentVariable(key) ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetInt(string key, int fallback)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(key), out var value) ? value : fallback;
    }

    private static string GetApiUrl() => Environment.GetEnvironmentVariable("ZABBIX_API_URL") ?? "";

    private static string GetApiToken() => Environment.GetEnvironmentVariable("ZABBIX_API_TOKEN") ?? "";

    private static string RedactUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public class ZabbixStatus
{
    public bool Enabled { get; set; }
    public bool Configured { get; set; }
    public string Status { get; set; } = "";
    public string ApiUrl { get; set; } = "";
    public string Version { get; set; } = "";
    public string AuthMode { get; set; } = "";
    public string Message { get; set; } = "";
}

public class ZabbixHostSummary
{
    public string HostId { get; set; } = "";
    public string Host { get; set; } = "";
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public int Status { get; set; }
    public int Available { get; set; }
    public List<string> Groups { get; set; } = new();
}

public class ZabbixProblemSummary
{
    public string EventId { get; set; } = "";
    public string ObjectId { get; set; } = "";
    public string Name { get; set; } = "";
    public string HostName { get; set; } = "";
    public int Severity { get; set; }
    public string SeverityName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public bool Acknowledged { get; set; }
    public bool Suppressed { get; set; }
    public List<string> Tags { get; set; } = new();
    public string SuggestedAction { get; set; } = "";
}

public class ZabbixReport
{
    public DateTime GeneratedAt { get; set; }
    public ZabbixStatus Status { get; set; } = new();
    public int HostsMonitored { get; set; }
    public int CriticalProblems { get; set; }
    public int AcknowledgedProblems { get; set; }
    public List<ZabbixProblemSummary> Problems { get; set; } = new();
    public string Summary { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public string Markdown { get; set; } = "";
}

public class ZabbixAcknowledgeRequest
{
    public string EventId { get; set; } = "";
    public string Message { get; set; } = "";
}

public class ZabbixAcknowledgeResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public List<string> EventIds { get; set; } = new();
}
