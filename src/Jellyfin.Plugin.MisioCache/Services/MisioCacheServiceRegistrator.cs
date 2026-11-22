using System;
using System.Net.Http;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MisioCache.Services;

/// <summary>Update services and independent client-side Smart Downloads loader. No media HTTP observer.</summary>
public sealed class MisioCacheServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
    {
        services.AddTransient<IStartupFilter, MisioCacheSmartDownloadsStartupFilter>();
        services.AddHttpClient("MisioCacheUpdates", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<MisioCacheUpdateService>();
        services.AddSingleton<MisioCacheNativeCatalogRefreshService>();
        services.AddHostedService<MisioCacheUpdateService>(provider => provider.GetRequiredService<MisioCacheUpdateService>());
    }
}
