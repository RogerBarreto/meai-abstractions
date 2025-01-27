
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;

public class AudioTranscriptionOptions
{
    public string? CompletionId { get; set; }

    /// <summary>Gets or sets the model ID for the audio transcription.</summary>
    public string? ModelId { get; set; }

    public string? AudioLanguage { get; set; }

    public int? AudioSampleRate { get; set; }

    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
}

    