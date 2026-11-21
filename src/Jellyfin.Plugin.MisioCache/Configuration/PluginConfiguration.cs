using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MisioCache.Configuration;

/// <summary>
/// Preferences for a future device-cache adapter. v0.1 reads and saves only these
/// preferences; it does NOT intercept playback, download media, or write video.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    public bool AutoCheckUpdates { get; set; } = true;
    public bool PreserveWatchedPartsUntilCompletion { get; set; } = true;
    public bool DownloadUntilEnd { get; set; } = true;
    public bool DownloadWhilePaused { get; set; } = true;
    public bool DeleteAfterCompleted { get; set; } = true;
    public int KeepAfterStopMinutes { get; set; } = 240;
    public int PlannedMaxDeviceCacheGiB { get; set; } = 8;
}
