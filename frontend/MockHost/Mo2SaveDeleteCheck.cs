using Avalonia.Controls;
using Avalonia.VisualTree;
using System.Security.Cryptography;

namespace Mo2.Frontend;

// Requires an external observer to cancel this host's native confirmation.
// Never accepts deletion. Uses existing saves and verifies their bytes afterward.
internal static class Mo2SaveDeleteCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        var report = Environment.GetEnvironmentVariable("MO2_SAVE_CANCEL_REPORT");
        if (string.IsNullOrEmpty(report) || File.Exists(report))
            throw new Exception("Choose a new cancellation-observer report path");
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!shell.Profile.IsConnected || shell.Profile.SelectingProfile) {
            if (DateTime.UtcNow > deadline) throw new Exception("Native profile did not connect");
            await Task.Delay(50);
        }
        var profile = shell.Profile;
        var target = profile.CurrentTarget;
        var saves = await profile.ReadSaves(target);
        var directory = profile.SavesDirectory ?? Environment.GetEnvironmentVariable("MO2_SAVE_CHECK_DIRECTORY");
        if (saves.Length < 2 || directory is null || !Directory.Exists(directory))
            throw new Exception("This check requires two native saves and their directory");
        static Dictionary<string, string> Hashes(string directory) => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(directory, path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var before = await Task.Run(() => Hashes(directory));
        var native = await profile.ReadListMenu("readFileMenu", saves.Take(2).Select(save => save.File).ToArray(), target, "saves");
        if (!native.Any(entry => entry.Text == "Delete 2 save(s)" && entry.Enabled))
            throw new Exception("Native MO2 does not offer the expected two-save action");
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 600, Height = 400 };
        Grid.SetRowSpan(host, root.RowDefinitions.Count); Grid.SetColumnSpan(host, root.ColumnDefinitions.Count);
        var view = new Mo2SavesView { ViewModel = new Mo2SavesPage(new FixtureWindows { ActiveWindow = shell }, profile) };
        root.Children.Add(host); host.Children.Add(view);
        try {
            deadline = DateTime.UtcNow.AddSeconds(20);
            while (!view.GetVisualDescendants().OfType<TreeDataGrid>().Any()) {
                if (DateTime.UtcNow > deadline) throw new Exception("Native Saves view did not attach");
                await Task.Delay(50);
            }
            var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            while (table.Rows?.Count != saves.Length) {
                if (DateTime.UtcNow > deadline) throw new Exception("Native Saves view did not load");
                await Task.Delay(50);
            }
            table.RowSelection!.Select(new IndexPath(0)); table.RowSelection.Select(new IndexPath(1));
            if (table.RowSelection.SelectedItems.Count != 2) throw new Exception("Native Saves view lost multiple selection");
            Console.WriteLine("READY save delete cancellation: invoking frontend two-save action");
            await view.RunAction("delete");
            if (!File.Exists(report) || (await File.ReadAllTextAsync(report)).Trim() != "cancelled")
                throw new Exception("No independent native confirmation cancellation was observed");
            if (target != profile.CurrentTarget) throw new Exception("Profile changed during save action");
            var after = await Task.Run(() => Hashes(directory));
            if (before.Count != after.Count || before.Any(pair => !after.TryGetValue(pair.Key, out var hash) || hash != pair.Value))
                throw new Exception("Save directory changed during cancellation check");
            var restored = await profile.ReadSaves(target);
            if (!saves.Select(save => save.File).Order().SequenceEqual(restored.Select(save => save.File).Order()))
                throw new Exception("Native save list changed during cancellation check");
            Console.WriteLine($"PASS save delete cancellation: frontend multiple selection, native two-save action, {before.Count} files unchanged and native list preserved");
        } finally { root.Children.Remove(host); }
    }
}
