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
            $"--rate {options.SourceSampleRate}",
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
}
