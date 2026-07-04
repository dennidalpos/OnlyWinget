namespace OnlyWinget.Domain.Selection;

public sealed class SelectionState<TKey>
    where TKey : notnull
{
    private readonly HashSet<TKey> available = [];
    private readonly HashSet<TKey> selected = [];

    public IReadOnlyCollection<TKey> Available => available;

    public IReadOnlyCollection<TKey> Selected => selected;

    public SelectionHeaderState HeaderState
    {
        get
        {
            if (available.Count == 0 || selected.Count == 0)
            {
                return SelectionHeaderState.Unchecked;
            }

            return selected.Count == available.Count
                ? SelectionHeaderState.Checked
                : SelectionHeaderState.Mixed;
        }
    }

    public bool IsSelected(TKey key) => selected.Contains(key);

    public void ReplaceAvailable(IEnumerable<TKey> keys)
    {
        available.Clear();
        available.UnionWith(keys);
        selected.IntersectWith(available);
    }

    public void SetSelected(TKey key, bool isSelected)
    {
        EnsureAvailable(key);

        if (isSelected)
        {
            selected.Add(key);
            return;
        }

        selected.Remove(key);
    }

    public void Toggle(TKey key)
    {
        EnsureAvailable(key);

        if (!selected.Remove(key))
        {
            selected.Add(key);
        }
    }

    public void ToggleAll()
    {
        if (available.Count == 0)
        {
            return;
        }

        if (selected.Count == available.Count)
        {
            selected.Clear();
            return;
        }

        selected.Clear();
        selected.UnionWith(available);
    }

    private void EnsureAvailable(TKey key)
    {
        if (!available.Contains(key))
        {
            throw new InvalidOperationException("Selection key is not available.");
        }
    }
}
