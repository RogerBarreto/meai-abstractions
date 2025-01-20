namespace ConsoleAssemblyAI;

using AssemblyAI;
using AssemblyAI.Transcripts;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

internal sealed partial class AssemblyAITranscriptionClient : IAudioTranscriptionClient
{
    public async Task<TranscriptionCompletion> TranscribeAsync(IAsyncEnumerable<AudioContent> audioContent, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        Transcript? transcript = null;

        var enumerator = audioContent.GetAsyncEnumerator(cancellationToken);
        await enumerator.MoveNextAsync();

        var firstChunk = enumerator.Current;

        // File by reference
        if (!firstChunk.ContainsData)
        {
            transcript = await _client.Transcripts.TranscribeAsync(
                new Uri(firstChunk.Uri), 
                transcriptParams: this.ToTranscriptOptionalParams(options), 
                cancellationToken: cancellationToken);
        }
        else
        {
            using var audioFileStream = new AudioContentAsyncEnumerableStream(audioContent, cancellationToken);

            var fileUpload = await _client.Files.UploadAsync(audioFileStream, new(), cancellationToken);

            transcript = await _client.Transcripts.TranscribeAsync(
                file: fileUpload, 
                transcriptParams: this.ToTranscriptOptionalParams(options), 
                cancellationToken: cancellationToken);
        }

        return this.ToTranscriptionCompletion(transcript);
    }

    public async Task<TranscriptionCompletion> TranscribeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var transcript = await _client.Transcripts.TranscribeAsync(stream, new(), cancellationToken);

        return this.ToTranscriptionCompletion(transcript);
    }
}
