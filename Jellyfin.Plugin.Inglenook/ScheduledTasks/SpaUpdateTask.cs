using Jellyfin.Plugin.Inglenook.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Inglenook.ScheduledTasks;

/// <summary>
/// Scheduled task that checks for Inglenook SPA updates from GitHub releases.
/// </summary>
public class SpaUpdateTask : IScheduledTask
{
    private readonly SpaManager _spaManager;
    private readonly ILogger<SpaUpdateTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpaUpdateTask"/> class.
    /// </summary>
    public SpaUpdateTask(SpaManager spaManager, ILogger<SpaUpdateTask> logger)
    {
        _spaManager = spaManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Check for Inglenook SPA Updates";

    /// <inheritdoc />
    public string Key => "InglenookSpaUpdate";

    /// <inheritdoc />
    public string Description => "Checks GitHub for new releases of the Inglenook web client and downloads them automatically.";

    /// <inheritdoc />
    public string Category => "Inglenook";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.StartupTrigger,
                MaxRuntimeTicks = TimeSpan.FromMinutes(15).Ticks
            },
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(24).Ticks
            }
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            _logger.LogWarning("Plugin configuration not available");
            return;
        }

        if (!config.EnableSpaServing)
        {
            _logger.LogInformation("SPA serving is disabled, skipping update check");
            return;
        }

        // Skip if auto-update is disabled and SPA is already available
        if (!config.EnableSpaAutoUpdate && _spaManager.IsAvailable)
        {
            _logger.LogInformation("SPA auto-update is disabled and SPA is already installed, skipping");
            return;
        }

        progress.Report(0);

        var release = await _spaManager.CheckForUpdateAsync(cancellationToken).ConfigureAwait(false);

        progress.Report(50);

        if (release is not null)
        {
            _logger.LogInformation("Downloading SPA update: {Version}", release.TagName);
            await _spaManager.DownloadAndExtractAsync(release, cancellationToken).ConfigureAwait(false);
        }

        progress.Report(100);
    }
}
