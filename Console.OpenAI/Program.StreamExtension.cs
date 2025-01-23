using ConsoleUtilities;
using MEAI.Abstractions;
using System.Diagnostics;

namespace ConsoleOpenAI;

internal sealed partial class Program
{
    private static async Task OpenAI_ITranscriptionClient_MicrophoneStreamingExtension()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var options = new AudioTranscriptionOptions
        {
            SourceSampleRate = 16_000,
            SourceFileName = "microphone.wav"
        };

        // Upload microphone audio for 5 seconds
        using var soxProcess = ConsoleUtils.GetMicrophoneStreamProcess(options, out var cancellationToken);
        using MemoryStream memoryStream = new();

        var stopWatch = Stopwatch.StartNew();
        soxProcess.Start();
        Console.WriteLine("Recording Started");

        var secondsRecording = 5;

        _ = Task.Run(async () =>
        {
            while (true)
            {
                // Attempting to pass the BaseStream and killing the process after 5 sec corrupts the stream,
                // so we need to copy the stream to a MemoryStream

                await soxProcess.StandardOutput.BaseStream.CopyToAsync(memoryStream);
                if (cancellationToken.IsCancellationRequested || stopWatch.Elapsed > TimeSpan.FromSeconds(secondsRecording))
                {
                    soxProcess.Kill();
                }
            }
        });

        await Task.Delay(TimeSpan.FromSeconds(secondsRecording));

        Console.WriteLine("Recording Ended");

        Console.WriteLine("Transcription Started");

        await foreach (var update in client.TranscribeStreamingAsync(memoryStream, options, CancellationToken.None))
        {
            Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime}] : {update.Text} ");
        }

        Console.WriteLine("Transcription Complete");

        soxProcess.Kill();
    }
}
