namespace ConsoleAssemblyAI;

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public static class AudioTranscriptionClientExtensions
{
    public static async Task<TranscriptionCompletion> TranscribeAsync(
        this IAudioTranscriptionClient client,
        AudioContent audioContent, 
        TranscriptionOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        IEnumerable<AudioContent> audioContents = [audioContent];
        return await client.TranscribeAsync(audioContents.ToAsyncEnumerable(), options, cancellationToken);
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
