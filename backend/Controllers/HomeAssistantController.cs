using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/home")]
public class HomeAssistantController : ControllerBase
{
    private readonly HomeAssistantService _home;

    public HomeAssistantController(HomeAssistantService home)
    {
        _home = home;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var online = await _home.PingAsync();

        return Ok(new
        {
            enabled = _home.IsEnabled(),
            online
        });
    }

    [HttpGet("states")]
    public async Task<IActionResult> States()
    {
        var json = await _home.GetStatesAsync();
        return Content(json, "application/json");
    }

    [HttpGet("state")]
    public async Task<IActionResult> State([FromQuery] string entityId)
    {
        var json = await _home.GetStateAsync(entityId);
        return Content(json, "application/json");
    }

    [HttpPost("turn-on")]
    public async Task<IActionResult> TurnOn([FromBody] HomeEntityRequest request)
    {
        var ok = await _home.TurnOnAsync(request.EntityId);
        return Ok(new { success = ok });
    }

    [HttpPost("turn-off")]
    public async Task<IActionResult> TurnOff([FromBody] HomeEntityRequest request)
    {
        var ok = await _home.TurnOffAsync(request.EntityId);
        return Ok(new { success = ok });
    }

    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromBody] HomeEntityRequest request)
    {
        var ok = await _home.ToggleAsync(request.EntityId);
        return Ok(new { success = ok });
    }

    [HttpPost("brightness")]
    public async Task<IActionResult> Brightness([FromBody] HomeBrightnessRequest request)
    {
        var ok = await _home.SetLightBrightnessAsync(request.EntityId, request.Brightness);
        return Ok(new { success = ok });
    }
}

public class HomeEntityRequest
{
    public string EntityId { get; set; } = "";
}

public class HomeBrightnessRequest
{
    public string EntityId { get; set; } = "";
    public int Brightness { get; set; }
}
