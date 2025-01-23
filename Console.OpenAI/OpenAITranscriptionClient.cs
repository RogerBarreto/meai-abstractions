using OpenAI.Audio;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace ConsoleOpenAI;

public class OpenAITranscriptionClient : IAudioTranscriptionClient
{
    private AudioClient _client;

    public OpenAITranscriptionClient(string apiKey, string? modelName = "whisper-1")
    {
        this._client = new AudioClient(modelName, apiKey);
    }

    public async Task<AudioTranscriptionCompletion> TranscribeAsync(IAsyncEnumerable<AudioContent> audioContent, MEAI.Abstractions.AudioTranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var enumerator = audioContent.GetAsyncEnumerator(cancellationToken);
        await enumerator.MoveNextAsync();

        var firstChunk = enumerator.Current;

        AudioTranscription transcriptionResult;
        Stopwatch stopwatch = new();
        if (!firstChunk.ContainsData)
        {
            // Check if the first chunk is a file path (file://)
            var uri = new Uri(firstChunk.Uri);
            if (uri.Scheme.ToLowerInvariant() != "file")
            {
                throw new NotSupportedException("Only file paths are supported.");
            }

            var filePath = uri.LocalPath;
            stopwatch.Start();
            transcriptionResult = await this._client.TranscribeAudioAsync(
                audioFilePath: filePath, 
                options: ToOpenAIOptions(options));
        }
        else
        {
            using var audioFileStream = new AudioContentAsyncEnumerableStream(audioContent, cancellationToken);

            stopwatch.Start();
            transcriptionResult = await this._client.TranscribeAudioAsync(
                audioFileStream, 
                "file.mp3", // this information internally is required but is only being used to create a header name in the multipart request.
                ToOpenAIOptions(options), cancellationToken);
        }
        stopwatch.Stop();

        return new AudioTranscriptionCompletion
        {
            RawRepresentation = transcriptionResult,
            Text = transcriptionResult.Text,
            StartTime = TimeSpan.Zero,
            EndTime = stopwatch.Elapsed
        };
    }

    public async IAsyncEnumerable<StreamingAudioTranscriptionUpdate> TranscribeStreamingAsync(
        IAsyncEnumerable<AudioContent> audioContent,
        MEAI.Abstractions.AudioTranscriptionOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var transcriptionCompletion = await this.TranscribeAsync(audioContent, options, cancellationToken);

        yield return new StreamingAudioTranscriptionUpdate
        {
            Kind = AudioTranscriptionUpdateKind.Transcribed,
            RawRepresentation = transcriptionCompletion.RawRepresentation,
            Text = transcriptionCompletion.Text,
            StartTime = transcriptionCompletion.StartTime,
            EndTime = transcriptionCompletion.EndTime
        };
    }

    public void Dispose()
    {
    }

    private static OpenAI.Audio.AudioTranscriptionOptions ToOpenAIOptions(MEAI.Abstractions.AudioTranscriptionOptions? options) 
        => new(); // To be implemented
}
