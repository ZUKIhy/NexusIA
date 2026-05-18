using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/network")]
public class NetworkController : ControllerBase
{
    private readonly NetworkMonitorService _network;

    public NetworkController(NetworkMonitorService network)
    {
        _network = network;
    }

    [HttpGet("devices")]
    public IActionResult Devices()
    {
        return Ok(new
        {
            devices = _network.GetKnownDevices()
        });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        return Ok(await _network.CheckNetworkStatusAsync());
    }

    [HttpPost("check")]
    public async Task<IActionResult> Check()
    {
        return Ok(await _network.CheckNetworkStatusAsync());
    }

    [HttpGet("unknown")]
    public async Task<IActionResult> Unknown()
    {
        return Ok(new
        {
            devices = await _network.DiscoverUnknownDevicesAsync()
        });
    }

    [HttpPost("unknown/resolve")]
    public IActionResult ResolveUnknown([FromBody] NetworkResolveRequest request)
    {
        return Ok(_network.ResolveUnknownDevice(request));
    }
}
