using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace Jellyfin.Plugin.MisioCache.Services;

/// <summary>
/// Volatile, opt-in, PASSIVE diagnostics of server-declared byte ranges. No media
/// payload, path, URL, token, IP, client ID, title or persistent media identifier
/// is stored. A per-process HMAC lets ranges of ONE media item be compared safely.
/// Content-Range lengths are NOT a measurement of bytes actually transferred or
/// proof of a cache hit/miss on the device.
/// </summary>
public sealed class MisioCacheHttpObservationStore
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(30);
    private const int MaxSamples = 2048;
    private const int MaxRecent = 25;
    private static readonly Regex SingleRange = new(
        @"^bytes\s+(\d+)-(\d+)/(\d+|\*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private readonly byte[] _ephemeralHmacKey = RandomNumberGenerator.GetBytes(32);
    private readonly ConcurrentQueue<Sample> _samples = new();
    private int _count;
    private volatile bool _pipelineInstalled;

    private readonly record struct Sample(DateTimeOffset At, string Kind, bool Range,
        int Status, bool ContentRange, string MediaTag, long? Start, long? End,
        long? RequestedStart);

    public void MarkPipelineInstalled() => _pipelineInstalled = true;

    // Compatibility overload for the original anonymous counters.
    public void Record(string kind, bool requestedRange, int statusCode, bool hasContentRange) =>
        Record(kind, requestedRange, statusCode, hasContentRange,
            string.Empty, string.Empty, Guid.Empty, isHead: false);

    /// <summary>
    /// Request/response headers are parsed in-memory and immediately discarded.
    /// HEAD is counted, but NEVER treated as transferred media data.
    /// </summary>
    public void Record(string kind, bool requestedRange, int statusCode, bool hasContentRange,
        string requestRangeHeader, string contentRangeHeader, Guid mediaGuid, bool isHead)
    {
        string tag = !isHead && kind == "file" && requestedRange && statusCode == 206
            && mediaGuid != Guid.Empty ? TransientMediaTag(mediaGuid) : string.Empty;
        long? start = null;
        long? end = null;
        long? requestedStart = null;
        if (!isHead && kind == "file" && requestedRange && statusCode == 206
            && hasContentRange && tag.Length > 0 && IsSingleByteRequest(requestRangeHeader))
        {
            requestedStart = ParseRequestedStart(requestRangeHeader);
            if (TryParseContentRange(contentRangeHeader, out var from, out var to))
            {
                start = from;
                end = to;
            }
        }
        _samples.Enqueue(new Sample(DateTimeOffset.UtcNow, kind, requestedRange, statusCode,
            hasContentRange, tag, start, end, requestedStart));
        Interlocked.Increment(ref _count);
        Trim();
    }

    private string TransientMediaTag(Guid mediaGuid)
    {
        // HMAC secret is regenerated at each server start. It is never serialized.
        var hash = HMACSHA256.HashData(_ephemeralHmacKey, mediaGuid.ToByteArray());
        return Convert.ToHexString(hash.AsSpan(0, 12));
    }

    private static bool IsSingleByteRequest(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 100) return false;
        var s = raw.Trim();
        if (!s.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return false;
        var spec = s.AsSpan(6).Trim();
        if (spec.IndexOf(',') >= 0) return false;
        var dash = spec.IndexOf('-');
        if (dash < 0 || (dash == 0 && spec.Length == 1)) return false;
        // A standard single byte range is N-, N-M or -N (suffix request).
        for (var i = 0; i < spec.Length; i++)
        {
            if (i != dash && (spec[i] < '0' || spec[i] > '9')) return false;
        }
        return true;
    }

    private static long? ParseRequestedStart(string raw)
    {
        var s = raw.Trim();
        if (!s.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return null;
        var spec = s.AsSpan(6);
        var dash = spec.IndexOf('-');
        if (dash <= 0 || spec.IndexOf(',') >= 0) return null;
        return long.TryParse(spec[..dash], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value >= 0 ? value : null;
    }

    internal static bool TryParseContentRange(string raw, out long first, out long last)
    {
        first = last = 0;
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 128) return false;
        Match match;
        try { match = SingleRange.Match(raw.Trim()); }
        catch (RegexMatchTimeoutException) { return false; }
        if (!match.Success
            || !long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out first)
            || !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out last)
            || first < 0 || last < first || last == long.MaxValue) return false;
        // Also reject invalid positions when the full resource length is provided.
        var totalText = match.Groups[3].Value;
        if (totalText == "*") return true;
        return long.TryParse(totalText, NumberStyles.None, CultureInfo.InvariantCulture, out var total)
            && total > 0 && last < total;
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

    // Sorted, disjoint, inclusive intervals by pseudo-media ID; never compare
    // different video files even if they have identical numeric byte positions.
    private static long OverlapAndInsert(List<(long Start, long End)> regions, long first, long last)
    {
        long overlap = 0;
        foreach (var r in regions)
        {
            if (r.End < first) continue;
            if (r.Start > last) break;
            var a = Math.Max(r.Start, first);
            var b = Math.Min(r.End, last);
            if (a <= b) overlap = checked(overlap + (b - a + 1));
        }
        regions.Add((first, last));
        regions.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = new List<(long Start, long End)>(regions.Count);
        foreach (var r in regions)
        {
            if (merged.Count == 0) { merged.Add(r); continue; }
            var prev = merged[^1];
            // Avoid arithmetic overflow in adjacency comparison.
            if (r.Start <= prev.End || (prev.End != long.MaxValue && r.Start == prev.End + 1))
                merged[^1] = (prev.Start, Math.Max(prev.End, r.End));
            else merged.Add(r);
        }
        regions.Clear();
        regions.AddRange(merged);
        return overlap;
    }

    public object Snapshot(bool enabled)
    {
        Trim();
        var samples = _samples.ToArray();
        var byMedia = new Dictionary<string, List<(long Start, long End)>>(StringComparer.Ordinal);
        var mediaLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        var recent = new List<object>();
        long declared = 0, repeated = 0;
        int parsed = 0, overlappingResponses = 0;
        int unavailable = 0;
        foreach (var item in samples)
        {
            if (item.Range && item.Status == 206 && item.ContentRange && item.Kind == "file" && !item.Start.HasValue)
                unavailable++;
            if (!item.Start.HasValue || !item.End.HasValue || item.MediaTag.Length == 0) continue;
            if (!mediaLabels.TryGetValue(item.MediaTag, out var label))
            {
                label = "M" + (mediaLabels.Count + 1).ToString(CultureInfo.InvariantCulture);
                mediaLabels.Add(item.MediaTag, label);
            }
            if (!byMedia.TryGetValue(item.MediaTag, out var intervals))
                byMedia.Add(item.MediaTag, intervals = new List<(long Start, long End)>());
            var start = item.Start.Value;
            var end = item.End.Value;
            var size = checked(end - start + 1);
            var overlap = OverlapAndInsert(intervals, start, end);
            declared = checked(declared + size);
            repeated = checked(repeated + overlap);
            parsed++;
            if (overlap > 0) overlappingResponses++;
            recent.Add(new
            {
                utc = item.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                media = label, requestedStart = item.RequestedStart,
                start, end, declaredBytes = size, overlappingBytes = overlap,
                newDeclaredBytes = size - overlap
            });
        }
        return new
        {
            enabled,
            pipelineInstalled = _pipelineInstalled,
            windowMinutes = 30,
            sampleLimit = MaxSamples,
            totalRequests = samples.Length,
            rangeRequests = samples.Count(x => x.Range),
            partialResponses206 = samples.Count(x => x.Status == 206),
            validPartialRanges = samples.Count(x => x.Range && x.Status == 206 && x.ContentRange),
            ignoredRange200 = samples.Count(x => x.Range && x.Status == 200),
            unsatisfiableRanges416 = samples.Count(x => x.Status == 416),
            fileRequests = samples.Count(x => x.Kind == "file"),
            playlistRequests = samples.Count(x => x.Kind == "playlist"),
            segmentRequests = samples.Count(x => x.Kind == "segment"),
            parsedFileRanges = parsed,
            unparsedFileRanges = unavailable,
            anonymousMediaCount = mediaLabels.Count,
            overlappingResponses,
            declaredPartialBytes = declared,
            declaredUniqueBytes = declared - repeated,
            declaredRepeatedBytes = repeated,
            recentRanges = recent.TakeLast(MaxRecent).ToArray(),
            // This cannot be inferred from Content-Range without tracking the body stream.
            measuredBytes = (long?)null,
            note = "Długości z nagłówków Content-Range (nie fizyczny transfer!). Nakładający się zakres nie dowodzi, że klient pobrał go wcześniej w całości ani że nie ma cache. Historia zeruje się po restarcie, wyczyszczeniu i upływie 30 minut."
        };
    }

    public void Clear()
    {
        while (_samples.TryDequeue(out _)) Interlocked.Decrement(ref _count);
    }
}
