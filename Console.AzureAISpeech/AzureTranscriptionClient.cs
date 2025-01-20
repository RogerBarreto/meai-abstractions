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

    public IAsyncEnumerable<StreamingTranscriptionUpdate> TranscribeStreamingAsync(IAsyncEnumerable<AudioContent> audioUpdates, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
        /*
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

        */
    }
}
