using NexusBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly LogService _logs;

    public LogsController(LogService logs)
    {
        _logs = logs;
    }

    [HttpGet("conversations")]
    public IActionResult Conversations() => Ok(new { content = _logs.GetConversationLogs() });

    [HttpGet("system")]
    public IActionResult SystemLogs() => Ok(new { content = _logs.GetSystemLogs() });
}
