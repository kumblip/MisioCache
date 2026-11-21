using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.MisioCache.Configuration;

namespace Jellyfin.Plugin.MisioCache;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid PluginId = Guid.Parse("5cf36828-5b7b-4d18-a28e-1e87922e505b");
    public static Plugin? Instance { get; private set; }

    public override Guid Id => PluginId;
    public override string Name => "MisioCache";
    public override string Description => "Diagnostyka klienta i pasywna analiza strumieni HTTP Range/206. Bez zmiany odtwarzacza.";

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        // Settings first: native Configure link resolves to configuration, not overview.
        yield return new PluginPageInfo
        {
            Name = "MisioCache",
            EmbeddedResourcePath = "Jellyfin.Plugin.MisioCache.Web.configPage.html",
            DisplayName = "MisioCache — ustawienia",
            MenuIcon = "settings",
            EnableInMainMenu = true
        };
        yield return new PluginPageInfo
        {
            Name = "MisioCacheOverview",
            EmbeddedResourcePath = "Jellyfin.Plugin.MisioCache.Web.overviewPage.html",
            DisplayName = "MisioCache",
            MenuIcon = "storage",
            EnableInMainMenu = true
        };
    }
}
