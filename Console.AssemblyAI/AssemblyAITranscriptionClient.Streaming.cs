namespace ConsoleAssemblyAI;

using AssemblyAI.Realtime;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

internal sealed partial class AssemblyAITranscriptionClient : IAudioTranscriptionClient
{
    public async IAsyncEnumerable<StreamingTranscriptionUpdate> TranscribeStreamingAsync(
        IAsyncEnumerable<AudioContent> audioUpdates, 
        TranscriptionOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Queue<StreamingTranscriptionUpdate> updates = [];

        using var transcriber = new RealtimeTranscriber(this.ToRealtimeTranscriberOptions(options));

        var connectionOpen = true;
        Stopwatch sessionTime = new();

        transcriber.SessionBegins.Subscribe(
            message =>
            {
                sessionTime.Start();
                updates.Enqueue(new StreamingTranscriptionUpdate
                {
                    RawRepresentation = message,
                    Message = $"Session begins: \n- Session ID: {message.SessionId}\n- Expires at: {message.ExpiresAt}",
                    EventName = "SessionBegins",
                });
            });

        transcriber.Closed.Subscribe(
            closeEvent =>
            {
                connectionOpen = false;
                updates.Enqueue(new StreamingTranscriptionUpdate
                {
                    RawRepresentation = closeEvent,
                    EventName = "Closed",
                    Message = $"Real-time connection closed: {closeEvent.Code} - {closeEvent.Reason}"
                });
            });

        transcriber.PartialTranscriptReceived.Subscribe(
            transcript =>
            {
                if (string.IsNullOrEmpty(transcript.Text)) return;

                updates.Enqueue(new StreamingTranscriptionUpdate
                {
                    StartTime = TimeSpan.FromMilliseconds(transcript.AudioStart),
                    EndTime = TimeSpan.FromMilliseconds(transcript.AudioEnd),
                    RawRepresentation = transcript,
                    EventName = "PartialTranscriptReceived",
                    Transcription = transcript.Text
                });
            });

        transcriber.FinalTranscriptReceived.Subscribe(
            transcript =>
            {
                updates.Enqueue(new StreamingTranscriptionUpdate
                {
                    StartTime = TimeSpan.FromMilliseconds(transcript.AudioStart),
                    EndTime = TimeSpan.FromMilliseconds(transcript.AudioEnd),
                    RawRepresentation = transcript,
                    EventName = "FinalTranscriptReceived",
                    Transcription = transcript.Text,
                });
            });

        transcriber.ErrorReceived.Subscribe(
            error =>
            {
                updates.Enqueue(new StreamingTranscriptionUpdate
                {
                    RawRepresentation = error,
                    EventName = "ErrorReceived",
                    Message = $"Real-time error: {error.Error}"
                });
            });

        await transcriber.ConnectAsync();

#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        _ = Task.Run(async () =>
        {
            await foreach (var audioUpdate in audioUpdates)
            {
                if (audioUpdate.Data.HasValue)
                {
                    await transcriber.SendAudioAsync(audioUpdate.Data.Value.ToArray());
                }
            }
        });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#pragma warning restore CA2016 // Forward the 'CancellationToken' parameter to methods

        while (connectionOpen || updates.Count > 0)
        {
            if (updates.Count > 0)
            {
                yield return updates.Dequeue();
            }
            else
            {
                // Prevent wasting CPU cycles for polling
                await Task.Delay(100);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        await transcriber.CloseAsync();
    }

    private RealtimeTranscriberOptions ToRealtimeTranscriberOptions(TranscriptionOptions? transcriptionOptions)
    {
        if (transcriptionOptions is not null)
        {
            return new()
            {
                ApiKey = this._apiKey,
                SampleRate = (uint)(transcriptionOptions.SourceSampleRate ?? 16_000),
                DisablePartialTranscripts = transcriptionOptions.AdditionalProperties?[nameof(RealtimeTranscriberOptions.DisablePartialTranscripts)] as bool? ?? false,
            };
        }

        return new()
        {
            ApiKey = this._apiKey,
            SampleRate = 16_000,
        };
    }
}
