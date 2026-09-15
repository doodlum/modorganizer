using NexusMods.App.UI.Extensions;
using ObservableCollections;
using R3;

namespace Mo2.Frontend;

internal static class Mo2SortedRootsCheck
{
    internal static void Run()
    {
        var roots = new ObservableList<int>(Enumerable.Range(0, 151));
        var changes = 0;
        using var subscription = roots.ObserveChanged().Subscribe(_ => changes++);
        var ascending = Comparer<int>.Default;
        if (roots.SortIfNeeded(ascending) || changes != 0) throw new Exception("Already sorted roots emitted a reset");
        var descending = Comparer<int>.Create((a, b) => b.CompareTo(a));
        if (!roots.SortIfNeeded(descending) || changes == 0 || !roots.SequenceEqual(Enumerable.Range(0,151).Reverse()))
            throw new Exception("Changed comparator did not reorder roots");
        changes = 0;
        if (roots.SortIfNeeded(descending) || changes != 0) throw new Exception("Repeated comparator reset sorted roots");
        roots[5] = 999;
        if (!roots.SortIfNeeded(descending) || roots[0] != 999) throw new Exception("Changed priority did not reorder roots");
        roots.Clear(); changes = 0;
        if (roots.SortIfNeeded(ascending) || changes != 0) throw new Exception("Empty roots emitted a reset");
        roots.Add(1); roots.Add(1); changes = 0;
        if (roots.SortIfNeeded(ascending) || changes != 0) throw new Exception("Equal priorities emitted a reset");
        Console.WriteLine("PASS sorted roots: no reset for sorted/empty/equal lists; actual order and priority changes still sort");
    }
}
