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
        long? RequestedStart, long? AcceptedBytes, long ElapsedMs, long? FirstWriteMs,
        bool Cancelled, bool Failed);

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
        => Record(kind, requestedRange, statusCode, hasContentRange, requestRangeHeader,
            contentRangeHeader, mediaGuid, isHead, null, 0, null, false, false);

    /// <summary>
    /// acceptedBytes is counted at the response-body writer boundary only. It
    /// means bytes submitted to ASP.NET transport, NOT bytes acknowledged over
    /// the network, stored on Android, or loaded into a client-side cache.
    /// </summary>
    public void Record(string kind, bool requestedRange, int statusCode, bool hasContentRange,
        string requestRangeHeader, string contentRangeHeader, Guid mediaGuid, bool isHead,
        long? acceptedBytes, long elapsedMs, long? firstWriteMs, bool cancelled, bool failed)
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
            hasContentRange, tag, start, end, requestedStart,
            acceptedBytes, elapsedMs, firstWriteMs, cancelled, failed));
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

    public object Snapshot(bool enabled, bool bodyMeterEnabled = false)
    {
        Trim();
        var samples = _samples.ToArray();
        var declaredIntervals = new Dictionary<string, List<(long Start, long End)>>(StringComparer.Ordinal);
        var meteredIntervals = new Dictionary<string, List<(long Start, long End)>>(StringComparer.Ordinal);
        var lastStarts = new Dictionary<string, long>(StringComparer.Ordinal);
        var mediaLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        var recent = new List<object>();
        long declared = 0, declaredRepeated = 0;
        long metered = 0, meteredRepeated = 0, meteredAll = 0;
        long forwardRepeated = 0, backwardRepeated = 0, maxJump = 0;
        long summedElapsedMs = 0;
        int parsed = 0, overlappingResponses = 0, meteredResponses = 0;
        int incompleteResponses = 0, unavailable = 0, unknownBodyResponses = 0;
        int cancelledResponses = 0, failedResponses = 0;
        int forwardStarts = 0, backwardStarts = 0, sameStarts = 0;
        var durations = new List<long>();
        var firstByteTimes = new List<long>();

        foreach (var item in samples)
        {
            if (item.Cancelled) cancelledResponses++;
            if (item.Failed) failedResponses++;
            if (item.AcceptedBytes.HasValue)
                meteredAll = checked(meteredAll + item.AcceptedBytes.Value);
            if (item.Range && item.Status == 206 && item.ContentRange && item.Kind == "file" && !item.Start.HasValue)
                unavailable++;
            if (!item.Start.HasValue || !item.End.HasValue || item.MediaTag.Length == 0) continue;
            if (!mediaLabels.TryGetValue(item.MediaTag, out var label))
            {
                label = "M" + (mediaLabels.Count + 1).ToString(CultureInfo.InvariantCulture);
                mediaLabels.Add(item.MediaTag, label);
            }
            if (!declaredIntervals.TryGetValue(item.MediaTag, out var interval))
                declaredIntervals.Add(item.MediaTag, interval = new List<(long Start, long End)>());
            var start = item.Start.Value;
            var end = item.End.Value;
            var size = checked(end - start + 1);
            var declaredOverlap = OverlapAndInsert(interval, start, end);
            declared = checked(declared + size);
            declaredRepeated = checked(declaredRepeated + declaredOverlap);
            parsed++;
            if (declaredOverlap > 0) overlappingResponses++;

            string movement = "pierwszy";
            long delta = 0;
            if (lastStarts.TryGetValue(item.MediaTag, out var lastStart))
            {
                delta = start >= lastStart ? start - lastStart : lastStart - start;
                if (delta > maxJump) maxJump = delta;
                if (start > lastStart) { movement = "przód"; forwardStarts++; }
                else if (start < lastStart) { movement = "tył"; backwardStarts++; }
                else { movement = "ten sam"; sameStarts++; }
            }
            lastStarts[item.MediaTag] = start;

            // A single HTTP 206 body maps a contiguous prefix of its
            // Content-Range. Cap the submitted count at the declared interval.
            // Missing instrumentation NEVER becomes an invented zero-byte body.
            long? accepted = item.AcceptedBytes;
            long? effective = accepted.HasValue ? Math.Min(size, Math.Max(0, accepted.Value)) : null;
            long? actualOverlap = null;
            if (effective.HasValue)
            {
                meteredResponses++;
                if (effective.Value < size) incompleteResponses++;
                if (effective.Value > 0)
                {
                    if (!meteredIntervals.TryGetValue(item.MediaTag, out var observed))
                        meteredIntervals.Add(item.MediaTag, observed = new List<(long Start, long End)>());
                    actualOverlap = OverlapAndInsert(observed, start, checked(start + effective.Value - 1));
                    metered = checked(metered + effective.Value);
                    meteredRepeated = checked(meteredRepeated + actualOverlap.Value);
                    if (movement == "przód") forwardRepeated = checked(forwardRepeated + actualOverlap.Value);
                    if (movement == "tył") backwardRepeated = checked(backwardRepeated + actualOverlap.Value);
                }
                else actualOverlap = 0;
                if (item.ElapsedMs >= 0) { durations.Add(item.ElapsedMs); summedElapsedMs += item.ElapsedMs; }
                if (item.FirstWriteMs.HasValue) firstByteTimes.Add(item.FirstWriteMs.Value);
            }
            else unknownBodyResponses++;

            recent.Add(new
            {
                utc = item.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                media = label, requestedStart = item.RequestedStart,
                start, end, declaredBytes = size, overlappingBytes = declaredOverlap,
                newDeclaredBytes = size - declaredOverlap,
                movement, moveDeltaBytes = delta,
                acceptedBytes = accepted, meteredRangeBytes = effective,
                meteredRepeatedBytes = actualOverlap,
                durationMs = item.ElapsedMs, firstWriteMs = item.FirstWriteMs,
                incomplete = effective.HasValue && effective.Value < size,
                cancelled = item.Cancelled, failed = item.Failed
            });
        }
        durations.Sort();
        firstByteTimes.Sort();
        return new
        {
            enabled,
            pipelineInstalled = _pipelineInstalled,
            bodyMeterEnabled,
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
            declaredUniqueBytes = declared - declaredRepeated,
            declaredRepeatedBytes = declaredRepeated,
            meteredResponses,
            unknownBodyResponses,
            incompleteResponses,
            cancelledResponses,
            failedResponses,
            meteredAllBodyBytes = meteredAll,
            meteredRangeBytes = metered,
            meteredUniqueBytes = metered - meteredRepeated,
            meteredRepeatedBytes = meteredRepeated,
            meteredRepeatPercent = metered > 0 ? Math.Round(100d * meteredRepeated / metered, 1) : (double?)null,
            forwardStarts,
            backwardStarts,
            sameStarts,
            forwardRepeatedBytes = forwardRepeated,
            backwardRepeatedBytes = backwardRepeated,
            maxRangeStartJumpBytes = maxJump,
            averageBodyMbps = summedElapsedMs > 0 ? Math.Round((double)metered * 8 / summedElapsedMs / 1000, 2) : (double?)null,
            p95RequestMs = durations.Count > 0 ? durations[(int)Math.Ceiling(durations.Count * .95) - 1] : (long?)null,
            p95FirstWriteMs = firstByteTimes.Count > 0 ? firstByteTimes[(int)Math.Ceiling(firstByteTimes.Count * .95) - 1] : (long?)null,
            recentRanges = recent.TakeLast(MaxRecent).ToArray(),
            // Legacy key kept for compatibility; it never represented actual transfer.
            measuredBytes = (long?)null,
            note = "Pomiar acceptedBytes oznacza bajty przekazane do interfejsu zapisu HTTP (nie gwarantuje dostarczenia do telefonu). Zakresy deklarowane pochodzą z Content-Range. Kierunek to zmiana startu zakresu, nie potwierdzony seek czasu. Brak pomiaru = nieznane, a nie 0. Historia RAM 30 min / 2048 próbek."
        };
    }

    public void Clear()
    {
        while (_samples.TryDequeue(out _)) Interlocked.Decrement(ref _count);
    }
}
