using Microsoft.Extensions.Configuration;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ConsoleOpenAI;

internal sealed partial class Program
{
    private static async Task OpenAI_ITranscriptionClient_MicrophoneStreaming()
    {
        using var client = new OpenAITranscriptionClient(s_apiKey);
        var options = new TranscriptionOptions
        {
            SourceSampleRate = 16_000
        };

        // Upload microphone audio for 5 seconds
        var audioContents = UploadMicrophoneAudio(options, TimeSpan.FromSeconds(5));

        await foreach (var update in client.TranscribeStreamingAsync(audioContents, options, CancellationToken.None))
        {
            Console.WriteLine($"Update: [{update.StartTime} --> {update.EndTime}] : {update.Transcription} ");
        }
    }

    private static async IAsyncEnumerable<AudioContent> UploadMicrophoneAudio(TranscriptionOptions options, TimeSpan duration)
    {
        using var soxProcess = GetMicrophoneStreamProcess(options);

        var stopwatch = Stopwatch.StartNew();
        soxProcess.Start();
        var soxOutputStream = soxProcess.StandardOutput.BaseStream;

        await foreach (var update in new AsyncEnumerableAudioStream(soxOutputStream, "audio/wav"))
        {
            yield return update;

            if (stopwatch.Elapsed > duration)
            {
                break;
            }
        }

        soxProcess.Kill();
    }
}
