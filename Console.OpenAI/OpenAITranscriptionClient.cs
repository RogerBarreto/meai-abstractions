using OpenAI.Audio;
using ConsoleAssemblyAI;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

public class OpenAITranscriptionClient : IAudioTranscriptionClient
{
    private readonly string _apiKey;
    private AudioClient _client;

    public OpenAITranscriptionClient(string apiKey)
    {
        _apiKey = apiKey;
        _client = new AudioClient(apiKey);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    public async Task<TranscriptionCompletion> TranscribeAsync(IAsyncEnumerable<AudioContent> audioContent, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var enumerator = audioContent.GetAsyncEnumerator(cancellationToken);
        await enumerator.MoveNextAsync();

        var firstChunk = enumerator.Current;

        TranscriptionResult transcriptionResult;

        if (!firstChunk.ContainsData)
        {
            transcriptionResult = await _client.TranscribeAsync(new Uri(firstChunk.Uri), cancellationToken);
        }
        else
        {
            using var audioFileStream = new AudioContentAsyncEnumerableStream(audioContent, cancellationToken);
            transcriptionResult = await _client.TranscribeAsync(audioFileStream, cancellationToken);
        }

        return new TranscriptionCompletion
        {
            RawRepresentation = transcriptionResult,
            CompletionId = transcriptionResult.Id,
            Content = new TranscribedContent(transcriptionResult.Text),
            StartTime = transcriptionResult.StartTime,
            EndTime = transcriptionResult.EndTime
        };
    }

    public async IAsyncEnumerable<StreamingTranscriptionUpdate> TranscribeStreamingAsync(IAsyncEnumerable<AudioContent> audioContent, TranscriptionOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var transcriptionCompletion = await TranscribeAsync(audioContent, options, cancellationToken);

        yield return new StreamingTranscriptionUpdate
        {
            EventName = "TranscriptionComplete",
            RawRepresentation = transcriptionCompletion.RawRepresentation,
            Transcription = transcriptionCompletion.Content?.Transcription,
            StartTime = transcriptionCompletion.StartTime,
            EndTime = transcriptionCompletion.EndTime
        };
    }
}
