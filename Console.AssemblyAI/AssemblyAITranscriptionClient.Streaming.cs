
using AssemblyAI.Realtime;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ConsoleAssemblyAI;

internal sealed partial class AssemblyAITranscriptionClient : IAudioTranscriptionClient
{
    public async IAsyncEnumerable<StreamingAudioTranscriptionUpdate> TranscribeStreamingAsync(
        IAsyncEnumerable<AudioContent> audioUpdates, 
        AudioTranscriptionOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Queue<StreamingAudioTranscriptionUpdate> updates = [];

        using var transcriber = new RealtimeTranscriber(this.ToRealtimeTranscriberOptions(options));

        var connectionOpen = true;
        Stopwatch sessionTime = new();
        string? sessionId = null;
        transcriber.SessionBegins.Subscribe(
            message =>
            {
                sessionId = message.SessionId;
                sessionTime.Start();
                updates.Enqueue(new StreamingAudioTranscriptionUpdate
                {
                    CompletionId = sessionId,
                    RawRepresentation = message,
                    Text = $"Session begins: \n- Session ID: {message.SessionId}\n- Expires at: {message.ExpiresAt}",
                    Kind = AudioTranscriptionUpdateKind.SessionOpen,
                });
            });

        transcriber.Closed.Subscribe(
            closeEvent =>
            {
                connectionOpen = false;
                updates.Enqueue(new StreamingAudioTranscriptionUpdate
                {
                    CompletionId = sessionId,
                    RawRepresentation = closeEvent,
                    Kind = AudioTranscriptionUpdateKind.SessionClose,
                    Text = $"Real-time connection closed: {closeEvent.Code} - {closeEvent.Reason}"
                });
            });

        transcriber.PartialTranscriptReceived.Subscribe(
            transcript =>
            {
                if (string.IsNullOrEmpty(transcript.Text))
                {
                    return;
                }

                updates.Enqueue(new StreamingAudioTranscriptionUpdate
                {
                    CompletionId = sessionId,
                    StartTime = TimeSpan.FromMilliseconds(transcript.AudioStart),
                    EndTime = TimeSpan.FromMilliseconds(transcript.AudioEnd),
                    RawRepresentation = transcript,
                    Kind = AudioTranscriptionUpdateKind.Transcribing,
                    Text = transcript.Text
                });
            });

        transcriber.FinalTranscriptReceived.Subscribe(
            transcript =>
            {
                updates.Enqueue(new StreamingAudioTranscriptionUpdate
                {
                    CompletionId = sessionId,
                    StartTime = TimeSpan.FromMilliseconds(transcript.AudioStart),
                    EndTime = TimeSpan.FromMilliseconds(transcript.AudioEnd),
                    RawRepresentation = transcript,
                    Kind = AudioTranscriptionUpdateKind.Transcribed,
                    Text = transcript.Text,
                });
            });

        transcriber.ErrorReceived.Subscribe(
            error =>
            {
                updates.Enqueue(new StreamingAudioTranscriptionUpdate
                {
                    CompletionId = sessionId,
                    RawRepresentation = error,
                    Kind = AudioTranscriptionUpdateKind.Error,
                    Text = $"Real-time error: {error.Error}"
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

    private RealtimeTranscriberOptions ToRealtimeTranscriberOptions(AudioTranscriptionOptions? transcriptionOptions)
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
