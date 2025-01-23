using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;

public static class StreamExtensions 
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this Stream audioStream, string? mediaType = null)
        where T : DataContent
    {
        var buffer = new byte[4096];
        while ((await audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            yield return (T)Activator.CreateInstance(typeof(T), [(ReadOnlyMemory<byte>)buffer, mediaType])!;
        }
    }
}