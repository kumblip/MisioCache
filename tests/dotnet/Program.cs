// Retain the existing CI .NET gate, but test the active Smart Downloads scope.
using System;
using System.IO;
var root = Directory.GetCurrentDirectory();
var services = File.ReadAllText(Path.Combine(root, "src", "Jellyfin.Plugin.MisioCache", "Services", "MisioCacheServiceRegistrator.cs"));
var config = File.ReadAllText(Path.Combine(root, "src", "Jellyfin.Plugin.MisioCache", "Configuration", "PluginConfiguration.cs"));
var settings = File.ReadAllText(Path.Combine(root, "src", "Jellyfin.Plugin.MisioCache", "Web", "configPage.html"));
if (services.Contains("AddTransient<IStartupFilter") || services.Contains("MisioCacheHttpObservationStore")
    || config.Contains("EnablePassiveHttpDiagnostics") || config.Contains("EnableResponseBodyByteMeter")
    || settings.Contains("mc-http-optin") || settings.Contains("mc-policy-form"))
    throw new Exception("Legacy HTTP telemetry was not removed");
if (!services.Contains("AddHostedService<MisioCacheUpdateService>")
    || !settings.Contains("mc-update-install") || !settings.Contains("mc-update-check"))
    throw new Exception("Native update integration was removed");
Console.WriteLine("MisioCache: telemetry removed, native update integration retained");
