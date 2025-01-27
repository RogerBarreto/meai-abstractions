using Microsoft.Extensions.Configuration;
using MEAI.Abstractions;
using ConsoleUtilities;

internal sealed partial class Program
{
    private static string s_subscriptionKey = String.Empty;
    private static string s_region = String.Empty;

    private static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        s_subscriptionKey = config["AzureAISpeech:SubscriptionKey"]!;
        s_region = config["AzureAISpeech:Region"]!;

        //await AzureAI_ITranscriptionClient_FileNonStreamingExtension();

        //await AzureAI_ITranscriptionClient_FileStreamingExtension();

        await AzureAI_ITranscriptionClient_MicrophoneStreamingExtensions();
    }

    private static async Task AzureAI_ITranscriptionClient_FileNonStreamingExtension()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);

        using var fileStream = File.OpenRead("Resources/ian.wav");

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(fileStream, new(), CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Text}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task AzureAI_ITranscriptionClient_FileStreamingExtension()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);
        var fileOptions = new AudioTranscriptionOptions
        {
            AudioSampleRate = 16_000
        };

        using var fileStream = File.OpenRead("Resources/ian.wav");
        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(fileStream, fileOptions, CancellationToken.None))
        {
            HandleAsyncUpdates(update);
        }
        Console.WriteLine("Transcription Complete");
    }

    private static async Task AzureAI_ITranscriptionClient_MicrophoneStreamingExtensions()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);
        var options = new AudioTranscriptionOptions
        {
            AudioSampleRate = 16_000
        };

        using var soxProcess = ConsoleUtils.GetMicrophoneStreamProcess(options, out var cancellationToken);
        soxProcess.Start();

        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(soxProcess.StandardOutput.BaseStream, options, cancellationToken))
        {
            HandleAsyncUpdates(update);
        }
        Console.WriteLine("Transcription Complete");

        soxProcess.Kill();
    }

    private static void HandleAsyncUpdates(StreamingAudioTranscriptionUpdate update)
    {
        if (update.Kind == AudioTranscriptionUpdateKind.Transcribed)
        {
            Console.WriteLine($"Transcribed: [{update.StartTime} --> {update.EndTime}] : {update.Text} ");
        }
        else if (update.Kind == AudioTranscriptionUpdateKind.Transcribing)
        {
            Console.WriteLine($"Transcribing: [{update.StartTime} --> {update.EndTime}] : {update.Text} ");
        }
        else if (update.Kind == AudioTranscriptionUpdateKind.Error)
        {
            Console.WriteLine($"Error: [{update.StartTime} --> {update.EndTime}] : {update.Text} ");
        }
        else if (update.Kind == AudioTranscriptionUpdateKind.SessionClose)
        {
            Console.WriteLine($"SessionClose: {update.Text} ");
        }
    }
}
