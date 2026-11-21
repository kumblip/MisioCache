using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;

namespace Jellyfin.Plugin.MisioCache.Services;

/// <summary>
/// Opt-in measurement at the ASP.NET Core response-body boundary. The original
/// body feature still performs every operation. No media bytes are read, stored,
/// buffered, transformed, or requested. Counts bytes ACCEPTED by the downstream
/// HTTP writer, NOT packets delivered to the player / written into phone cache.
/// </summary>
internal sealed class MisioCacheResponseByteMeter
{
    private long _bytes;
    private long _writes;
    private long _firstTick;
    private int _unverifiedSendfile;
    public long Bytes => Interlocked.Read(ref _bytes);
    public long Writes => Interlocked.Read(ref _writes);
    public long FirstWriteTick => Interlocked.Read(ref _firstTick);
    public bool UnverifiedSendfile => Volatile.Read(ref _unverifiedSendfile) != 0;
    public void MarkUnverifiedSendfile() => Volatile.Write(ref _unverifiedSendfile, 1);
    public void Add(long size)
    {
        if (size <= 0) return;
        Interlocked.CompareExchange(ref _firstTick, Stopwatch.GetTimestamp(), 0);
        Interlocked.Add(ref _bytes, size);
        Interlocked.Increment(ref _writes);
    }
}

/// <summary>
/// Delegates to the SAME original Kestrel response feature. No asynchronous
/// prefetch, buffering, changes to headers, compression, or media player logic.
/// </summary>
internal sealed class MisioCacheMeasuringBodyFeature : IHttpResponseBodyFeature
{
    private readonly IHttpResponseBodyFeature _inner;
    private readonly MisioCacheResponseByteMeter _meter;
    private readonly Stream _stream;
    private PipeWriter? _writer;

    public MisioCacheMeasuringBodyFeature(IHttpResponseBodyFeature inner, MisioCacheResponseByteMeter meter)
    {
        _inner = inner;
        _meter = meter;
        _stream = new MeteredStream(inner.Stream, meter);
    }

    public Stream Stream => _stream;
    public PipeWriter Writer => _writer ??= new MeteredPipeWriter(_inner.Writer, _meter);
    public void DisableBuffering() => _inner.DisableBuffering();
    public Task StartAsync(CancellationToken cancellationToken = default) => _inner.StartAsync(cancellationToken);
    public Task CompleteAsync() => _inner.CompleteAsync();

    public async Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
    {
        // SendFile is NOT a Stream.Write. Count only after the original operation
        // completed successfully; incomplete/cancelled sendfile stays 'unknown'.
        try
        {
            await _inner.SendFileAsync(path, offset, count, cancellationToken).ConfigureAwait(false);
            var amount = count;
            if (!amount.HasValue)
            {
                var length = new FileInfo(path).Length;
                amount = Math.Max(0, length - offset);
            }
            _meter.Add(Math.Max(0, amount.Value));
        }
        catch
        {
            _meter.MarkUnverifiedSendfile();
            throw;
        }
    }

    private sealed class MeteredStream : Stream
    {
        private readonly Stream _inner;
        private readonly MisioCacheResponseByteMeter _meter;
        public MeteredStream(Stream inner, MisioCacheResponseByteMeter meter) { _inner = inner; _meter = meter; }
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            _meter.Add(count);
        }
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _inner.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            _meter.Add(count);
        }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            _meter.Add(buffer.Length);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _inner.Write(buffer);
            _meter.Add(buffer.Length);
        }
        public override void WriteByte(byte value)
        {
            _inner.WriteByte(value);
            _meter.Add(1);
        }
        // The container's HTTP feature owns the real stream: do not close it.
        protected override void Dispose(bool disposing) => base.Dispose(disposing);
    }

    private sealed class MeteredPipeWriter : PipeWriter
    {
        private readonly PipeWriter _inner;
        private readonly MisioCacheResponseByteMeter _meter;
        public MeteredPipeWriter(PipeWriter inner, MisioCacheResponseByteMeter meter) { _inner = inner; _meter = meter; }
        public override bool CanGetUnflushedBytes => _inner.CanGetUnflushedBytes;
        public override long UnflushedBytes => _inner.UnflushedBytes;
        public override void Advance(int bytes) { _inner.Advance(bytes); _meter.Add(bytes); }
        public override Memory<byte> GetMemory(int sizeHint = 0) => _inner.GetMemory(sizeHint);
        public override Span<byte> GetSpan(int sizeHint = 0) => _inner.GetSpan(sizeHint);
        public override void CancelPendingFlush() => _inner.CancelPendingFlush();
        public override void Complete(Exception? exception = null) => _inner.Complete(exception);
        public override ValueTask CompleteAsync(Exception? exception = null) => _inner.CompleteAsync(exception);
        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
            => _inner.FlushAsync(cancellationToken);
    }
}
