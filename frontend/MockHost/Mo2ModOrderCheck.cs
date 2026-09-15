namespace Mo2.Frontend;

internal static class Mo2ModOrderCheck
{
    public static void Run()
    {
        var count = 0;
        for (var size = 1; size <= 8; size++) {
            var order = Enumerable.Range(0, size).Select(x => "Mod " + x).ToArray();
            for (var mask = 0; mask < 1 << size; mask++) {
                var selected = order.Where((_, i) => (mask & (1 << i)) != 0).ToArray();
                foreach (var target in order) foreach (var after in new[] { false, true }) {
                    var expected = order.ToList();
                    if (!selected.Contains(target)) {
                        expected.RemoveAll(selected.Contains);
                        expected.InsertRange(expected.IndexOf(target) + (after ? 1 : 0), selected);
                    }
                    var actual = order.ToList();
                    foreach (var move in Mo2ModOrder.Plan(order, selected.Reverse(), target, after)) {
                        if (!selected.Contains(move.Name)) throw new InvalidOperationException("Planner moved an unselected mod");
                        actual.Remove(move.Name); actual.Insert(move.Priority, move.Name);
                    }
                    if (!actual.SequenceEqual(expected)) throw new InvalidOperationException("Selection order or insertion target was lost");
                    count++;
                }
            }
        }
        foreach (var input in new[] { new[] { "missing" }, new[] { "Mod 0", "missing" } }) {
            try { Mo2ModOrder.Plan(["Mod 0", "Mod 1"], input, "Mod 1", false); }
            catch (InvalidOperationException) { continue; }
            throw new InvalidOperationException("Missing selections must be rejected");
        }
        Console.WriteLine($"PASS {count} single/multiple selection moves, before/after, non-contiguous selections, native relative order and missing-selection rejection");
    }
}
