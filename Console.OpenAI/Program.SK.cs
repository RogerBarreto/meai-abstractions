using Microsoft.Extensions.Configuration;
using MEAI.Abstractions;
using ConsoleUtilities;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace ConsoleOpenAI;

internal sealed partial class Program
{
    private static async Task OpenAI_ITranscriptionClient_FileNonStreamingFromSKService()
    {
        var client = new OpenAIAudioToTextService(modelId: "whisper-1", apiKey: s_apiKey)
            .ToAudioTranscriptionClient();

        var fileName = "Resources/ian.wav";
        using var fileStream = File.OpenRead(fileName);

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(fileStream, new(), CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Text}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task OpenAI_ITranscriptionClient_FileNonStreamingToSKService()
    {
        var client = new OpenAITranscriptionClient(s_apiKey).ToAudioToTextService();

        var fileName = "Resources/ian.wav";
        var audioContent = new Microsoft.SemanticKernel.AudioContent(File.ReadAllBytes(fileName), "audio/wave");

        Console.WriteLine("Transcription Started");
        var textContent = (await client.GetTextContentsAsync(audioContent, new())).FirstOrDefault();
        Console.WriteLine($"Transcription: {textContent}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task OpenAI_ITranscriptionClient_MicrophoneStreamingSK()
    {
        var client = new OpenAIAudioToTextService(modelId: "whisper-1", apiKey: s_apiKey)
            .ToAudioTranscriptionClient();

        var options = new AudioTranscriptionOptions
        {
            AudioSampleRate = 16_000
        };

        Console.WriteLine("Transcription Started");
        // Upload microphone streams for each 5 seconds of recording
        await foreach (var recordedStream in ConsoleUtils.UploadMicrophoneAudioStreamAsync(options, TimeSpan.FromSeconds(5)))
        {
            await foreach (var update in client.TranscribeStreamingAsync(recordedStream, options, CancellationToken.None))
            {
                Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime}] : {update.Text} ");
            }
        }
        Console.WriteLine("Transcription Complete");
    }
}
