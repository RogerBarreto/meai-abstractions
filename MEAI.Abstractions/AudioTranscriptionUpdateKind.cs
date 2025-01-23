using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace MEAI.Abstractions;

/// <summary>
/// Describes the intended purpose of a message within a chat completion interaction.
/// </summary>
[JsonConverter(typeof(Converter))]
public readonly struct AudioTranscriptionUpdateKind : IEquatable<AudioTranscriptionUpdateKind>
{
    public static AudioTranscriptionUpdateKind SessionOpen { get; } = new("sessionopen");

    public static AudioTranscriptionUpdateKind Error { get; } = new("error");

    /// <summary>Used when the transcription is in progress, without waiting for silence.</summary>
    public static AudioTranscriptionUpdateKind Transcribing { get; } = new("transcribing");

    /// <summary>Used when the transcription is complete after small period of silence.</summary>
    public static AudioTranscriptionUpdateKind Transcribed { get; } = new("transcribed");

    public static AudioTranscriptionUpdateKind SessionClose { get; } = new("sessionclose");

    /// <summary>
    /// Gets the value associated with this <see cref="ChatRole"/>.
    /// </summary>
    /// <remarks>
    /// The value will be serialized into the "role" message field of the Chat Message format.
    /// </remarks>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatRole"/> struct with the provided value.
    /// </summary>
    /// <param name="value">The value to associate with this <see cref="ChatRole"/>.</param>
    [JsonConstructor]
    public AudioTranscriptionUpdateKind(string value)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>
    /// Returns a value indicating whether two <see cref="ChatRole"/> instances are equivalent, as determined by a
    /// case-insensitive comparison of their values.
    /// </summary>
    /// <param name="left">The first <see cref="ChatRole"/> instance to compare.</param>
    /// <param name="right">The second <see cref="ChatRole"/> instance to compare.</param>
    /// <returns><see langword="true"/> if left and right are both null or have equivalent values; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(AudioTranscriptionUpdateKind left, AudioTranscriptionUpdateKind right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Returns a value indicating whether two <see cref="ChatRole"/> instances are not equivalent, as determined by a
    /// case-insensitive comparison of their values.
    /// </summary>
    /// <param name="left">The first <see cref="ChatRole"/> instance to compare. </param>
    /// <param name="right">The second <see cref="ChatRole"/> instance to compare. </param>
    /// <returns><see langword="true"/> if left and right have different values; <see langword="false"/> if they have equivalent values or are both null.</returns>
    public static bool operator !=(AudioTranscriptionUpdateKind left, AudioTranscriptionUpdateKind right)
    {
        return !(left == right);
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is AudioTranscriptionUpdateKind otherRole && this.Equals(otherRole);

    /// <inheritdoc/>
    public bool Equals(AudioTranscriptionUpdateKind other)
        => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override int GetHashCode()
        => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Provides a <see cref="JsonConverter{AudioTranscriptionUpdateKind}"/> for serializing <see cref="AudioTranscriptionUpdateKind"/> instances.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class Converter : JsonConverter<AudioTranscriptionUpdateKind>
    {
        /// <inheritdoc />
        public override AudioTranscriptionUpdateKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString()!);

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, AudioTranscriptionUpdateKind value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStringValue(value.Value);
        }
    }
}
