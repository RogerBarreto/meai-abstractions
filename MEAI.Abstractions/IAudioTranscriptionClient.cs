namespace ConsoleAssemblyAI;

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public interface IAudioTranscriptionClient : IDisposable
{
    Task<TranscriptionCompletion> TranscribeAsync(
        IAsyncEnumerable<AudioContent> inputAudio, 
        TranscriptionOptions? options = null, 
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamingTranscriptionUpdate> TranscribeStreamingAsync(
        IAsyncEnumerable<AudioContent> inputAudio,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default);
}
