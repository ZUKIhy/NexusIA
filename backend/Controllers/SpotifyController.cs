using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/spotify")]
public class SpotifyController : ControllerBase
{
    private readonly SpotifyService _spotify;

    public SpotifyController(SpotifyService spotify)
    {
        _spotify = spotify;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(_spotify.GetStatus());
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        return Redirect(_spotify.BuildLoginUrl());
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return BadRequest(new { error });

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "Spotify nao retornou codigo OAuth." });

        await _spotify.ExchangeCodeAsync(code);

        return Content(
            "Spotify conectado ao Nexus. Pode fechar esta aba e pedir: Nexus, tocar minhas musicas.",
            "text/plain; charset=utf-8"
        );
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current()
    {
        return Ok(await _spotify.GetCurrentAsync());
    }

    [HttpPost("play")]
    public async Task<IActionResult> Play([FromBody] SpotifyPlayRequest request)
    {
        return Ok(new { message = await _spotify.PlayAsync(request) });
    }

    [HttpPost("pause")]
    public async Task<IActionResult> Pause()
    {
        return Ok(new { message = await _spotify.PauseAsync() });
    }

    [HttpPost("next")]
    public async Task<IActionResult> Next()
    {
        return Ok(new { message = await _spotify.NextAsync() });
    }

    [HttpPost("previous")]
    public async Task<IActionResult> Previous()
    {
        return Ok(new { message = await _spotify.PreviousAsync() });
    }

    [HttpPost("volume")]
    public async Task<IActionResult> Volume([FromBody] SpotifyVolumeRequest request)
    {
        return Ok(new { message = await _spotify.SetVolumeAsync(request.VolumePercent) });
    }
}
