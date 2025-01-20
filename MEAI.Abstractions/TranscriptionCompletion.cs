namespace ConsoleAssemblyAI;

using Microsoft.Extensions.AI;
using System.Text.Json.Serialization;

public class TranscriptionCompletion
{
    /// <summary>Gets or sets the ID of the chat completion.</summary>
    public string? CompletionId { get; set; }

    /// <summary>Gets or sets the model ID used in the creation of the chat completion.</summary>
    public string? ModelId { get; set; }

    public TranscribedContent? Content { get; set; }

    public TimeSpan? StartTime { get; set; }

    public TimeSpan? EndTime { get; set; }

    [JsonIgnore]
    public object? RawRepresentation { get; set; }

    /// <summary>Gets or sets any additional properties associated with the chat completion.</summary>
    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
}
