using Microsoft.Extensions.Configuration;
using MEAI.Abstractions;
using ConsoleUtilities;

namespace ConsoleOpenAI;

internal sealed partial class Program
{
    private static string s_apiKey = String.Empty;

    private static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        s_apiKey = config["OpenAI:ApiKey"]!;

        // await OpenAI_ITranscriptionClient_FileNonStreamingFromSKService();

        await OpenAI_ITranscriptionClient_FileNonStreamingToSKService();

        // await OpenAI_ITranscriptionClient_MicrophoneStreamingSK();
    }

    private static async Task OpenAI_ITranscriptionClient_FileNonStreamingExtension()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var fileName = "Resources/ian.wav";
        using var fileStream = File.OpenRead(fileName);

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(fileStream, new(), CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Text}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task OpenAI_ITranscriptionClient_MicrophoneStreamingExtension()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
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
