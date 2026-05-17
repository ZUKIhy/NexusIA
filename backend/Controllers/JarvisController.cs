using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NexusBackend.Hubs;
using NexusBackend.Models;
using NexusBackend.Services;

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
            return await LocalAnswer(message, _operation.GenerateReport(), intent);

        if (intent == "shift_handoff")
            return await LocalAnswer(message, _operation.GenerateHandover(), intent);

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

