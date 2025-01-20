namespace MEAI.Abstractions;

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class AudioContentAsyncEnumerableStream : Stream
{
    private readonly IAsyncEnumerator<AudioContent> _enumerator;
    private bool _isCompleted;
    private byte[] _remainingData;
    private int _remainingDataOffset;
    private long _position;

    internal AudioContentAsyncEnumerableStream(IAsyncEnumerable<AudioContent> asyncEnumerable, CancellationToken cancellationToken = default)
    {
        _enumerator = asyncEnumerable.GetAsyncEnumerator(cancellationToken);
        _remainingData = Array.Empty<byte>();
        _remainingDataOffset = 0;
        _position = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => _position;
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
        if (_isCompleted)
            return 0;

        int bytesRead = 0;

        while (bytesRead < count)
        {
            if (_remainingDataOffset < _remainingData.Length)
            {
                int bytesToCopy = Math.Min(count - bytesRead, _remainingData.Length - _remainingDataOffset);
                Array.Copy(_remainingData, _remainingDataOffset, buffer, offset + bytesRead, bytesToCopy);
                _remainingDataOffset += bytesToCopy;
                bytesRead += bytesToCopy;
                _position += bytesToCopy;
            }
            else
            {
                if (!await _enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    _isCompleted = true;
                    break;
                }

                if (!_enumerator.Current.Data.HasValue)
                {
                    _isCompleted = true;
                    break;
                }

                _remainingData = _enumerator.Current.Data.Value.ToArray();
                _remainingDataOffset = 0;
            }
        }

        return bytesRead;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }
}

