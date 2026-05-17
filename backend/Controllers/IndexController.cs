using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/index")]
public class IndexController : ControllerBase
{
    private readonly DocumentIndexService _index;

    public IndexController(DocumentIndexService index)
    {
        _index = index;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var items = _index.BuildIndex();

        return Ok(new
        {
            total = items.Count,
            procedures = items.Count(item => item.Type.Equals("procedimento", StringComparison.OrdinalIgnoreCase)),
            indexes = items.Count(item => item.Type.Equals("index", StringComparison.OrdinalIgnoreCase)),
            generatedKnowledge = items.Count(item => item.Type.Equals("conhecimento-gerado", StringComparison.OrdinalIgnoreCase)),
            items
        });
    }

    [HttpPost("rebuild")]
    public IActionResult Rebuild()
    {
        var path = _index.SaveIndex();

        return Ok(new
        {
            saved = true,
            path
        });
    }
}
