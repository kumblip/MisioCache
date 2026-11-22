// Retain the .NET CI gate: reject retired HTTP/video telemetry while allowing
// the independent, index.html-only Smart Downloads client bootstrap.
using System;
using System.IO;

var root = Directory.GetCurrentDirectory();
var services = File.ReadAllText(Path.Combine(root, "src", "Jellyfin.Plugin.MisioCache", "Services", "MisioCacheServiceRegistrator.cs"));
var config = File.ReadAllText(Path.Combine(root, "src", "Jellyfin.Plugin.MisioCache", "Configuration", "PluginConfiguration.cs"));
var settings = File.ReadAllText(Path.Combine(root, "src", "Jellyfin.Plugin.MisioCache", "Web", "configPage.html"));

// IStartupFilter is not inherently telemetry: Smart Downloads uses it only to
// inject its own JavaScript into the HTML entry point. Check the *retired*
// middleware and observability services by name, rather than banning the DI
// interface for unrelated features.
if (services.Contains("MisioCachePassiveHttpStartupFilter", StringComparison.Ordinal)
    || services.Contains("MisioCacheHttpObservationStore", StringComparison.Ordinal)
    || services.Contains("MisioCacheResponseByteMeter", StringComparison.Ordinal)
    || config.Contains("EnablePassiveHttpDiagnostics", StringComparison.Ordinal)
    || config.Contains("EnableResponseBodyByteMeter", StringComparison.Ordinal)
    || settings.Contains("mc-http-optin", StringComparison.Ordinal)
    || settings.Contains("mc-policy-form", StringComparison.Ordinal))
{
    throw new Exception("Legacy HTTP telemetry was not removed");
}

if (!services.Contains("AddTransient<IStartupFilter, MisioCacheSmartDownloadsStartupFilter>", StringComparison.Ordinal))
{
    throw new Exception("Smart Downloads HTML bootstrap is not registered");
}

if (!services.Contains("AddHostedService<MisioCacheUpdateService>", StringComparison.Ordinal)
    || !settings.Contains("mc-update-install", StringComparison.Ordinal)
    || !settings.Contains("mc-update-check", StringComparison.Ordinal))
{
    throw new Exception("Native update integration was removed");
}

Console.WriteLine("MisioCache: legacy HTTP telemetry absent, Smart Downloads bootstrap and native updates retained");
