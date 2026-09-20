using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;

namespace Mo2.Frontend;

// A FOMOD that installs by running a C# program is refused, and refused without
// leaving anything behind. The archive the refusal was written for is the Mod
// Configuration Menu, whose fomod folder holds script.cs and no ModuleConfig.xml,
// so the shape checked here is that shape.
//
// The second half is the part that matters as much: the refusal has to be narrow.
// An ordinary archive with no fomod folder still installs.
internal static class Mo2FomodScriptCheck
{
    public static async Task Run(string? realArchive = null)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var work = Path.Combine(Path.GetTempPath(), "mo2-fomod-script-" + Guid.NewGuid().ToString("N"));
        var mods = Path.Combine(work, "mods");
        Directory.CreateDirectory(mods);
        try {
            var scripted = Archive(work, "Scripted Mod", files: new() {
                ["fomod/script.cs"] = "class Script : FalloutNewVegasBaseScript { }",
                ["fomod/info.xml"] = "<fomod/>",
                ["Data/Scripted.esp"] = "esp",
            });

            var refusal = await Refused(() => Mo2FomodInstaller.Install(services, scripted, mods));
            if (refusal is null) throw new Exception("A C# script FOMOD installed instead of being refused");
            if (refusal is not NotSupportedException) throw new Exception($"Refused with the wrong kind of error: {refusal.GetType().Name}");
            foreach (var word in new[] { "script.cs", "Mod Organizer" })
                if (!refusal.Message.Contains(word, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"The refusal does not mention {word}: {refusal.Message}");
            if (Directory.GetDirectories(mods).Length != 0)
                throw new Exception("The refused archive left a mod folder behind");
            Console.WriteLine($"  refusal: {refusal.Message}");

            var plain = Archive(work, "Plain Mod", files: new() {
                ["Readme.txt"] = "readme",
                ["Data/Plain.esp"] = "esp",
                ["Data/textures/plain.dds"] = "dds",
            });
            var installed = await Mo2FomodInstaller.Install(services, plain, mods);
            if (installed.Files != 3) throw new Exception($"An ordinary archive installed {installed.Files} files, expected 3");
            if (!File.Exists(Path.Combine(installed.Directory, "Data", "Plain.esp")))
                throw new Exception("An ordinary archive did not install its files");
            Console.WriteLine($"  ordinary archive: {installed.ModName} ({installed.Files} files)");

            // The shape a real one arrives in: the script is not in the archive at
            // all, but inside a .fomod container the archive carries. Refusing it
            // means unpacking that container first, which is what an install does.
            if (realArchive is { Length: > 0 }) {
                var real = await Refused(() => Mo2FomodInstaller.Install(services, realArchive, mods));
                if (real is not NotSupportedException)
                    throw new Exception($"{Path.GetFileName(realArchive)} was not refused: {real?.Message ?? "it installed"}");
                if (Directory.GetDirectories(mods).Length != 1)
                    throw new Exception($"{Path.GetFileName(realArchive)} left a mod folder behind");
                Console.WriteLine($"  {Path.GetFileName(realArchive)}: {real.Message}");
            }

            Console.WriteLine("PASS fomod scripts: a C# script FOMOD is refused with nothing written, an ordinary archive still installs");
        } finally {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }

    private static async Task<Exception?> Refused(Func<Task> action)
    {
        try { await action(); return null; } catch (Exception error) { return error; }
    }

    private static string Archive(string work, string name, Dictionary<string, string> files)
    {
        var staging = Path.Combine(work, "make", name);
        foreach (var (path, content) in files) {
            var full = Path.Combine(staging, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
        var archive = Path.Combine(work, name + ".zip");
        ZipFile.CreateFromDirectory(staging, archive);
        return archive;
    }
}
