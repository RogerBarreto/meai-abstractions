using Microsoft.Extensions.Configuration;
using ConsoleAssemblyAI;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
        var audioContents = UploadAudioFile("path_to_audio_file.wav");

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

        var audioContents = UploadMicrophoneAudio(options);

        await foreach (var update in client.TranscribeStreamingAsync(audioContents, options, CancellationToken.None))
        {
            switch (update.EventName)
            {
                case "TranscriptionComplete":
                    Console.WriteLine($"TranscriptionComplete: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
                    break;
            }
        }
    }

    private static async Task OpenAI_ITranscriptionClient_FileStreaming()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var fileOptions = new TranscriptionOptions
        {
            SourceSampleRate = 16_000
        };

        var audioContents = UploadAudioFile("path_to_audio_file.wav");

        await foreach (var update in client.TranscribeStreamingAsync(audioContents, fileOptions, CancellationToken.None))
        {
            switch (update.EventName)
            {
                case "TranscriptionComplete":
                    Console.WriteLine($"TranscriptionComplete: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
                    break;
            }
        }
    }

    private static async IAsyncEnumerable<AudioContent> UploadAudioFile(string filePath)
    {
        using var fileStream = File.OpenRead(filePath);
        await foreach (var update in new AsyncEnumerableAudioStream(fileStream))
        {
            yield return update;
        }
    }

    private static async IAsyncEnumerable<AudioContent> UploadMicrophoneAudio(TranscriptionOptions options)
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

        soxProcess.Start();
        var soxOutputStream = soxProcess.StandardOutput.BaseStream;

        await foreach (var update in new AsyncEnumerableAudioStream(soxOutputStream))
        {
            yield return update;
        }

        soxProcess.Kill();
    }
}
