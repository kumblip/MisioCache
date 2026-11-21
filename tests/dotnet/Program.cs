using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Text.Json;
using Jellyfin.Plugin.MisioCache.Services;

// Standalone release gate: actual production C# store/algorithm, no mock engine.
static JsonElement Snapshot(MisioCacheHttpObservationStore store)
    => JsonSerializer.SerializeToElement(store.Snapshot(true));
static long Get(JsonElement value, string field) => value.GetProperty(field).GetInt64();
static void Eq(long actual, long expected, string message)
{
    if (actual != expected) throw new Exception($"{message}: expected {expected}, got {actual}");
}
static void Put(MisioCacheHttpObservationStore store, Guid media, long from, long to, long total = 10000)
    => store.Record("file", true, 206, true, $"bytes={from}-{to}",
        $"bytes {from}-{to}/{total}", media, isHead: false);

var a = Guid.NewGuid();
var b = Guid.NewGuid();
var store = new MisioCacheHttpObservationStore();
store.MarkPipelineInstalled();
Put(store, a, 0, 999);
Put(store, a, 500, 1499);
Put(store, b, 0, 999);
var snapshot = Snapshot(store);
Eq(Get(snapshot, "declaredPartialBytes"), 3000, "sum of declared ranges");
Eq(Get(snapshot, "declaredRepeatedBytes"), 500, "repeated overlap for same video");
Eq(Get(snapshot, "declaredUniqueBytes"), 2500, "unique union across two video keys");
Eq(Get(snapshot, "overlappingResponses"), 1, "overlapping response count");
Eq(Get(snapshot, "anonymousMediaCount"), 2, "media separation");
Eq(Get(snapshot, "parsedFileRanges"), 3, "parsed ranges count");
if (snapshot.GetRawText().Contains(a.ToString(), StringComparison.OrdinalIgnoreCase)
    || snapshot.GetRawText().Contains(b.ToString(), StringComparison.OrdinalIgnoreCase))
    throw new Exception("Response exposed original media GUID");
Put(store, a, 0, 999);
snapshot = Snapshot(store);
Eq(Get(snapshot, "declaredRepeatedBytes"), 1500, "full duplicate counts as overlap");
Eq(Get(snapshot, "declaredUniqueBytes"), 2500, "full duplicate does not add unique bytes");

// HEAD advertises a Content-Range but transfers no body and must not
// count towards byte overlap or declared transferred response lengths.
store.Record("file", true, 206, true, "bytes=0-999", "bytes 0-999/10000", a, isHead: true);
snapshot = Snapshot(store);
Eq(Get(snapshot, "declaredPartialBytes"), 4000, "HEAD ignored in byte accounting");

// Multiparts and malformed ranges must never be assigned incorrect intervals.
store.Record("file", true, 206, true, "bytes=0-10,30-40", "bytes 0-10/10000", a, isHead: false);
store.Record("file", true, 206, true, "bytes=0-10", "bytes 20-5/10000", a, isHead: false);
snapshot = Snapshot(store);
Eq(Get(snapshot, "unparsedFileRanges"), 3, "unparsed multi/head/malformed ranges");
Eq(Get(snapshot, "declaredRepeatedBytes"), 1500, "bad ranges do not change overlap");
if (MisioCacheHttpObservationStore.TryParseContentRange("bytes 10-100/100", out _, out _))
    throw new Exception("Content-Range end cannot reach total length");
if (MisioCacheHttpObservationStore.TryParseContentRange("bytes 2-1/100", out _, out _))
    throw new Exception("Descending range accepted");
if (!MisioCacheHttpObservationStore.TryParseContentRange("bytes 3-8/*", out var start, out var end)
    || start != 3 || end != 8)
    throw new Exception("Valid unknown-total Content-Range rejected");

store.Clear();
snapshot = Snapshot(store);
Eq(Get(snapshot, "declaredPartialBytes"), 0, "reset removes history");
Eq(Get(snapshot, "declaredRepeatedBytes"), 0, "reset removes overlaps");

