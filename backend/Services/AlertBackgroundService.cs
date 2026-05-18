namespace NexusBackend.Services;

public class AlertBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private bool? _lastHomeOnline;
    private bool? _lastInternetOnline;

    public AlertBackgroundService(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled())
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var alerts = scope.ServiceProvider.GetRequiredService<AlertService>();
                var telegram = scope.ServiceProvider.GetRequiredService<TelegramService>();
                var home = scope.ServiceProvider.GetRequiredService<HomeAssistantService>();
                var network = scope.ServiceProvider.GetRequiredService<NetworkMonitorService>();

                var homeOnline = await home.PingAsync();
                if (_lastHomeOnline is true && !homeOnline)
                {
                    alerts.Record(
                        "home-assistant",
                        "Home Assistant caiu",
                        "critico",
                        "A API do Home Assistant nao respondeu.",
                        "Verificar energia, rede e host do Home Assistant."
                    );
                    await telegram.SendMessageAsync("Nexus Alert\nSeveridade: critico\nHome Assistant caiu\n\nA API do Home Assistant nao respondeu.");
                }
                _lastHomeOnline = homeOnline;

                var networkStatus = await network.CheckNetworkStatusAsync();
                if (_lastInternetOnline is true && !networkStatus.InternetOnline)
                {
                    alerts.Record(
                        "internet",
                        "Internet caiu",
                        "critico",
                        "Os alvos de ping externos nao responderam.",
                        "Verificar modem, roteador e operadora."
                    );
                    await telegram.SendMessageAsync("Nexus Alert\nSeveridade: critico\nInternet caiu\n\nOs alvos de ping externos nao responderam.");
                }
                _lastInternetOnline = networkStatus.InternetOnline;
            }
            catch
            {
                // Alerting must never take the backend down.
            }

            await Task.Delay(GetInterval(), stoppingToken);
        }
    }

    private static bool IsEnabled()
    {
        return (Environment.GetEnvironmentVariable("ALERTS_ENABLED") ?? "true")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan GetInterval()
    {
        var raw = Environment.GetEnvironmentVariable("ALERTS_CHECK_INTERVAL_SECONDS");
        return int.TryParse(raw, out var seconds)
            ? TimeSpan.FromSeconds(Math.Clamp(seconds, 30, 3600))
            : TimeSpan.FromMinutes(5);
    }
}
