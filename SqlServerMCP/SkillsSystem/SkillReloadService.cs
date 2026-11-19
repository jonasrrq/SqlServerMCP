using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SqlServerMCP.Skills;

/// <summary>
/// Periodically reloads external skill directories. A failed reload keeps the last good snapshot.
/// </summary>
public sealed class SkillReloadService : BackgroundService
{
    private readonly SkillRegistry _registry;
    private readonly SkillOptions _options;
    private readonly ILogger<SkillReloadService> _logger;

    public SkillReloadService(
        SkillRegistry registry,
        SkillOptions options,
        ILogger<SkillReloadService> logger)
    {
        _registry = registry;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ReloadOnChange)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.ReloadIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await _registry.ReloadAsync(stoppingToken).ConfigureAwait(false);
                _logger.LogDebug("Skills reloaded from configured providers.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Skills reload failed; keeping the previous snapshot.");
            }
        }
    }
}
