using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using ConsoleAssemblyAI;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;

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

    public async Task<TranscriptionCompletion> TranscribeAsync(IAsyncEnumerable<AudioContent> audioContent, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        speechConfig.SpeechRecognitionLanguage = options?.SourceLanguage ?? "en-US";

        var enumerator = audioContent.GetAsyncEnumerator(cancellationToken);
        await enumerator.MoveNextAsync();

        var firstChunk = enumerator.Current;

        using var audioConfig = firstChunk.ContainsData
            ? AudioConfig.FromStreamInput(new AudioContentAsyncEnumerableStream(audioContent, cancellationToken))
            : AudioConfig.FromWavFileInput(firstChunk.Uri);

        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);
        var result = await recognizer.RecognizeOnceAsync();

        return new TranscriptionCompletion
        {
            RawRepresentation = result,
            CompletionId = result.ResultId,
            ModelId = speechConfig.EndpointId,
            Content = new TranscribedContent(result.Text)
        };
    }

    public async IAsyncEnumerable<StreamingTranscriptionUpdate> TranscribeStreamingAsync(IAsyncEnumerable<AudioContent> audioUpdates, TranscriptionOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        speechConfig.SpeechRecognitionLanguage = options?.SourceLanguage ?? "en-US";

        using var audioConfig = AudioConfig.FromStreamInput(new AudioContentAsyncEnumerableStream(audioUpdates, cancellationToken));
        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        var stopRecognition = new TaskCompletionSource<int>();
        Queue<StreamingTranscriptionUpdate> updates = new();

        recognizer.Recognizing += (s, e) =>
        {
            updates.Enqueue(new StreamingTranscriptionUpdate
            {
                RawRepresentation = e,
                EventName = "Recognizing",
                Transcription = e.Result.Text
            });
        };

        recognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                updates.Enqueue(new StreamingTranscriptionUpdate
                {
                    RawRepresentation = e,
                    EventName = "Recognized",
                    Transcription = e.Result.Text
                });
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                updates.Enqueue(new StreamingTranscriptionUpdate
                {
                    RawRepresentation = e,
                    EventName = "NoMatch",
                    Transcription = "Speech could not be recognized."
                });
            }
        };

        recognizer.Canceled += (s, e) =>
        {
            updates.Enqueue(new StreamingTranscriptionUpdate
            {
                RawRepresentation = e,
                EventName = "Canceled",
                Transcription = $"Reason={e.Reason}, ErrorCode={e.ErrorCode}, ErrorDetails={e.ErrorDetails}"
            });

            stopRecognition.TrySetResult(0);
        };

        recognizer.SessionStopped += (s, e) =>
        {
            updates.Enqueue(new StreamingTranscriptionUpdate
            {
                RawRepresentation = e,
                EventName = "SessionStopped",
                Transcription = "Session stopped event."
            });

            stopRecognition.TrySetResult(0);
        };

        await recognizer.StartContinuousRecognitionAsync();

        while (!stopRecognition.Task.IsCompleted || updates.Count > 0)
        {
            if (updates.Count > 0)
            {
                yield return updates.Dequeue();
            }
            else
            {
                await Task.Delay(100);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        await recognizer.StopContinuousRecognitionAsync();
    }
}
