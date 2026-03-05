using System.Runtime.CompilerServices;
using System.Threading.Channels;

public static class IAsyncEnumerableExtensions
{
    // Specialized function for asynchronous selection and early termination (as compared to the normal exception)
    // With a cancelation token.
    public static async IAsyncEnumerable<V> SelectAsync<T, V>(this IAsyncEnumerable<T> values, Func<T, Task<V>> func, int limit, [EnumeratorCancellation] CancellationToken token)
    {
        var throttle = new SemaphoreSlim(limit, limit);
        var channel = Channel.CreateUnbounded<Task<V>>();

        var consumer = Task.Run(async () =>
        {
            try
            {
                await foreach (var v in values)
                {
                    if (token.IsCancellationRequested)
                        break;

                    await throttle.WaitAsync();

                    var task = Task.Run(async () =>
                    {
                        try { return await func(v); }
                        finally { throttle.Release(); }
                    });

                    await channel.Writer.WriteAsync(task, CancellationToken.None);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, CancellationToken.None);

        await foreach(var task in channel.Reader.ReadAllAsync(CancellationToken.None))
            yield return await task;
    }
}