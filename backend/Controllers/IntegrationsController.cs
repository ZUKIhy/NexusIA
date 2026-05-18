using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/integrations")]
public class IntegrationsController : ControllerBase
{
    private readonly TelegramService _telegram;
    private readonly GoogleCalendarService _calendar;
    private readonly GmailService _gmail;

    public IntegrationsController(TelegramService telegram, GoogleCalendarService calendar, GmailService gmail)
    {
        _telegram = telegram;
        _calendar = calendar;
        _gmail = gmail;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new
        {
            telegram = _telegram.GetStatus(),
            googleCalendar = _calendar.GetStatus(),
            gmail = _gmail.GetStatus()
        });
    }

    [HttpPost("telegram/send")]
    public async Task<IActionResult> SendTelegram([FromBody] TelegramSendRequest request)
    {
        return Ok(await _telegram.SendMessageAsync(request.Message));
    }

    [HttpPost("telegram/alert")]
    public async Task<IActionResult> SendTelegramAlert([FromBody] TelegramAlertRequest request)
    {
        return Ok(await _telegram.SendAlertAsync(request.Title, request.Severity, request.Detail));
    }

    [HttpGet("telegram/commands")]
    public async Task<IActionResult> TelegramCommands()
    {
        return Ok(new { results = await _telegram.GetCommandUpdatesAsync() });
    }

    [HttpPost("telegram/command")]
    public async Task<IActionResult> TelegramCommand([FromBody] TelegramCommandRequest request)
    {
        return Ok(await _telegram.ExecuteCommandAsync(request.Command));
    }

    [HttpGet("calendar/briefing")]
    public async Task<IActionResult> CalendarBriefing()
    {
        return Ok(await _calendar.GetBriefingAsync());
    }

    [HttpGet("gmail/briefing")]
    public async Task<IActionResult> GmailBriefing()
    {
        return Ok(await _gmail.GetBriefingAsync());
    }
}

public class TelegramSendRequest
{
    public string Message { get; set; } = "";
}

public class TelegramAlertRequest
{
    public string Title { get; set; } = "";
    public string Severity { get; set; } = "info";
    public string Detail { get; set; } = "";
}

public class TelegramCommandRequest
{
    public string Command { get; set; } = "";
}
