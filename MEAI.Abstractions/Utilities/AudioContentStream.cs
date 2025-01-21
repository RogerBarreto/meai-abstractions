﻿
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;
internal class AudioContentAsyncEnumerableStream : Stream
{
    private readonly IAsyncEnumerator<AudioContent> _enumerator;
    private bool _isCompleted;
    private byte[] _remainingData;
    private int _remainingDataOffset;
    private long _position;

    internal AudioContentAsyncEnumerableStream(IAsyncEnumerable<AudioContent> asyncEnumerable, CancellationToken cancellationToken = default)
    {
        this._enumerator = asyncEnumerable.GetAsyncEnumerator(cancellationToken);
        this._remainingData = Array.Empty<byte>();
        this._remainingDataOffset = 0;
        this._position = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => this._position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Use ReadAsync instead for asynchronous reading.");
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
    {
        if (this._isCompleted)
        {
            return 0;
        }

        int bytesRead = 0;

        while (bytesRead < count)
        {
            if (this._remainingDataOffset < this._remainingData.Length)
            {
                int bytesToCopy = Math.Min(count - bytesRead, this._remainingData.Length - this._remainingDataOffset);
                Array.Copy(this._remainingData, this._remainingDataOffset, buffer, offset + bytesRead, bytesToCopy);
                this._remainingDataOffset += bytesToCopy;
                bytesRead += bytesToCopy;
                this._position += bytesToCopy;
            }
            else
            {
                if (!await this._enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    this._isCompleted = true;
                    break;
                }

                if (!this._enumerator.Current.Data.HasValue)
                {
                    this._isCompleted = true;
                    break;
                }

                this._remainingData = this._enumerator.Current.Data.Value.ToArray();
                this._remainingDataOffset = 0;
            }
        }

        return bytesRead;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }
}

