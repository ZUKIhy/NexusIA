using System.Text.Json;
using System.Text.RegularExpressions;

namespace NexusBackend.Services;

public class DocumentIndexService
{
    private readonly ObsidianService _obsidian;

    public DocumentIndexService(ObsidianService obsidian)
    {
        _obsidian = obsidian;
    }

    public List<DocumentIndexItem> BuildIndex()
    {
        var basePath = _obsidian.GetFullPath("");

        if (!Directory.Exists(basePath))
            return new List<DocumentIndexItem>();

        var files = Directory.GetFiles(basePath, "*.md", SearchOption.AllDirectories);
        var items = new List<DocumentIndexItem>();

        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file);
                var relativePath = Path.GetRelativePath(basePath, file);
                var info = new FileInfo(file);

                items.Add(new DocumentIndexItem
                {
                    Path = relativePath,
                    Title = ExtractTitle(content, file),
                    Type = ExtractFrontmatterValue(content, "type"),
                    Status = ExtractFrontmatterValue(content, "status"),
                    Tags = ExtractTags(content),
                    ModifiedAt = info.LastWriteTime,
                    Size = info.Length,
                    Preview = content.Length > 300 ? content[..300] : content
                });
            }
            catch
            {
                // Ignore files temporarily locked by sync tools or editors.
            }
        }

        return items
            .OrderBy(item => item.Path)
            .ToList();
    }

    public string SaveIndex()
    {
        var path = _obsidian.GetFullPath("07_Nexus/document-index.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(BuildIndex(), new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
        return path;
    }

    private static string ExtractTitle(string content, string file)
    {
        var match = Regex.Match(content, @"^#\s+(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(file);
    }

    private static string ExtractFrontmatterValue(string content, string key)
    {
        var match = Regex.Match(content, $"^{Regex.Escape(key)}:\\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static List<string> ExtractTags(string content)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(content, @"#([\p{L}\p{N}_\-\/]+)"))
            tags.Add(match.Groups[1].Value);

        var frontmatterTags = Regex.Match(content, @"tags:\s*\n((\s*-\s*.+\n?)+)", RegexOptions.IgnoreCase);

        if (frontmatterTags.Success)
        {
            foreach (Match tagMatch in Regex.Matches(frontmatterTags.Groups[1].Value, @"-\s*(.+)"))
                tags.Add(tagMatch.Groups[1].Value.Trim());
        }

        return tags.OrderBy(tag => tag).ToList();
    }
}

public class DocumentIndexItem
{
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public DateTime ModifiedAt { get; set; }
    public long Size { get; set; }
    public string Preview { get; set; } = "";
}
