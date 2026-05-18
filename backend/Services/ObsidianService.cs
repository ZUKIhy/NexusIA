namespace NexusBackend.Services;

using System.Text.RegularExpressions;

public record MarkdownSearchResult(
    string Path,
    string Title,
    string Folder,
    int Score,
    string Snippet,
    bool IsPrioritySource
);

public class ObsidianService
{
    private readonly object _fileLock = new();
    private readonly object _cacheLock = new();
    private List<MarkdownDocument>? _markdownCache;
    private DateTime _markdownCacheBuiltAt = DateTime.MinValue;

    public string VaultPath { get; }

    public ObsidianService()
    {
        VaultPath = Environment.GetEnvironmentVariable("VAULT_PATH") ?? "../vault";
        Directory.CreateDirectory(GetFullPath(""));
        EnsureVaultStructure();
    }

    public string GetFullPath(string relativePath)
    {
        var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), VaultPath));
        var fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));

        if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho inválido fora do vault.");

        return fullPath;
    }

    public void EnsureVaultStructure()
    {
        var folders = new[]
        {
            "00_Inbox", "01_Daily", "02_Projects", "03_Memory",
            "04_Logs", "05_Tasks", "06_Commands"
        };

        foreach (var folder in folders)
            Directory.CreateDirectory(GetFullPath(folder));

        EnsureFile("03_Memory/memory.md", "# Memória do Nexus\n\n");
        EnsureFile("04_Logs/conversations.md", "# Logs de Conversas\n\n");
        EnsureFile("04_Logs/system.md", "# Logs do Sistema\n\n");
        EnsureFile("05_Tasks/tasks.md", "# Tarefas\n\n");
        EnsureFile("06_Commands/catalog.md", "# Catálogo de Comandos\n\n");
    }

    public void EnsureFile(string relativePath, string initialContent)
    {
        var path = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            File.WriteAllText(path, initialContent);
            InvalidateMarkdownCache();
        }
    }

    public void AppendToFile(string relativePath, string content)
    {
        var path = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        lock (_fileLock)
        {
            File.AppendAllText(path, content);
            InvalidateMarkdownCache();
        }
    }

    public string ReadFile(string relativePath)
    {
        var path = GetFullPath(relativePath);
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    public IEnumerable<string> SearchMarkdown(string query)
    {
        return SearchMarkdownDetailed(query).Select(result => $"Arquivo: {result.Path}\n{result.Snippet}");
    }

    public IEnumerable<MarkdownSearchResult> SearchMarkdownDetailed(string query)
    {
        var coreTerms = GetCoreTerms(query);
        var terms = ExpandQueryTerms(query);
        var results = new List<MarkdownSearchResult>();

        foreach (var document in GetMarkdownDocuments())
        {
            var searchable = $"{document.Path}\n{document.Title}\n{document.Content}";
            var coreMatches = coreTerms.Count(term => TermMatches(searchable, term));
            var matchedTerms = terms.Count(term => TermMatches(searchable, term));

            if (matchedTerms == 0 || (coreTerms.Length >= 2 && coreMatches == 0))
                continue;

            var priorityBoost = GetPriorityBoost(document.Path);
            var score = matchedTerms + (coreMatches * 3) + priorityBoost + GetTitleBoost(document.Title, terms) + GetPhraseBoost(searchable, query);
            var snippet = BuildRelevantSnippet(document.Content, terms);

            results.Add(new MarkdownSearchResult(
                document.Path,
                document.Title,
                document.Folder,
                score,
                snippet,
                priorityBoost >= 10
            ));
        }

        return results
            .OrderByDescending(result => result.Score)
            .ThenByDescending(result => result.IsPrioritySource)
            .ThenBy(result => result.Title);
    }

    private IReadOnlyList<MarkdownDocument> GetMarkdownDocuments()
    {
        lock (_cacheLock)
        {
            if (_markdownCache is not null && DateTime.UtcNow - _markdownCacheBuiltAt < TimeSpan.FromSeconds(20))
                return _markdownCache;

            var basePath = GetFullPath("");
            _markdownCache = Directory.GetFiles(basePath, "*.md", SearchOption.AllDirectories)
                .Select(file => BuildMarkdownDocument(basePath, file))
                .Where(document => !ShouldExcludeFromSearch(document.Path))
                .ToList();
            _markdownCacheBuiltAt = DateTime.UtcNow;

            return _markdownCache;
        }
    }

    private void InvalidateMarkdownCache()
    {
        lock (_cacheLock)
        {
            _markdownCache = null;
            _markdownCacheBuiltAt = DateTime.MinValue;
        }
    }

    private static MarkdownDocument BuildMarkdownDocument(string basePath, string file)
    {
        var content = File.ReadAllText(file);
        var relative = Path.GetRelativePath(basePath, file);
        var title = ExtractTitle(content, relative);
        var folder = Path.GetDirectoryName(relative) ?? "";

        return new MarkdownDocument(relative, title, folder, content);
    }

    private static bool ShouldExcludeFromSearch(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return normalized.StartsWith("04_Logs/", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ExpandQueryTerms(string query)
    {
        var rawTerms = GetCoreTerms(query).ToList();

        var expanded = new List<string>(rawTerms);

        void AddIfPresent(string source, params string[] additions)
        {
            if (rawTerms.Any(term => term.Equals(source, StringComparison.OrdinalIgnoreCase)))
                expanded.AddRange(additions);
        }

        AddIfPresent("jornal", "journal");
        AddIfPresent("journal", "jornal");
        AddIfPresent("binario", "binário");
        AddIfPresent("binário", "binario");
        AddIfPresent("dados", "/Dados");
        AddIfPresent("rede", "network", "troubleshooting");
        AddIfPresent("qvd", "Shift", "BI");
        AddIfPresent("purgv3", "purge", "purgV3");
        AddIfPresent("purge", "purgV3", "purgv3");
        AddIfPresent("gpo", "política", "politica", "grupo", "Group", "Policy", "gpmc");
        AddIfPresent("politica", "política", "gpo", "grupo");
        AddIfPresent("política", "politica", "gpo", "grupo");
        AddIfPresent("pagina", "página", "paginas", "páginas", "web", "navegador", "browser", "Edge", "Chrome");
        AddIfPresent("página", "pagina", "paginas", "páginas", "web", "navegador", "browser", "Edge", "Chrome");
        AddIfPresent("paginas", "pagina", "página", "páginas", "web", "navegador", "browser", "Edge", "Chrome");
        AddIfPresent("páginas", "pagina", "página", "paginas", "web", "navegador", "browser", "Edge", "Chrome");
        AddIfPresent("abertura", "abrir", "inicialização", "inicializacao", "startup", "homepage");
        AddIfPresent("edge", "Microsoft", "Edge", "navegador", "browser");
        AddIfPresent("chrome", "Google", "Chrome", "navegador", "browser");

        return expanded
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] GetCoreTerms(string query)
    {
        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeTerm)
            .SelectMany(ExpandVerbTerm)
            .Where(term => term.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeTerm(string term)
    {
        return term.Trim(' ', '.', ',', '?', '!', ':', ';', '"', '\'');
    }

    private static IEnumerable<string> ExpandVerbTerm(string term)
    {
        var normalized = term.ToLowerInvariant();

        if (IsStopWord(normalized))
            yield break;

        if (normalized is "como" or "sobre" or "procure" or "busque" or "buscar" or "pesquise" or "pesquisar" or "procedimento" or "documento" or "obsidian" or "obsidia" or "criar" or "crie" or "fazer" or "faça" or "para" or "gerar" or "gere" or "gerado" or "gerada" or "novo" or "nova")
            yield break;

        if (normalized is "limpar" or "limpeza")
        {
            yield return "limpeza";
            yield break;
        }

        yield return term;
    }

    private static bool IsStopWord(string term)
    {
        return term is
            "a" or "o" or "as" or "os" or "um" or "uma" or "uns" or "umas" or
            "de" or "do" or "da" or "dos" or "das" or "em" or "no" or "na" or "nos" or "nas" or
            "e" or "ou" or "que" or "qual" or "quais" or "me" or "minha" or "meu" or "suas" or "seus" or
            "como" or "sobre" or "para" or "por" or "com" or "sem" or
            "procure" or "busque" or "buscar" or "pesquise" or "pesquisar" or
            "responda" or "explique" or "explicar" or "frase" or "conceito" or "resuma" or "resumir" or
            "significa" or "significado" or "termo" or "inexistente" or
            "procedimento" or "documento" or "documentacao" or "documentação" or
            "nexus" or "obsidian" or "obsidia" or "criar" or "crie" or "fazer" or "faca" or "faça" or "faÃ§a" or
            "gerar" or "gere" or "gerado" or "gerada" or "novo" or "nova";
    }

    private static string ExtractTitle(string content, string relativePath)
    {
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# "))
                return trimmed[2..].Trim();
        }

        return Path.GetFileNameWithoutExtension(relativePath);
    }

    private static int GetPriorityBoost(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');

        if (normalized.StartsWith("06_Documents/Procedimentos-Padronizados/", StringComparison.OrdinalIgnoreCase))
            return 10;

        if (normalized.StartsWith("06_Documents/Base-de-Conhecimento/Zuculin/", StringComparison.OrdinalIgnoreCase))
            return 5;

        if (normalized.StartsWith("04_Knowledge/", StringComparison.OrdinalIgnoreCase))
            return 3;

        if (normalized.StartsWith("07_Nexus/", StringComparison.OrdinalIgnoreCase))
            return 1;

        return 0;
    }

    private static int GetTitleBoost(string title, string[] terms)
    {
        return terms.Count(term => TermMatches(title, term)) * 2;
    }

    private static int GetPhraseBoost(string content, string query)
    {
        var cleanQuery = NormalizeWhitespace(query);
        if (cleanQuery.Length < 4)
            return 0;

        return content.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase) ? 8 : 0;
    }

    private static bool TermMatches(string content, string term)
    {
        if (term.Length <= 2)
        {
            var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}])";
            return Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return content.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRelevantSnippet(string content, string[] terms)
    {
        var clean = RemoveFrontmatter(content).Trim();
        var index = FindFirstTermIndex(clean, terms);

        if (index < 0)
            return Truncate(NormalizeWhitespace(clean), 420);

        var start = Math.Max(0, index - 180);
        var length = Math.Min(clean.Length - start, 520);
        var snippet = clean.Substring(start, length);

        if (start > 0)
            snippet = "..." + snippet;

        if (start + length < clean.Length)
            snippet += "...";

        return NormalizeWhitespace(snippet);
    }

    private static string RemoveFrontmatter(string content)
    {
        if (!content.StartsWith("---"))
            return content;

        var end = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        return end >= 0 ? content[(end + 4)..] : content;
    }

    private static int FindFirstTermIndex(string content, string[] terms)
    {
        var best = -1;

        foreach (var term in terms)
        {
            var index = content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (best < 0 || index < best))
                best = index;
        }

        return best;
    }

    private static string NormalizeWhitespace(string text)
    {
        return string.Join(" ", text.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    private record MarkdownDocument(string Path, string Title, string Folder, string Content);
}
