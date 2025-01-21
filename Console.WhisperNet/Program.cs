using ConsoleWhisperNet;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

internal class Program
{
    static async Task Main(string[] args)
    {
        var ggmlType = GgmlType.LargeV3;
        var modelFileName = "ggml-large-v3.bin";

        using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);

        // This section detects whether the "ggml-base.bin" file exists in our project disk. If it doesn't, it downloads it from the internet
        if (!File.Exists(modelFileName))
        {
            await DownloadModel(modelFileName, ggmlType);
        }

        RuntimeOptions.RuntimeLibraryOrder = [
            RuntimeLibrary.Cuda
        ];

        // await Whisper_ITranscriptionClient_FileNonStreaming();

        // await Whisper_ITranscriptionClient_FileStreaming();
        // await Whisper_ITranscriptionClient_MicrophoneStreaming();

        await Whisper_Manual(modelFileName);
    }

    private static async Task Whisper_ITranscriptionClient_FileStreaming()
    {
        var modelFile = "ggml-large-v3.bin";
        using var client = new WhisperTranscriptionClient(modelFile);

        var audioContents = UploadAudioFile("barbara.wav");

        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(audioContents, new(), CancellationToken.None))
        {
            Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime} : {update.Transcription} ");
        }
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task Whisper_ITranscriptionClient_FileNonStreaming()
    {
        var modelFile = "ggml-large-v3.bin";
        using var client = new WhisperTranscriptionClient(modelFile);

        var audioContents = UploadAudioFile("barbara.wav");

        Console.WriteLine("Transcription Started");
        var completion = await client.TranscribeAsync(audioContents, new(), CancellationToken.None);
        Console.WriteLine($"Transcription: [{completion.StartTime} --> {completion.EndTime}] : {completion.Content!.Transcription}");
        Console.WriteLine("Transcription Complete.");
    }

    private static async Task Whisper_ITranscriptionClient_MicrophoneStreaming()
    {
        var modelFile = "ggml-large-v3.bin";
        using var client = new WhisperTranscriptionClient(modelFile);

        Console.WriteLine("Transcription Started");
        
        // Whisper API doesn't support real-time, to achieve real-time behavior we need to record audio
        // saving it in memory of X seconds and then send them to the API for transcription.
        await foreach (var audioContents in UploadMicrophoneAudio(new TranscriptionOptions { SourceSampleRate = 16_000 }))
        {
            await foreach (var update in client.TranscribeStreamingAsync(audioContents, new(), CancellationToken.None))
            {
                Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
            }
        }

        Console.WriteLine("Transcription Complete.");
    }

    private static async IAsyncEnumerable<AudioContent> UploadAudioFile(string filePath)
    {
        using var fileStream = File.OpenRead(filePath);

        await foreach (var update in new AsyncEnumerableAudioStream(fileStream))
        {
            yield return update;
        }
    }

    private static async IAsyncEnumerable<IAsyncEnumerable<AudioContent>> UploadMicrophoneAudio(TranscriptionOptions options)
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
            "output.wav",
            "trim 0 5"
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

        Queue<byte[]> intervalStreams = [];

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                soxProcess.Start();
                await soxProcess.WaitForExitAsync();

                intervalStreams.Enqueue(File.ReadAllBytes("output.wav"));

                File.Delete("output.wav");
            }
        });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        while (!ct.IsCancellationRequested)
        {
            if (intervalStreams.Count > 0)
            {
                yield return UploadMicrophoneAudioChunk(intervalStreams.Dequeue());
            }
            else
            {
                await Task.Delay(100);
            }
        }
    }

    static async IAsyncEnumerable<AudioContent> UploadMicrophoneAudioChunk(byte[] audioData)
    {
        var buffer = new byte[4096];
        var bytesRead = 0;
        using var audioStream = new MemoryStream(audioData);
        while ((bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            yield return new AudioContent(buffer);
        }
    }

    private static async Task DownloadModel(string fileName, GgmlType ggmlType)
    {
        Console.WriteLine($"Downloading Model {fileName}");
        using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType);
        using var fileWriter = File.OpenWrite(fileName);
        await modelStream.CopyToAsync(fileWriter);
    }

    private static async Task Whisper_Manual(string modelFileName)
    {
        Console.WriteLine("Hello, World!");
        var wavFilename = "fernanda.wav";

        // This section creates the whisperFactory object which is used to create the processor object.
        using var whisperFactory = WhisperFactory.FromPath(modelFileName);

        // This section creates the processor object which is used to process the audio file, it uses language `auto` to detect the language of the audio file.
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage("auto")
            .Build();

        // This section creates the whisperFactory object which is used to create the processor object.
        using var fileStream = File.OpenRead(wavFilename);

        // This section processes the audio file and prints the results (start time, end time and text) to the console.
        await foreach (var result in processor.ProcessAsync(fileStream))
        {
            Console.WriteLine($"{result.Start}->{result.End}: {result.Text}");
        }
    }
}
