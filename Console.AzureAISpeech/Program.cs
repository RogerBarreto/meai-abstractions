using Microsoft.Extensions.Configuration;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using ConsoleAssemblyAI;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

        await AzureAI_ITranscriptionClient_NonStreaming();
        // await AzureAI_ITranscriptionClient_MicrophoneStreaming();
    }

    private static async Task AzureAI_ITranscriptionClient_NonStreaming()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);
        var audioContents = UpdateAudioFile("ian.wav");

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(audioContents, new(), CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Content!.Transcription}");
        Console.WriteLine("Transcription Complete.");
    }
    private static async IAsyncEnumerable<AudioContent> UpdateAudioFile(string filePath)
    {
        using var fileStream = File.OpenRead(filePath);
        await foreach (var update in new AsyncEnumerableAudioStream(fileStream))
        {
            yield return update;
        }
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

        var speechRecognitionResult = await speechRecognizer.RecognizeOnceAsync();
        Console.WriteLine($"RECOGNIZED: Text={speechRecognitionResult.Text}");
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
        using var soxOutputStream = soxProcess.StandardOutput.BaseStream;

        await foreach (var update in new AsyncEnumerableAudioStream(soxOutputStream))
        {
            if (ct.IsCancellationRequested)
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

        await foreach (var update in client.TranscribeStreamingAsync(audioContents, options, CancellationToken.None))
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
                    Console.WriteLine($"NoMatch: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
                    break;
                case "Canceled":
                    Console.WriteLine($"Canceled: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
                    break;
                case "SessionStopped":
                    Console.WriteLine($"SessionStopped: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
                    break;
            }
        }
    }

    private static async Task AzureAI_ITranscriptionClient_FileStreaming()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);
        var fileOptions = new TranscriptionOptions
        {
            SourceSampleRate = 48_000,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "DisablePartialTranscripts", false }
            }
        };

        var audioContents = UpdateAudioFile("PathToFile.wav");

        await foreach (var update in client.TranscribeStreamingAsync(audioContents, fileOptions, CancellationToken.None))
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
                    Console.WriteLine($"NoMatch: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
                    break;
                case "Canceled":
                    Console.WriteLine($"Canceled: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
                    break;
                case "SessionStopped":
                    Console.WriteLine($"SessionStopped: {update.Transcription} ");
                    break;
            }
        }
    }

    private static Task AzureAI_Streaming()
    {
        return Task.CompletedTask;
    }

    private static Task AzureAI_NonStreaming()
    {
        return Task.CompletedTask;
    }
}
