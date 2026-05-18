namespace NexusBackend.Services;

public class SpotifyLearningBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SpotifyLearningBackgroundService> _logger;

    public SpotifyLearningBackgroundService(
        IServiceProvider services,
        ILogger<SpotifyLearningBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled())
            return;

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var learning = scope.ServiceProvider.GetRequiredService<SpotifyLearningService>();
                var result = await learning.LearnAsync();
                _logger.LogInformation("Spotify learning: {Message}", result.Message);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao aprender perfil musical pelo Spotify.");
            }

            await Task.Delay(GetInterval(), stoppingToken);
        }
    }

    private static bool IsEnabled()
    {
        return (Environment.GetEnvironmentVariable("SPOTIFY_LEARNING_ENABLED") ?? "false")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan GetInterval()
    {
        var raw = Environment.GetEnvironmentVariable("SPOTIFY_LEARNING_INTERVAL_HOURS") ?? "24";
        return int.TryParse(raw, out var hours)
            ? TimeSpan.FromHours(Math.Clamp(hours, 1, 168))
            : TimeSpan.FromHours(24);
    }
}
