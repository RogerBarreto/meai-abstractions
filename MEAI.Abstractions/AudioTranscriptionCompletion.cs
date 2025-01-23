
using Microsoft.Extensions.AI;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MEAI.Abstractions;

public class AudioTranscriptionCompletion
{
    private IList<AIContent>? _contents;

    /// <summary>Initializes a new instance of the <see cref="ChatMessage"/> class.</summary>
    [JsonConstructor]
    public AudioTranscriptionCompletion()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ChatMessage"/> class.</summary>
    /// <param name="contents">The contents for this message.</param>
    public AudioTranscriptionCompletion(IList<AIContent> contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        this._contents = contents;
    }

    /// <summary>Initializes a new instance of the <see cref="ChatMessage"/> class.</summary>
    /// <param name="content">Content of the message.</param>
    public AudioTranscriptionCompletion(string? content)
        : this(content is null ? [] : [new TextContent(content)])
    {
    }

    /// <summary>Gets or sets the ID of the chat completion.</summary>
    public string? CompletionId { get; set; }

    /// <summary>Gets or sets the model ID used in the creation of the chat completion.</summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// Gets or sets the text of the first <see cref="TextContent"/> instance in <see cref="Contents" />.
    /// </summary>
    /// <remarks>
    /// If there is no <see cref="TextContent"/> instance in <see cref="Contents" />, then the getter returns <see langword="null" />,
    /// and the setter adds a new <see cref="TextContent"/> instance with the provided value.
    /// </remarks>
    [JsonIgnore]
    public string? Text
    {
        get => Contents.FindFirst<TextContent>()?.Text;
        set
        {
            if (Contents.FindFirst<TextContent>() is { } textContent)
            {
                textContent.Text = value;
            }
            else if (value is not null)
            {
                Contents.Add(new TextContent(value));
            }
        }
    }

    /// <summary>Gets or sets the chat message content items.</summary>
    [AllowNull]
    public IList<AIContent> Contents
    {
        get => this._contents ??= [];
        set => this._contents = value;
    }

    public TimeSpan? StartTime { get; set; }

    public TimeSpan? EndTime { get; set; }

    [JsonIgnore]
    public object? RawRepresentation { get; set; }

    /// <summary>Gets or sets any additional properties associated with the chat completion.</summary>
    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
}
