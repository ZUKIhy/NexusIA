using NexusBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/memory")]
public class MemoryController : ControllerBase
{
    private readonly MemoryService _memory;

    public MemoryController(MemoryService memory)
    {
        _memory = memory;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(new { content = _memory.ReadAll() });

    [HttpGet("search")]
    public IActionResult Search([FromQuery] string q) => Ok(new { query = q, content = _memory.Search(q) });

    [HttpPost("save")]
    public IActionResult Save([FromBody] SaveMemoryRequest request)
    {
        _memory.Save(request.Content);
        return Ok(new { saved = true });
    }
}

public class SaveMemoryRequest
{
    public string Content { get; set; } = "";
}
