using MEAI.Abstractions;
using Microsoft.SemanticKernel.AudioToText;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel;

[Experimental("SKEX0001")]
internal class AudioTranscriptionClientAudioToTextService : IAudioTranscriptionClient
{
    private readonly IAudioToTextService _audioToTextService;

    public AudioTranscriptionClientAudioToTextService(IAudioToTextService audioToTextService)
    {
        ArgumentNullException.ThrowIfNull(audioToTextService);

        this._audioToTextService = audioToTextService;
    }

    public void Dispose()
    {
    }

    public async Task<AudioTranscriptionCompletion> TranscribeAsync(IAsyncEnumerable<Microsoft.Extensions.AI.AudioContent> audioContents, AudioTranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var enumerator = audioContents.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync() || enumerator.Current is null)
        {
            throw new ArgumentException("No audio content provided.", nameof(audioContents));
        }

        Microsoft.SemanticKernel.TextContent? skTextContent = null;
        var msAudioContent = enumerator.Current;
        if (!msAudioContent.ContainsData)
        {
            skTextContent = await _audioToTextService.GetTextContentAsync(new(new Uri(msAudioContent.Uri)), ToPromptExecutionSettings(options), cancellationToken: cancellationToken);
        }
        else
        {
            using var audioStream = audioContents.ToStream(msAudioContent);
            using MemoryStream ms = new();
            await audioStream.CopyToAsync(ms, cancellationToken);

            skTextContent = await _audioToTextService.GetTextContentAsync(new(ms.ToArray(), msAudioContent.MediaType), ToPromptExecutionSettings(options), cancellationToken: cancellationToken);
        }

        var result = new AudioTranscriptionCompletion()
        {
            Text = skTextContent.Text,
            AdditionalProperties = [],
            RawRepresentation = skTextContent
        };

        if (skTextContent.Metadata is not null)
        {
            foreach (var kv in skTextContent.Metadata)
            {
                result.AdditionalProperties[kv.Key] = kv.Value;
            }
        }

        return result;
    }

    public async IAsyncEnumerable<StreamingAudioTranscriptionUpdate> TranscribeStreamingAsync(IAsyncEnumerable<Microsoft.Extensions.AI.AudioContent> audioContents, AudioTranscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var enumerator = audioContents.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync() || enumerator.Current is null)
        {
            throw new ArgumentException("No audio content provided.", nameof(audioContents));
        }

        Microsoft.SemanticKernel.TextContent? skTextContent = null;
        var msAudioContent = enumerator.Current;
        if (!msAudioContent.ContainsData)
        {
            skTextContent = await _audioToTextService.GetTextContentAsync(new(new Uri(msAudioContent.Uri)), ToPromptExecutionSettings(options), cancellationToken: cancellationToken);
        }
        else
        {
            using var audioStream = audioContents.ToStream(msAudioContent);
            using MemoryStream ms = new();
            await audioStream.CopyToAsync(ms, cancellationToken);

            skTextContent = await _audioToTextService.GetTextContentAsync(new(ms.ToArray(), msAudioContent.MediaType), ToPromptExecutionSettings(options), cancellationToken: cancellationToken);
        }

        var update = new StreamingAudioTranscriptionUpdate()
        {
            Kind = AudioTranscriptionUpdateKind.Transcribed,
            Text = skTextContent.Text,
            AdditionalProperties = [],
            RawRepresentation = skTextContent
        };

        if (skTextContent.Metadata is not null)
        {
            foreach (var kv in skTextContent.Metadata)
            {
                update.AdditionalProperties[kv.Key] = kv.Value;
            }
        }

        yield return update;
    }

    private static PromptExecutionSettings? ToPromptExecutionSettings(AudioTranscriptionOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        var settings = new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            ModelId = options.ModelId,
        };

        if (options.AudioLanguage is not null)
        {
            settings.ExtensionData["audio_language"] = options.AudioLanguage;
        }

        if (options.AudioSampleRate is not null)
        {
            settings.ExtensionData["audio_sample_rate"] = options.AudioSampleRate;
        }

        // Transfer over loosely-typed members of ChatOptions.

        if (options.AdditionalProperties is not null)
        {
            foreach (var kvp in options.AdditionalProperties)
            {
                if (kvp.Value is not null)
                {
                    settings.ExtensionData[kvp.Key] = kvp.Value;
                }
            }
        }

        return settings;
    }
}