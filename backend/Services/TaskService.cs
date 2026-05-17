namespace NexusBackend.Services;

public class TaskService
{
    private readonly ObsidianService _obsidian;

    public TaskService(ObsidianService obsidian)
    {
        _obsidian = obsidian;
    }

    public void Create(string task)
    {
        var entry = "- [ ] " + task + "  \n  - Criada em: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n";
        _obsidian.AppendToFile("05_Tasks/tasks.md", entry);
    }

    public string ReadAll() => _obsidian.ReadFile("05_Tasks/tasks.md");
}
