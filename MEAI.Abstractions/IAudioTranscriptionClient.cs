
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;

public interface IAudioTranscriptionClient : IDisposable
{
    Task<TranscriptionCompletion> TranscribeAsync(
        IAsyncEnumerable<AudioContent> audioContents, 
        TranscriptionOptions? options = null, 
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamingTranscriptionUpdate> TranscribeStreamingAsync(
        IAsyncEnumerable<AudioContent> audioContents,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default);
}
