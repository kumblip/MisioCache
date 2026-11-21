using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

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
                // Only temporary numeric parsing of these headers; no raw header is kept.
                var rawRequestedRange = requestedRange ? context.Request.Headers["Range"].ToString() : string.Empty;
                var hasVideoId = TryExtractMediaId(path, out var mediaId);
                // Only file GET uses the optional body writer meter. All other requests
                // keep the original pipeline byte-for-byte, including HEAD and HLS.
                // A missing or incompatible feature MUST NOT prevent playback.
                var startTick = Stopwatch.GetTimestamp();
                var originalFeature = context.Features.Get<IHttpResponseBodyFeature>();
                MisioCacheResponseByteMeter? meter = null;
                MisioCacheMeasuringBodyFeature? wrapper = null;
                if (kind == "file" && HttpMethods.IsGet(method) && originalFeature != null
                    && Jellyfin.Plugin.MisioCache.Plugin.Instance?.Configuration.EnableResponseBodyByteMeter == true)
                {
                    try
                    {
                        meter = new MisioCacheResponseByteMeter();
                        wrapper = new MisioCacheMeasuringBodyFeature(originalFeature, meter);
                        context.Features.Set<IHttpResponseBodyFeature>(wrapper);
                    }
                    catch { meter = null; wrapper = null; }
                }

                bool failed = false;
                try
                {
                    await nextRequest().ConfigureAwait(false);
                }
                catch
                {
                    failed = true;
                    throw;
                }
                finally
                {
                    // Never leave the instrumentation feature installed for another
                    // middleware or request, even when a player disconnects.
                    if (wrapper != null && ReferenceEquals(context.Features.Get<IHttpResponseBodyFeature>(), wrapper))
                    {
                        try { context.Features.Set(originalFeature); }
                        catch { /* best effort */ }
                    }
                    try
                    {
                        var elapsedMs = (long)Stopwatch.GetElapsedTime(startTick).TotalMilliseconds;
                        var firstMs = meter?.FirstWriteTick > 0
                            ? (long?)Stopwatch.GetElapsedTime(startTick, meter.FirstWriteTick).TotalMilliseconds
                            : null;
                        // Zero bytes alone is NOT a trustworthy measurement when
                        // another middleware bypassed the feature or SendFile failed.
                        long? acceptedBytes = meter != null && !meter.UnverifiedSendfile && meter.Writes > 0
                            ? meter.Bytes : null;
                        _store.Record(kind, requestedRange, context.Response.StatusCode,
                            context.Response.Headers.ContainsKey("Content-Range"),
                            rawRequestedRange, context.Response.Headers["Content-Range"].ToString(),
                            hasVideoId ? mediaId : Guid.Empty, HttpMethods.IsHead(method),
                            acceptedBytes, elapsedMs, firstMs,
                            context.RequestAborted.IsCancellationRequested, failed);
                    }
                    catch { /* telemetry must never break native playback */ }
                }
            });
            next(app);
        };
    }

    // Extract a GUID transiently ONLY for HMAC correlation. Never retain it, the
    // full URL, query, title, user or access token in the observer's samples.
    private static bool TryExtractMediaId(string path, out Guid mediaId)
    {
        mediaId = Guid.Empty;
        var span = path.AsSpan();
        while (!span.IsEmpty)
        {
            int slash = span.IndexOf('/');
            if (slash < 0) return false;
            span = span[(slash + 1)..];
            if (!span.StartsWith("videos/", StringComparison.OrdinalIgnoreCase)) continue;
            span = span[7..];
            int end = span.IndexOf('/');
            if (end < 0) return false;
            return Guid.TryParse(span[..end], out mediaId);
        }
        return false;
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
