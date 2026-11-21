using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace Jellyfin.Plugin.MisioCache.Services;

/// <summary>
/// Bounded, volatile, anonymous HTTP diagnostics.  No media bytes, authorization,
/// addresses, query strings, identifiers, titles or file paths are retained.
/// Not a video cache and never proof that the client's device reused media bytes.
/// </summary>
public sealed class MisioCacheHttpObservationStore
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(30);
    private const int MaxSamples = 2048;
    private readonly ConcurrentQueue<Sample> _samples = new();
    private int _count;
    private volatile bool _pipelineInstalled;

    private readonly record struct Sample(DateTimeOffset At, string Kind, bool Range,
        int Status, bool ContentRange);

    /// <summary>Set only when ASP.NET Core actually invokes the startup filter.</summary>
    public void MarkPipelineInstalled() => _pipelineInstalled = true;

    public void Record(string kind, bool requestedRange, int statusCode, bool hasContentRange)
    {
        _samples.Enqueue(new Sample(DateTimeOffset.UtcNow, kind, requestedRange, statusCode, hasContentRange));
        Interlocked.Increment(ref _count);
        Trim();
    }

    private void Trim()
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(Window);
        while (_samples.TryPeek(out var first)
            && (Volatile.Read(ref _count) > MaxSamples || first.At < cutoff))
        {
            if (_samples.TryDequeue(out _)) Interlocked.Decrement(ref _count);
        }
    }

    public object Snapshot(bool enabled)
    {
        Trim();
        var items = _samples.ToArray();
        return new
        {
            enabled,
            pipelineInstalled = _pipelineInstalled,
            windowMinutes = 30,
            sampleLimit = MaxSamples,
            totalRequests = items.Length,
            rangeRequests = items.Count(x => x.Range),
            partialResponses206 = items.Count(x => x.Status == 206),
            validPartialRanges = items.Count(x => x.Range && x.Status == 206 && x.ContentRange),
            ignoredRange200 = items.Count(x => x.Range && x.Status == 200),
            unsatisfiableRanges416 = items.Count(x => x.Status == 416),
            fileRequests = items.Count(x => x.Kind == "file"),
            playlistRequests = items.Count(x => x.Kind == "playlist"),
            segmentRequests = items.Count(x => x.Kind == "segment"),
            // Response Content-Length is NOT an accurate transfer counter; do not claim byte counts.
            measuredBytes = (long?)null,
            note = "Tylko żądania HTTP widoczne na NAS-ie. 206 nie dowodzi, że urządzenie zapisuje ani ponownie używa danych. Brak pomiaru bajtów."
        };
    }

    public void Clear()
    {
        while (_samples.TryDequeue(out _)) Interlocked.Decrement(ref _count);
    }
}
