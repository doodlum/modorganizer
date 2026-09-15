using System.IO.Compression;
using System.Security.Cryptography;

namespace Mo2.Frontend;

internal sealed record Mo2ExternalArchivedFile(string Path, string ArchivePath, long Bytes, string Sha256);
internal sealed record Mo2ExternalArchiveCopy(string Archive, string GameDirectory, Mo2ExternalArchivedFile[] Files);

internal static class Mo2ExternalArchive
{
    internal static bool IsDataPath(string path) => path.Equals("Data", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("Data/", StringComparison.OrdinalIgnoreCase);

    internal static string Create(Mo2ExternalScan scan, string selected, string downloads, CancellationToken token)
        => CreateCopy(scan, selected, downloads, token).Archive;

    internal static Mo2ExternalArchiveCopy CreateCopy(Mo2ExternalScan scan, string selected, string downloads, CancellationToken token)
    {
        ValidateWindowsName(selected);
        selected = Mo2ExternalFiles.Normalize(selected);
        if (!IsDataPath(selected)) throw new InvalidOperationException("Select external files inside Data to import as an MO2 mod.");
        var files = scan.Files.Where(file => file.Path.Equals(selected, StringComparison.Ordinal) ||
            file.Path.StartsWith(selected + "/", StringComparison.Ordinal)).ToArray();
        if (files.Length == 0) throw new InvalidOperationException("The selected external files are no longer available.");
        if (files.Any(file => file.Kind != "External file")) throw new InvalidOperationException("External links cannot be imported as mod files.");
        // Linux permits names that would overwrite one another when MO2's
        // Windows installer extracts them. Validate the complete selection
        // before creating any archive or reading source content.
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files) {
            token.ThrowIfCancellationRequested();
            ValidateWindowsName(file.Path);
            var relative = Mo2ExternalFiles.Normalize(file.Path);
            if (!IsDataPath(relative) || !relative.Contains('/')) throw new InvalidDataException("Invalid Data file path.");
            var name = relative[(relative.IndexOf('/') + 1)..];
            ValidateWindowsName(name);
            if (!names.Add(name)) throw new InvalidDataException("Selected files have conflicting Windows names: " + name);
        }
        foreach (var name in names) {
            for (var slash = name.IndexOf('/'); slash >= 0; slash = name.IndexOf('/', slash + 1))
                if (names.Contains(name[..slash]))
                    throw new InvalidDataException("A selected file conflicts with a folder on Windows: " + name[..slash]);
        }
        var root = Path.GetFullPath(scan.GameDirectory);
        var staging = Path.Combine(downloads, ".mo2-external-" + Guid.NewGuid().ToString("N"));
        var archive = Path.Combine(downloads, "External files-" + Guid.NewGuid().ToString("N") + ".zip");
        var copied = new List<Mo2ExternalArchivedFile>();
        Directory.CreateDirectory(staging);
        try {
            var temporary = Path.Combine(staging, "import.zip");
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create)) {
                foreach (var file in files) {
                    token.ThrowIfCancellationRequested();
                    var relative = Mo2ExternalFiles.Normalize(file.Path);
                    if (!IsDataPath(relative) || !relative.Contains('/')) throw new InvalidDataException("Invalid Data file path.");
                    var path = Path.Combine(root, relative);
                    CheckPath(root, relative);
                    using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var archivePath = relative[(relative.IndexOf('/') + 1)..];
                    var entry = zip.CreateEntry(archivePath, CompressionLevel.Fastest);
                    using var output = entry.Open();
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[81920];
                    long bytes = 0;
                    int read;
                    while ((read = input.Read(buffer)) > 0) {
                        token.ThrowIfCancellationRequested(); output.Write(buffer, 0, read);
                        hash.AppendData(buffer, 0, read); bytes += read;
                    }
                    CheckPath(root, relative);
                    copied.Add(new(relative, archivePath, bytes, Convert.ToHexString(hash.GetHashAndReset())));
                }
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, archive);
            return new(archive, root, copied.ToArray());
        } finally { Directory.Delete(staging, recursive: true); }
    }

    internal static void ValidateWindowsName(string relative)
    {
        // Validate each folder too: device names remain reserved with extensions.
        // Do not use Path.GetInvalidFileNameChars(), which follows the Linux host.
        foreach (var part in relative.Split('/')) {
            var stem = part.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            var device = stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) ||
                    stem.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".Contains(stem[3]));
            if (part.Length == 0 || part.EndsWith(' ') || part.EndsWith('.') || device ||
                part.Any(character => character < 32 || "<>:\"\\|?*".Contains(character)))
                throw new InvalidDataException("Selected file or folder name cannot be preserved by the Windows installer: " + relative);
        }
    }

    private static void CheckPath(string root, string relative)
    {
        var current = root;
        foreach (var part in relative.Split('/').Prepend("")) {
            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A selected file or parent folder is now a link. Rescan before importing.");
        }
    }

    internal static string[] ChangedOriginals(Mo2ExternalArchiveCopy copy, CancellationToken token)
        => ChangedFiles(copy, copy.GameDirectory, installed: false, token);

    internal static string[] ChangedInstalledFiles(Mo2ExternalArchiveCopy copy, string directory, CancellationToken token)
        => ChangedFiles(copy, directory, installed: true, token);

    private static string[] ChangedFiles(Mo2ExternalArchiveCopy copy, string directory, bool installed, CancellationToken token)
    {
        var changed = new List<string>();
        var buffer = new byte[81920];
        var entries = new Dictionary<string, string[]>();
        foreach (var file in copy.Files) {
            token.ThrowIfCancellationRequested();
            try {
                var relative = Mo2ExternalFiles.Normalize(installed ? file.ArchivePath : file.Path);
                if (installed) {
                    // MO2 may merge into folders whose casing differs from the
                    // ZIP. Reject ambiguous Linux names rather than guessing.
                    var parent = directory;
                    foreach (var part in relative.Split('/')) {
                        if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked install directory");
                        if (!entries.TryGetValue(parent, out var children)) entries[parent] = children = Directory.GetFileSystemEntries(parent);
                        var matches = children.Where(path => string.Equals(Path.GetFileName(path), part, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (matches.Length != 1) throw new IOException("Missing or ambiguous installed file");
                        parent = matches[0];
                    }
                    relative = Path.GetRelativePath(directory, parent);
                }
                CheckPath(directory, relative);
                using var input = new FileStream(Path.Combine(directory, relative), FileMode.Open, FileAccess.Read, FileShare.Read);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long bytes = 0;
                int read;
                while ((read = input.Read(buffer)) > 0) {
                    token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); bytes += read;
                }
                CheckPath(directory, relative);
                if (bytes != file.Bytes || Convert.ToHexString(hash.GetHashAndReset()) != file.Sha256) changed.Add(file.Path);
            } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { changed.Add(file.Path); }
        }
        return changed.ToArray();
    }
}
