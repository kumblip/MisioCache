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
Console.WriteLine("MisioCache v0.1.4 range engine: all 15 checks passed");
