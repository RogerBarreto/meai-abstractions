
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;

public static class AudioTranscriptionClientExtensions
{
    public static Task<TranscriptionCompletion> TranscribeAsync(
        this IAudioTranscriptionClient client,
        AudioContent audioContent, 
        TranscriptionOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        IEnumerable<AudioContent> audioContents = [audioContent];
        return client.TranscribeAsync(audioContents.ToAsyncEnumerable(), options, cancellationToken);
    }

    public static Task<TranscriptionCompletion> TranscribeAsync(
        this IAudioTranscriptionClient client,
        Stream audioStream,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default)
        => client.TranscribeAsync(
            new AsyncEnumerableAudioStream(audioStream, ToMediaType(options?.SourceFileName)), 
            options, 
            cancellationToken);
    

    public static IAsyncEnumerable<StreamingTranscriptionUpdate> TranscribeStreamingAsync(
        this IAudioTranscriptionClient client,
        Stream audioStream,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default)
        => client.TranscribeStreamingAsync(
            new AsyncEnumerableAudioStream(audioStream, ToMediaType(options?.SourceFileName)),
            options,
            cancellationToken);

    private static string? ToMediaType(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName).TrimStart('.');

        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return $"audio/{extension}";
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        foreach (var item in source)
        {
            yield return item;
        }
    }
}
