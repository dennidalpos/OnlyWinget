using System.Collections.ObjectModel;

namespace OnlyWinget.Presentation;

public static class ObservableCollectionExtensions
{
    public static void ReplaceWith<T>(this ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    public static void SynchronizeWith<T, TKey>(this ObservableCollection<T> target, IEnumerable<T> source, Func<T, TKey> keySelector, System.Action<T, T>? updateAction = null)
        where TKey : notnull
    {
        var desired = source.ToArray();
        var desiredKeys = desired.Select(keySelector).ToHashSet();
        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!desiredKeys.Contains(keySelector(target[index])))
            {
                target.RemoveAt(index);
            }
        }

        for (var index = 0; index < desired.Length; index++)
        {
            var key = keySelector(desired[index]);
            var currentIndex = -1;
            for (var candidate = index; candidate < target.Count; candidate++)
            {
                if (EqualityComparer<TKey>.Default.Equals(keySelector(target[candidate]), key))
                {
                    currentIndex = candidate;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                target.Insert(index, desired[index]);
                continue;
            }

            if (currentIndex != index)
            {
                target.Move(currentIndex, index);
            }

            if (updateAction != null)
            {
                updateAction(target[index], desired[index]);
            }
            else if (!EqualityComparer<T>.Default.Equals(target[index], desired[index]))
            {
                target[index] = desired[index];
            }
        }
    }
}
