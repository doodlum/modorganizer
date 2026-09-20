using System.Diagnostics;
using FomodInstaller.Scripting.XmlScript;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusMods.Abstractions.GuidedInstallers;
using NexusMods.Games.FOMOD;
using NexusMods.Games.FOMOD.CoreDelegates;
using NexusMods.Games.FOMOD.UI;
using FomodMod = FomodInstaller.Interface.Mod;

namespace Mo2.Frontend;

internal sealed record Mo2FomodInstallResult(string ModName, string Directory, int Files, bool Guided);

// Installing an archive in the frontend, including FOMODs.
//
// This used to hand the archive to MO2, which picks one of its own installer
// plugins and shows that plugin's window. A frontend-hosted MO2 draws nothing on
// screen, so a scripted installer was a window the user could never answer while
// it held MO2's event loop — the whole host stopped until it was killed.
//
// The FOMOD engine here is the one NMA and Vortex both use, driven through NMA's
// own core delegates and guided-installer window, so an XML FOMOD asks its
// questions in this application. The engine returns the files to install and
// they are written into an MO2 mod folder, which is all an installed mod is.
//
// The other kind of FOMOD carries a C# program instead of that XML, which MO2
// installs by compiling it and letting it drive WinForms. Nothing here runs one:
// it is refused by name rather than installed some other way, because the files
// a script lays down are whatever the script decides, and copying the archive
// instead would produce a mod the author never described.
internal static class Mo2FomodInstaller
{
    // What a FOMOD calls its own configuration, and the folder holding it, which
    // is never part of the installed mod.
    private const string ConfigName = "ModuleConfig.xml";
    private const string ConfigFolder = "fomod";

    internal static async Task<Mo2FomodInstallResult> Install(
        IServiceProvider services, string archive, string modsRoot, CancellationToken token = default)
    {
        if (!File.Exists(archive)) throw new FileNotFoundException("The mod archive does not exist", archive);
        var name = CleanName(Path.GetFileNameWithoutExtension(archive));
        var staging = Path.Combine(Path.GetTempPath(), "mo2-install-" + Guid.NewGuid().ToString("N"));
        try {
            Mo2ArchiveFiles.Extract(archive, staging, token);
            // An archive whose whole content sits in one folder installs from that
            // folder, which is what every installer does with a wrapped mod.
            var root = Unwrap(staging);
            // A .fomod is itself an archive holding the mod, with the readme and
            // anything else sitting beside it. Installing the outer archive as it
            // stands would put that container in the mod folder and none of its
            // contents, so it is unpacked and becomes the mod.
            if (Nested(root) is { } nested) {
                var inner = Path.Combine(staging, ".fomod");
                Mo2ArchiveFiles.Extract(nested, inner, token);
                root = Unwrap(inner);
                name = CleanName(Path.GetFileNameWithoutExtension(nested));
            }
            var script = FindConfig(root);
            // Refused before anything is reserved or written, so a refused archive
            // leaves no half-made mod folder behind.
            if (script is null && FindProgram(root) is { } program)
                throw new NotSupportedException(
                    $"\"{name}\" is a C# script FOMOD ({Path.GetFileName(program)}). " +
                    "Installing it means running the mod's own program, which this application does not do. " +
                    "Install this mod with Mod Organizer itself.");
            var destination = Reserve(modsRoot, name);

            var files = script is null
                ? Simple(root, destination)
                : await Guided(services, root, script, destination, name, token);

            return new Mo2FomodInstallResult(Path.GetFileName(destination), destination, files, script is not null);
        } finally {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch (IOException) { }
        }
    }

