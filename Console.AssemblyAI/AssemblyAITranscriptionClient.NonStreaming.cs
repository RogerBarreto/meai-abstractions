
using AssemblyAI.Transcripts;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;

namespace ConsoleAssemblyAI;

internal sealed partial class AssemblyAITranscriptionClient : IAudioTranscriptionClient
{
    public async Task<AudioTranscriptionCompletion> TranscribeAsync(IAsyncEnumerable<AudioContent> audioContent, AudioTranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        Transcript? transcript = null;

        var enumerator = audioContent.GetAsyncEnumerator(cancellationToken);
        await enumerator.MoveNextAsync();

        var firstChunk = enumerator.Current;

        // File by reference
        if (!firstChunk.ContainsData)
        {
            transcript = await this._client.Transcripts.TranscribeAsync(
                new Uri(firstChunk.Uri), 
                transcriptParams: this.ToTranscriptOptionalParams(options), 
                cancellationToken: cancellationToken);
        }
        else
        {
            using var audioFileStream = new AudioContentAsyncEnumerableStream(audioContent, firstChunk, cancellationToken);
            var fileUpload = await this._client.Files.UploadAsync(audioFileStream, new(), cancellationToken);

            transcript = await this._client.Transcripts.TranscribeAsync(
                file: fileUpload, 
                transcriptParams: this.ToTranscriptOptionalParams(options), 
                cancellationToken: cancellationToken);
        }

        return this.ToTranscriptionCompletion(transcript);
    }

    public async Task<AudioTranscriptionCompletion> TranscribeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var transcript = await this._client.Transcripts.TranscribeAsync(stream, new(), cancellationToken);

        return this.ToTranscriptionCompletion(transcript);
    }
}
