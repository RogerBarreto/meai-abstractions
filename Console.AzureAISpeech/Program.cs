using Microsoft.Extensions.Configuration;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ConsoleUtilities;

internal sealed class Program
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

        // await AzureAI_ITranscriptionClient_FileNonStreaming();
        //await AzureAI_ITranscriptionClient_FileNonStreamingExtension();

        //await AzureAI_ITranscriptionClient_FileStreaming();
        await AzureAI_ITranscriptionClient_FileStreamingExtension();

        // await AzureAI_ITranscriptionClient_MicrophoneStreaming();

        // await AzureAI_MicrophoneManual();
    }

    private static async Task AzureAI_ITranscriptionClient_FileNonStreaming()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);

        var audioContents = ConsoleUtils.UploadAudioFileAsync("Resources/ian.wav", "audio/wav");

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(audioContents, new(), CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Content!.Transcription}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task AzureAI_ITranscriptionClient_FileNonStreamingExtension()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);

        using var fileStream = File.OpenRead("Resources/ian.wav");

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(fileStream, new(), CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Content!.Transcription}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task AzureAI_MicrophoneManual()
    {
        var speechConfig = SpeechConfig.FromSubscription(s_subscriptionKey, s_region);

        using var audioConfigStream = AudioInputStream.CreatePushStream();

        var audioEnumerable = UploadMicrophoneAudio(new TranscriptionOptions
        {
            SourceSampleRate = 16_000
        });

        using var audioConfig = AudioConfig.FromStreamInput(audioConfigStream);
        using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

        Stopwatch stopwatch = new();
        stopwatch.Start();
        
        Console.WriteLine("Speak into your microphone.");

        await foreach (var audioContent in audioEnumerable)
        {
            audioConfigStream.Write(audioContent.Data!.Value.ToArray());

            if (stopwatch.ElapsedMilliseconds > 5000)
            {
                audioConfigStream.Write([], 0);
                break;
            }
        }

        Console.WriteLine("Transcription Started");
        var speechRecognitionResult = await speechRecognizer.RecognizeOnceAsync();
        Console.WriteLine($"RECOGNIZED: Text={speechRecognitionResult.Text}");

        Console.WriteLine("Transcription Complete");
    }

    private static async IAsyncEnumerable<AudioContent> UploadMicrophoneAudio(TranscriptionOptions options)
    {
        using var soxProcess = ConsoleUtils.GetMicrophoneStreamProcess(options, out var cancellationToken);

        soxProcess.Start();
        using var soxOutputStream = soxProcess.StandardOutput.BaseStream;

        await foreach (var update in ConsoleUtils.UploadStreamAsync(soxOutputStream, "audio/wav"))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            yield return update;
        }

        soxProcess.Kill();
    }

    private static async Task AzureAI_ITranscriptionClient_MicrophoneStreaming()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);
        var options = new TranscriptionOptions
        {
            SourceSampleRate = 16_000
        };

        var audioContents = UploadMicrophoneAudio(options);

        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(audioContents, options, CancellationToken.None))
        {
            HandleAsyncUpdates(update);
        }
        Console.WriteLine("Transcription Complete");
    }

    private static async Task AzureAI_ITranscriptionClient_FileStreaming()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);
        var fileOptions = new TranscriptionOptions
        {
            SourceSampleRate = 16_000
        };

        var audioContents = ConsoleUtils.UploadAudioFileAsync("Resources/ian.wav", "audio/wav");

        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(audioContents, fileOptions, CancellationToken.None))
        {
            HandleAsyncUpdates(update);
        }
        Console.WriteLine("Transcription Complete");
    }

    private static async Task AzureAI_ITranscriptionClient_FileStreamingExtension()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);
        var fileOptions = new TranscriptionOptions
        {
            SourceSampleRate = 16_000
        };

        using var fileStream = File.OpenRead("Resources/ian.wav");
        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(fileStream, fileOptions, CancellationToken.None))
        {
            HandleAsyncUpdates(update);
        }
        Console.WriteLine("Transcription Complete");
    }

    private static void HandleAsyncUpdates(StreamingTranscriptionUpdate update)
    {
        switch (update.EventName)
        {
            case "Recognizing":
                Console.WriteLine($"Recognizing: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
                break;
            case "Recognized":
                Console.WriteLine($"Recognized: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
                break;
            case "NoMatch":
                Console.WriteLine($"NoMatch: [{update.StartTime} --> {update.EndTime}] : {update.Message} ");
                break;
            case "Canceled":
                Console.WriteLine($"Canceled: [{update.StartTime} --> {update.EndTime}] : {update.Message} ");
                break;
            case "SessionStopped":
                Console.WriteLine($"SessionStopped: {update.Message} ");
                break;
        }
    }
}
