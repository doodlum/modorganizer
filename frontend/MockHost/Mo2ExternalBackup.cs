using System.Text.Json;

namespace Mo2.Frontend;

internal static class Mo2ExternalBackup
{
    internal static string Root(string gameDirectory) => Path.Combine(
        Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDirectory)))!,
        ".mo2-external-backups", Path.GetFileName(Path.TrimEndingDirectorySeparator(gameDirectory)));

    internal static string? Latest(string gameDirectory)
    {
        var root = Root(gameDirectory);
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateDirectories(root).Where(path => Guid.TryParse(Path.GetFileName(path), out _) &&
            File.Exists(Path.Combine(path, "manifest.json")))
            .OrderByDescending(path => File.GetLastWriteTimeUtc(Path.Combine(path, "manifest.json"))).FirstOrDefault();
    }

    internal static string Move(Mo2ExternalArchiveCopy copy, string installedDirectory, Mo2ExternalScan fresh, CancellationToken token)
    {
        if (Path.GetFullPath(copy.GameDirectory) != Path.GetFullPath(fresh.GameDirectory) || copy.Files.Length == 0)
            throw new InvalidOperationException("The imported game no longer matches the scan.");
        var external = fresh.Files.Where(file => file.Kind == "External file").Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        if (copy.Files.Any(file => !external.Contains(file.Path)))
            throw new InvalidOperationException("Some imported originals are no longer external files. Rescan before cleanup.");
        if (Mo2ExternalArchive.ChangedOriginals(copy, token).Length != 0 ||
            Mo2ExternalArchive.ChangedInstalledFiles(copy, installedDirectory, token).Length != 0)
            throw new InvalidOperationException("Original or installed files changed. Import and verify them again before cleanup.");
        var backup = Path.Combine(Root(copy.GameDirectory), Guid.NewGuid().ToString("N"));
        var filesRoot = Path.Combine(backup, "files");
        Directory.CreateDirectory(filesRoot);
        NoLinks(backup);
        // Persist intent before moving anything. An interrupted batch can be
        // restored from whichever entries exist in files/ after a restart.
        using (var journal = new FileStream(Path.Combine(backup, "manifest.json"), FileMode.CreateNew, FileAccess.Write)) {
            JsonSerializer.Serialize(journal, copy); journal.Flush(flushToDisk: true);
        }
        var moved = new List<Mo2ExternalArchivedFile>();
        try {
            foreach (var file in copy.Files) {
                token.ThrowIfCancellationRequested();
                var relative = Mo2ExternalFiles.Normalize(file.Path);
                var single = copy with { Files = [file] };
                if (Mo2ExternalArchive.ChangedOriginals(single, token).Length != 0)
                    throw new IOException("An original changed during cleanup.");
                var destination = Path.Combine(filesRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); NoLinks(Path.GetDirectoryName(destination)!);
                File.Move(Path.Combine(copy.GameDirectory, relative), destination);
                moved.Add(file);
                if (Mo2ExternalArchive.ChangedOriginals(single with { GameDirectory = filesRoot }, token).Length != 0)
                    throw new IOException("An original changed while being moved.");
            }
            if (Mo2ExternalArchive.ChangedInstalledFiles(copy, installedDirectory, token).Length != 0)
                throw new IOException("Installed files changed during cleanup.");
            return backup;
        } catch {
            // Never overwrite a new game file during rollback. Keep the journal
            // and remaining backup entries if automatic restoration cannot finish.
            foreach (var file in moved.AsEnumerable().Reverse()) {
                var original = Path.Combine(copy.GameDirectory, file.Path);
                NoLinks(Path.GetDirectoryName(original)!);
                File.Move(Path.Combine(filesRoot, file.Path), original);
            }
            RemoveEmptyBackup(backup);
            throw;
        }
    }

    internal static int Restore(string gameDirectory, string backup, CancellationToken token)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(backup)) != Path.GetFullPath(Root(gameDirectory)) ||
            !Guid.TryParse(Path.GetFileName(backup), out _)) throw new InvalidDataException("Invalid external-files backup.");
        NoLinks(backup);
        var copy = JsonSerializer.Deserialize<Mo2ExternalArchiveCopy>(File.ReadAllText(Path.Combine(backup, "manifest.json")))
            ?? throw new InvalidDataException("Unreadable external-files backup.");
        if (Path.GetFullPath(copy.GameDirectory) != Path.GetFullPath(gameDirectory)) throw new InvalidDataException("Backup belongs to another game.");
        var filesRoot = Path.Combine(backup, "files");
        var pending = copy.Files.Where(file => File.Exists(Path.Combine(filesRoot, Mo2ExternalFiles.Normalize(file.Path)))).ToArray();
        var alreadyRestored = copy.Files.Except(pending).ToArray();
        if (Mo2ExternalArchive.ChangedOriginals(copy with { Files = alreadyRestored }, token).Length != 0)
            throw new IOException("Some files are missing from the backup and do not match the game folder. Keep this backup for recovery.");
        if (Mo2ExternalArchive.ChangedOriginals(copy with { GameDirectory = filesRoot, Files = pending }, token).Length != 0)
            throw new IOException("Backup files changed; restoration stopped.");
        foreach (var file in pending) {
            var destination = Path.Combine(gameDirectory, file.Path);
            if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("A game file already exists at " + file.Path + ". Move it aside before restoring.");
            NoLinks(Path.GetDirectoryName(destination)!);
        }
        var restored = 0;
        foreach (var file in pending) {
            token.ThrowIfCancellationRequested();
            var destination = Path.Combine(gameDirectory, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!); NoLinks(Path.GetDirectoryName(destination)!);
            File.Move(Path.Combine(filesRoot, file.Path), destination); restored++;
        }
        RemoveEmptyBackup(backup);
        return restored;
    }

    private static void RemoveEmptyBackup(string backup)
    {
        File.Delete(Path.Combine(backup, "manifest.json"));
        void RemoveEmpty(string directory) {
            foreach (var child in Directory.EnumerateDirectories(directory))
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) RemoveEmpty(child);
            try { Directory.Delete(directory); }
            catch (IOException) { } // Preserve any files added outside this operation.
            catch (UnauthorizedAccessException) { }
        }
        RemoveEmpty(backup);
    }

    private static void NoLinks(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A backup or game directory is a link; no files were overwritten.");
    }
}
