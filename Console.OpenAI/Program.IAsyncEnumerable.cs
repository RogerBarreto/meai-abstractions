using Microsoft.Extensions.Configuration;
using MEAI.Abstractions;
using ConsoleUtilities;
using Microsoft.Extensions.AI;
using System.Diagnostics;

namespace ConsoleOpenAI;

internal sealed partial class Program
{
    private static async Task OpenAI_ITranscriptionClient_FileStreaming()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var fileOptions = new AudioTranscriptionOptions
        {
            SourceSampleRate = 16_000
        };

        var audioContents = ConsoleUtils.UploadAudioFileAsync("Resources/ian.wav", "audio/wav");

        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(audioContents, fileOptions, CancellationToken.None))
        {
            Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime}] : {update.Text} ");
        }
        Console.WriteLine("Transcription Complete");
    }

    private static async Task OpenAI_ITranscriptionClient_MicrophoneStreaming()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var options = new AudioTranscriptionOptions
        {
            SourceSampleRate = 16_000
        };

        // Upload microphone audio for 5 seconds
        var audioContents = UploadMicrophoneAudio(options, TimeSpan.FromSeconds(5));

        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(audioContents, options, CancellationToken.None))
        {
            Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime}] : {update.Text} ");
        }
        Console.WriteLine("Transcription Complete");
    }

    private static async IAsyncEnumerable<AudioContent> UploadMicrophoneAudio(AudioTranscriptionOptions options, TimeSpan duration)
    {
        using var soxProcess = ConsoleUtils.GetMicrophoneStreamProcess(options, out var cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        soxProcess.Start();
        var soxOutputStream = soxProcess.StandardOutput.BaseStream;

        await foreach (var update in ConsoleUtils.UploadStreamAsync(soxOutputStream, "audio/wav"))
        {
            if (cancellationToken.IsCancellationRequested || stopwatch.Elapsed > duration)
            {
                break;
            }

            yield return update;
        }

        soxProcess.Kill();
    }
}
