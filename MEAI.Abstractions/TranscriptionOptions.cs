namespace ConsoleAssemblyAI;

using Microsoft.Extensions.AI;

public class TranscriptionOptions
{
    /// <summary>Gets or sets the model ID for the audio transcription.</summary>
    public string? ModelId { get; set; }

    public string? SourceLanguage { get; set; }

    public int? SourceSampleRate { get; set; }

    public int? AudioSampleRate { get; set; }
    
    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
}