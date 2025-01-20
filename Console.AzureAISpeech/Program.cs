using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using ConsoleAssemblyAI;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;

internal sealed class Program
{
    private static string s_subscriptionKey = String.Empty;
    private static string s_region = String.Empty;

    private static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        s_subscriptionKey = config["AzureAI:SubscriptionKey"]!;
        s_region = config["AzureAI:Region"]!;

        await AzureAI_ITranscriptionClient_MicrophoneStreaming();
        // await AzureAI_Streaming();
        // await AzureAI_NonStreaming();
        // await AzureAI_ITranscriptionClient_NonStreaming();
    }

    private static async Task AzureAI_ITranscriptionClient_MicrophoneStreaming()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);
        var options = new TranscriptionOptions
        {
            SourceSampleRate = 16_000,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "DisablePartialTranscripts", true }
            }
        };

        var audioContents = UploadMicrophoneAudio(options);

        await foreach (var update in client.TranscribeStreamingAsync(audioContents, options, CancellationToken.None))
        {
            switch (update.EventName)
            {
                case "Recognizing":
                    Console.WriteLine($"Recognizing: {update.Transcription} ");
                    break;
                case "Recognized":
                    Console.WriteLine($"Recognized: {update.Transcription} ");
                    break;
                case "NoMatch":
                    Console.WriteLine($"NoMatch: {update.Transcription} ");
                    break;
                case "Canceled":
                    Console.WriteLine($"Canceled: {update.Transcription} ");
                    break;
                case "SessionStopped":
                    Console.WriteLine($"SessionStopped: {update.Transcription} ");
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
                    Console.WriteLine($"Recognizing: {update.Transcription} ");
                    break;
                case "Recognized":
                    Console.WriteLine($"Recognized: {update.Transcription} ");
                    break;
                case "NoMatch":
                    Console.WriteLine($"NoMatch: {update.Transcription} ");
                    break;
                case "Canceled":
                    Console.WriteLine($"Canceled: {update.Transcription} ");
                    break;
                case "SessionStopped":
                    Console.WriteLine($"SessionStopped: {update.Transcription} ");
                    break;
            }
        }
    }

    private static async IAsyncEnumerable<AudioContent> UpdateAudioFile(string filePath)
    {
        using var fileStream = File.OpenRead(filePath);

        var buffer = new byte[4096];
        var bytesRead = 0;
        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            yield return new AudioContent(buffer);
        }
    }

    private static async IAsyncEnumerable<AudioContent> UploadMicrophoneAudio(TranscriptionOptions options)
    {
        var cts = new CancellationTokenSource();
        var ct = cts.Token;

        Console.CancelKeyPress += (sender, e) => cts.Cancel();

        var soxArguments = string.Join(' ', [
           RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-t waveaudio default" : "--default-device",
            "--no-show-progress",
            $"--rate {options.SourceSampleRate}",
            "--channels 1",
            "--encoding signed-integer",
            "--bits 16",
            "--type wav",
            "-" 
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

        var buffer = new byte[4096];
        var bytesRead = 0;
        while ((bytesRead = await soxOutputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            if (ct.IsCancellationRequested) break;
            yield return new AudioContent(buffer);
        }

        soxProcess.Kill();
    }

    private static async Task AzureAI_ITranscriptionClient_NonStreaming()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);
        var audioContent = new AudioContent(File.ReadAllBytes("PathToFile.wav"));

        var result = await client.TranscribeAsync(audioContent, new()
        {
            SourceLanguage = "en-US",
        }, CancellationToken.None);
    }

    private static async Task AzureAI_Streaming()
    {
        var cts = new CancellationTokenSource();
        var ct = cts.Token;

        Console.CancelKeyPress += (sender, e) => cts.Cancel();

        var sampleRate = 16_000U;
        var speechConfig = SpeechConfig.FromSubscription(s_subscriptionKey, s_region);
        speechConfig.SpeechRecognitionLanguage = "en-US";

        using var audioConfig = AudioConfig.FromStreamInput(new AudioContentAsyncEnumerableStream(UploadMicrophoneAudio(new TranscriptionOptions { SourceSampleRate = (int)sampleRate })));
        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        var stopRecognition = new TaskCompletionSource<int>();

        recognizer.Recognizing += (s, e) =>
        {
            Console.WriteLine($"Recognizing: {e.Result.Text}");
        };

        recognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Console.WriteLine($"Recognized: {e.Result.Text}");
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine($"NoMatch: Speech could not be recognized.");
            }
        };

        recognizer.Canceled += (s, e) =>
        {
            Console.WriteLine($"Canceled: Reason={e.Reason}");

            if (e.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"Canceled: ErrorCode={e.ErrorCode}");
                Console.WriteLine($"Canceled: ErrorDetails={e.ErrorDetails}");
                Console.WriteLine($"Canceled: Did you set the speech resource key and region values?");
            }

            stopRecognition.TrySetResult(0);
        };

        recognizer.SessionStopped += (s, e) =>
        {
            Console.WriteLine("\n    Session stopped event.");
            stopRecognition.TrySetResult(0);
        };

        await recognizer.StartContinuousRecognitionAsync();
        await Task.WhenAny(stopRecognition.Task);
        await recognizer.StopContinuousRecognitionAsync();
    }

    private static async Task AzureAI_NonStreaming()
    {
        var speechConfig = SpeechConfig.FromSubscription(s_subscriptionKey, s_region);
        speechConfig.SpeechRecognitionLanguage = "en-US";

        using var audioConfig = AudioConfig.FromWavFileInput("PathToFile.wav");
        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        var result = await recognizer.RecognizeOnceAsync();

        Console.WriteLine(result.Text);
    }
}
