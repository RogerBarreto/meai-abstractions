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

    public void Dispose()
    {
    }

    public async Task<TranscriptionCompletion> TranscribeAsync(IAsyncEnumerable<AudioContent> audioContent, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
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
                ToFileName(firstChunk.MediaType), 
                ToOpenAIOptions(options), cancellationToken);
        }
        stopwatch.Stop();

        return new TranscriptionCompletion
        {
            RawRepresentation = transcriptionResult,
            Content = new TranscribedContent(transcriptionResult.Text),
            StartTime = TimeSpan.Zero,
            EndTime = stopwatch.Elapsed
        };
    }

    public async IAsyncEnumerable<StreamingTranscriptionUpdate> TranscribeStreamingAsync(
        IAsyncEnumerable<AudioContent> audioContent, 
        TranscriptionOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var transcriptionCompletion = await this.TranscribeAsync(audioContent, options, cancellationToken);

        yield return new StreamingTranscriptionUpdate
        {
            EventName = "TranscriptionComplete",
            RawRepresentation = transcriptionCompletion.RawRepresentation,
            Transcription = transcriptionCompletion.Content?.Transcription,
            StartTime = transcriptionCompletion.StartTime,
            EndTime = transcriptionCompletion.EndTime
        };
    }

    private static AudioTranscriptionOptions ToOpenAIOptions(TranscriptionOptions? options) 
        => new(); // To be implemented

    // flac, mp3, mp4, mpeg, mpga, m4a, ogg, wav, or webm.
    private static string ToFileName(string? mediaType)
         => mediaType switch
         {
             "audio/flac" => "file.flac",
             "audio/mp3" => "file.mp3",
             "audio/mp4" => "file.mp4",
             "audio/mpeg" => "file.mpeg",
             "audio/mpga" => "file.mpga",
             "audio/m4a" => "file.m4a",
             "audio/ogg" => "file.ogg",
             "audio/wav" or "audio/wave" => "file.wav",
             "audio/webm" => "file.webm",
             _ => throw new NotSupportedException($"Media type {mediaType} is not supported.")

         };

}
