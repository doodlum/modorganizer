namespace Mo2.Frontend;

internal static class Mo2DataFiltersCheck
{
    internal static void Run()
    {
        // Expected visibility for the eight checkbox combinations, derived from
        // src/filetreemodel.cpp shouldShowFile/shouldShowFolder. Bit index uses
        // conflicts=1, archives=2, hidden=4. No game files or host state are changed.
        (Mo2DataEntry Entry, int Visible)[] cases = [
            (new("meshes", true, [], ""), 0xff),
            (new("meshes.MOHIDDEN", true, [], ""), 0x50),
            (new("a.nif", false, ["A"], ""), 0x55),
            (new("a.nif", false, ["A", "B"], ""), 0xff),
            (new("a.nif", false, ["A"], "a.bsa"), 0x44),
            (new("a.nif", false, ["A", "B"], "a.bsa"), 0xcc),
            (new("a.nif.mohidden", false, ["A"], ""), 0x50),
            (new("a.nif.MOHIDDEN", false, ["A", "B"], ""), 0x50),
            (new("a.nif.mohidden", false, ["A", "B"], "a.bsa"), 0x40),
        ];
        foreach (var (entry, visible) in cases)
            for (var flags = 0; flags < 8; flags++)
                if (entry.MatchesVisibility((flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0)
                    != ((visible & (1 << flags)) != 0))
                    throw new Exception($"Data visibility differs from Qt: {entry.Name}, folder={entry.Directory}, archive={entry.Archive}, flags={flags}");
        if (Mo2DataView.DataRelativePath("Tools/NVSE", "tool.exe") != "Tools/NVSE/tool.exe"
            || Mo2DataView.DataRelativePath("", "tool.exe") != "tool.exe")
            throw new Exception("Data action lost its containing folder");
        // Qt spawn.cpp distinguishes these program types from handler-opened files.
        foreach (var name in new[] { "tool.EXE", "setup.cmd", "run.BAT", "tool.jar" })
            if (!Mo2DataView.IsExecutable(new(name, false, [], "")))
                throw new Exception("Qt program type was offered Open instead of Execute: " + name);
        foreach (var name in new[] { "plugin.dll", "script.ps1", "tool.exe.mohidden", "archive.bsa", "readme" })
            if (Mo2DataView.IsExecutable(new(name, false, [], "")))
                throw new Exception("Nonprogram was offered Execute: " + name);
        Console.WriteLine("PASS Data launch captions: Qt executable, batch and Java types use Execute; other files use Open");
        Console.WriteLine("PASS Data action path: nested folders retained; root files remain relative");
        Console.WriteLine("PASS Data visibility: 72 cases covering hidden folders, hidden conflicts, archives and checkbox combinations");
        Console.WriteLine("LIMIT: folder pruning by descendant content and rendered checkbox interaction are not covered here");
    }
}
