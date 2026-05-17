using Microsoft.AspNetCore.SignalR;

namespace NexusBackend.Hubs;

public class NexusHub : Hub
{
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("nexus:log", new
        {
            level = "info",
            message = "SignalR conectado ao Nexus."
        });
    }
}
