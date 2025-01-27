using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using MEAI.Abstractions;

public class AzureTranscriptionClient : IAudioTranscriptionClient
{
    private readonly string _subscriptionKey;
    private readonly string _region;

    public AzureTranscriptionClient(string subscriptionKey, string region)
    {
        this._subscriptionKey = subscriptionKey;
        this._region = region;
    }

    public void Dispose()
    {
    }

    public async Task<AudioTranscriptionCompletion> TranscribeAsync(IAsyncEnumerable<AudioContent> audioContents, AudioTranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
       
        var speechConfig = SpeechConfig.FromSubscription(this._subscriptionKey, this._region);
        speechConfig.SpeechRecognitionLanguage = options?.AudioLanguage ?? "en-US";
        PushAudioInputStream? audioConfigStream = null;  
        AudioConfig? audioConfig = null;

        var lastContentSize = 0;
        await foreach (var audioContent in audioContents)
        {
            if (!audioContent.ContainsData)
            {
                throw new NotSupportedException("Azure Speech does not work with audio data reference. Please provide the audio data directly.");
            }

            if (audioContent.Data.HasValue)
            {
                audioConfigStream ??= AudioInputStream.CreatePushStream();
                audioConfig ??= AudioConfig.FromStreamInput(audioConfigStream);

                var buffer = audioContent.Data.Value.ToArray();
                lastContentSize = buffer.Length;
                audioConfigStream.Write(buffer);
            }
        }

        if (audioConfigStream is null)
        {
            throw new InvalidOperationException("No audio data was provided.");
        }

        using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

        if (lastContentSize > 0)
        {
            // Signal finish streaming audio
            audioConfigStream.Write([], 0);
        }

        var speechRecognitionResult = await speechRecognizer.RecognizeOnceAsync();

        audioConfig?.Dispose();
        audioConfigStream?.Dispose();

        return new AudioTranscriptionCompletion
        {
            RawRepresentation = speechRecognitionResult,
            CompletionId = speechRecognitionResult.ResultId,
            Text = speechRecognitionResult.Text,
            StartTime = TimeSpan.Zero,
            EndTime = speechRecognitionResult.Duration,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [nameof(speechRecognitionResult.Reason)] = speechRecognitionResult.Reason
            }
        };
    }

    public async IAsyncEnumerable<StreamingAudioTranscriptionUpdate> TranscribeStreamingAsync(IAsyncEnumerable<AudioContent> audioContents, AudioTranscriptionOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var speechConfig = SpeechConfig.FromSubscription(this._subscriptionKey, this._region);
        speechConfig.SpeechRecognitionLanguage = options?.AudioLanguage ?? "en-US";
        using var audioConfigStream = AudioInputStream.CreatePushStream();
        using var audioConfig = AudioConfig.FromStreamInput(audioConfigStream);
        Stopwatch sessionStopwatch = new();

        var upStreamTask = Task.Run(async () =>
        {
            var lastContentSize = 0;
            var dataProcessed = false;
            await foreach (var audioContent in audioContents)
            {
                if (!audioContent.ContainsData)
                {
                    throw new NotSupportedException("Azure Speech does not work with audio data reference. Please provide the audio data directly.");
                }

                if (audioContent.Data.HasValue)
                {
                    dataProcessed = true;
                    var buffer = audioContent.Data.Value.ToArray();
                    lastContentSize = buffer.Length;
                    audioConfigStream.Write(buffer);
                }
            }

            if (!dataProcessed)
            {
                throw new InvalidOperationException("No audio data was provided.");
            }

            if (lastContentSize > 0)
            {
                // Signal finish streaming audio
                audioConfigStream.Write([], 0);
            }
        });

        await Task.Delay(1000);

        var stopRecognition = new TaskCompletionSource<int>();
        Queue<StreamingAudioTranscriptionUpdate> updates = new();

        using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

        speechRecognizer.SessionStarted += (s, e) =>
        {
            updates.Enqueue(new StreamingAudioTranscriptionUpdate
            {
                CompletionId = e.SessionId,
                StartTime = TimeSpan.Zero,
                EndTime = TimeSpan.Zero,
                RawRepresentation = e,
                Kind = AudioTranscriptionUpdateKind.SessionOpen,
                Text = "Session started.",
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    [nameof(e.SessionId)] = e.SessionId
                }
            });
        };

        speechRecognizer.Recognizing += (s, e) =>
        {
            var startTime = TimeSpan.FromTicks(e.Result.OffsetInTicks);
            switch (e.Result.Reason)
            {
                case ResultReason.RecognizingSpeech:
                    updates.Enqueue(new StreamingAudioTranscriptionUpdate
                    {
                        CompletionId = e.SessionId,
                        StartTime = startTime,
                        EndTime = GetEndTime(startTime, e.Result.Duration),
                        RawRepresentation = e,
                        Kind = AudioTranscriptionUpdateKind.Transcribing,
                        Text = e.Result.Text
                    });
                    break;

                default:
                    updates.Enqueue(new StreamingAudioTranscriptionUpdate
                    {
                        CompletionId = e.SessionId,
                        StartTime = startTime,
                        EndTime = GetEndTime(startTime, e.Result.Duration),
                        RawRepresentation = e,
                        Kind = new AudioTranscriptionUpdateKind(e.Result.Reason.ToString().ToLowerInvariant()),
                        Text = e.Result.Text
                    });
                    break;
            }
        };

        speechRecognizer.Recognized += (s, e) =>
        {
            var startTime = TimeSpan.FromTicks(e.Result.OffsetInTicks);
            switch (e.Result.Reason)
            {
                case ResultReason.RecognizedSpeech:
                    updates.Enqueue(new StreamingAudioTranscriptionUpdate
                    {
                        CompletionId = e.SessionId,
                        StartTime = startTime,
                        EndTime = GetEndTime(startTime, e.Result.Duration),
                        RawRepresentation = e,
                        Kind = AudioTranscriptionUpdateKind.Transcribed,
                        Text = e.Result.Text
                    });
                    break;

                default:
                    updates.Enqueue(new StreamingAudioTranscriptionUpdate
                    {
                        CompletionId = e.SessionId,
                        StartTime = startTime,
                        EndTime = GetEndTime(startTime, e.Result.Duration),
                        RawRepresentation = e,
                        Kind = new AudioTranscriptionUpdateKind(e.Result.Reason.ToString().ToLowerInvariant()),
                        Text = e.Result.Text
                    });
                    break;
            }
        };

        speechRecognizer.Canceled += (s, e) =>
        {
            var canceledTime = e.Offset > (ulong)long.MaxValue
                    ? TimeSpan.MaxValue
                    : TimeSpan.FromTicks((long)e.Offset);

            switch (e.Reason)
            {
                case CancellationReason.Error:
                    updates.Enqueue(
                        new StreamingAudioTranscriptionUpdate(
                        [new ErrorContent() {
                            Code = e.ErrorCode.ToString(),
                            Details = e.ErrorDetails,
                            Message = e.Reason.ToString(),
                        }])
                        {
                            CompletionId = e.SessionId,
                            StartTime = canceledTime,
                            EndTime = canceledTime,
                            RawRepresentation = e,
                            Kind = AudioTranscriptionUpdateKind.Error,
                            Text = e.ErrorDetails
                        });
                    break;

                default:
                    updates.Enqueue(
                        new StreamingAudioTranscriptionUpdate
                        {
                            CompletionId = e.SessionId,
                            StartTime = canceledTime,
                            EndTime = canceledTime,
                            RawRepresentation = e,
                            Kind = new AudioTranscriptionUpdateKind(e.Reason.ToString().ToLowerInvariant()),
                            Text = e.ErrorDetails
                        });
                    break;
            }

            stopRecognition.TrySetResult(0);
        };

        speechRecognizer.SessionStopped += (s, e) =>
        {
            sessionStopwatch.Stop();

            updates.Enqueue(new StreamingAudioTranscriptionUpdate
            {
                CompletionId = e.SessionId,
                StartTime = TimeSpan.Zero,
                EndTime = sessionStopwatch.Elapsed,
                RawRepresentation = e,
                Kind = AudioTranscriptionUpdateKind.SessionClose,
                Text = "Session stopped.",
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    [nameof(e.SessionId)] = e.SessionId
                }
            });

            stopRecognition.TrySetResult(0);
        };

        sessionStopwatch.Start();
        await speechRecognizer.StartContinuousRecognitionAsync();

        while (!stopRecognition.Task.IsCompleted || updates.Count > 0)
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

        await speechRecognizer.StopContinuousRecognitionAsync();
    }

    private static TimeSpan GetEndTime(TimeSpan offSet, TimeSpan duration)
        => offSet + duration;
}
