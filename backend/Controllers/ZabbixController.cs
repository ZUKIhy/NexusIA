using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/zabbix")]
public class ZabbixController : ControllerBase
{
    private readonly ZabbixService _zabbix;

    public ZabbixController(ZabbixService zabbix)
    {
        _zabbix = zabbix;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        return Ok(await _zabbix.GetStatusAsync());
    }

    [HttpGet("hosts")]
    public async Task<IActionResult> Hosts()
    {
        return Ok(new { hosts = await _zabbix.GetHostsAsync() });
    }

    [HttpGet("problems")]
    public async Task<IActionResult> Problems([FromQuery] int? minSeverity = null, [FromQuery] bool recent = false)
    {
        return Ok(new { problems = await _zabbix.GetProblemsAsync(minSeverity, recent) });
    }

    [HttpPost("report")]
    public async Task<IActionResult> Report([FromBody] ZabbixReportRequest request)
    {
        return Ok(await _zabbix.GenerateReportAsync(request.MinSeverity, request.SaveToObsidian, request.NotifyCritical));
    }

    [HttpPost("acknowledge")]
    public async Task<IActionResult> Acknowledge([FromBody] ZabbixAcknowledgeRequest request)
    {
        return Ok(await _zabbix.AcknowledgeAsync(request));
    }
}

public class ZabbixReportRequest
{
    public int? MinSeverity { get; set; }
    public bool SaveToObsidian { get; set; } = true;
    public bool NotifyCritical { get; set; }
}
