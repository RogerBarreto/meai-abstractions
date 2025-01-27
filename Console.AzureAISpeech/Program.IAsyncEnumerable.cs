using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using ConsoleUtilities;

internal sealed partial class Program
{
    private static async Task AzureAI_ITranscriptionClient_FileNonStreaming()
    {
        using var client = new AzureTranscriptionClient(s_subscriptionKey, s_region);

        var audioContents = ConsoleUtils.UploadAudioFileAsync("Resources/ian.wav", "audio/wav");

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(audioContents, new(), CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Text}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task AzureAI_MicrophoneManual()
    {
        var speechConfig = SpeechConfig.FromSubscription(s_subscriptionKey, s_region);

        using var audioConfigStream = AudioInputStream.CreatePushStream();

        var audioEnumerable = UploadMicrophoneAudio(new AudioTranscriptionOptions
        {
            AudioSampleRate = 16_000
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

    private static async IAsyncEnumerable<AudioContent> UploadMicrophoneAudio(AudioTranscriptionOptions options)
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
        var options = new AudioTranscriptionOptions
        {
            AudioSampleRate = 16_000
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
        var fileOptions = new AudioTranscriptionOptions
        {
            AudioSampleRate = 16_000
        };

        var audioContents = ConsoleUtils.UploadAudioFileAsync("Resources/ian.wav", "audio/wav");

        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(audioContents, fileOptions, CancellationToken.None))
        {
            HandleAsyncUpdates(update);
        }
        Console.WriteLine("Transcription Complete");
    }
}
