using System;
using System.Net.Http;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MisioCache.Services;

/// <summary>Only update services; no HTTP observer, player hooks or media cache.</summary>
public sealed class MisioCacheServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
    {
        services.AddHttpClient("MisioCacheUpdates", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<MisioCacheUpdateService>();
        services.AddSingleton<MisioCacheNativeCatalogRefreshService>();
        services.AddHostedService<MisioCacheUpdateService>(provider => provider.GetRequiredService<MisioCacheUpdateService>());
    }
}
