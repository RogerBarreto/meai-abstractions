using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using ConsoleAssemblyAI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Diagnostics;

public class AzureTranscriptionClient : IAudioTranscriptionClient
{
    private readonly string _subscriptionKey;
    private readonly string _region;

    public AzureTranscriptionClient(string subscriptionKey, string region)
    {
        _subscriptionKey = subscriptionKey;
        _region = region;
    }

    public void Dispose()
    {
    }

    public async Task<TranscriptionCompletion> TranscribeAsync(IAsyncEnumerable<AudioContent> audioContents, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
       
        var speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        speechConfig.SpeechRecognitionLanguage = options?.SourceLanguage ?? "en-US";
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

        return new TranscriptionCompletion
        {
            RawRepresentation = speechRecognitionResult,
            CompletionId = speechRecognitionResult.ResultId,
            Content = new TranscribedContent(speechRecognitionResult.Text),
            StartTime = TimeSpan.Zero,
            EndTime = speechRecognitionResult.Duration,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [nameof(speechRecognitionResult.Reason)] = speechRecognitionResult.Reason
            }
        };
    }

    public async IAsyncEnumerable<StreamingTranscriptionUpdate> TranscribeStreamingAsync(IAsyncEnumerable<AudioContent> audioContents, TranscriptionOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        speechConfig.SpeechRecognitionLanguage = options?.SourceLanguage ?? "en-US";
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
        Queue<StreamingTranscriptionUpdate> updates = new();

        using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

        speechRecognizer.Recognizing += (s, e) =>
        {
            var startTime = TimeSpan.FromTicks(e.Result.OffsetInTicks);
            updates.Enqueue(new StreamingTranscriptionUpdate
            {
                StartTime = startTime,
                EndTime = GetEndTime(startTime, e.Result.Duration),
                RawRepresentation = e,
                EventName = "Recognizing",
                Transcription = e.Result.Text
            });
        };

        speechRecognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                var startTime = TimeSpan.FromTicks(e.Result.OffsetInTicks);
                updates.Enqueue(new StreamingTranscriptionUpdate
                {
                    StartTime = startTime,
                    EndTime = GetEndTime(startTime, e.Result.Duration),
                    RawRepresentation = e,
                    EventName = "Recognized",
                    Transcription = e.Result.Text
                });
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                var startTime = TimeSpan.FromTicks(e.Result.OffsetInTicks);
                updates.Enqueue(new StreamingTranscriptionUpdate
                {
                    StartTime = startTime,
                    EndTime = GetEndTime(startTime, e.Result.Duration),
                    RawRepresentation = e,
                    EventName = "NoMatch",
                    Transcription = "Speech could not be recognized."
                });
            }
        };

        speechRecognizer.Canceled += (s, e) =>
        {
            var canceledTime = e.Offset > (ulong)long.MaxValue
                    ? TimeSpan.MaxValue
                    : TimeSpan.FromTicks((long)e.Offset);

            updates.Enqueue(new StreamingTranscriptionUpdate
            {
                StartTime = canceledTime,
                EndTime = canceledTime,
                RawRepresentation = e,
                EventName = "Canceled",
                Transcription = $"Reason={e.Reason}, ErrorCode={e.ErrorCode}, ErrorDetails={e.ErrorDetails}"
            });

            stopRecognition.TrySetResult(0);
        };

        speechRecognizer.SessionStopped += (s, e) =>
        {
            sessionStopwatch.Stop();

            updates.Enqueue(new StreamingTranscriptionUpdate
            {
                StartTime = TimeSpan.Zero,
                EndTime = sessionStopwatch.Elapsed,
                RawRepresentation = e,
                EventName = "SessionStopped",
                Transcription = "Session stopped event.",
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
