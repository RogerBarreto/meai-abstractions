
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;

/// <summary>
/// An async enumerable facilitator that reads audio content from a stream.
/// </summary>
public class AsyncEnumerableAudioStream : IAsyncEnumerable<AudioContent>
{
    private IAsyncEnumerable<AudioContent> _asyncEnumerable;
    Stream _audioStream;
    private readonly string? _mediaType;

    public AsyncEnumerableAudioStream(Stream audioStream, string? mediaType = null)
    {
        this._audioStream = audioStream;
        this._mediaType = mediaType;
        this._asyncEnumerable = this.ReadAudioStream();
    }

    public IAsyncEnumerator<AudioContent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => this._asyncEnumerable.GetAsyncEnumerator(cancellationToken);

    private async IAsyncEnumerable<AudioContent> ReadAudioStream()
    {
        // Allow multiple reads from the same stream
        if (this._audioStream.CanSeek && this._audioStream.Position > 0)
        {
            this._audioStream.Seek(0, SeekOrigin.Begin);
        }

        var buffer = new byte[4096];
        var bytesRead = 0;
        while ((bytesRead = await this._audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            yield return new AudioContent(buffer, this._mediaType);
        }
    }
}
