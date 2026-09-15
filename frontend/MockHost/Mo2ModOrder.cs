namespace Mo2.Frontend;

internal static class Mo2ModOrder
{
    internal record Move(string Name, int Priority);
    // Move only selected rows. Work backwards toward a fixed unselected anchor so
    // non-contiguous selections retain their native order in both directions.
    public static Move[] Plan(string[] order, IEnumerable<string> names, string target, bool after)
    {
        var selected = names.ToHashSet(StringComparer.Ordinal);
        if (order.Distinct(StringComparer.Ordinal).Count() != order.Length || !order.Contains(target) || selected.Any(x => !order.Contains(x)))
            throw new InvalidOperationException("Mod list changed; select the mods again");
        if (selected.Count == 0 || selected.Contains(target)) return [];
        var moving = order.Where(selected.Contains).ToArray();
        var remaining = order.Where(x => !selected.Contains(x)).ToList();
        var insertion = remaining.IndexOf(target) + (after ? 1 : 0);
        string? anchor = insertion < remaining.Count ? remaining[insertion] : null;
        var current = order.ToList(); var moves = new List<Move>();
        foreach (var name in moving.Reverse()) {
            var from = current.IndexOf(name);
            current.RemoveAt(from);
            var to = anchor is null ? current.Count : current.IndexOf(anchor);
            current.Insert(to, name);
            if (from != to) moves.Add(new Move(name, to));
            anchor = name;
        }
        return moves.ToArray();
    }
}
