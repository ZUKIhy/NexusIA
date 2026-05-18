using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NexusBackend.Hubs;
using NexusBackend.Models;
using NexusBackend.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/nexus")]
[Route("api/jarvis")]
public class NexusController : ControllerBase
{
    private readonly OpenAIService _openAI;
    private readonly MemoryService _memory;
    private readonly TaskService _tasks;
    private readonly LogService _logs;
    private readonly ObsidianService _obsidian;
    private readonly ComputerControlService _computer;
    private readonly OperationService _operation;
    private readonly OllamaService _ollama;
    private readonly AutoKnowledgeService _autoKnowledge;
    private readonly HomeAssistantService _home;
    private readonly NetworkMonitorService _network;
    private readonly WeatherService _weather;
    private readonly SpotifyService _spotify;
    private readonly SpotifyLearningService _spotifyLearning;
    private readonly TodayService _today;
    private readonly ZabbixService _zabbix;
    private readonly IHubContext<NexusHub> _hub;

    public NexusController(
        OpenAIService openAI,
        MemoryService memory,
        TaskService tasks,
        LogService logs,
        ObsidianService obsidian,
        ComputerControlService computer,
        OperationService operation,
        OllamaService ollama,
        AutoKnowledgeService autoKnowledge,
        HomeAssistantService home,
        NetworkMonitorService network,
        WeatherService weather,
        SpotifyService spotify,
        SpotifyLearningService spotifyLearning,
        TodayService today,
        ZabbixService zabbix,
        IHubContext<NexusHub> hub)
    {
        _openAI = openAI;
        _memory = memory;
        _tasks = tasks;
        _logs = logs;
        _obsidian = obsidian;
        _computer = computer;
        _operation = operation;
        _ollama = ollama;
        _autoKnowledge = autoKnowledge;
        _home = home;
        _network = network;
        _weather = weather;
        _spotify = spotify;
        _spotifyLearning = spotifyLearning;
        _today = today;
        _zabbix = zabbix;
        _hub = hub;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var vault = Environment.GetEnvironmentVariable("VAULT_PATH") ?? "../vault";
        var fullVault = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), vault));
        var markdownCount = 0;
        var lastModified = "";

        if (Directory.Exists(fullVault))
        {
            var files = Directory.GetFiles(fullVault, "*.md", SearchOption.AllDirectories);
            markdownCount = files.Length;

            var latest = files
                .Select(file => new FileInfo(file))
                .OrderByDescending(file => file.LastWriteTime)
                .FirstOrDefault();

            if (latest is not null)
                lastModified = latest.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        var taskCount = Math.Max(0, _tasks.ReadAll().Split("- [ ]", StringSplitOptions.RemoveEmptyEntries).Length - 1);
        var memoryCount = Math.Max(0, _memory.ReadAll().Split("## ", StringSplitOptions.RemoveEmptyEntries).Length - 1);

        return Ok(new
        {
            status = "online",
            time = DateTime.Now,
            openaiConfigured = IsOpenAIConfigured(),
            ollamaEnabled = (Environment.GetEnvironmentVariable("OLLAMA_ENABLED") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434",
            ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "nexus-qwen3b",
            backend = "http://localhost:5000",
            vault = fullVault,
            vaultExists = Directory.Exists(fullVault),
            markdownDocuments = markdownCount,
            tasks = taskCount,
            memories = memoryCount,
            lastVaultUpdate = lastModified,
            mode = "obsidian -> ollama -> openai",
            gpu = "NVIDIA GeForce RTX 3060 12GB",
            nextFeatures = new[]
            {
                "Indexador Obsidian",
                "Padronizador de procedimentos",
                "Comandos locais"
            }
        });
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        var message = request.Message.Trim();

        await _hub.Clients.All.SendAsync("nexus:thinking", new { message = "Pensando..." });

        var intent = DetectIntent(message);

        if (intent == "operation_mode_on")
        {
            _operation.SetOperationMode(true);
            return await LocalAnswer(message, "Modo operaÃ§Ã£o ativado. Vou responder com resumo, riscos, passo a passo, validaÃ§Ã£o e prÃ³xima aÃ§Ã£o.", intent);
        }

        if (intent == "operation_mode_off")
        {
            _operation.SetOperationMode(false);
            return await LocalAnswer(message, "Modo operaÃ§Ã£o desativado. Voltei ao modo normal.", intent);
        }

        if (intent == "start_attendance")
            return await LocalAnswer(message, _operation.StartSession(CleanAttendanceCommand(message)), intent);

        if (intent == "attendance_report")
            return await LocalAnswer(message, await _operation.GenerateReport(), intent);

        if (intent == "shift_handoff")
            return await LocalAnswer(message, await _operation.GenerateHandover(), intent);

        if (intent == "presence_check")
        {
            var presenceAnswer = "Estou por aqui, Gabriel. Sistemas ativos, Obsidian conectado e pronto para ajudar. Quer continuar de onde paramos ou vamos começar algo novo?";
            return await LocalAnswer(message, presenceAnswer, intent);
        }

        if (intent == "time_check")
        {
            var timeAnswer = $"Agora são {DateTime.Now:HH:mm}, Gabriel. Quer que eu verifique suas tarefas ou seguimos em algum procedimento?";
            return await LocalAnswer(message, timeAnswer, intent);
        }

        if (intent == "date_check")
        {
            var culture = new System.Globalization.CultureInfo("pt-BR");
            var dateAnswer = $"Hoje é {DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy", culture)}. Posso te ajudar a organizar o foco do dia, se quiser.";
            return await LocalAnswer(message, dateAnswer, intent);
        }

        if (intent == "system_status")
        {
            var vault = Environment.GetEnvironmentVariable("VAULT_PATH") ?? "../vault";
            var ollamaEnabled = (Environment.GetEnvironmentVariable("OLLAMA_ENABLED") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
            var statusAnswer =
                "Status do Nexus: online. " +
                "Backend ativo em http://localhost:5000. " +
                $"Vault configurado em: {vault}. " +
                $"Ollama ativo: {(ollamaEnabled ? "sim" : "nao")}. " +
                $"OpenAI configurada: {(IsOpenAIConfigured() ? "sim" : "nao")}. " +
                "Modo atual: Obsidian primeiro, Ollama em seguida, OpenAI apenas como ultimo recurso.";

            return await LocalAnswer(message, statusAnswer, intent);
        }

        if (intent == "list_tasks")
        {
            var tasks = _tasks.ReadAll();
            var tasksAnswer = string.IsNullOrWhiteSpace(tasks)
                ? "Nao encontrei tarefas registradas."
                : $"Estas sao suas tarefas registradas no Obsidian:\n\n{tasks}";

            return await LocalAnswer(message, tasksAnswer, intent);
        }

        if (intent == "list_memory" || intent == "search_memory")
        {
            var memory = _memory.ReadAll();
            var memoryAnswer = string.IsNullOrWhiteSpace(memory)
                ? "Ainda nao encontrei memorias registradas."
                : $"Estas sao as memorias registradas no Obsidian:\n\n{memory}";

            return await LocalAnswer(message, memoryAnswer, intent);
        }

        if (intent == "conversation_starter")
            return await ConversationStarter(message, intent);

        if (intent == "casual_conversation")
            return await CasualConversation(message, intent);

        if (intent == "save_memory")
        {
            var memoryText = CleanMemoryCommand(message);
            _memory.Save(memoryText);
            await _hub.Clients.All.SendAsync("nexus:memory_saved", new { content = memoryText });
        }

        if (intent == "create_task")
        {
            var taskText = CleanTaskCommand(message);
            _tasks.Create(taskText);
            await _hub.Clients.All.SendAsync("nexus:task_created", new { content = taskText });
        }

        if (intent == "home_assistant_control")
            return await HomeAssistantAnswer(message, intent);

        if (intent == "home_assistant_confirm")
            return await HomeAssistantConfirmAnswer(message, intent);

        if (intent == "network_status")
            return await NetworkStatusAnswer(message, intent);

        if (intent == "weather_report")
            return await WeatherReportAnswer(message, intent);

        if (intent == "spotify_control")
            return await SpotifyAnswer(message, intent);

        if (intent == "spotify_learning")
            return await SpotifyLearningAnswer(message, intent);

        if (intent == "today_briefing")
            return await TodayBriefingAnswer(message, intent);

        if (intent == "zabbix_status")
            return await ZabbixStatusAnswer(message, intent);

        if (intent == "zabbix_report")
            return await ZabbixReportAnswer(message, intent);

        if (intent == "auto_knowledge")
            return await AutoKnowledgeAnswer(message, intent);

        if (intent == "auto_knowledge_force")
            return await AutoKnowledgeForceAnswer(message, intent);

        if (intent == "procedure_answer")
            return await ProcedureAnswer(message, intent);

        if (intent == "search_docs")
            return await SearchDocsAnswer(message, intent);

        if (intent == "computer_control")
        {
            var action = ExecuteComputerCommand(message);
            var computerAnswer = action.Message;
            _logs.Conversation(message, computerAnswer);

            await _hub.Clients.All.SendAsync("nexus:log", new
            {
                level = action.Success ? "info" : "warn",
                message = computerAnswer
            });

            await _hub.Clients.All.SendAsync("nexus:speaking", new { answer = computerAnswer });

            return Ok(new ChatResponse
            {
                Answer = computerAnswer,
                Intent = intent,
                MemorySaved = false,
                TaskCreated = false
            });
        }

        if (message.Length < 120 && IsProbablyCasual(message))
            return await CasualConversation(message, "casual_conversation");

        var answer = await _openAI.AskAsync(message);
        if (_operation.IsOperationMode())
            answer = _operation.BuildOperationFrame(answer);

        _logs.Conversation(message, answer);

        await _hub.Clients.All.SendAsync("nexus:log", new { level = "info", message = $"Comando processado: {message}" });
        await _hub.Clients.All.SendAsync("nexus:speaking", new { answer });

        return Ok(new ChatResponse
        {
            Answer = answer,
            Intent = intent,
            MemorySaved = intent == "save_memory",
            TaskCreated = intent == "create_task"
        });
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate()
    {
        await _hub.Clients.All.SendAsync("nexus:activated", new { message = "Nexus ativado." });
        return Ok(new { status = "activated" });
    }

    private async Task<ActionResult<ChatResponse>> SearchDocsAnswer(string message, string intent)
    {
        var query = CleanDocsCommand(message);
        var results = _obsidian.SearchMarkdownDetailed(query)
            .Take(5)
            .ToList();

        var docsAnswer = BuildDocsAnswer(query, results);
        if (_operation.IsOperationMode())
        {
            _operation.RegisterProcedureConsulted(query, results);
            docsAnswer = _operation.BuildOperationFrame(docsAnswer, "Abrir o documento mais relevante e validar pré-requisitos.");
        }

        _logs.Conversation(message, docsAnswer);

        await _hub.Clients.All.SendAsync("nexus:log", new
        {
            level = "info",
            message = $"Busca documental realizada: {query}"
        });

        await _hub.Clients.All.SendAsync("nexus:speaking", new { answer = docsAnswer });

        return Ok(new ChatResponse
        {
            Answer = docsAnswer,
            Intent = intent,
            MemorySaved = false,
            TaskCreated = false,
            DocsQuery = query,
            DocsResults = results.Select(result => new
            {
                path = result.Path,
                title = result.Title,
                folder = result.Folder,
                score = result.Score,
                snippet = result.Snippet,
                isPrioritySource = result.IsPrioritySource
            })
        });
    }

    private async Task<ActionResult<ChatResponse>> ProcedureAnswer(string message, string intent)
    {
        var query = CleanProcedureCommand(message);
        var results = _obsidian.SearchMarkdownDetailed(query)
            .Take(3)
            .ToList();

        string answer;

        if (results.Count == 0)
        {
            answer = $"Nao encontrei um procedimento sobre: {query}";
        }
        else
        {
            answer =
                $"Encontrei documentacao sobre: {query}\n\n" +
                "Abaixo estao os trechos mais relevantes. Use como base operacional e valide antes de executar em producao.\n\n";

            for (var i = 0; i < results.Count; i++)
            {
                answer +=
                    $"## Documento {i + 1}\n" +
                    $"Titulo: {results[i].Title}\n" +
                    $"Caminho: {results[i].Path}\n" +
                    $"Trecho: {NormalizeSnippet(results[i].Snippet)}\n\n" +
                    "### Lembretes de seguranca\n" +
                    "- Validar ambiente antes de executar.\n" +
                    "- Conferir backup quando houver risco de remocao ou alteracao.\n" +
                    "- Registrar evidencias apos execucao.\n" +
                    "- Nao executar comandos destrutivos sem confirmacao.\n\n" +
                    "---\n\n";
            }
        }

        return await LocalAnswer(message, answer, intent);
    }

    private async Task<ActionResult<ChatResponse>> AutoKnowledgeAnswer(string message, string intent)
    {
        var query = CleanAutoKnowledgeCommand(message);
        var existingResults = _obsidian.SearchMarkdownDetailed(query)
            .Where(result => IsUsableKnowledgeResult(result.Path))
            .Where(result => IsRelevantKnowledgeResult(query, result))
            .Take(3)
            .ToList();

        if (existingResults.Count > 0)
        {
            var answer =
                $"Encontrei conteudo na sua base local sobre: {query}\n\n" +
                string.Join("\n\n---\n\n", existingResults.Select(result =>
                    $"Arquivo: {result.Path}\nTitulo: {result.Title}\n{NormalizeSnippet(result.Snippet)}"));

            return await LocalAnswer(message, answer, "auto_knowledge_found_in_obsidian");
        }

        var generated = await _autoKnowledge.GenerateAndSaveAsync(message);

        if (generated is null)
        {
            var fallback = "Nao encontrei isso no Obsidian e nao consegui gerar uma nova nota com o Ollama agora.";
            return await LocalAnswer(message, fallback, "auto_knowledge_failed");
        }

        var response =
            "Nao encontrei esse assunto na sua base local, entao gerei uma nova nota usando o Ollama.\n\n" +
            $"Arquivo criado: {generated.Path}\n\n" +
            $"Resumo:\n{generated.Answer}";

        return await LocalAnswer(message, response, "auto_knowledge_created");
    }

    private async Task<ActionResult<ChatResponse>> AutoKnowledgeForceAnswer(string message, string intent)
    {
        var generated = await _autoKnowledge.GenerateAndSaveAsync(message);

        if (generated is null)
        {
            return await LocalAnswer(
                message,
                "Nao consegui gerar a nova nota com o Ollama agora.",
                "auto_knowledge_force_failed"
            );
        }

        var response =
            "Gerei e salvei uma nova nota no Obsidian.\n\n" +
            $"Titulo: {generated.Title}\n" +
            $"Arquivo: {generated.Path}\n\n" +
            $"Resumo:\n{generated.Answer}";

        return await LocalAnswer(message, response, "auto_knowledge_force_created");
    }

    private async Task<ActionResult<ChatResponse>> HomeAssistantAnswer(string message, string intent)
    {
        if (!_home.IsEnabled())
            return await LocalAnswer(message, "Home Assistant nao esta habilitado no Nexus.", "home_assistant_disabled");

        var devicesMap = _obsidian.ReadFile("07_Nexus/home-assistant-devices.md");
        if (string.IsNullOrWhiteSpace(devicesMap))
            return await LocalAnswer(message, "Nao encontrei o mapa de dispositivos em 07_Nexus/home-assistant-devices.md.", "home_assistant_devices_missing");

        var prompt = $@"
Voce e Nexus.

Converta o comando do Gabriel em uma acao Home Assistant.

Responda somente em JSON valido, sem markdown.

Formato:
{{
  ""action"": ""turn_on|turn_off|toggle|set_brightness|get_state|list_states"",
  ""entity_id"": ""dominio.nome"",
  ""brightness"": 0,
  ""needs_confirmation"": true
}}

Regras:
- Para listar o que esta ligado em casa, use action list_states e entity_id vazio.
- Para consultar status de um dispositivo, use action get_state.
- Para brilho, use set_brightness e brightness de 1 a 100.
- Para cenas do Home Assistant, use action turn_on com entity_id scene.nome.
- Marque needs_confirmation como true para fechaduras, portoes, alarmes, cameras, aquecedores, ar-condicionado, cortinas e tomadas criticas.
- Para luzes e tomadas simples, needs_confirmation pode ser false.

Mapa de dispositivos:
{devicesMap}

Comando:
{message}
";

        var parsed = await _ollama.AskAsync(prompt);

        if (string.IsNullOrWhiteSpace(parsed))
            return await LocalAnswer(message, "Nao consegui interpretar o comando da casa inteligente.", intent);

        HomeAssistantParsedAction action;

        try
        {
            action = ParseHomeAssistantAction(parsed);
        }
        catch
        {
            return await LocalAnswer(
                message,
                $"Nao consegui interpretar o JSON da acao. Resposta recebida: {parsed}",
                "home_assistant_parse_error"
            );
        }

        if (action.Action == "list_states")
        {
            var statesJson = await _home.GetStatesAsync();
            var answer = BuildHomeAssistantStatesSummary(statesJson);
            return await LocalAnswer(message, answer, "home_assistant_states");
        }

        if (string.IsNullOrWhiteSpace(action.EntityId))
            return await LocalAnswer(message, "Nao encontrei o dispositivo correspondente no mapa do Home Assistant.", intent);

        if (action.Action == "get_state")
        {
            var stateJson = await _home.GetStateAsync(action.EntityId);
            var answer = BuildHomeAssistantStateSummary(action.EntityId, stateJson);
            return await LocalAnswer(message, answer, "home_assistant_state");
        }

        var needsConfirmation = action.NeedsConfirmation || IsSensitiveHomeAssistantAction(action);
        if (needsConfirmation)
        {
            return await LocalAnswer(
                message,
                $"Essa acao exige confirmacao: {action.Action} em {action.EntityId}. Diga: Nexus, confirmar acao {action.Action} {action.EntityId}",
                "home_assistant_needs_confirmation"
            );
        }

        return await ExecuteHomeAssistantAction(message, action, intent);
    }

    private async Task<ActionResult<ChatResponse>> HomeAssistantConfirmAnswer(string message, string intent)
    {
        if (!_home.IsEnabled())
            return await LocalAnswer(message, "Home Assistant nao esta habilitado no Nexus.", "home_assistant_disabled");

        var cleaned = message
            .Replace("Nexus", "", StringComparison.OrdinalIgnoreCase)
            .Replace("confirmar acao", "", StringComparison.OrdinalIgnoreCase)
            .Replace("confirmar ação", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',', '?', '!', ':', ';');

        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return await LocalAnswer(
                message,
                "Para confirmar, diga: Nexus, confirmar acao turn_on light.quarto",
                "home_assistant_confirmation_invalid"
            );
        }

        var action = new HomeAssistantParsedAction(parts[0], parts[1], 0, false);
        return await ExecuteHomeAssistantAction(message, action, intent);
    }

    private async Task<ActionResult<ChatResponse>> ExecuteHomeAssistantAction(string message, HomeAssistantParsedAction action, string intent)
    {
        bool ok = action.Action switch
        {
            "turn_on" => await _home.TurnOnAsync(action.EntityId),
            "turn_off" => await _home.TurnOffAsync(action.EntityId),
            "toggle" => await _home.ToggleAsync(action.EntityId),
            "set_brightness" => await _home.SetLightBrightnessAsync(action.EntityId, action.Brightness),
            _ => false
        };

        var answer = ok
            ? $"Comando enviado para o Home Assistant: {action.Action} em {action.EntityId}."
            : "Tentei executar o comando, mas o Home Assistant nao confirmou sucesso.";

        _obsidian.AppendToFile(
            "07_Nexus/operation-log.md",
            $"\n\n## {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nAcao: home-assistant\nComando: {message}\nResultado: {answer}\n"
        );

        return await LocalAnswer(message, answer, intent);
    }

    private async Task<ActionResult<ChatResponse>> NetworkStatusAnswer(string message, string intent)
    {
        var status = await _network.CheckNetworkStatusAsync();
        var offline = status.Devices.Where(device => device.Monitor && !device.Online).ToList();
        var online = status.Devices.Where(device => device.Online).ToList();

        var answer =
            $"Status da rede: {status.Status}\n" +
            $"Internet: {(status.InternetOnline ? "online" : "offline")}\n" +
            $"Monitorados: {status.MonitoredDevices}\n" +
            $"Online: {status.OnlineDevices}\n" +
            $"Offline: {status.OfflineDevices}\n" +
            $"Desconhecidos: {status.UnknownDevices.Count}\n" +
            $"Alertas hoje: {status.AlertsToday}\n\n";

        if (online.Count > 0)
        {
            answer += "Dispositivos online:\n";
            answer += string.Join("\n", online.Select(device => $"- {device.Name} ({device.Ip})"));
            answer += "\n\n";
        }

        if (offline.Count > 0)
        {
            answer += "Dispositivos offline:\n";
            answer += string.Join("\n", offline.Select(device => $"- {device.Name} ({device.Ip})"));
            answer += "\n\n";
        }

        if (status.UnknownDevices.Count > 0)
        {
            answer += "Dispositivos desconhecidos detectados:\n";
            answer += string.Join("\n", status.UnknownDevices.Select(device => $"- {device.Ip} / {device.Mac}"));
            answer += "\n\nGabriel, abra o painel Network para marcar se conhece ou nao.";
        }

        return await LocalAnswer(message, answer.Trim(), intent);
    }

    private async Task<ActionResult<ChatResponse>> TodayBriefingAnswer(string message, string intent)
    {
        var briefing = await _today.BuildAsync();
        var answer =
            $"Briefing do dia - {briefing.Date:dd/MM/yyyy HH:mm}\n\n" +
            $"Tempo em Sao Jose do Rio Preto:\n" +
            $"- {briefing.Weather.Summary}\n" +
            $"- Recomendacao: {briefing.Weather.Recommendation}\n\n" +
            $"{briefing.Summary}\n\n" +
            $"Modo operacao: {(briefing.OperationMode ? "ativo" : "inativo")}\n" +
            $"Ollama: {(briefing.OllamaEnabled ? "ativo" : "inativo")}\n" +
            $"Pendencias abertas: {briefing.OpenTasks.Count}\n" +
            $"Itens para revisao: {briefing.ReviewItems.Count}\n\n" +
            $"Sugestao do Nexus: {briefing.Suggestion}";

        if (briefing.OpenTasks.Count > 0)
            answer += "\n\nTarefas:\n" + string.Join("\n", briefing.OpenTasks.Select(task => $"- {task.Replace("- [ ]", "").Trim()}"));

        if (briefing.Alerts.Count > 0)
            answer += "\n\nAlertas recentes:\n" + string.Join("\n", briefing.Alerts.Take(5).Select(alert => $"- {alert.Title} ({alert.Severity})"));

        return await LocalAnswer(message, answer, intent);
    }

    private async Task<ActionResult<ChatResponse>> WeatherReportAnswer(string message, string intent)
    {
        string answer;

        try
        {
            answer = await _weather.GetWeatherReportTextAsync();
        }
        catch
        {
            answer = "Nao consegui consultar a previsao do tempo agora. A API da Open-Meteo pode estar indisponivel ou sem conexao no momento.";
        }

        return await LocalAnswer(message, answer, intent);
    }

    private async Task<ActionResult<ChatResponse>> ZabbixStatusAnswer(string message, string intent)
    {
        var status = await _zabbix.GetStatusAsync();
        var problems = await _zabbix.GetProblemsAsync(4);
        var answer =
            $"Status Zabbix: {status.Status}\n" +
            $"Configurado: {(status.Configured ? "sim" : "nao")}\n" +
            $"Versao: {status.Version}\n" +
            $"Autenticacao: {status.AuthMode}\n" +
            $"Mensagem: {status.Message}\n\n" +
            $"Problemas High/Disaster ativos: {problems.Count}";

        if (_operation.IsOperationMode())
            answer = _operation.BuildOperationFrame(answer, "Abrir /zabbix e validar os problemas criticos antes de reconhecer eventos.");

        return await LocalAnswer(message, answer, intent);
    }

    private async Task<ActionResult<ChatResponse>> ZabbixReportAnswer(string message, string intent)
    {
        var report = await _zabbix.GenerateReportAsync(ExtractZabbixMinimumSeverity(message), saveToObsidian: true, notifyCritical: true);
        var answer =
            $"{report.Summary}\n\n" +
            $"Recomendacao: {report.Recommendation}\n\n" +
            "Principais problemas:\n" +
            (report.Problems.Count == 0
                ? "- Nenhum problema ativo encontrado."
                : string.Join("\n", report.Problems.Take(8).Select(problem =>
                    $"- [{problem.SeverityName}] {problem.Name} | {problem.HostName} | EventId {problem.EventId}")));

        if (_operation.IsOperationMode())
            answer = _operation.BuildOperationFrame(answer, "Reconhecer eventos em atendimento e registrar evidencias no chamado.");

        return await LocalAnswer(message, answer, intent);
    }

    private async Task<ActionResult<ChatResponse>> SpotifyAnswer(string message, string intent)
    {
        if (!_spotify.IsEnabled() || !_spotify.IsConfigured())
        {
            return await LocalAnswer(
                message,
                "Spotify ainda nao esta configurado. No backend/.env, defina SPOTIFY_ENABLED=true, SPOTIFY_CLIENT_ID, SPOTIFY_CLIENT_SECRET e SPOTIFY_REDIRECT_URI=http://localhost:5000/api/spotify/callback.",
                "spotify_not_configured"
            );
        }

        if (!_spotify.IsAuthenticated())
        {
            return await LocalAnswer(
                message,
                "Spotify configurado, mas ainda nao autenticado. Abra http://localhost:5000/api/spotify/login, autorize o app e depois peca para tocar suas musicas.",
                "spotify_login_required"
            );
        }

        try
        {
            var lower = NormalizeSearchText(message);
            string answer;

            if (lower.Contains("pausar") || lower.Contains("pause") || lower.Contains("parar musica") || lower.Contains("para a musica"))
            {
                answer = await _spotify.PauseAsync();
            }
            else if (lower.Contains("proxima") || lower.Contains("prox ") || lower.Contains("passa musica") || lower.Contains("passar musica"))
            {
                answer = await _spotify.NextAsync();
            }
            else if (lower.Contains("anterior") || lower.Contains("voltar musica") || lower.Contains("volte a musica"))
            {
                answer = await _spotify.PreviousAsync();
            }
            else if (lower.Contains("volume"))
            {
                var volume = ExtractFirstNumber(message);
                answer = volume.HasValue
                    ? await _spotify.SetVolumeAsync(volume.Value)
                    : "Diga o volume em porcentagem, por exemplo: Nexus, volume 40%.";
            }
            else if (lower.Contains("tocando agora") || lower.Contains("musica atual") || lower.Contains("o que esta tocando"))
            {
                var current = await _spotify.GetCurrentAsync();
                answer = string.IsNullOrWhiteSpace(current.Track)
                    ? current.Message
                    : $"{(current.IsPlaying ? "Tocando" : "Pausado")}: {current.Track} - {current.Artist} em {current.Device}. Volume: {current.VolumePercent}%.";
            }
            else
            {
                var query = CleanSpotifyPlayCommand(message);
                answer = await _spotify.PlayAsync(new SpotifyPlayRequest { Query = query });
            }

            return await LocalAnswer(message, answer, intent);
        }
        catch (Exception ex)
        {
            return await LocalAnswer(message, ex.Message, "spotify_error");
        }
    }

    private async Task<ActionResult<ChatResponse>> SpotifyLearningAnswer(string message, string intent)
    {
        try
        {
            var result = await _spotifyLearning.LearnAsync();
            if (!result.Success)
                return await LocalAnswer(message, result.Message, "spotify_learning_failed");

            var answer =
                $"{result.Message}\n\n" +
                $"Arquivo atualizado: {result.ProfilePath}\n\n" +
                result.MemorySummary;

            return await LocalAnswer(message, answer, intent);
        }
        catch (Exception ex)
        {
            var answer =
                "Nao consegui aprender seus gostos musicais agora. " +
                "Se aparecer erro de permissao do Spotify, abra http://localhost:5000/api/spotify/login e autorize novamente, porque adicionei permissoes novas para top musicas e historico recente.\n\n" +
                ex.Message;

            return await LocalAnswer(message, answer, "spotify_learning_error");
        }
    }

    private static HomeAssistantParsedAction ParseHomeAssistantAction(string parsed)
    {
        var cleanJson = CleanJsonResponse(parsed);
        using var doc = JsonDocument.Parse(cleanJson);
        var root = doc.RootElement;

        var action = root.TryGetProperty("action", out var actionProperty)
            ? actionProperty.GetString() ?? ""
            : "";
        var entityId = root.TryGetProperty("entity_id", out var entityProperty)
            ? entityProperty.GetString() ?? ""
            : "";
        var needsConfirmation = root.TryGetProperty("needs_confirmation", out var confirmationProperty) &&
            confirmationProperty.ValueKind == JsonValueKind.True;
        var brightness = root.TryGetProperty("brightness", out var brightnessProperty) &&
            brightnessProperty.TryGetInt32(out var brightnessValue)
                ? Math.Clamp(brightnessValue, 1, 100)
                : 100;

        return new HomeAssistantParsedAction(action, entityId, brightness, needsConfirmation);
    }

    private static string CleanJsonResponse(string response)
    {
        var clean = response.Trim();

        if (!clean.StartsWith("```"))
            return clean;

        clean = Regex.Replace(clean, @"^```(?:json)?\s*", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\s*```$", "");
        return clean.Trim();
    }

    private static bool IsSensitiveHomeAssistantAction(HomeAssistantParsedAction action)
    {
        var target = $"{action.Action} {action.EntityId}".ToLowerInvariant();

        return target.Contains("lock.") ||
            target.Contains("alarm_control_panel.") ||
            target.Contains("camera.") ||
            target.Contains("cover.") ||
            target.Contains("climate.") ||
            target.Contains("portao") ||
            target.Contains("portão") ||
            target.Contains("fechadura") ||
            target.Contains("alarme") ||
            target.Contains("camera") ||
            target.Contains("câmera") ||
            target.Contains("aquecedor") ||
            target.Contains("servidor");
    }

    private static string BuildHomeAssistantStateSummary(string entityId, string stateJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(stateJson);
            var root = doc.RootElement;
            var state = root.TryGetProperty("state", out var stateProperty) ? stateProperty.GetString() : "";
            var friendlyName = entityId;

            if (root.TryGetProperty("attributes", out var attributes) &&
                attributes.TryGetProperty("friendly_name", out var nameProperty))
            {
                friendlyName = nameProperty.GetString() ?? entityId;
            }

            return $"Status de {friendlyName} ({entityId}): {state}.";
        }
        catch
        {
            return $"Resposta do Home Assistant para {entityId}:\n{stateJson}";
        }
    }

    private static string BuildHomeAssistantStatesSummary(string statesJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(statesJson);
            var active = new List<string>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var entityId = item.GetProperty("entity_id").GetString() ?? "";
                var state = item.GetProperty("state").GetString() ?? "";

                if (state is "off" or "unavailable" or "unknown")
                    continue;

                if (!IsVisibleHomeAssistantDomain(entityId))
                    continue;

                var name = entityId;
                if (item.TryGetProperty("attributes", out var attributes) &&
                    attributes.TryGetProperty("friendly_name", out var nameProperty))
                {
                    name = nameProperty.GetString() ?? entityId;
                }

                active.Add($"- {name} ({entityId}): {state}");
            }

            return active.Count == 0
                ? "Nao encontrei dispositivos ligados ou ativos no Home Assistant."
                : "Dispositivos ligados ou ativos no Home Assistant:\n" + string.Join("\n", active.Take(25));
        }
        catch
        {
            return "Recebi os estados do Home Assistant, mas nao consegui resumir a resposta.";
        }
    }

    private static bool IsVisibleHomeAssistantDomain(string entityId)
    {
        return entityId.StartsWith("light.", StringComparison.OrdinalIgnoreCase) ||
            entityId.StartsWith("switch.", StringComparison.OrdinalIgnoreCase) ||
            entityId.StartsWith("fan.", StringComparison.OrdinalIgnoreCase) ||
            entityId.StartsWith("climate.", StringComparison.OrdinalIgnoreCase) ||
            entityId.StartsWith("cover.", StringComparison.OrdinalIgnoreCase) ||
            entityId.StartsWith("media_player.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUsableKnowledgeResult(string path)
    {
        var normalized = path.Replace('\\', '/');

        return !normalized.StartsWith("07_Nexus/", StringComparison.OrdinalIgnoreCase) &&
            !normalized.EndsWith("/_index.md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRelevantKnowledgeResult(string query, MarkdownSearchResult result)
    {
        var terms = ExtractSpecificTerms(query);

        if (terms.Length == 0)
            return result.Score >= 8;

        var searchable = NormalizeSearchText($"{result.Path} {result.Title} {result.Snippet}");
        var matches = terms.Count(term => searchable.Contains(term, StringComparison.OrdinalIgnoreCase));

        return terms.Length == 1 ? matches == 1 : matches == terms.Length;
    }

    private static string[] ExtractSpecificTerms(string text)
    {
        var normalized = NormalizeSearchText(text);

        return Regex.Matches(normalized, @"[a-z0-9]+")
            .Select(match => match.Value)
            .Where(term => term.Length > 2 || term is "vm" or "ct")
            .Where(term => !IsAutoKnowledgeStopWord(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsAutoKnowledgeStopWord(string term)
    {
        return term is
            "nexus" or "como" or "fazer" or "faco" or "faca" or "gerar" or "criar" or
            "uma" or "novo" or "nova" or "para" or "sobre" or "passo" or "ensine" or
            "explique" or "configurar" or "instalar";
    }

    private static string NormalizeSearchText(string text)
    {
        return text
            .ToLowerInvariant()
            .Replace("á", "a")
            .Replace("à", "a")
            .Replace("ã", "a")
            .Replace("â", "a")
            .Replace("é", "e")
            .Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ô", "o")
            .Replace("õ", "o")
            .Replace("ú", "u")
            .Replace("ç", "c");
    }

    private async Task<ActionResult<ChatResponse>> LocalAnswer(string userMessage, string answer, string intent)
    {
        _logs.Conversation(userMessage, answer);

        await _hub.Clients.All.SendAsync("nexus:log", new
        {
            level = "info",
            message = $"Resposta local executada: {intent}"
        });

        await _hub.Clients.All.SendAsync("nexus:speaking", new { answer });

        return Ok(new ChatResponse
        {
            Answer = answer,
            Intent = intent
        });
    }

    private async Task<ActionResult<ChatResponse>> CasualConversation(string message, string intent)
    {
        var prompt = BuildCasualPrompt(message, includeOperationalContext: true);
        var answer = await _ollama.AskAsync(prompt);

        if (string.IsNullOrWhiteSpace(answer))
            answer = "Estou por aqui, Gabriel. Pronto para te ajudar. Quer continuar evoluindo o Nexus ou prefere revisar alguma tarefa agora?";

        answer = CleanConversationalAnswer(answer, message);
        SaveConversationMemory(message, answer);
        return await LocalAnswer(message, answer.Trim(), intent);
    }

    private async Task<ActionResult<ChatResponse>> ConversationStarter(string message, string intent)
    {
        var tasks = _tasks.ReadAll();
        var memory = _memory.ReadAll();
        var operationLog = _obsidian.ReadFile("07_Nexus/operation-log.md");

        var prompt = $@"
Você é Nexus, assistente pessoal de Gabriel.

Sugira um assunto útil para conversar agora.
Use o contexto abaixo.
Pode sugerir algo sobre o projeto Nexus, tarefas, procedimentos, rotina operacional ou organização do Obsidian.
Seja natural e breve.

Tarefas:
{tasks}

Memórias:
{memory}

Log operacional:
{operationLog}

Responda como se estivesse puxando assunto com Gabriel.
";

        var answer = await _ollama.AskAsync(prompt);

        if (string.IsNullOrWhiteSpace(answer))
            answer = "Podemos falar sobre o próximo passo do Nexus, revisar suas tarefas ou escolher um procedimento importante para melhorar.";

        answer = CleanConversationalAnswer(answer, message);
        SaveConversationMemory(message, answer);
        return await LocalAnswer(message, answer.Trim(), intent);
    }

    private static string DetectIntent(string message)
    {
        var lower = message.ToLowerInvariant();
        var command = StripWakeWord(lower);

        if (lower.Contains("sair do modo operaÃ§Ã£o") || lower.Contains("sair do modo operacao") || lower.Contains("desativar modo operaÃ§Ã£o") || lower.Contains("desativar modo operacao"))
            return "operation_mode_off";

        if (lower.Contains("entrar em modo operaÃ§Ã£o") || lower.Contains("entrar em modo operacao") || lower.Contains("modo operaÃ§Ã£o") || lower.Contains("modo operacao"))
            return "operation_mode_on";

        if (lower.Contains("iniciar atendimento"))
            return "start_attendance";

        if (lower.Contains("gerar relatÃ³rio de atendimento") || lower.Contains("gerar relatorio de atendimento"))
            return "attendance_report";

        if (lower.Contains("criar passagem de turno") || lower.Contains("gerar passagem de turno"))
            return "shift_handoff";

        if (
            lower.Contains("puxe assunto") ||
            lower.Contains("puxar assunto") ||
            lower.Contains("sobre o que podemos falar") ||
            lower.Contains("me diga algo interessante")
        )
            return "conversation_starter";

        if (
            lower.Contains("estÃ¡ por aÃ­") ||
            lower.Contains("está por aí") ||
            lower.Contains("esta por ai") ||
            lower.Contains("tÃ¡ por aÃ­") ||
            lower.Contains("tá por aí") ||
            lower.Contains("ta por ai") ||
            lower.Contains("vocÃª estÃ¡ aÃ­") ||
            lower.Contains("você está aí") ||
            lower.Contains("voce esta ai") ||
            lower.Contains("online")
        )
            return "presence_check";

        if (lower.Contains("que horas") || lower.Contains("hora atual") || lower.Contains("horário") || lower.Contains("horÃ¡rio") || lower.Contains("horario"))
            return "time_check";

        if (lower.Contains("que dia") || lower.Contains("data de hoje") || lower.Contains("dia é hoje") || lower.Contains("dia Ã© hoje") || lower.Contains("dia e hoje"))
            return "date_check";

        if (lower.Contains("seu status") || lower.Contains("status do sistema") || lower.Contains("qual status"))
            return "system_status";

        if (
            lower.Contains("tempo") ||
            lower.Contains("clima") ||
            lower.Contains("previsao do tempo") ||
            lower.Contains("previsão do tempo") ||
            lower.Contains("vai chover") ||
            lower.Contains("chuva hoje") ||
            lower.Contains("chover hoje") ||
            lower.Contains("rio preto")
        )
            return "weather_report";

        if (
            lower.Contains("aprenda meus gostos musicais") ||
            lower.Contains("aprender meus gostos musicais") ||
            lower.Contains("aprenda minhas musicas") ||
            lower.Contains("aprenda minhas músicas") ||
            lower.Contains("me conheca pelo spotify") ||
            lower.Contains("me conheça pelo spotify") ||
            lower.Contains("salve meus gostos musicais") ||
            lower.Contains("memoria musical") ||
            lower.Contains("memória musical")
        )
            return "spotify_learning";

        if (
            lower.Contains("spotify") ||
            lower.Contains("musica") ||
            lower.Contains("música") ||
            lower.Contains("playlist") ||
            lower.Contains("tocar ") ||
            lower.Contains("toque ") ||
            lower.Contains("tocando") ||
            lower.Contains("musica atual") ||
            lower.Contains("música atual") ||
            lower.Contains("pausar") ||
            lower.Contains("pause") ||
            lower.Contains("proxima") ||
            lower.Contains("próxima") ||
            lower.Contains("anterior") ||
            lower.Contains("volume")
        )
            return "spotify_control";

        if (
            lower.Contains("como está") ||
            lower.Contains("como esta") ||
            lower.Contains("como você está") ||
            lower.Contains("como voce esta") ||
            lower.Contains("bom dia") ||
            lower.Contains("boa tarde") ||
            lower.Contains("boa noite") ||
            lower.Contains("e aí") ||
            lower.Contains("e ai") ||
            lower.Contains("tudo bem") ||
            lower.Contains("estou cansado") ||
            lower.Contains("to cansado") ||
            lower.Contains("estou meio perdido") ||
            lower.Contains("o que fazemos agora") ||
            lower.Contains("conversa comigo")
        )
            return "casual_conversation";

        if (lower.Contains("mostre minhas tarefas") || lower.Contains("minhas tarefas") || lower.Contains("listar tarefas") || lower.Contains("liste minhas tarefas"))
            return "list_tasks";

        if (
            lower.Contains("mostre minhas memÃ³rias") ||
            lower.Contains("mostre minhas memorias") ||
            lower.Contains("minhas memÃ³rias") ||
            lower.Contains("minhas memorias") ||
            lower.Contains("o que vocÃª lembra") ||
            lower.Contains("o que voce lembra") ||
            lower.Contains("o que vocÃƒÂª lembra") ||
            lower.Contains("minhas memÃƒÂ³rias")
        )
            return "list_memory";

        if (lower.Contains("lembre") || lower.Contains("memorize") || lower.Contains("salve na memÃ³ria") || lower.Contains("salve na memÃƒÂ³ria"))
            return "save_memory";

        if (lower.Contains("tarefa") || lower.Contains("lembrete") || lower.Contains("me lembre de"))
            return "create_task";

        if (
            lower.Contains("briefing do dia") ||
            lower.Contains("painel do dia") ||
            lower.Contains("resumo do dia") ||
            lower.Contains("como esta o dia") ||
            lower.Contains("como está o dia")
        )
            return "today_briefing";

        if (
            lower.Contains("zabbix") &&
            (lower.Contains("relatorio") || lower.Contains("relatÃ³rio") || lower.Contains("problemas") || lower.Contains("alertas") || lower.Contains("criticos") || lower.Contains("crÃ­ticos"))
        )
            return "zabbix_report";

        if (
            lower.Contains("zabbix") &&
            (lower.Contains("status") || lower.Contains("monitoramento") || lower.Contains("hosts"))
        )
            return "zabbix_status";

        if (
            lower.Contains("status da rede") ||
            lower.Contains("quem esta online") ||
            lower.Contains("quem está online") ||
            lower.Contains("algum dispositivo caiu") ||
            lower.Contains("verifique dispositivos criticos") ||
            lower.Contains("verifique dispositivos críticos") ||
            lower.Contains("verificar dispositivos") ||
            lower.Contains("dispositivos da rede")
        )
            return "network_status";

        if (
            lower.Contains("confirmar acao") ||
            lower.Contains("confirmar ação")
        )
            return "home_assistant_confirm";

        if (
            lower.Contains("ligar luz") ||
            lower.Contains("liga a luz") ||
            lower.Contains("ligue a luz") ||
            lower.Contains("acender luz") ||
            lower.Contains("acende a luz") ||
            lower.Contains("desligar luz") ||
            lower.Contains("desliga a luz") ||
            lower.Contains("apagar luz") ||
            lower.Contains("apaga a luz") ||
            lower.Contains("ligar tomada") ||
            lower.Contains("liga a tomada") ||
            lower.Contains("desligar tomada") ||
            lower.Contains("desliga a tomada") ||
            lower.Contains("ligar ventilador") ||
            lower.Contains("liga o ventilador") ||
            lower.Contains("desligar ventilador") ||
            lower.Contains("desliga o ventilador") ||
            lower.Contains("aumenta a luz") ||
            lower.Contains("diminuir a luz") ||
            lower.Contains("luz do") ||
            lower.Contains("luz da") ||
            lower.Contains("status da luz") ||
            lower.Contains("status do dispositivo") ||
            lower.Contains("o que esta ligado em casa") ||
            lower.Contains("o que está ligado em casa")
        )
            return "home_assistant_control";

        if (
            lower.Contains("aprenda sobre") ||
            lower.Contains("gere uma nota sobre") ||
            lower.Contains("crie uma nota sobre") ||
            lower.Contains("crie um procedimento sobre")
        )
            return "auto_knowledge_force";

        if (
            lower.Contains("crie um passo a passo") ||
            lower.Contains("criar passo a passo") ||
            lower.Contains("como fazer") ||
            lower.Contains("como gerar") ||
            lower.Contains("como criar") ||
            lower.Contains("como faco") ||
            lower.Contains("como faço") ||
            lower.Contains("como faÃ§o") ||
            lower.Contains("me ensine") ||
            (lower.Contains("me explique") && !lower.Contains("procedimento")) ||
            lower.Contains("o que e") ||
            lower.Contains("o que é") ||
            lower.Contains("como configurar") ||
            lower.Contains("como instalar")
        )
            return "auto_knowledge";

        if (
            lower.Contains("me explique o procedimento") ||
            lower.Contains("explique o procedimento") ||
            lower.Contains("resuma o procedimento") ||
            lower.Contains("passo a passo sobre") ||
            lower.Contains("como fazer") ||
            lower.Contains("como faÃ§o") ||
            lower.Contains("como faco")
        )
            return "procedure_answer";

        if (
            lower.Contains("procure procedimento") ||
            lower.Contains("buscar procedimento") ||
            lower.Contains("busque procedimento") ||
            lower.Contains("procurar procedimento") ||
            lower.Contains("pesquise procedimento") ||
            lower.Contains("documentaÃ§Ã£o sobre") ||
            lower.Contains("documentaÃƒÂ§ÃƒÂ£o sobre") ||
            lower.Contains("documentacao sobre") ||
            lower.Contains("o que existe sobre") ||
            lower.Contains("base sobre") ||
            lower.Contains("procure sobre") ||
            lower.Contains("busque sobre") ||
            lower.Contains("obsidia") ||
            lower.Contains("gpo") ||
            lower.Contains("polÃ­tica de grupo") ||
            lower.Contains("polÃƒÂ­tica de grupo") ||
            lower.Contains("politica de grupo") ||
            lower.Contains("pÃ¡gina web") ||
            lower.Contains("pÃƒÂ¡gina web") ||
            lower.Contains("pagina web") ||
            (lower.Contains("obsidian") && (lower.Contains("procure") || lower.Contains("busque") || lower.Contains("buscar") || lower.Contains("pesquise") || lower.Contains("pesquisar"))) ||
            (lower.Contains("procedimento") && (lower.Contains("procure") || lower.Contains("busque") || lower.Contains("buscar") || lower.Contains("pesquise") || lower.Contains("pesquisar"))) ||
            (lower.Contains("documento") && (lower.Contains("procure") || lower.Contains("busque") || lower.Contains("buscar") || lower.Contains("pesquise") || lower.Contains("pesquisar")))
        )
            return "search_docs";

        if (
            lower.Contains("assuma controle do pc") ||
            lower.Contains("assumir controle do pc") ||
            lower.Contains("controle do pc") ||
            lower.Contains("controlar meu pc") ||
            lower.Contains("controlar meu computador") ||
            lower.Contains("tome conta do pc") ||
            lower.Contains("tomar conta do pc") ||
            lower.Contains("tome conta do computador") ||
            lower.Contains("tomar conta do computador") ||
            command.StartsWith("abra ") ||
            command.StartsWith("abrir ") ||
            command.StartsWith("abre ") ||
            lower.Contains("abra a pasta") ||
            lower.Contains("abrir a pasta") ||
            lower.Contains("abra o programa") ||
            lower.Contains("abrir o programa") ||
            lower.Contains("pesquise no navegador") ||
            lower.Contains("pesquisar no navegador") ||
            lower.Contains("procure no google") ||
            lower.Contains("pesquise no google") ||
            lower.Contains("pesquise no youtube") ||
            lower.Contains("procure no youtube") ||
            lower.Contains("mande mensagem") ||
            lower.Contains("manda mensagem") ||
            lower.Contains("enviar mensagem") ||
            lower.Contains("envie mensagem") ||
            command.StartsWith("acesse ") ||
            command.StartsWith("acessar ") ||
            command.StartsWith("digite ") ||
            command.StartsWith("escreva ") ||
            command.StartsWith("cole ")
        )
            return "computer_control";

        return "chat_normal";
    }

    private static bool IsOpenAIConfigured()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return !string.IsNullOrWhiteSpace(apiKey) && apiKey != "coloque_sua_chave_aqui";
    }

    private record HomeAssistantParsedAction(
        string Action,
        string EntityId,
        int Brightness,
        bool NeedsConfirmation
    );

    private static bool IsProbablyCasual(string message)
    {
        var lower = message.ToLowerInvariant();

        if (message.Length > 140)
            return false;

        var casualWords = new[]
        {
            "oi", "olá", "ola", "bom dia", "boa tarde", "boa noite",
            "tudo bem", "como vai", "como está", "como esta",
            "beleza", "tranquilo", "e aí", "e ai",
            "cansado", "perdido", "desanimado", "animado",
            "o que fazemos", "por onde começo", "por onde comeco", "me ajuda"
        };

        return casualWords.Any(word => lower.Contains(word));
    }

    private string BuildCasualPrompt(string message, bool includeOperationalContext)
    {
        var personality = _obsidian.ReadFile("07_Nexus/personality.md");
        var conversationMemory = _obsidian.ReadFile("07_Nexus/conversation-memory.md");
        var operationMode = includeOperationalContext ? _obsidian.ReadFile("07_Nexus/operation-mode.md") : "";
        var tasks = includeOperationalContext ? _tasks.ReadAll() : "";

        return $@"
Você é Nexus, assistente pessoal de Gabriel.

Responda como uma IA conversacional natural, próxima e útil.
Não seja seco demais.
Não seja longo demais.
Use português do Brasil.
Se fizer sentido, puxe um assunto útil com base no contexto.
Faça no máximo uma pergunta no final.
Seja breve: no máximo 4 frases.
Se Gabriel disser que está cansado, sugira algo leve e não pressione.
Não finja emoções humanas profundas, mas pode soar amigável, calmo e presente.

Personalidade:
{personality}

Memória conversacional:
{conversationMemory}

Modo operação:
{operationMode}

Tarefas:
{tasks}

Mensagem do Gabriel:
{message}

Responda de forma natural.
";
    }

    private void SaveConversationMemory(string message, string answer)
    {
        var summary = answer.Length > 300 ? answer[..300] : answer;
        _obsidian.AppendToFile(
            "07_Nexus/conversation-memory.md",
            $"\n\n## {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nÚltima conversa casual: {message}\nResumo da resposta: {summary}\n");
    }

    private static string CleanConversationalAnswer(string answer, string userMessage)
    {
        var clean = answer.Trim();
        var lower = userMessage.ToLowerInvariant();

        if (lower.Contains("cansado") || lower.Contains("cansada"))
        {
            return "Entendi, Gabriel. Então vamos com calma. Podemos fazer algo leve agora: revisar uma pendência, organizar uma nota ou só escolher o próximo passo sem mexer em código pesado. Quer seguir por algo mais tranquilo?";
        }

        if (clean.Length > 420)
            clean = clean[..420].Trim();

        var firstQuestion = clean.IndexOf('?');
        if (firstQuestion < 0)
        {
            var sentences = clean
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(3)
                .ToArray();

            return sentences.Length == 0 ? clean : string.Join(". ", sentences) + ".";
        }

        return clean[..(firstQuestion + 1)].Trim();
    }

    private static string CleanMemoryCommand(string message)
    {
        return message
            .Replace("Nexus", "", StringComparison.OrdinalIgnoreCase)
            .Replace("lembre que", "", StringComparison.OrdinalIgnoreCase)
            .Replace("memorize que", "", StringComparison.OrdinalIgnoreCase)
            .Replace("salve na memÃ³ria que", "", StringComparison.OrdinalIgnoreCase)
            .Replace("salve na memÃƒÂ³ria que", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',');
    }

    private static string CleanTaskCommand(string message)
    {
        return message
            .Replace("Nexus", "", StringComparison.OrdinalIgnoreCase)
            .Replace("crie uma tarefa para", "", StringComparison.OrdinalIgnoreCase)
            .Replace("criar tarefa para", "", StringComparison.OrdinalIgnoreCase)
            .Replace("me lembre de", "", StringComparison.OrdinalIgnoreCase)
            .Replace("lembrete para", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',');
    }

    private static string CleanDocsCommand(string message)
    {
        return message
            .Replace("Nexus", "", StringComparison.OrdinalIgnoreCase)
            .Replace("procure procedimento sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("buscar procedimento sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("busque procedimento sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("procurar procedimento sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("pesquise procedimento sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("documentaÃ§Ã£o sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("documentaÃƒÂ§ÃƒÂ£o sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("documentacao sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("o que existe sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("base sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("procure sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("busque sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("procure", "", StringComparison.OrdinalIgnoreCase)
            .Replace("buscar", "", StringComparison.OrdinalIgnoreCase)
            .Replace("busque", "", StringComparison.OrdinalIgnoreCase)
            .Replace("pesquise", "", StringComparison.OrdinalIgnoreCase)
            .Replace("pesquisar", "", StringComparison.OrdinalIgnoreCase)
            .Replace("como", "", StringComparison.OrdinalIgnoreCase)
            .Replace("no obsidian", "", StringComparison.OrdinalIgnoreCase)
            .Replace("no obsidia", "", StringComparison.OrdinalIgnoreCase)
            .Replace("na obsidian", "", StringComparison.OrdinalIgnoreCase)
            .Replace("na obsidia", "", StringComparison.OrdinalIgnoreCase)
            .Replace("no vault", "", StringComparison.OrdinalIgnoreCase)
            .Replace("na base", "", StringComparison.OrdinalIgnoreCase)
            .Replace("criar uma", "", StringComparison.OrdinalIgnoreCase)
            .Replace("criar um", "", StringComparison.OrdinalIgnoreCase)
            .Replace("crie uma", "", StringComparison.OrdinalIgnoreCase)
            .Replace("crie um", "", StringComparison.OrdinalIgnoreCase)
            .Replace("fazer uma", "", StringComparison.OrdinalIgnoreCase)
            .Replace("fazer um", "", StringComparison.OrdinalIgnoreCase)
            .Replace("limpar o jornal", "limpeza journal", StringComparison.OrdinalIgnoreCase)
            .Replace("limpar journal", "limpeza journal", StringComparison.OrdinalIgnoreCase)
            .Replace("jornal", "journal", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',', '?', '!');
    }

    private static string CleanProcedureCommand(string message)
    {
        return message
            .Replace("Nexus", "", StringComparison.OrdinalIgnoreCase)
            .Replace("me explique o procedimento sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("explique o procedimento sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("resuma o procedimento sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("passo a passo sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("como fazer", "", StringComparison.OrdinalIgnoreCase)
            .Replace("como faÃ§o", "", StringComparison.OrdinalIgnoreCase)
            .Replace("como faco", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',', '?', '!');
    }

    private static string CleanAutoKnowledgeCommand(string message)
    {
        return message
            .Replace("Nexus", "", StringComparison.OrdinalIgnoreCase)
            .Replace("crie um passo a passo para", "", StringComparison.OrdinalIgnoreCase)
            .Replace("crie um passo a passo", "", StringComparison.OrdinalIgnoreCase)
            .Replace("criar passo a passo para", "", StringComparison.OrdinalIgnoreCase)
            .Replace("como fazer", "", StringComparison.OrdinalIgnoreCase)
            .Replace("como gerar", "", StringComparison.OrdinalIgnoreCase)
            .Replace("como criar", "", StringComparison.OrdinalIgnoreCase)
            .Replace("como faço", "", StringComparison.OrdinalIgnoreCase)
            .Replace("como faco", "", StringComparison.OrdinalIgnoreCase)
            .Replace("me ensine", "", StringComparison.OrdinalIgnoreCase)
            .Replace("me explique", "", StringComparison.OrdinalIgnoreCase)
            .Replace("o que é", "", StringComparison.OrdinalIgnoreCase)
            .Replace("o que e", "", StringComparison.OrdinalIgnoreCase)
            .Replace("como configurar", "", StringComparison.OrdinalIgnoreCase)
            .Replace("como instalar", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',', '?', '!');
    }

    private static string CleanAttendanceCommand(string message)
    {
        return message
            .Replace("Nexus", "", StringComparison.OrdinalIgnoreCase)
            .Replace("iniciar atendimento sobre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("iniciar atendimento", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',', '?', '!');
    }

    private ComputerActionResult ExecuteComputerCommand(string message)
    {
        var lower = message.ToLowerInvariant().Trim();

        if (
            lower.Contains("assuma controle") ||
            lower.Contains("assumir controle") ||
            lower.Contains("controle do pc") ||
            lower.Contains("controlar meu pc") ||
            lower.Contains("controlar meu computador") ||
            lower.Contains("tome conta") ||
            lower.Contains("tomar conta")
        )
        {
            return new ComputerActionResult(true, BuildComputerControlHelp(), "operator-mode");
        }

        if (lower.Contains("pesquise no navegador") || lower.Contains("pesquisar no navegador"))
            return _computer.SearchWeb(RemoveCommand(message, "Nexus", "pesquise no navegador", "pesquisar no navegador"));

        if (lower.Contains("procure no google") || lower.Contains("pesquise no google"))
            return _computer.SearchWeb(RemoveCommand(message, "Nexus", "procure no google", "pesquise no google"));

        if (lower.Contains("pesquise no youtube") || lower.Contains("procure no youtube"))
        {
            var query = RemoveCommand(message, "Nexus", "pesquise no youtube", "procure no youtube", "por");
            return _computer.OpenUrl("https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(query));
        }

        if (lower.Contains("mande mensagem") || lower.Contains("manda mensagem") || lower.Contains("enviar mensagem") || lower.Contains("envie mensagem"))
            return ExecuteMessageCommand(message);

        if (lower.StartsWith("digite ") || lower.StartsWith("escreva ") || lower.StartsWith("cole "))
            return _computer.PasteText(RemoveCommand(message, "Nexus", "digite", "escreva", "cole"));

        if (lower.Contains("pasta"))
            return _computer.OpenFolder(RemoveCommand(message, "Nexus", "abra a pasta", "abrir a pasta", "abre a pasta", "pasta"));

        if (lower.Contains("http://") || lower.Contains("https://") || lower.Contains(".com") || lower.Contains(".br") || lower.StartsWith("acesse ") || lower.StartsWith("acessar "))
            return _computer.OpenSite(RemoveCommand(message, "Nexus", "abra", "abrir", "abre", "acesse", "acessar", "site", "o site"));

        var target = RemoveCommand(message, "Nexus", "abra o programa", "abrir o programa", "abre o programa", "abra", "abrir", "abre");
        var siteResult = _computer.OpenSite(target);
        if (siteResult.Success && siteResult.Target?.StartsWith("http", StringComparison.OrdinalIgnoreCase) == true)
            return siteResult;

        return _computer.OpenApp(target);
    }

    private ComputerActionResult ExecuteMessageCommand(string message)
    {
        var cleaned = RemoveCommand(
            message,
            "Nexus",
            "mande mensagem para",
            "manda mensagem para",
            "enviar mensagem para",
            "envie mensagem para",
            "mande mensagem",
            "manda mensagem",
            "enviar mensagem",
            "envie mensagem");

        var separatorIndex = cleaned.IndexOf(':');
        var separatorLength = 1;

        if (separatorIndex < 0)
        {
            separatorIndex = cleaned.IndexOf(" dizendo ", StringComparison.OrdinalIgnoreCase);
            separatorLength = " dizendo ".Length;
        }

        if (separatorIndex < 0)
        {
            separatorIndex = cleaned.IndexOf(" com o texto ", StringComparison.OrdinalIgnoreCase);
            separatorLength = " com o texto ".Length;
        }

        if (separatorIndex < 0)
        {
            return new ComputerActionResult(
                false,
                "Para preparar mensagem, use: Nexus, mande mensagem para 11999999999: texto da mensagem.");
        }

        var recipient = cleaned[..separatorIndex]
            .Replace("dizendo", "", StringComparison.OrdinalIgnoreCase)
            .Replace("com o texto", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',', ':', ';');

        var text = cleaned[(separatorIndex + separatorLength)..]
            .Replace("dizendo", "", StringComparison.OrdinalIgnoreCase)
            .Replace("com o texto", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',', ':', ';');

        if (string.IsNullOrWhiteSpace(recipient) || string.IsNullOrWhiteSpace(text))
        {
            return new ComputerActionResult(
                false,
                "Faltou o destinatário ou o texto. Exemplo: Nexus, mande mensagem para 11999999999: estou chegando.");
        }

        return _computer.PrepareWhatsAppMessage(recipient, text);
    }

    private static string StripWakeWord(string message)
    {
        return message
            .Replace("nexus", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',', '?', '!', ':', ';');
    }

    private static string BuildComputerControlHelp()
    {
        return """
        Modo operador assistido ativado.

        Eu posso controlar o computador nestas acoes seguras:

        1. Abrir programas
        Exemplo: Nexus, abra o Chrome

        2. Abrir pastas
        Exemplo: Nexus, abra a pasta Downloads

        3. Abrir sites
        Exemplo: Nexus, abra o YouTube
        Exemplo: Nexus, acesse Gmail

        4. Pesquisar no navegador
        Exemplo: Nexus, pesquise no navegador criar GPO para abertura de paginas
        Exemplo: Nexus, pesquise no YouTube tutorial de GPO

        5. Digitar/colar texto na janela ativa
        Exemplo: Nexus, digite texto de teste

        6. Preparar mensagens no WhatsApp
        Exemplo: Nexus, mande mensagem para 11999999999: estou chegando

        Atencao: antes de usar o comando digite, clique no campo correto. Para mensagens, eu deixo pronta para voce revisar e confirmar.

        Por seguranca, eu nao vou apagar arquivos, executar comandos destrutivos ou mexer em credenciais sem confirmacao explicita.
        """;
    }

    private static string RemoveCommand(string message, params string[] tokens)
    {
        var cleaned = message;

        foreach (var token in tokens)
            cleaned = cleaned.Replace(token, "", StringComparison.OrdinalIgnoreCase);

        return cleaned.Trim(' ', '.', ',', '?', '!', ':', ';');
    }

    private static string CleanSpotifyPlayCommand(string message)
    {
        var cleaned = RemoveCommand(
            message,
            "Nexus",
            "spotify",
            "tocar minhas musicas",
            "tocar minhas músicas",
            "toque minhas musicas",
            "toque minhas músicas",
            "tocar musica",
            "tocar música",
            "toque musica",
            "toque música",
            "tocar playlist",
            "toque playlist",
            "tocar",
            "toque",
            "play"
        );

        if (string.IsNullOrWhiteSpace(cleaned) ||
            cleaned.Equals("minhas musicas", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("minhas músicas", StringComparison.OrdinalIgnoreCase))
            return "";

        return cleaned;
    }

    private static int? ExtractFirstNumber(string message)
    {
        var match = Regex.Match(message, @"\d+");
        return match.Success && int.TryParse(match.Value, out var value)
            ? Math.Clamp(value, 0, 100)
            : null;
    }

    private static int? ExtractZabbixMinimumSeverity(string message)
    {
        var lower = message.ToLowerInvariant();

        if (lower.Contains("disaster") || lower.Contains("desastre"))
            return 5;

        if (lower.Contains("high") || lower.Contains("alta") || lower.Contains("critico") || lower.Contains("crÃ­tico"))
            return 4;

        if (lower.Contains("average") || lower.Contains("media") || lower.Contains("mÃ©dia"))
            return 3;

        if (lower.Contains("warning") || lower.Contains("aviso"))
            return 2;

        return null;
    }

    private static string BuildDocsAnswer(string query, IReadOnlyList<MarkdownSearchResult> results)
    {
        if (results.Count == 0)
            return $"Nao encontrei documentos no Obsidian sobre: {query}\n\nTente buscar por termos mais especificos, como `journal`, `Shift BI`, `rede`, `purgV3`, `/Dados` ou `binario`.";

        var best = results[0];
        var answer = "Resumo\n";
        answer += $"Encontrei {results.Count} documento(s) relacionado(s) no Obsidian para: `{query}`.\n";
        answer += $"O mais relevante parece ser: {best.Title}.\n\n";
        answer += "Documentos encontrados\n";

        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var snippet = NormalizeSnippet(result.Snippet);
            var priority = result.IsPrioritySource ? "prioritario" : "base importada";

            answer += $"{i + 1}. {result.Title}\n";
            answer += $"Fonte: {priority}\n";
            answer += $"Caminho: {result.Path}\n";
            answer += $"Trecho: {snippet}\n\n";
        }

        answer += "Proxima etapa\n";
        answer += $"Abra a pagina Docs e pesquise por `{query}` para visualizar o arquivo completo.";
        return answer;
    }

    private static string NormalizeSnippet(string snippet)
    {
        var normalized = string.Join(" ", snippet.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length > 320 ? normalized[..320] + "..." : normalized;
    }

    private string BuildAttendanceReport()
    {
        var log = _obsidian.ReadFile("07_Nexus/operation-log.md");
        return _operation.BuildOperationFrame(
            $"RelatÃ³rio de atendimento gerado com base no log operacional.\n\n{TrimForChat(log)}",
            "Revisar o relatÃ³rio, complementar evidÃªncias e registrar o chamado.");
    }

    private string BuildShiftHandoff()
    {
        var log = _obsidian.ReadFile("07_Nexus/operation-log.md");
        return _operation.BuildOperationFrame(
            $"Passagem de turno baseada nas Ãºltimas aÃ§Ãµes registradas.\n\n{TrimForChat(log)}",
            "Enviar a passagem de turno para o responsÃ¡vel e manter links dos procedimentos usados.");
    }

    private static string TrimForChat(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "Nenhum registro operacional encontrado.";

        return content.Length <= 1800 ? content : content[^1800..];
    }
}
