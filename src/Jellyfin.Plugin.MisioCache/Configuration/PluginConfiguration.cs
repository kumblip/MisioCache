using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MisioCache.Configuration;

/// <summary>
/// Server-wide settings. Smart Downloads preferences are opt-in per account and
/// device, stored by the official Jellyfin Android WebView rather than globally.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    public bool AutoCheckUpdates { get; set; } = true;
}
