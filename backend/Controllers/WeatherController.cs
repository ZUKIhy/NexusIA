using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/weather")]
public class WeatherController : ControllerBase
{
    private readonly WeatherService _weather;

    public WeatherController(WeatherService weather)
    {
        _weather = weather;
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current()
    {
        return Ok(await _weather.GetCurrentWeatherAsync());
    }

    [HttpGet("report")]
    public async Task<IActionResult> Report()
    {
        return Ok(await _weather.GetWeatherReportAsync());
    }
}
