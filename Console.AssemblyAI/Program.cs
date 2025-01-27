using AssemblyAI.Transcripts;
using ConsoleAssemblyAI;
using ConsoleUtilities;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace ConsoleOpenAI;

internal sealed partial class Program
{
    private static string s_apiKey = String.Empty;
    private static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        s_apiKey = config["AssemblyAI:ApiKey"]!;

        await AssemblyAI_ITranscriptionClient_MicrophoneStreamingExtension();

        // await AssemblyAI_ITranscriptionClient_FileStreamingExtension();

        // await AssemblyAI_ITranscriptionClient_NonStreamingExtension();
    }

    private static async Task AssemblyAI_ITranscriptionClient_MicrophoneStreamingExtension()
    {
        using var client = new AssemblyAITranscriptionClient(s_apiKey);
        var options = new AudioTranscriptionOptions
        {
            AudioSampleRate = 16_000,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "DisablePartialTranscripts", false }
            }
        };

        using var soxProcess = ConsoleUtils.GetMicrophoneStreamProcess(options, out var cancellationToken);

        soxProcess.Start();
        var soxOutputStream = soxProcess.StandardOutput.BaseStream;

        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(soxOutputStream, options, cancellationToken))
        {
            HandleAsyncUpdates(update);
        }
        Console.WriteLine("Transcription Complete");

        soxProcess.Kill();
    }

    private static async Task AssemblyAI_ITranscriptionClient_FileStreamingExtension()
    {
        using var client = new AssemblyAITranscriptionClient(s_apiKey);
        var fileOptions = new AudioTranscriptionOptions
        {
            AudioSampleRate = 16_000,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "DisablePartialTranscripts", false }
            }
        };

        using var fileStream = File.OpenRead("Resources/ian.wav"); // Assembly AI RealTime transcription only supports English and wave format as input
        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(fileStream, fileOptions, CancellationToken.None))
        {
            HandleAsyncUpdates(update);
        }
        Console.WriteLine("Transcription Complete");
    }

    private static async Task AssemblyAI_ITranscriptionClient_NonStreamingExtension()
    {
        using var client = new AssemblyAITranscriptionClient(s_apiKey);
        using var fileStream = File.OpenRead("Resources/barbara.ogg");

        Console.WriteLine("Transcription Started");
        var result = await client.TranscribeAsync(fileStream, new()
        {
            AudioLanguage = nameof(TranscriptLanguageCode.Pt),
        }, CancellationToken.None);

        Console.WriteLine($"Transcription: {result?.Text}");
        Console.WriteLine("Transcription Complete");
    }

    private static void HandleAsyncUpdates(StreamingAudioTranscriptionUpdate update)
    {
        if (update.Kind == AudioTranscriptionUpdateKind.Transcribing)
        {
            Console.WriteLine($"Transcribing: [{update.StartTime} --> {update.EndTime}] : {update.Text} ");
        }
        else if (update.Kind == AudioTranscriptionUpdateKind.Transcribed)
        {
            Console.WriteLine($"Transcribed: [{update.StartTime} --> {update.EndTime}] : {update.Text} ");
        }
        else if (update.Kind == AudioTranscriptionUpdateKind.SessionOpen)
        {
            Console.WriteLine($"SessionOpen: {update.Text}");
        }
        else if (update.Kind == AudioTranscriptionUpdateKind.SessionClose)
        {
            Console.WriteLine($"SessionClose: {update.Text}");
        }
        else if (update.Kind == AudioTranscriptionUpdateKind.Error)
        {
            Console.WriteLine($"Error: {update.Text}");
        }
    }
}