namespace Mo2.Frontend;

// A fresh window draws the columns MO2 draws.
//
// MO2 has more columns than it shows: it hides Content, Nexus ID, Uploader, Game,
// Install time and Notes until someone turns them on. Of the columns this list
// carries, that means Content, Uploader and Notes — and showing them made Notes
// look like a column MO2 does not have, because MO2 never draws it unasked.
//
// The columns still exist and can still be switched on, which is the other half:
// hiding by default is not the same as removing, and MO2 lets every column but the
// name be toggled.
internal static class Mo2ModColumnDefaultCheck
{
    public static void Run()
    {
        var hidden = Mo2ModRow.HiddenByDefault;
        var names = Mo2ModRow.Headers.ToDictionary(x => x.Column, x => x.Name);

        foreach (var column in new[] { Mo2ModRow.Content, Mo2ModRow.Uploader, Mo2ModRow.Notes })
            if (!hidden.Contains(column))
                throw new Exception($"{names[column]} is shown by default, but MO2 hides it");

        // The ones MO2 does draw on a first run have to survive this.
        foreach (var column in new[] { Mo2ModRow.Name, Mo2ModRow.Conflicts, Mo2ModRow.Flags, Mo2ModRow.Category, Mo2ModRow.Version })
            if (hidden.Contains(column))
                throw new Exception($"{names[column]} is hidden by default, but MO2 draws it");

        // Hidden, not gone: every one of them is still offered.
        var optional = Mo2ModRow.OptionalColumns.Select(x => x.Column).ToHashSet();
        foreach (var column in hidden)
            if (!optional.Contains(column))
                throw new Exception($"{names[column]} is hidden and cannot be switched back on");
        if (optional.Contains(Mo2ModRow.Name))
            throw new Exception("The name column is optional, but MO2 never lets it be turned off");

        Console.WriteLine($"  hidden by default: {string.Join(", ", hidden.Select(x => names[x]))}");
        Console.WriteLine($"  shown by default:  {string.Join(", ", Mo2ModRow.Headers.Where(x => !hidden.Contains(x.Column)).Select(x => x.Name))}");
        Console.WriteLine("PASS mod columns: a fresh window draws MO2's own columns, and the ones MO2 hides can still be switched on");
    }
}
