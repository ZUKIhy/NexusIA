namespace NexusBackend.Services;

public class TelegramPollingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TelegramPollingBackgroundService> _logger;
    private long? _offset;

    public TelegramPollingBackgroundService(IServiceProvider services, ILogger<TelegramPollingBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var telegram = scope.ServiceProvider.GetRequiredService<TelegramService>();

                if (!TelegramService.IsPollingEnabled())
                    return;

                var updates = await telegram.GetUpdatesAsync(_offset, stoppingToken);

                if (!_offset.HasValue && !telegram.ShouldProcessExistingUpdates())
                {
                    var latest = updates.Select(update => update.UpdateId).DefaultIfEmpty(0).Max();
                    if (latest > 0)
                        _offset = latest + 1;

                    await Task.Delay(GetIdleDelay(), stoppingToken);
                    continue;
                }

                foreach (var update in updates.OrderBy(update => update.UpdateId))
                {
                    _offset = update.UpdateId + 1;
                    await telegram.HandleIncomingMessageAsync(update.ChatId, update.Text);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telegram polling falhou.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private static TimeSpan GetIdleDelay()
    {
        var raw = Environment.GetEnvironmentVariable("TELEGRAM_POLLING_INTERVAL_SECONDS");
        return int.TryParse(raw, out var seconds)
            ? TimeSpan.FromSeconds(Math.Clamp(seconds, 2, 60))
            : TimeSpan.FromSeconds(2);
    }
}
