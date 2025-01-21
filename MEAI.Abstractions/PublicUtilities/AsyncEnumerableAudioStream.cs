namespace MEAI.Abstractions;

using Microsoft.Extensions.AI;
using System.Collections.Generic;
using System.IO;
using System.Threading;

/// <summary>
/// An async enumerable facilitator that reads audio content from a stream.
/// </summary>
public class AsyncEnumerableAudioStream : IAsyncEnumerable<AudioContent>
{
    private IAsyncEnumerable<AudioContent> _asyncEnumerable;
    Stream _audioStream;

    public AsyncEnumerableAudioStream(Stream audioStream)
    {
        _audioStream = audioStream;
        _asyncEnumerable = this.ReadAudioStream();
    }

    public IAsyncEnumerator<AudioContent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => _asyncEnumerable.GetAsyncEnumerator(cancellationToken);

    private async IAsyncEnumerable<AudioContent> ReadAudioStream()
    {
        // Allow multiple reads from the same stream
        if (_audioStream.CanSeek && _audioStream.Position > 0)
        {
            _audioStream.Seek(0, SeekOrigin.Begin);
        }

        var buffer = new byte[4096];
        var bytesRead = 0;
        while ((bytesRead = await _audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            yield return new AudioContent(buffer);
        }
    }
}
