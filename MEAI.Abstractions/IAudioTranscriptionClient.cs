namespace ConsoleAssemblyAI;

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
