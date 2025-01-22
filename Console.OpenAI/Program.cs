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

        // await OpenAI_ITranscriptionClient_FileNonStreamingExtension();
        // await OpenAI_ITranscriptionClient_MicrophoneStreaming();

        // await OpenAI_ITranscriptionClient_FileStreaming();
        await OpenAI_ITranscriptionClient_MicrophoneStreamingExtension();
    }

    private static async Task OpenAI_ITranscriptionClient_FileNonStreaming()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var audioContents = ConsoleUtils.UploadAudioFileAsync("Resources/ian.wav", "audio/wav");

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(audioContents, new(), CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Content!.Transcription}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task OpenAI_ITranscriptionClient_FileNonStreamingExtension()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var fileName = "Resources/ian.wav";
        using var fileStream = File.OpenRead(fileName);

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(fileStream, new() 
        {  
            SourceFileName = fileName,
        }, CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Content!.Transcription}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task OpenAI_ITranscriptionClient_FileStreaming()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var fileOptions = new AudioTranscriptionOptions
        {
            SourceSampleRate = 16_000
        };

        var audioContents = ConsoleUtils.UploadAudioFileAsync("Resources/ian.wav", "audio/wav");

        await foreach (var update in client.TranscribeStreamingAsync(audioContents, fileOptions, CancellationToken.None))
        {
            Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
        }
    }
}
