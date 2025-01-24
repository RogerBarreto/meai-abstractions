using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;
public static class IAsyncEnumerableExtensions
{
    public static Stream ToStream<T>(this IAsyncEnumerable<T> stream, T? firstChunk = null, CancellationToken cancellationToken = default) 
        where T : DataContent
        => new DataContentAsyncEnumerableStream<T>(stream, firstChunk, cancellationToken);
}
