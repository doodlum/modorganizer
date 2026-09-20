namespace Mo2.Frontend;

// Icons are extracted from the games themselves, so what this can check depends
// on what the machine has installed. It reports every supported game either way,
// and fails only on a game whose executable is present but yields no usable icon.
internal static class Mo2GameIconCheck
{
    internal static void Run(string[] arguments)
    {
        var explicitExecutables = arguments.Skip(1).ToArray();
        if (explicitExecutables.Length > 0) {
            var failed = false;
            foreach (var executable in explicitExecutables) {
                if (!File.Exists(executable)) { Console.Error.WriteLine($"FAIL missing executable: {executable}"); failed = true; continue; }
                if (!Report(Path.GetFileName(executable), executable)) failed = true;
            }
            if (failed) throw new Exception("An executable produced no usable icon");
            Console.WriteLine("PASS supplied executables produced square icons");
            return;
        }

        var installed = 0;
        var failures = 0;
        foreach (var game in Mo2SupportedGames.All) {
            var executable = Mo2SupportedGames.ExecutablePath(game.Name);
            if (executable is null) { Console.WriteLine($"  {game.Name}: not installed"); continue; }
            installed++;
            if (!Report(game.Name, executable)) failures++;
        }
        if (failures > 0) throw new Exception($"{failures} installed game(s) produced no usable icon");
        Console.WriteLine(installed == 0
            ? "PASS game icons: no supported game is installed here, so every widget falls back to the shared placeholder"
            : $"PASS game icons: {installed} installed game(s) produced a square icon from their own executable");
    }

    private static bool Report(string name, string executable)
    {
        using var icon = Mo2ExeIcon.Extract(executable);
        if (icon is null) { Console.Error.WriteLine($"FAIL {name}: no icon in {executable}"); return false; }
        if (icon.Width != icon.Height || icon.Width == 0) {
            Console.Error.WriteLine($"FAIL {name}: icon is {icon.Width}x{icon.Height}, not square");
            return false;
        }
        var opaque = 0;
        for (var y = 0; y < icon.Height; y++)
            for (var x = 0; x < icon.Width; x++)
                if (icon.GetPixel(x, y).Alpha > 0) opaque++;
        if (opaque == 0) { Console.Error.WriteLine($"FAIL {name}: icon decoded to fully transparent pixels"); return false; }
        var coverage = 100.0 * opaque / (icon.Width * icon.Height);
        Console.WriteLine($"  {name}: {icon.Width}x{icon.Height}, {coverage:F0}% opaque, from {Path.GetFileName(executable)}");
        return true;
    }
}
