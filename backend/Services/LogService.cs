namespace NexusBackend.Services;

public class LogService
{
    private readonly ObsidianService _obsidian;

    public LogService(ObsidianService obsidian)
    {
        _obsidian = obsidian;
    }

    public void Conversation(string userMessage, string answer)
    {
        var entry =
            "\n\n## " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
            "\n\n**Usuário:** " + userMessage +
            "\n\n**Nexus:** " + answer +
            "\n\n---\n";

        _obsidian.AppendToFile("04_Logs/conversations.md", entry);
    }

    public void System(string message)
    {
        var entry = "- `" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "` " + message + "\n";
        _obsidian.AppendToFile("04_Logs/system.md", entry);
    }

    public string GetConversationLogs() => _obsidian.ReadFile("04_Logs/conversations.md");
    public string GetSystemLogs() => _obsidian.ReadFile("04_Logs/system.md");
}
