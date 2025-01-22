﻿
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;

public interface IAudioTranscriptionClient : IDisposable
{
    Task<TranscriptionCompletion> TranscribeAsync(
        IAsyncEnumerable<AudioContent> audioContents, 
        AudioTranscriptionOptions? options = null, 
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamingAudioTranscriptionUpdate> TranscribeStreamingAsync(
        IAsyncEnumerable<AudioContent> audioContents,
        AudioTranscriptionOptions? options = null,
        CancellationToken cancellationToken = default);
}
