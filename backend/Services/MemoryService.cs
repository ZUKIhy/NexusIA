namespace NexusBackend.Services;

public class MemoryService
{
    private readonly ObsidianService _obsidian;

    public MemoryService(ObsidianService obsidian)
    {
        _obsidian = obsidian;
    }

    public void Save(string content)
    {
        var entry = "\n\n## " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + content + "\n";
        _obsidian.AppendToFile("03_Memory/memory.md", entry);
    }

    public string ReadAll() => _obsidian.ReadFile("03_Memory/memory.md");

    public string Search(string query)
    {
        var results = _obsidian.SearchMarkdown(query).Take(5);
        return string.Join("\n\n---\n\n", results);
    }
}
