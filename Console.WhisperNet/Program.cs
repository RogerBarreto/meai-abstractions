using ConsoleUtilities;
using ConsoleWhisperNet;
using MEAI.Abstractions;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

internal partial class Program
{
    static async Task Main(string[] args)
    {
        var ggmlType = GgmlType.LargeV3;
        var modelFileName = "ggml-large-v3.bin";

        using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Warning);

        // This section detects whether the "ggml-base.bin" file exists in our project disk. If it doesn't, it downloads it from the internet
        if (!File.Exists(modelFileName))
        {
            await DownloadModel(modelFileName, ggmlType);
        }

        RuntimeOptions.RuntimeLibraryOrder = [
            RuntimeLibrary.Cuda
        ];

        // await Whisper_ITranscriptionClient_MicrophoneStreamingExtension();

        // await Whisper_ITranscriptionClient_FileNonStreamingExtension();

        await Whisper_ITranscriptionClient_FileStreamingExtension();
    }

    private static async Task Whisper_ITranscriptionClient_FileNonStreamingExtension()
    {
        var modelFile = "ggml-large-v3.bin";
        using var client = new WhisperTranscriptionClient(modelFile);

        using var fileStream = File.OpenRead("Resources/ian.wav");

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(fileStream, new(), CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Text}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task Whisper_ITranscriptionClient_FileStreamingExtension()
    {
        var modelFile = "ggml-large-v3.bin";
        using var client = new WhisperTranscriptionClient(modelFile);

        using var fileStream = File.OpenRead("Resources/barbara.wav");

        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(fileStream, new(), CancellationToken.None))
        {
            Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime} : {update.Text} ");
        }
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task Whisper_ITranscriptionClient_MicrophoneStreamingExtension()
    {
        var modelFile = "ggml-large-v3.bin";
        using var client = new WhisperTranscriptionClient(modelFile);

        Console.WriteLine("Transcription Started");

        // Whisper API doesn't support real-time, to achieve real-time behavior we need to record audio
        // saving it in memory of X seconds and then send them to the API for transcription.
        await foreach (var fileStream in ConsoleUtils.UploadMicrophoneAudioStreamAsync(new AudioTranscriptionOptions { AudioSampleRate = 16_000 }, TimeSpan.FromSeconds(5)))
        {
            await foreach (var update in client.TranscribeStreamingAsync(fileStream, new(), CancellationToken.None))
            {
                Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime}] : {update.Text} ");
            }
        }

        Console.WriteLine("Transcription Complete.");
    }
}
