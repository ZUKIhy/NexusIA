using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/computer")]
public class ComputerController : ControllerBase
{
    private readonly ComputerControlService _computer;

    public ComputerController(ComputerControlService computer)
    {
        _computer = computer;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(_computer.GetCapabilities());
    }

    [HttpGet("apps")]
    public IActionResult Apps([FromQuery] string q = "")
    {
        return Ok(new
        {
            query = q,
            results = string.IsNullOrWhiteSpace(q)
                ? Array.Empty<string>()
                : _computer.SearchInstalledApps(q).ToArray()
        });
    }

    [HttpPost("open-app")]
    public IActionResult OpenApp([FromBody] ComputerTextRequest request)
    {
        return Ok(_computer.OpenApp(request.Text));
    }

    [HttpPost("open-folder")]
    public IActionResult OpenFolder([FromBody] ComputerTextRequest request)
    {
        return Ok(_computer.OpenFolder(request.Text));
    }

    [HttpPost("open-url")]
    public IActionResult OpenUrl([FromBody] ComputerTextRequest request)
    {
        return Ok(_computer.OpenUrl(request.Text));
    }

    [HttpPost("open-site")]
    public IActionResult OpenSite([FromBody] ComputerTextRequest request)
    {
        return Ok(_computer.OpenSite(request.Text));
    }

    [HttpPost("search-web")]
    public IActionResult SearchWeb([FromBody] ComputerTextRequest request)
    {
        return Ok(_computer.SearchWeb(request.Text));
    }

    [HttpPost("whatsapp-message")]
    public IActionResult WhatsAppMessage([FromBody] ComputerMessageRequest request)
    {
        return Ok(_computer.PrepareWhatsAppMessage(request.Recipient, request.Message));
    }

    [HttpPost("paste")]
    public IActionResult Paste([FromBody] ComputerTextRequest request)
    {
        return Ok(_computer.PasteText(request.Text));
    }
}

public class ComputerTextRequest
{
    public string Text { get; set; } = "";
}

public class ComputerMessageRequest
{
    public string Recipient { get; set; } = "";
    public string Message { get; set; } = "";
}
