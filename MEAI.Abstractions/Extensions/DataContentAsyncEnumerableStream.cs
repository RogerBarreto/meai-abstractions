
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;

internal class DataContentAsyncEnumerableStream<T> : Stream 
    where T : DataContent
{
    private readonly IAsyncEnumerator<T> _enumerator;
    private bool _isCompleted;
    private byte[] _remainingData;
    private int _remainingDataOffset;
    private long _position;
    private AudioContent? _firstChunk;

    internal DataContentAsyncEnumerableStream(IAsyncEnumerable<T> asyncEnumerable, CancellationToken cancellationToken = default)
    {
        this._enumerator = asyncEnumerable.GetAsyncEnumerator(cancellationToken);
        this._remainingData = Array.Empty<byte>();
        this._remainingDataOffset = 0;
        this._position = 0;
    }

    internal DataContentAsyncEnumerableStream(IAsyncEnumerable<T> asyncEnumerable, AudioContent firstChunk, CancellationToken cancellationToken = default)
        : this(asyncEnumerable, cancellationToken)
    {
        this._firstChunk = firstChunk;
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
                // Special case when the first chunk was skipped and needs to be read
                if (this._position == 0 && this._firstChunk is not null && this._firstChunk.Data.HasValue)
                {
                    this._remainingData = this._firstChunk.Data.Value.ToArray();
                    this._remainingDataOffset = 0;

                    continue;
                }

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

    internal void AddFirstChunk(AudioContent firstChunk)
    {
        throw new NotImplementedException();
    }
}

