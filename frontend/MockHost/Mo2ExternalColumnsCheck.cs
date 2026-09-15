namespace Mo2.Frontend;

// Opt-in check for the External Files tree matching NMA's own External Changes
// view: folders report an aggregated size and a file count, files report their
// own size, links report neither, and sizes step through B / KB / MB.
internal static class Mo2ExternalColumnsCheck
{
    internal static void Run()
    {
        var files = new[] {
            new Mo2ExternalFile("Data/Mods/ContentPatcher/config.json", 165, "File"),
            new Mo2ExternalFile("Data/Mods/ContentPatcher/ContentPatcher.dll", 59, "File"),
            new Mo2ExternalFile("Data/Mods/link.esp", 0, "External link"),
            new Mo2ExternalFile("Data/save-backups/backup.zip", 4_194_304, "File"),
        };
        var roots = Mo2ExternalFileNode.Build(files, "");
        var data = roots.SelectMany(x => x.DescendantsAndSelf()).ToArray();

        Mo2ExternalFileNode Find(string name) => data.FirstOrDefault(x => x.Name == name)
            ?? throw new Exception("No node named " + name);

        var patcher = Find("ContentPatcher");
        if (patcher.FileCount != 2) throw new Exception("Folder file count is " + patcher.FileCount + ", expected 2");
        if (patcher.SizeText != "224 B") throw new Exception("Folder size is " + patcher.SizeText + ", expected 224 B");

        var mods = Find("Mods");
        if (mods.FileCount != 3) throw new Exception("Parent folder did not include the link: " + mods.FileCount);
        if (mods.SizeText != "224 B") throw new Exception("A link should add no bytes: " + mods.SizeText);

        var link = Find("link.esp");
        if (link.SizeText != "—") throw new Exception("Links should show no size, got " + link.SizeText);
        if (link.FileCountText.Length != 0) throw new Exception("Files should have no file count");

        var config = Find("config.json");
        if (config.SizeText != "165 B") throw new Exception("File size is " + config.SizeText);

        var backups = Find("save-backups");
        if (backups.SizeText != "4 MB") throw new Exception("Megabyte step is " + backups.SizeText + ", expected 4 MB");

        // The headers are NMA's own, taken from its column definitions rather than
        // typed out here. This page had them in capitals, which no other table in
        // either app uses.
        var headers = Mo2ExternalFilesView.ColumnHeaders;
        string[] expected = [
            NexusMods.App.UI.Controls.SharedColumns.NameWithFileIcon.GetColumnHeader(),
            NexusMods.App.UI.Controls.SharedColumns.ItemSizeOverGamePath.GetColumnHeader(),
            NexusMods.App.UI.Controls.SharedColumns.FileCount.GetColumnHeader(),
        ];
        if (!headers.SequenceEqual(expected))
            throw new Exception($"Columns read [{string.Join(", ", headers)}], not NMA's [{string.Join(", ", expected)}]");

        Console.WriteLine("PASS external columns: folders aggregate size and file count, files report their own size, " +
            "links report neither, sizes step through B and MB, and the columns are headed " +
            $"[{string.Join(", ", headers)}] from NMA's own definitions, as its External Changes view does");
    }
}
