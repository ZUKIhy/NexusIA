using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/today")]
public class TodayController : ControllerBase
{
    private readonly TodayService _today;

    public TodayController(TodayService today)
    {
        _today = today;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await _today.BuildAsync());
    }
}
