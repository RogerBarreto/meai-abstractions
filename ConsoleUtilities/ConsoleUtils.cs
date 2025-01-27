using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ConsoleUtilities;

public class ConsoleUtils
{
    public static Process GetMicrophoneStreamProcess(AudioTranscriptionOptions options, out CancellationToken ct)
    {
        var cts = new CancellationTokenSource();
        ct = cts.Token;

        Console.CancelKeyPress += (sender, e) =>
        {
            cts.Cancel();
            e.Cancel = true;
        };

        var soxArguments = string.Join(' ', [
           // --default-device doesn't work on Windows
           RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-t waveaudio default" : "--default-device",
            "--no-show-progress",
            $"--rate {options.AudioSampleRate}",
            "--channels 1",
            "--encoding signed-integer",
            "--bits 16",
            "--type wav",
            "-" // pipe
       ]);

        var soxProcess = new Process
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

        return soxProcess;
    }

    public static async IAsyncEnumerable<AudioContent> UploadAudioFileAsync(string filePath, string mediaType)
    {
        using var fileStream = File.OpenRead(filePath);
        await foreach (var update in UploadStreamAsync(fileStream, mediaType))
        {
            yield return update;
        }
    }

    public static async IAsyncEnumerable<AudioContent> UploadStreamAsync(Stream stream, string mediaType)
    {
        var buffer = new byte[4096];
        while ((await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            yield return new AudioContent(buffer, mediaType);
        }
    }

    public static async IAsyncEnumerable<Stream> UploadMicrophoneAudioStreamAsync(AudioTranscriptionOptions options, TimeSpan interval)
    {
        interval = interval.TotalSeconds < 1 ? TimeSpan.FromSeconds(1) : interval;

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        Console.CancelKeyPress += (sender, e) => 
        {
            cts.Cancel();
            e.Cancel = true;
        };

        var soxArguments = string.Join(' ', [
           // --default-device doesn't work on Windows
           RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-t waveaudio default" : "--default-device",
            "--no-show-progress",
            $"--rate {options.AudioSampleRate}",
            "--channels 1",
            "--encoding signed-integer",
            "--bits 16",
            "--type wav",
            "output.wav",
            $"trim 0 {Math.Ceiling(interval.TotalSeconds)}" // Record and save to a file for every 5 seconds
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
            while (!cancellationToken.IsCancellationRequested)
            {
                // If the file exists, the process don't overwrite it need to ensure it is deleted
                if (File.Exists("output.wav"))
                {
                    File.Delete("output.wav");
                }

                soxProcess.Start();
                await soxProcess.WaitForExitAsync();

                intervalStreams.Enqueue(File.ReadAllBytes("output.wav"));

                File.Delete("output.wav");
            }
        });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        Console.WriteLine("Microphone recording started");
        while (!cancellationToken.IsCancellationRequested)
        {
            if (intervalStreams.Count > 0)
            {
                yield return new MemoryStream(intervalStreams.Dequeue());
            }
            else
            {
                await Task.Delay(100);
            }
        }

        if (soxProcess?.HasExited == false)
        {
            soxProcess.Kill();
        }

        Console.WriteLine("Microphone recording completed");
    }
}
