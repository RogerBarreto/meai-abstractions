
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;

public static class AudioTranscriptionClientExtensions
{
    public static Task<TranscriptionCompletion> TranscribeAsync(
        this IAudioTranscriptionClient client,
        AudioContent audioContent, 
        AudioTranscriptionOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        IEnumerable<AudioContent> audioContents = [audioContent];
        return client.TranscribeAsync(audioContents.ToAsyncEnumerable(), options, cancellationToken);
    }

    public static Task<TranscriptionCompletion> TranscribeAsync(
        this IAudioTranscriptionClient client,
        Stream audioStream,
        AudioTranscriptionOptions? options = null,
        CancellationToken cancellationToken = default)
        => client.TranscribeAsync(
            audioStream.ToAsyncEnumerable(ToMediaType(options?.SourceFileName)), 
            options, 
            cancellationToken);
    

    public static IAsyncEnumerable<StreamingAudioTranscriptionUpdate> TranscribeStreamingAsync(
        this IAudioTranscriptionClient client,
        Stream audioStream,
        AudioTranscriptionOptions? options = null,
        CancellationToken cancellationToken = default)
        => client.TranscribeStreamingAsync(
            audioStream.ToAsyncEnumerable(ToMediaType(options?.SourceFileName)),
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

    private static async IAsyncEnumerable<AudioContent> ToAsyncEnumerable(this Stream audioStream, string? mediaType = null)
    {
        var buffer = new byte[4096];
        var bytesRead = 0;
        while ((bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            yield return new AudioContent(buffer, mediaType);
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }
    }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
}