    // Everything but the FOMOD's own configuration folder. This is what an archive
    // with no script installs as, and what a scripted archive the engine cannot
    // read falls back to rather than showing the user nothing.
    private static int Simple(string root, string destination)
    {
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
            var relative = Path.GetRelativePath(root, file);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals(ConfigFolder, StringComparison.OrdinalIgnoreCase))) continue;
            Place(file, Path.Combine(destination, relative));
            count++;
        }
        return count;
    }

    private static async Task<int> Guided(
        IServiceProvider services, string root, string script, string destination, string name, CancellationToken token)
    {
        var scriptType = new XmlScriptType();
        var relative = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/')).ToList();

        var mod = new FomodMod(
            listModFiles: relative,
            stopPatterns: FomodConstants.StopPattern,
            installScriptPath: Path.GetRelativePath(root, script).Replace('\\', '/'),
            tempFolderPath: string.Empty,
            scriptType: scriptType);
        // Loading the script from disk is the library's own job only when it owns
        // the extraction; the text is supplied here instead.
        await mod.InitializeWithoutLoadingScript();

        var installer = services.GetRequiredService<IGuidedInstaller>();
        var delegates = new InstallerDelegates(services.GetRequiredService<ILoggerFactory>(), installer);
        installer.SetupInstaller(name);
        try {
            var executor = scriptType.CreateExecutor(mod, delegates);
            var parsed = scriptType.LoadScript(await File.ReadAllTextAsync(script, token), true);
            var instructions = await executor.Execute(parsed, "", null);

            var errors = instructions.Where(instruction => instruction.type == "error").ToArray();
            if (errors.Length != 0) throw new InvalidOperationException(string.Join("; ", errors.Select(error => error.source)));

            var count = 0;
            foreach (var instruction in instructions.Where(instruction => instruction.type == "copy")) {
                var source = Path.Combine(root, instruction.source.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(source)) continue;
                Place(source, Path.Combine(destination, instruction.destination.Replace('/', Path.DirectorySeparatorChar)));
                count++;
            }
            // A script that selected nothing has installed nothing; the archive is
            // still the user's, so it falls back rather than leaving an empty mod.
            return count > 0 ? count : Simple(root, destination);
        } finally { installer.CleanupInstaller(); }
    }

    private static void Place(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    // MO2 names a mod by its folder, so a second install of the same archive gets
    // its own folder rather than overwriting the first.
    private static string Reserve(string modsRoot, string name)
    {
        Directory.CreateDirectory(modsRoot);
        var candidate = Path.Combine(modsRoot, name);
        for (var attempt = 2; Directory.Exists(candidate); attempt++)
            candidate = Path.Combine(modsRoot, $"{name} ({attempt})");
        Directory.CreateDirectory(candidate);
        return candidate;
    }

    // The one .fomod container in an archive, when it is the mod rather than a
    // stray file beside a mod that is already unpacked.
    private static string? Nested(string root)
    {
        if (FindConfig(root) is not null) return null;
        var found = Directory.EnumerateFiles(root, "*.fomod", SearchOption.TopDirectoryOnly).ToArray();
        return found.Length == 1 ? found[0] : null;
    }

    // The C# program a scripted FOMOD installs with. MO2 looks for it beside the
    // XML in the fomod folder, under whatever name the author gave it.
    private static string? FindProgram(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .FirstOrDefault(file => Path.GetFileName(Path.GetDirectoryName(file))?
                .Equals(ConfigFolder, StringComparison.OrdinalIgnoreCase) == true);

    private static string? FindConfig(string root) =>
        Directory.EnumerateFiles(root, ConfigName, SearchOption.AllDirectories)
            .FirstOrDefault(file => Path.GetFileName(Path.GetDirectoryName(file))?
                .Equals(ConfigFolder, StringComparison.OrdinalIgnoreCase) == true);

    private static string Unwrap(string root)
    {
        var current = root;
        for (var depth = 0; depth < 4; depth++) {
            if (Directory.EnumerateFiles(current).Any()) return current;
            var folders = Directory.GetDirectories(current);
            if (folders.Length != 1) return current;
            current = folders[0];
        }
        return current;
    }

    private static string CleanName(string name)
    {
        var cleaned = new string(name.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? ' ' : character).ToArray()).Trim();
        // Archive names carry the Nexus file and version ids; MO2 names a mod for
        // the mod, so the trailing "-12345-1-2-3456789" is dropped.
        var parts = cleaned.Split('-');
        var keep = parts.TakeWhile(part => !part.Trim().All(char.IsDigit) || part.Trim().Length == 0).ToArray();
        var result = (keep.Length > 0 ? string.Join('-', keep) : cleaned).Trim();
        return result.Length > 0 ? result : "Mod";
    }
}
