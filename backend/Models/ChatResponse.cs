namespace NexusBackend.Models;

public class ChatResponse
{
    public string Answer { get; set; } = "";
    public string Intent { get; set; } = "chat_normal";
    public bool MemorySaved { get; set; }
    public bool TaskCreated { get; set; }
    public string? DocsQuery { get; set; }
    public IEnumerable<object>? DocsResults { get; set; }
}
