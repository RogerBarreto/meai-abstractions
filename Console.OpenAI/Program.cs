using Microsoft.Extensions.Configuration;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ConsoleOpenAI;

internal sealed class Program
{
    private static string s_apiKey = String.Empty;

    private static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        s_apiKey = config["OpenAI:ApiKey"]!;

        await OpenAI_ITranscriptionClient_FileNonStreaming();
        // await OpenAI_ITranscriptionClient_MicrophoneStreaming();
        // await OpenAI_ITranscriptionClient_FileStreaming();
    }

    private static async Task OpenAI_ITranscriptionClient_FileNonStreaming()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var audioContents = UploadAudioFile("ian.wav");

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(audioContents, new(), CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Content!.Transcription}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task OpenAI_ITranscriptionClient_MicrophoneStreaming()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var options = new TranscriptionOptions
        {
            SourceSampleRate = 16_000
        };

        // Upload microphone audio for 5 seconds
        var audioContents = UploadMicrophoneAudio(options, TimeSpan.FromSeconds(5));

        await foreach (var update in client.TranscribeStreamingAsync(audioContents, options, CancellationToken.None))
        {
            Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
        }
    }

    private static async Task OpenAI_ITranscriptionClient_FileStreaming()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var fileOptions = new TranscriptionOptions
        {
            SourceSampleRate = 16_000
        };

        var audioContents = UploadAudioFile("ian.wav");

        await foreach (var update in client.TranscribeStreamingAsync(audioContents, fileOptions, CancellationToken.None))
        {
            Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
        }
    }

    private static async IAsyncEnumerable<AudioContent> UploadAudioFile(string filePath)
    {
        using var fileStream = File.OpenRead(filePath);
        await foreach (var update in new AsyncEnumerableAudioStream(fileStream, "audio/wav"))
        {
            yield return update;
        }
    }

    private static async IAsyncEnumerable<AudioContent> UploadMicrophoneAudio(TranscriptionOptions options, TimeSpan duration)
    {
        var cts = new CancellationTokenSource();
        var ct = cts.Token;

        Console.CancelKeyPress += (sender, e) => cts.Cancel();

        var soxArguments = string.Join(' ', [
           // --default-device doesn't work on Windows
           RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-t waveaudio default" : "--default-device",
            "--no-show-progress",
            $"--rate {options.SourceSampleRate}",
            "--channels 1",
            "--encoding signed-integer",
            "--bits 16",
            "--type wav",
            "-" // pipe
       ]);

        using var soxProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sox",
                Arguments = soxArguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var stopwatch = Stopwatch.StartNew();
        soxProcess.Start();
        var soxOutputStream = soxProcess.StandardOutput.BaseStream;

        await foreach (var update in new AsyncEnumerableAudioStream(soxOutputStream, "audio/wav"))
        {
            yield return update;
            if (stopwatch.Elapsed > duration)
            {
                break;
            }
        }

        soxProcess.Kill();
    }
}
