
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;

public static class AudioTranscriptionClientExtensions
{
    public static Task<AudioTranscriptionCompletion> TranscribeAsync(
        this IAudioTranscriptionClient client,
        AudioContent audioContent, 
        AudioTranscriptionOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        IEnumerable<AudioContent> audioContents = [audioContent];
        return client.TranscribeAsync(audioContents.ToAsyncEnumerable(), options, cancellationToken);
    }

    public static Task<AudioTranscriptionCompletion> TranscribeAsync(
        this IAudioTranscriptionClient client,
        Stream audioStream,
        AudioTranscriptionOptions? options = null,
        CancellationToken cancellationToken = default)
        => client.TranscribeAsync(
            audioStream.ToAsyncEnumerable<AudioContent>(), 
            options, 
            cancellationToken);
    

    public static IAsyncEnumerable<StreamingAudioTranscriptionUpdate> TranscribeStreamingAsync(
        this IAudioTranscriptionClient client,
        Stream audioStream,
        AudioTranscriptionOptions? options = null,
        CancellationToken cancellationToken = default)
        => client.TranscribeStreamingAsync(
            audioStream.ToAsyncEnumerable<AudioContent>(),
            options,
            cancellationToken);

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
