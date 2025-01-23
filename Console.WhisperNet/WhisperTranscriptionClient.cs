using MEAI.Abstractions;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text;
using Whisper.net;

namespace ConsoleWhisperNet;

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
        if (this._processor != null)
        {
            this._processor.Dispose();
        }

        if (this._factory != null)
        {
            this._factory.Dispose();
        }
    }

    public async Task<AudioTranscriptionCompletion> TranscribeAsync(IAsyncEnumerable<AudioContent> inputAudio, AudioTranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var audioContentsStream = new AudioContentAsyncEnumerableStream(inputAudio);

        AudioTranscriptionCompletion completion = new();

        if (this._processor is null)
        {
            this._processor = this._factory
                .CreateBuilder()
                .WithLanguage("auto")
                .Build();
        }

        StringBuilder fullTranscription = new();
        List<SegmentData> segments = [];
        await foreach (var segment in this._processor.ProcessAsync(audioContentsStream, cancellationToken))
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
        completion.Text = fullTranscription.ToString();
        

        return completion;
    }

    public async IAsyncEnumerable<StreamingAudioTranscriptionUpdate> TranscribeStreamingAsync(
        IAsyncEnumerable<AudioContent> inputAudio, 
        AudioTranscriptionOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var audioContentsStream = new AudioContentAsyncEnumerableStream(inputAudio);

        if (this._processor is null)
        {
            this._processor = this._factory
                .CreateBuilder()
                .WithLanguage("auto")
                .Build();
        }

        await foreach (var segment in this._processor.ProcessAsync(audioContentsStream, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            yield return new StreamingAudioTranscriptionUpdate()
            {
                Kind = AudioTranscriptionUpdateKind.Transcribing,
                RawRepresentation = segment,
                Text = segment.Text,
                StartTime = segment.Start,
                EndTime = segment.End
            };
        }
    }
}
