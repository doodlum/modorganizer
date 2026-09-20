namespace Mo2.Frontend;

internal static class Mo2ExternalFilesCheck
{
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "mo2-external-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            // Full path with native separators: the copy records GetFullPath of the
            // game directory, and Path.Combine leaves an embedded "a/b" segment alone,
            // so a mixed-separator fixture path only compares equal on Linux.
            var game = Path.GetFullPath(Path.Combine(root, "steamapps", "common", "Test Game"));
            Directory.CreateDirectory(Path.Combine(game, "Data"));
            File.WriteAllText(Path.Combine(game, "Data/Base.esm"), "vanilla");
            File.WriteAllText(Path.Combine(game, "Data/DLC.esm"), "dlc");
            File.WriteAllText(Path.Combine(game, "Data/extra.esp"), "mod");
            File.WriteAllText(Path.Combine(game, "loader.dll"), "loader");
            var owned = new HashSet<string>(["data/base.esm", "DATA/DLC.ESM"], StringComparer.OrdinalIgnoreCase);
            if (Mo2ExternalFiles.Normalize("Data\\DLC.esm") != "Data/DLC.esm") throw new Exception("Path normalization failed");
            foreach (var invalid in new[] { "../outside", "/absolute", "C:/absolute", "Data/../outside", "Data//file" }) {
                try { Mo2ExternalFiles.Normalize(invalid); throw new Exception("Invalid manifest path accepted"); }
                catch (InvalidDataException) { }
            }
            var files = Mo2ExternalFiles.FindExtras(game, owned, default);
            if (!files.Select(x => x.Path).SequenceEqual(new[] { "Data/extra.esp", "loader.dll" }))
                throw new Exception("Steam game or DLC file incorrectly classified");
            if (OperatingSystem.IsLinux()) {
                var outside = Path.Combine(root, "outside"); Directory.CreateDirectory(outside);
                File.WriteAllText(Path.Combine(outside, "must-not-follow"), "fixture");
                Directory.CreateSymbolicLink(Path.Combine(game, "linked"), outside);
                files = Mo2ExternalFiles.FindExtras(game, owned, default);
                if (files.Length != 3 || files.Single(x => x.Path == "linked").Kind != "External link")
                    throw new Exception("External symlink followed or omitted");
            }
            var acf = Path.Combine(root, "steamapps/appmanifest_1.acf");
            File.WriteAllText(acf, "\"AppState\" { \"appid\" \"1\" \"installdir\" \"Test Game\" \"InstalledDepots\" { \"4294967294\" { \"manifest\" \"1\" } } }");
            foreach (var gamePath in new[] { game, game + Path.DirectorySeparatorChar }) {
                try { Mo2ExternalFiles.Scan(gamePath, "1"); throw new Exception("Missing manifest classified files"); }
                catch (FileNotFoundException error) when (error.Message.Contains("not cached")) { }
            }
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { Mo2ExternalFiles.FindExtras(game, owned, cancellation.Token); throw new Exception("Scan ignored cancellation"); }
            catch (OperationCanceledException) { }
            Mo2ExternalFile[] treeFiles = [new("root.dll", 10, "External file"),
                new("Data/NVSE/Plugins/plugin.dll", 20, "External file"),
                new("Data/extra.esp", 30, "External file"), new("linked", 0, "External link")];
            var tree = Mo2ExternalFileNode.Build(treeFiles, "");
            if (tree[0].Path != "Data" || !tree[0].IsExpanded || tree[0].Children[0].Name != "NVSE" ||
                tree[0].Children[0].IsExpanded || tree.Single(node => node.Path == "linked").IsFolder)
                throw new Exception("External folder ordering, expansion or link classification failed");
            var leaves = tree.SelectMany(node => node.DescendantsAndSelf()).Where(node => !node.IsFolder).Select(node => node.File).ToHashSet();
            if (!leaves.SetEquals(treeFiles)) throw new Exception("External tree lost or duplicated files");
            var search = Mo2ExternalFileNode.Build(treeFiles, "PLUGIN.DLL");
            if (search.Length != 1 || search[0].DescendantsAndSelf().Count() != 4 ||
                search[0].DescendantsAndSelf().Any(node => node.IsFolder && !node.IsExpanded))
                throw new Exception("External search failed to retain and expand ancestors");
            if (Mo2ExternalFileNode.Build(treeFiles, "no match").Length != 0 ||
                Mo2ExternalFileNode.Build(treeFiles, "", new Dictionary<string, bool> { ["Data"] = false })[0].IsExpanded)
                throw new Exception("External search empty result or expansion restoration failed");
            if (OperatingSystem.IsLinux()) {
                var physical = Path.Combine(root, "physical-names");
                Directory.CreateDirectory(Path.Combine(physical, "Data/owned"));
                File.WriteAllText(Path.Combine(physical, "Data/owned/same.esp"), "Steam-owned");
                var unusual = new[] { "Data/owned\\same.esp", "Data/mixed\\folder/file.txt", "Data/note:backup.txt" };
                foreach (var name in unusual) {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(physical, name))!);
                    File.WriteAllText(Path.Combine(physical, name), "external");
                }
                var physicalFiles = Mo2ExternalFiles.FindExtras(physical,
                    new HashSet<string>(["Data/owned/same.esp"], StringComparer.OrdinalIgnoreCase), default);
                if (!physicalFiles.Select(file => file.Path).ToHashSet().SetEquals(unusual))
                    throw new Exception("Physical filename lost during Steam ownership classification");
                var physicalTree = Mo2ExternalFileNode.Build(physicalFiles, "");
                var data = physicalTree.Single(node => node.Path == "Data");
                var folder = data.Children.Single(node => node.Path == "Data/mixed\\folder");
                if (!folder.IsFolder || folder.Name != "mixed\\folder" || folder.Children.Single().Path != unusual[1] ||
                    data.Children.Single(node => node.Path == unusual[0]).Name != "owned\\same.esp" ||
                    data.Children.Single(node => node.Path == unusual[2]).Name != "note:backup.txt")
                    throw new Exception("External tree changed a physical filename or folder path");
                var physicalDownloads = Path.Combine(root, "physical-downloads"); Directory.CreateDirectory(physicalDownloads);
                var physicalScan = new Mo2ExternalScan(physical, 1, 1, physicalFiles);
                foreach (var selection in unusual.Append("Data")) {
                    try { Mo2ExternalArchive.CreateCopy(physicalScan, selection, physicalDownloads, default); throw new Exception("Unrepresentable physical name was imported"); }
                    catch (InvalidDataException) { }
                }
                if (Directory.GetFileSystemEntries(physicalDownloads).Length != 0 ||
                    unusual.Any(name => File.ReadAllText(Path.Combine(physical, name)) != "external") ||
                    File.ReadAllText(Path.Combine(physical, "Data/owned/same.esp")) != "Steam-owned")
                    throw new Exception("Rejected physical-name import changed files or left an archive");
                Console.WriteLine("PASS physical names: literal backslashes and colons retained in ownership/tree paths; incompatible imports rejected without changing sources");
            }
            var downloads = Path.Combine(root, "downloads"); Directory.CreateDirectory(downloads);
            var scan = new Mo2ExternalScan(game, 1, owned.Count, files);
            var copy = Mo2ExternalArchive.CreateCopy(scan, "Data", downloads, default);
            var archive = copy.Archive;
            using (var zip = System.IO.Compression.ZipFile.OpenRead(archive)) {
                if (zip.Entries.Count != 1 || zip.Entries[0].FullName != "extra.esp")
                    throw new Exception("Import archive included Steam files or incorrect Data nesting");
                using var reader = new StreamReader(zip.Entries[0].Open());
                if (reader.ReadToEnd() != "mod" || File.ReadAllText(Path.Combine(game, "Data/extra.esp")) != "mod")
                    throw new Exception("Import copy changed source bytes");
            }
            var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("mod")));
            if (copy.GameDirectory != game || copy.Files.Length != 1 ||
                copy.Files[0] != new Mo2ExternalArchivedFile("Data/extra.esp", "extra.esp", 3, expectedHash))
                throw new Exception("Import verification record does not describe the bytes copied");
            if (Mo2ExternalArchive.ChangedOriginals(copy, default).Length != 0) throw new Exception("Unchanged original rejected");
            var original = Path.Combine(game, "Data/extra.esp");
            File.WriteAllText(original, "new");
            if (!Mo2ExternalArchive.ChangedOriginals(copy, default).SequenceEqual(new[] { "Data/extra.esp" }))
                throw new Exception("Same-length source edit was not detected");
            File.Delete(original);
            if (Mo2ExternalArchive.ChangedOriginals(copy, default).Length != 1) throw new Exception("Missing original was not detected");
            File.WriteAllText(original, "mod");
            var installedDirectory = Path.Combine(root, "installed"); Directory.CreateDirectory(installedDirectory);
            var installedFile = Path.Combine(installedDirectory, "Extra.ESP"); File.WriteAllText(installedFile, "mod");
            if (Mo2ExternalArchive.ChangedInstalledFiles(copy, installedDirectory, default).Length != 0)
                throw new Exception("Matching installed bytes with different filename casing were rejected");
            var backup = Mo2ExternalBackup.Move(copy, installedDirectory, scan, default);
            if (File.Exists(original) || Mo2ExternalBackup.Latest(game) != backup)
                throw new Exception("External cleanup did not move originals into a discoverable backup");
            File.WriteAllText(Path.Combine(backup, "keep.txt"), "unrelated note");
            File.WriteAllText(original, "replacement");
            try { Mo2ExternalBackup.Restore(game, backup, default); throw new Exception("Restore overwrote a new game file"); }
            catch (IOException) { }
            if (File.ReadAllText(original) != "replacement" || !Directory.Exists(backup))
                throw new Exception("Restore conflict damaged the new file or backup");
            File.Delete(original);
            if (Mo2ExternalBackup.Restore(game, backup, default) != 1 || File.ReadAllText(original) != "mod" || Mo2ExternalBackup.Latest(game) is not null)
                throw new Exception("External backup did not restore exact originals");
            if (File.ReadAllText(Path.Combine(backup, "keep.txt")) != "unrelated note")
                throw new Exception("Restore cleanup removed an unrelated backup file");
            File.WriteAllText(original, "new");
            try { Mo2ExternalBackup.Move(copy, installedDirectory, scan, default); throw new Exception("Cleanup moved a changed original"); }
            catch (InvalidOperationException) { }
            File.WriteAllText(original, "mod");
            try { Mo2ExternalBackup.Move(copy, installedDirectory, scan, cancellation.Token); throw new Exception("Cleanup ignored cancellation"); }
            catch (OperationCanceledException) { }
            File.WriteAllText(installedFile, "new");
            if (Mo2ExternalArchive.ChangedInstalledFiles(copy, installedDirectory, default).Length != 1)
                throw new Exception("Modified installed bytes were accepted");
            File.Delete(installedFile);
            if (Mo2ExternalArchive.ChangedInstalledFiles(copy, installedDirectory, default).Length != 1)
                throw new Exception("Missing installed file was accepted");
            if (OperatingSystem.IsLinux()) {
                File.CreateSymbolicLink(installedFile, original);
                if (Mo2ExternalArchive.ChangedInstalledFiles(copy, installedDirectory, default).Length != 1)
                    throw new Exception("Linked installed file was accepted");
                File.Delete(installedFile); File.WriteAllText(installedFile, "mod");
                File.WriteAllText(Path.Combine(installedDirectory, "extra.esp"), "mod");
                if (Mo2ExternalArchive.ChangedInstalledFiles(copy, installedDirectory, default).Length != 1)
                    throw new Exception("Ambiguous installed filename was accepted");
            }
            try { Mo2ExternalArchive.ChangedOriginals(copy, cancellation.Token); throw new Exception("Source verification ignored cancellation"); }
            catch (OperationCanceledException) { }
            try { Mo2ExternalArchive.Create(scan, "loader.dll", downloads, default); throw new Exception("Root file imported as Data mod"); }
            catch (InvalidOperationException) { }
            try { Mo2ExternalArchive.Create(scan, "Data", downloads, cancellation.Token); throw new Exception("Import ignored cancellation"); }
            catch (OperationCanceledException) { }
            foreach (var conflicting in new[] {
                new[] { "Data/Example.esp", "Data/example.esp" },
                new[] { "Data/Textures", "Data/textures/example.dds" },
                new[] { "Data/textures/example.dds", "Data/Textures" }
            }) {
                var ambiguous = new Mo2ExternalScan(game, 1, 2,
                    conflicting.Select(path => new Mo2ExternalFile(path, 1, "External file")).ToArray());
                try { Mo2ExternalArchive.Create(ambiguous, "Data", downloads, default); throw new Exception("Ambiguous Windows archive paths accepted"); }
                catch (InvalidDataException error) when (error.Message.Contains("Windows")) { }
            }
            if (OperatingSystem.IsLinux()) {
                var link = Path.Combine(game, "Data/linked.esp"); File.CreateSymbolicLink(link, Path.Combine(game, "Data/extra.esp"));
                var stale = new Mo2ExternalScan(game, 1, 2, [new("Data/linked.esp", 3, "External file")]);
                try { Mo2ExternalArchive.Create(stale, "Data", downloads, default); throw new Exception("Import followed a replaced link"); }
                catch (IOException) { }
            }
            foreach (var invalidName in new[] { "NUL.txt", "con", "aux.esp", "PRN/log.txt", "COM1.ini",
                "lpt9.dds", "COM¹.txt", "LPT²/file.txt", "CONIN$", "CONOUT$", "NUL .txt",
                "trailing. ", "trailing.", "folder /file.esp", "bad?.esp", "bad*.esp", "bad|name",
                "bad<name", "bad>name", "bad\"name", "control\u0001.txt" }) {
                var invalidScan = new Mo2ExternalScan(game, 1, 1, [new("Data/" + invalidName, 1, "External file")]);
                try { Mo2ExternalArchive.Create(invalidScan, "Data", downloads, default); throw new Exception("Unrepresentable Windows name accepted"); }
                catch (InvalidDataException error) when (error.Message.Contains("Windows installer")) { }
            }
            foreach (var validName in new[] { "Textures/normal.dds", "CONifer.esp", "COM10.txt", "LPT0.txt",
                "my mod (1).esp", ".hidden", "café/雪.dds" }) Mo2ExternalArchive.ValidateWindowsName(validName);
            if (Directory.GetFiles(downloads).Length != 1 || Directory.GetDirectories(downloads).Length != 0)
                throw new Exception("Failed import left staging files or published a partial archive");
            Console.WriteLine("PASS external files: game/DLC ownership, case and slash normalization, extras, symlinks, invalid paths, missing manifests and cancellation");
            Console.WriteLine("PASS external file tree: folders first, exact file preservation, links remain leaves, search ancestors and expansion restoration");
            Console.WriteLine("PASS external import archive: only extra Data files, correct relative paths and bytes, originals retained, root files rejected, cancellation/link failure cleaned");
            Console.WriteLine("PASS external import Windows paths: case collisions and file/folder collisions rejected before reading sources or publishing archives");
            Console.WriteLine("PASS external import Windows names: devices, invalid characters and trailing dots/spaces rejected before archive creation; ordinary and Unicode names accepted");
            Console.WriteLine("PASS external import copy record: source path, archive path, byte count and SHA-256 match the copied entry");
            Console.WriteLine("PASS external import source verification: unchanged, same-length edit, missing file and cancellation");
            Console.WriteLine("PASS installed-copy verification: matching bytes with Windows casing, modified/missing files, links and ambiguous names");
            Console.WriteLine("PASS external backup: move, persistent discovery, no-overwrite restore conflict, exact restoration, changed originals and cancellation");
        } finally { Directory.Delete(root, recursive: true); }
    }
}
