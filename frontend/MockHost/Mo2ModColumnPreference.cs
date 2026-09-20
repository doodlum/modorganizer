using System.Text.Json;

namespace Mo2.Frontend;

// Remembers which optional Mods columns the user switched off, alongside the other
// frontend preferences. Mo2ColumnToggle cannot serve this page: it keys on a table
// source rebuilt per render, while Mods draws its own column grid.
internal static class Mo2ModColumnPreference
{
    private static string Path => Mo2ConfigPaths.Combine("columns-mods.json");

    internal static void Load()
    {
        try {
            if (JsonSerializer.Deserialize<int[]>(File.ReadAllText(Path)) is { } saved) {
                Mo2ModRow.HiddenColumns.Clear();
                foreach (var column in saved) Mo2ModRow.HiddenColumns.Add(column);
            }
        } catch (IOException) { } catch (JsonException) { }
    }

    internal static void Save()
    {
        try {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path + ".tmp", JsonSerializer.Serialize(Mo2ModRow.HiddenColumns.Order().ToArray()));
            File.Move(Path + ".tmp", Path, true);
        } catch (IOException) { }
    }
}
