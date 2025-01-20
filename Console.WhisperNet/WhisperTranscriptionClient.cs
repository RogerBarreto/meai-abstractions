namespace Console.WhisperNet;

using ConsoleAssemblyAI;
using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;

internal sealed partial class WhisperTranscriptionClient : IAudioTranscriptionClient
{
    private readonly WhisperFactory _factory;
    private WhisperProcessor? _processor;

    public WhisperTranscriptionClient(string modelFileName)
    {
        this._factory = WhisperFactory.FromPath(modelFileName);
    }

    public void Dispose()
    {
        if (_processor != null)
        {
            _processor.Dispose();
        }

        if (_factory != null)
        {
            _factory.Dispose();
        }
    }

    public async Task<TranscriptionCompletion> TranscribeAsync(IAsyncEnumerable<AudioContent> inputAudio, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var audioContentsStream = new AudioContentAsyncEnumerableStream(inputAudio);

        TranscriptionCompletion completion = new();

        using var processor = _factory.CreateBuilder().WithLanguage("auto").Build();

        StringBuilder fullTranscription = new();
        List<SegmentData> segments = [];
        await foreach (var segment in processor.ProcessAsync(audioContentsStream, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            completion.StartTime ??= segment.Start;
            completion.EndTime = segment.End;

            segments.Add(segment);
            fullTranscription.Append(segment.Text);
        }

        completion.RawRepresentation = segments;
        completion.Content = new(fullTranscription.ToString());
        

        return completion;
    }

    public async IAsyncEnumerable<StreamingTranscriptionUpdate> TranscribeStreamingAsync(
        IAsyncEnumerable<AudioContent> inputAudio, 
        TranscriptionOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var audioContentsStream = new AudioContentAsyncEnumerableStream(inputAudio);

        if (_processor is null)
        {
            this._processor = _factory.CreateBuilder()
                .WithLanguage("auto")
                .Build();
        }

        await foreach (var segment in _processor.ProcessAsync(audioContentsStream, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            yield return new StreamingTranscriptionUpdate()
            {
                EventName = "Update",
                RawRepresentation = segment,
                Transcription = segment.Text,
                StartTime = segment.Start,
                EndTime = segment.End
            };
        }
    }
}
