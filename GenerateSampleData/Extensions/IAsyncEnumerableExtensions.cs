public static class IAsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<V> SelectAsync<T, V>(this IAsyncEnumerable<T> values, Func<T, Task<V>> func)
    {
        await foreach(var v in values)
        {
            yield return await func(v);
        }
    }
}