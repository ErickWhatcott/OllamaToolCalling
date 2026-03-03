// A compiler warning raised by the compiler due to the strange pattern of select async.
#pragma warning disable CS8425

public static class IAsyncEnumerableExtensions
{
    // Specialized function for asynchronous selection and early termination (as compared to the normal exception)
    // With a cancelation token.
    public static async IAsyncEnumerable<V> SelectAsync<T, V>(this IAsyncEnumerable<T> values, Func<T, Task<V>> func, CancellationToken token)
    {
        await foreach(var v in values)
        {
            if(token.IsCancellationRequested)
                yield break;

            yield return await func(v);
        }
    }
}

#pragma warning restore CS8425