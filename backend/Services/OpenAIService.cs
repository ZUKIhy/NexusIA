using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NexusBackend.Services;

public class OpenAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MemoryService _memoryService;
    private readonly TaskService _taskService;
    private readonly ObsidianService _obsidianService;
    private readonly OllamaService _ollamaService;

    public OpenAIService(
        IHttpClientFactory httpClientFactory,
        MemoryService memoryService,
        TaskService taskService,
        ObsidianService obsidianService,
        OllamaService ollamaService)
    {
        _httpClientFactory = httpClientFactory;
        _memoryService = memoryService;
        _taskService = taskService;
        _obsidianService = obsidianService;
        _ollamaService = ollamaService;
    }

    public async Task<string> AskAsync(string message)
    {
        var docs = _obsidianService.SearchMarkdownDetailed(message)
            .Where(result => IsRelevantKnowledgeResult(message, result))
            .Take(3)
            .ToList();

        if (docs.Count > 0)
            return BuildDocsFirstAnswer(message, docs);

        var ollamaAnswer = await _ollamaService.AskAsync(BuildLocalPrompt(message));

        if (!string.IsNullOrWhiteSpace(ollamaAnswer))
        {
            SaveGeneratedKnowledge(message, ollamaAnswer, "ollama");
            return ollamaAnswer.Trim();
        }

        var openAiAnswer = await TryAskOpenAI(message);

        if (!string.IsNullOrWhiteSpace(openAiAnswer))
        {
            SaveGeneratedKnowledge(message, openAiAnswer, "openai");
            return openAiAnswer.Trim();
        }

        return MockAnswer(message);
    }

    public string BuildLocalPrompt(string message)
    {
        var context = BuildContext(message);

        return $@"
Voce e Nexus, assistente pessoal tecnico de Gabriel.

Responda em portugues do Brasil.
Seja direto, pratico e tecnico.
Use o contexto abaixo se for relevante.
Nao invente senhas, caminhos, credenciais ou comandos.
Se nao tiver certeza, diga que precisa consultar a documentacao.
Para procedimentos, sempre organize em:
- Objetivo
- Pre-requisitos
- Passo a passo
- Cuidados
- Validacao
- Rollback

Contexto do Obsidian:
{context}

Pergunta do Gabriel:
{message}
";
    }

    public bool IsOpenAIConfigured()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return !string.IsNullOrWhiteSpace(apiKey) && apiKey != "coloque_sua_chave_aqui";
    }

    private async Task<string?> TryAskOpenAI(string message)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4.1-mini";

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "coloque_sua_chave_aqui")
            return null;

        var prompt = BuildOpenAIPrompt(message);
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model,
            input = prompt
        };

        var json = JsonSerializer.Serialize(payload);
        var response = await client.PostAsync(
            "https://api.openai.com/v1/responses",
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            if (body.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase))
                return "Meu cerebro OpenAI esta conectado, mas sem quota disponivel. Posso continuar em modo local com Ollama e Obsidian.";

            return $"Tive um problema ao chamar a OpenAI: {response.StatusCode}. Detalhe: {body}";
        }

        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.TryGetProperty("output_text", out var outputText))
            return outputText.GetString();

        return ExtractTextFromResponse(doc.RootElement);
    }

    private string BuildOpenAIPrompt(string message)
    {
        var context = BuildContext(message);

        return $@"
Voce e N.E.X.U.S, assistente pessoal tecnico de Gabriel.

Regras:
- Responda em portugues do Brasil.
- Seja direto, util, tecnico e organizado.
- Use a memoria e o contexto abaixo apenas se for relevante.
- Nao invente memorias, senhas, caminhos, credenciais ou comandos.
- Para procedimentos, organize em objetivo, pre-requisitos, passo a passo, cuidados, validacao e rollback.

Memoria e contexto:
{context}

Mensagem do Gabriel:
{message}
";
    }

    private string BuildContext(string message)
    {
        var memory = _memoryService.Search(message);
        var tasks = _taskService.ReadAll();
        var docs = _obsidianService.SearchMarkdownDetailed(message)
            .Where(result => IsRelevantKnowledgeResult(message, result))
            .Take(3)
            .Select(result => $"Arquivo: {result.Path}\nTitulo: {result.Title}\nTrecho: {result.Snippet}");

        return $@"
Memorias relacionadas:
{memory}

Documentos relacionados:
{string.Join("\n\n---\n\n", docs)}

Tarefas:
{tasks}
";
    }

    private void SaveGeneratedKnowledge(string question, string answer, string source)
    {
        var slug = Slugify(question);
        var path = $"04_Knowledge/Nexus-Auto/{slug}.md";

        var content = $@"---
type: conhecimento-gerado
source: {source}
status: revisar
created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
tags:
  - nexus
  - conhecimento-gerado
---

# {question.Trim()}

## Pergunta
{question.Trim()}

## Resposta
{answer.Trim()}

## Observacao
Conteudo criado automaticamente pelo Nexus apos nao encontrar resposta direta no Obsidian. Revisar antes de usar como procedimento operacional.
";

        _obsidianService.EnsureFile(path, content);
    }

    private static string BuildDocsFirstAnswer(string message, IReadOnlyList<MarkdownSearchResult> docs)
    {
        var best = docs[0];
        var answer = $"Encontrei informacao no Obsidian antes de usar IA externa.\n\n";
        answer += $"Mais relevante: {best.Title}\n";
        answer += $"Caminho: {best.Path}\n\n";
        answer += "Trechos encontrados:\n\n";

        for (var i = 0; i < docs.Count; i++)
        {
            answer += $"{i + 1}. {docs[i].Title}\n";
            answer += $"Caminho: {docs[i].Path}\n";
            answer += $"Trecho: {NormalizeSnippet(docs[i].Snippet)}\n\n";
        }

        answer += "Use a pagina Docs para abrir o arquivo completo. Se quiser, posso transformar esse conteudo em procedimento padronizado.";
        return answer;
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
            .Where(term => !IsKnowledgeStopWord(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsKnowledgeStopWord(string term)
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

    private static string NormalizeSnippet(string snippet)
    {
        var normalized = string.Join(" ", snippet.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length > 420 ? normalized[..420] + "..." : normalized;
    }

    private static string? ExtractTextFromResponse(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output)) return null;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content)) continue;

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text))
                    return text.GetString();
            }
        }

        return null;
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? $"resposta-{DateTime.Now:yyyyMMddHHmmss}" : slug[..Math.Min(slug.Length, 80)];
    }

    private static string MockAnswer(string message)
    {
        if (message.Contains("ola", StringComparison.OrdinalIgnoreCase) || message.Contains("olá", StringComparison.OrdinalIgnoreCase))
            return "Ola, Gabriel. Estou online em modo local.";

        if (message.Contains("lembre", StringComparison.OrdinalIgnoreCase))
            return "Entendido. Vou salvar isso na memoria.";

        if (message.Contains("tarefa", StringComparison.OrdinalIgnoreCase) || message.Contains("lembrete", StringComparison.OrdinalIgnoreCase))
            return "Entendido. Vou criar essa tarefa.";

        return "Estou funcionando em modo local. Nao encontrei resposta no Obsidian, Ollama nao respondeu e a OpenAI nao ficou disponivel.";
    }
}
