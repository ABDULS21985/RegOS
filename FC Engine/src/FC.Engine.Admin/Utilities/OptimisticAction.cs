namespace FC.Engine.Admin.Utilities;

/// <summary>
/// Helpers for optimistic UI updates: immediately apply a value change, trigger a
/// re-render, execute the async operation, then revert and re-render on failure.
/// </summary>
public static class OptimisticAction
{
    /// <summary>
    /// Optimistically sets <paramref name="optimisticValue"/> via <paramref name="setter"/>,
    /// triggers a re-render via <paramref name="onStateChanged"/>, then calls
    /// <paramref name="action"/>. On success invokes <paramref name="onSuccess"/>.
    /// On failure reverts to the original value, re-renders, and invokes
    /// <paramref name="onFailure"/>.
    /// </summary>
    /// <returns>True if the action succeeded; false if the state was reverted.</returns>
    public static async Task<bool> Execute<T>(
        Func<T> getter,
        Action<T> setter,
        T optimisticValue,
        Func<Task> action,
        Action? onStateChanged = null,
        Action? onSuccess = null,
        Action<Exception>? onFailure = null)
    {
        var original = getter();
        setter(optimisticValue);
        onStateChanged?.Invoke();

        try
        {
            await action();
            onSuccess?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            setter(original);
            onStateChanged?.Invoke();
            onFailure?.Invoke(ex);
            return false;
        }
    }

    /// <summary>
    /// Bulk optimistic update: applies <paramref name="optimisticTransform"/> to every item,
    /// triggers a single re-render, then executes <paramref name="action"/> for all items
    /// in parallel. Items whose action throws are reverted to their original value and
    /// <paramref name="onItemFailure"/> is called per failing item. A final re-render is
    /// triggered if any item was reverted.
    /// </summary>
    public static async Task BulkExecute<TItem>(
        IList<TItem> items,
        Func<TItem, TItem> optimisticTransform,
        Func<TItem, Task> action,
        Action? onStateChanged = null,
        Action<TItem, Exception>? onItemFailure = null)
    {
        var originals = items.ToArray();

        for (int i = 0; i < items.Count; i++)
            items[i] = optimisticTransform(originals[i]);

        onStateChanged?.Invoke();

        var results = await Task.WhenAll(
            Enumerable.Range(0, originals.Length).Select(async i =>
            {
                Exception? err = null;
                try { await action(originals[i]); }
                catch (Exception ex) { err = ex; }
                return (Index: i, Error: err);
            }));

        bool anyReverted = false;
        foreach (var (index, error) in results)
        {
            if (error is not null)
            {
                items[index] = originals[index];
                anyReverted = true;
                onItemFailure?.Invoke(originals[index], error);
            }
        }

        if (anyReverted)
            onStateChanged?.Invoke();
    }
}
