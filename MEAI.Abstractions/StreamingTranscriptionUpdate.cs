
using Microsoft.Extensions.AI;
using System.Text.Json.Serialization;

namespace MEAI.Abstractions;

public class StreamingTranscriptionUpdate
{
    [JsonIgnore]
    public object? RawRepresentation { get; set; }

    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }

    public string? ModelId { get; set; }

    public TimeSpan? StartTime { get; set; }

    public TimeSpan? EndTime { get; set; }

    public string? Transcription { get; set; }

    public string? Message { get; set; }

    public required string EventName { get; set; }
}
