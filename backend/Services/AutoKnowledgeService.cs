using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NexusBackend.Services;

public class AutoKnowledgeService
{
    private readonly ObsidianService _obsidian;
    private readonly OllamaService _ollama;
    private readonly DocumentIndexService _index;

    public AutoKnowledgeService(
        ObsidianService obsidian,
        OllamaService ollama,
        DocumentIndexService index)
    {
        _obsidian = obsidian;
        _ollama = ollama;
        _index = index;

        EnsureAutoKnowledgeIndex();
    }

    public async Task<AutoKnowledgeResult?> GenerateAndSaveAsync(string userQuestion)
    {
        if (!IsEnabled())
            return null;

        var title = BuildTitle(userQuestion);
        var slug = Slugify(title);
        var folder = GetFolder();
        var targetPath = BuildAvailablePath(folder, slug);
        var prompt = BuildPrompt(userQuestion);

        var answer = await _ollama.AskAsync(prompt);

        if (string.IsNullOrWhiteSpace(answer))
            return null;

        var content = $"""
        ---
        type: auto-knowledge
        status: {GetInitialStatus()}
        source: ollama
        created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
        tags:
          - nexus
          - auto-aprendizado
          - revisar
        ---

        # {title}

        ## Pergunta original
        {userQuestion}

        ## Resposta gerada pelo Nexus
        {answer.Trim()}

        ## Revisao humana
        - [ ] Revisado por Gabriel
        - [ ] Validado em ambiente seguro
        - [ ] Movido para base definitiva, se necessario

        ## Observacoes
        Esta nota foi gerada automaticamente pelo Nexus usando Ollama local.
        Antes de usar em producao, revise os comandos e valide o procedimento.
        """;

        _obsidian.EnsureFile(targetPath, content);

        if (ShouldRebuildIndex())
            _index.SaveIndex();

        _obsidian.AppendToFile(
            $"{folder}/_index.md",
            $"\n- [[{Path.GetFileNameWithoutExtension(targetPath)}|{title}]] - criado em {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n"
        );

        _obsidian.AppendToFile(
            "07_Nexus/operation-log.md",
            $"\n\n## {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nAcao: auto-aprendizado\nPergunta: {userQuestion}\nArquivo criado: {targetPath}\nMotor: Ollama\n"
        );

        return new AutoKnowledgeResult
        {
            Title = title,
            Path = targetPath,
            Content = content,
            Answer = answer.Trim()
        };
    }

    private void EnsureAutoKnowledgeIndex()
    {
        var folder = GetFolder();

        _obsidian.EnsureFile(
            $"{folder}/_index.md",
            """
            ---
            type: index
            area: nexus-auto
            tags:
              - nexus
              - auto-aprendizado
            ---

            # Nexus Auto-Aprendizado

            Notas criadas automaticamente pelo Nexus quando uma pergunta nao foi encontrada na base local.

            ## Regras
            - Toda nota criada aqui deve ser revisada antes de uso em producao.
            - O Nexus pode usar essas notas como memoria tecnica futura.
            - Procedimentos criticos devem ser movidos para `06_Documents/Procedimentos-Padronizados` apos revisao.

            ## Notas
            """
        );
    }

    private string BuildAvailablePath(string folder, string slug)
    {
        var targetPath = $"{folder}/{slug}.md";

        if (!File.Exists(_obsidian.GetFullPath(targetPath)))
            return targetPath;

        return $"{folder}/{slug}-{DateTime.Now:yyyyMMddHHmmss}.md";
    }

    private static bool IsEnabled()
    {
        return (Environment.GetEnvironmentVariable("AUTO_KNOWLEDGE_ENABLED") ?? "true")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldRebuildIndex()
    {
        return (Environment.GetEnvironmentVariable("AUTO_KNOWLEDGE_REBUILD_INDEX") ?? "true")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFolder()
    {
        var folder = Environment.GetEnvironmentVariable("AUTO_KNOWLEDGE_FOLDER") ?? "04_Knowledge/Nexus-Auto";
        return folder.Replace('\\', '/').Trim('/');
    }

    private static string GetInitialStatus()
    {
        var requireReview = (Environment.GetEnvironmentVariable("AUTO_KNOWLEDGE_REQUIRE_REVIEW") ?? "true")
            .Equals("true", StringComparison.OrdinalIgnoreCase);

        return requireReview ? "revisar" : "aprovado";
    }

    private static string BuildPrompt(string userQuestion)
    {
        return $"""
        Voce e Nexus, assistente tecnico de Gabriel.

        Gabriel fez uma pergunta que ainda nao existe na base local do Obsidian.

        Gere uma nota tecnica em Markdown, clara, pratica e segura.

        Regras:
        - Responda em portugues do Brasil.
        - Nao invente senhas, IPs, usuarios, caminhos internos ou credenciais.
        - Se for procedimento tecnico, inclua cuidados e validacao.
        - Se envolver comandos, explique o que cada comando faz.
        - Se houver risco, destaque antes do passo a passo.
        - Nao seja longo demais.
        - Use estrutura organizada.

        Formato desejado:

        ## Resumo

        ## Quando usar

        ## Pre-requisitos

        ## Passo a passo

        ## Comandos uteis

        ## Cuidados

        ## Validacao

        ## Problemas comuns

        ## Proximos passos

        Pergunta:
        {userQuestion}
        """;
    }

    private static string BuildTitle(string question)
    {
        var clean = RemoveKnownCommandPhrases(question)
            .Trim(' ', '.', '?', '!', ',', ':', ';');

        if (string.IsNullOrWhiteSpace(clean))
            clean = "Nota gerada pelo Nexus";

        return char.ToUpper(clean[0]) + clean[1..];
    }

    private static string Slugify(string text)
    {
        var normalized = RemoveDiacritics(text).ToLowerInvariant();

        normalized = Regex.Replace(normalized, @"[^a-z0-9]+", "-");
        normalized = normalized.Trim('-');

        if (string.IsNullOrWhiteSpace(normalized))
            normalized = $"nota-{DateTime.Now:yyyyMMddHHmmss}";

        return normalized;
    }

    private static string RemoveKnownCommandPhrases(string text)
    {
        var clean = text;
        var phrases = new[]
        {
            "Nexus",
            "aprenda sobre",
            "gere uma nota sobre",
            "crie uma nota sobre",
            "crie um procedimento sobre",
            "crie um passo a passo para",
            "crie um passo a passo",
            "criar passo a passo para",
            "como fazer",
            "como gerar",
            "como criar",
            "como faco",
            "como faço",
            "me ensine",
            "me explique",
            "o que e",
            "o que é",
            "como configurar",
            "como instalar"
        };

        foreach (var phrase in phrases)
            clean = clean.Replace(phrase, "", StringComparison.OrdinalIgnoreCase);

        return clean;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(capacity: normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

public class AutoKnowledgeResult
{
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
    public string Content { get; set; } = "";
    public string Answer { get; set; } = "";
}