for (int i = 0; i < 2051; i++) store.Record("playlist", false, 200, false);
snapshot = Snapshot(store);
Eq(Get(snapshot, "totalRequests"), 2048, "bounded samples");

// Opt-in body-writer metrics: a declared overlapping range is not necessarily
// a repeated ACTUALLY SUBMITTED byte range. Verify forward/backward and unknown.
store.Clear();
store.Record("file", true, 206, true, "bytes=0-999", "bytes 0-999/10000",
    a, false, 200L, 20L, 2L, false, false);
store.Record("file", true, 206, true, "bytes=200-999", "bytes 200-999/10000",
    a, false, 300L, 30L, 3L, false, false);
store.Record("file", true, 206, true, "bytes=100-999", "bytes 100-999/10000",
    a, false, 300L, 40L, 4L, true, false);
store.Record("file", true, 206, true, "bytes=400-999", "bytes 400-999/10000",
    a, false, null, 4L, null, false, false);
snapshot = Snapshot(store);
Eq(Get(snapshot, "declaredPartialBytes"), 3300, "declared telemetry retained");
Eq(Get(snapshot, "meteredRangeBytes"), 800, "submitted range bytes");
Eq(Get(snapshot, "meteredUniqueBytes"), 500, "submitted union");
Eq(Get(snapshot, "meteredRepeatedBytes"), 300, "submitted repeated bytes");
Eq(Get(snapshot, "forwardStarts"), 2, "forward start changes");
Eq(Get(snapshot, "backwardStarts"), 1, "backward start changes");
Eq(Get(snapshot, "forwardRepeatedBytes"), 0, "forward repeat baseline");
Eq(Get(snapshot, "backwardRepeatedBytes"), 300, "backward repeat baseline");
Eq(Get(snapshot, "unknownBodyResponses"), 1, "unknown transfer is not zero");
Eq(Get(snapshot, "incompleteResponses"), 3, "partial body submissions");
Eq(Get(snapshot, "cancelledResponses"), 1, "cancelled requests");
Eq(Get(snapshot, "maxRangeStartJumpBytes"), 300, "max start delta");
if (snapshot.GetProperty("recentRanges")[3].GetProperty("meteredRangeBytes").ValueKind != JsonValueKind.Null)
    throw new Exception("Unknown body must remain unknown, never a fabricated zero");

// Compile and exercise the EXACT production HTTP feature decorator (no fake
// measured counter implementation). A failing API change blocks GitHub Release.
using (var sink = new MemoryStream())
{
    var meter = new MisioCacheResponseByteMeter();
    var inner = new TestBodyFeature(sink);
    var wrapped = new MisioCacheMeasuringBodyFeature(inner, meter);
    await wrapped.Stream.WriteAsync(new byte[10].AsMemory());
    Eq(meter.Bytes, 10, "stream WriteAsync counted");
    wrapped.Writer.GetMemory(5).Span[..5].Fill(7);
    wrapped.Writer.Advance(5);
    await wrapped.Writer.FlushAsync();
    Eq(meter.Bytes, 15, "pipe Writer.Advance counted");
    await wrapped.SendFileAsync("unused_path_with_known_count", 0, 7);
    Eq(meter.Bytes, 22, "completed SendFile counted");
    Eq(sink.Length, 22, "decorator never drops or duplicates bytes");
}
Console.WriteLine("MisioCache: range, response-body feature, transfer/direction tests passed");

sealed class TestBodyFeature : IHttpResponseBodyFeature
{
    private readonly MemoryStream _sink;
    public TestBodyFeature(MemoryStream sink)
    {
        _sink = sink;
        Writer = PipeWriter.Create(sink, new StreamPipeWriterOptions(leaveOpen: true));
    }
    public Stream Stream => _sink;
    public PipeWriter Writer { get; }
    public void DisableBuffering() { }
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CompleteAsync() => Task.CompletedTask;
    public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
        => _sink.WriteAsync(new byte[checked((int)(count ?? 0))].AsMemory(), cancellationToken).AsTask();
}

