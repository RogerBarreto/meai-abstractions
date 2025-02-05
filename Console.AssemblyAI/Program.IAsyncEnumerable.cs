﻿
using AssemblyAI;
using AssemblyAI.Realtime;
using AssemblyAI.Transcripts;
using ConsoleAssemblyAI;
using ConsoleUtilities;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ConsoleOpenAI;

internal sealed partial class Program
{
    private static async Task AssemblyAI_ITranscriptionClient_MicrophoneStreaming()
    {
        using var client = new AssemblyAITranscriptionClient(s_apiKey);
        var options = new AudioTranscriptionOptions
        {
            AudioSampleRate = 16_000,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "DisablePartialTranscripts", false }
            }
        };

        var audioContents = UploadMicrophoneAudio(options);

        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(audioContents, options, CancellationToken.None))
        {
            HandleAsyncUpdates(update);
        }
        Console.WriteLine("Transcription Complete");
    }

    private static async Task AssemblyAI_ITranscriptionClient_FileStreaming()
    {
        using var client = new AssemblyAITranscriptionClient(s_apiKey);
        var fileOptions = new AudioTranscriptionOptions
        {
            AudioSampleRate = 48_000,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "DisablePartialTranscripts", false }
            }
        };

        var audioContents = ConsoleUtils.UploadAudioFileAsync("Resources/barbara.wav", "audio/wav");

        Console.WriteLine("Transcription Started");
        await foreach (var update in client.TranscribeStreamingAsync(audioContents, fileOptions, CancellationToken.None))
        {
            HandleAsyncUpdates(update);
        }
        Console.WriteLine("Transcription Complete");
    }

    private static async IAsyncEnumerable<AudioContent> UploadMicrophoneAudio(AudioTranscriptionOptions options)
    {
        using var soxProcess = ConsoleUtils.GetMicrophoneStreamProcess(options, out var cancellationToken);

        soxProcess.Start();
        var soxOutputStream = soxProcess.StandardOutput.BaseStream;

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

    private static async Task AssemblyAI_ITranscriptionClient_NonStreaming()
    {
        using var client = new AssemblyAITranscriptionClient(s_apiKey);
        var audioContent = new AudioContent(File.ReadAllBytes("Resources/barbara.wav"));

        Console.WriteLine("Transcription Started");
        var result = await client.TranscribeAsync(audioContent, new()
        {
            AudioLanguage = nameof(TranscriptLanguageCode.Pt),
        }, CancellationToken.None);
        Console.WriteLine($"Transcription: {result?.Text}");
        Console.WriteLine("Transcription Complete");
    }

    private static async Task AssemblyAI_Manual_Streaming()
    {
        var cts = new CancellationTokenSource();
        var ct = cts.Token;

        Console.CancelKeyPress += (sender, e) => cts.Cancel();

        var sampleRate = 16_000U;
        using var transcriber = new RealtimeTranscriber(new RealtimeTranscriberOptions
        {
            ApiKey = s_apiKey,
            SampleRate = sampleRate,
            DisablePartialTranscripts = true
        });

        transcriber.SessionBegins.Subscribe(
            message => Console.WriteLine($"Session begins: \n- Session ID: {message.SessionId}\n- Expires at: {message.ExpiresAt}"));

        transcriber.PartialTranscriptReceived.Subscribe(transcript =>
        {
            // don't do anything if nothing was said
            if (string.IsNullOrEmpty(transcript.Text))
            {
                return;
            }

            Console.WriteLine($"Partial: {transcript.Text}");
        });

        transcriber.FinalTranscriptReceived.Subscribe(transcript =>
        {
            Console.WriteLine($"Final: {transcript.Text}");
        });

        transcriber.ErrorReceived.Subscribe(error => Console.WriteLine($"Real-time error: {error.Error}"));
        transcriber.Closed.Subscribe(closeEvent =>
            Console.WriteLine("Real-time connection closed: {0} - {1}",
                closeEvent.Code,
                closeEvent.Reason
            )
        );

        await transcriber.ConnectAsync().ConfigureAwait(false);

        var soxArguments = string.Join(' ', [
            // --default-device doesn't work on Windows
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-t waveaudio default" : "--default-device",
            "--no-show-progress",
            $"--rate {sampleRate}",
            "--channels 1",
            "--encoding signed-integer",
            "--bits 16",
            "--type wav",
            "-" // pipe
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

        soxProcess.Start();
        var soxOutputStream = soxProcess.StandardOutput.BaseStream;
        var buffer = new byte[4096];
        while (await soxOutputStream.ReadAsync(buffer, 0, buffer.Length, ct) > 0)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await transcriber.SendAudioAsync(buffer);
        }

        soxProcess.Kill();
        await transcriber.CloseAsync();
    }

    private static async Task AssemblyAI_Manual_NonStreaming()
    {
        var apiKey = s_apiKey;

        var client = new AssemblyAIClient(apiKey);
        var transcript = await client.Transcripts.TranscribeAsync(new FileInfo("C:\\Users\\roger\\OneDrive\\Desktop\\WhatsApp Audio 2025-01-15 at 23.46.28_aa2ddeb9.m4a"),
            new TranscriptOptionalParams
            {
                LanguageCode = TranscriptLanguageCode.Pt,
            });

        // var transcript = await client.Transcripts.TranscribeAsync(new FileInfo("/path/to/foo.wav"));

        transcript.EnsureStatusCompleted();

        Console.WriteLine(transcript.Text);
    }
}