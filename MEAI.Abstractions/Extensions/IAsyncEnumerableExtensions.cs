using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;
public static class IAsyncEnumerableExtensions
{
    public static Stream ToStream<T>(this IAsyncEnumerable<T> stream) 
        where T : DataContent
        => new DataContentAsyncEnumerableStream<T>(stream);
}
