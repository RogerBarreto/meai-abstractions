
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;

public class AudioTranscriptionOptions
{
    /// <summary>Gets or sets the model ID for the audio transcription.</summary>
    public string? ModelId { get; set; }

    public string? SourceLanguage { get; set; }

    public int? SourceSampleRate { get; set; }

    public string? SourceFileName { get; set; }

    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
}

    