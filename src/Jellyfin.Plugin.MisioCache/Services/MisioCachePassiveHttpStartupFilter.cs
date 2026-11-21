using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.MisioCache.Services;

/// <summary>
/// A PASS-THROUGH, opt-in observer.  Does not read request/response bodies,
/// intercept streaming, send requests or change the response. Not a player adapter.
/// </summary>
public sealed class MisioCachePassiveHttpStartupFilter : IStartupFilter
{
    private readonly MisioCacheHttpObservationStore _store;

    public MisioCachePassiveHttpStartupFilter(MisioCacheHttpObservationStore store)
    {
        _store = store;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            _store.MarkPipelineInstalled();
            app.Use(async (context, nextRequest) =>
            {
                // Fast exit. Only a Boolean setting is read; nothing is captured while disabled.
                if (Jellyfin.Plugin.MisioCache.Plugin.Instance?.Configuration.EnablePassiveHttpDiagnostics != true)
                {
                    await nextRequest().ConfigureAwait(false);
                    return;
                }

                // Avoid affecting native media, API, authentication or normal errors.
                // Only GET/HEAD media routes are considered; never access query strings.
                var method = context.Request.Method;
                var path = context.Request.Path.Value ?? string.Empty;
                if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
                {
                    await nextRequest().ConfigureAwait(false);
                    return;
                }
                if (!TryClassify(path, out var kind))
                {
                    await nextRequest().ConfigureAwait(false);
                    return;
                }

                var requestedRange = context.Request.Headers.ContainsKey("Range");
                try
                {
                    await nextRequest().ConfigureAwait(false);
                }
                finally
                {
                    // Even a telemetry failure must never affect the media response.
                    try
                    {
                        _store.Record(kind, requestedRange, context.Response.StatusCode,
                            context.Response.Headers.ContainsKey("Content-Range"));
                    }
                    catch { /* diagnostics are intentionally best effort */ }
                }
            });
            next(app);
        };
    }

    // Match native Jellyfin Videos endpoints but not audio-only, image/API/other traffic.
    // Video IDs are checked for GUID shape, never stored or sent back to the panel.
    internal static bool TryClassify(string path, out string kind)
    {
        kind = string.Empty;
        var s = path.AsSpan();
        while (!s.IsEmpty)
        {
            var index = s.IndexOf('/');
            if (index < 0) break;
            s = s[(index + 1)..];
            if (!s.StartsWith("videos/", StringComparison.OrdinalIgnoreCase)) continue;
            var slash = s.IndexOf('/');
            if (slash < 0) continue;
            s = s[(slash + 1)..];
            var idEnd = s.IndexOf('/');
            if (idEnd <= 0) continue;
            var id = s[..idEnd];
            if (id.Length != 32 && id.Length != 36) continue;
            // Guid.TryParse accepts 32 hex digits and standard hyphenated GUIDs.
            if (!Guid.TryParse(id, out _)) continue;
            var tail = s[(idEnd + 1)..];
            if (tail.IsEmpty) continue;
            var suffix = tail.ToString();
            if (suffix.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
                || suffix.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase))
                kind = "playlist";
            else if (suffix.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                || suffix.EndsWith(".m4s", StringComparison.OrdinalIgnoreCase)
                || suffix.EndsWith(".aac", StringComparison.OrdinalIgnoreCase))
                kind = "segment";
            else if (suffix.StartsWith("stream", StringComparison.OrdinalIgnoreCase)
                || suffix.StartsWith("hls", StringComparison.OrdinalIgnoreCase)
                || suffix.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                || suffix.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
                kind = "file";
            return kind.Length != 0;
        }
        return false;
    }
}
