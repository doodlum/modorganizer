using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2FilePageLifecycleCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!shell.Profile.IsConnected || shell.Profile.SelectingProfile) {
            if (DateTime.UtcNow > until) throw new Exception("Native profile did not connect");
            await Task.Delay(50);
        }
        await Mo2SaveSelectionCheck.Run(shell, window);
        await Mo2ArchiveSelectionCheck.Run(shell, window);
        var windows = new FixtureWindows { ActiveWindow = shell };
        foreach (var delayedFailure in new[] { false, true })
        foreach (var reopenBeforeCompletion in new[] { false, true }) {
            await Check<Mo2Archive>(window, read => new Mo2ArchivesView(read) {
                ViewModel = new Mo2ArchivesPage(windows, shell.Profile)
            }, new("old.bsa", "Fixture", true, false), new("fresh.bsa", "Fixture", true, false), reopenBeforeCompletion, delayedFailure);
            await Check<Mo2OverwriteFile>(window, read => new Mo2OverwriteView(read) {
                ViewModel = new Mo2OverwritePage(windows, shell.Profile)
            }, new("old.txt", 1), new("fresh.txt", 2), reopenBeforeCompletion, delayedFailure);
            await Check<Mo2Save>(window, read => new Mo2SavesView(read) {
                ViewModel = new Mo2SavesPage(windows, shell.Profile)
            }, new("Old", "old.fos"), new("Fresh", "fresh.fos"), reopenBeforeCompletion, delayedFailure);
            await Check<Mo2DataEntry>(window, read => new Mo2DataView((target, _) => read(target)) {
                ViewModel = new Mo2DataPage(windows, shell.Profile)
            }, new("old.txt", false, ["Fixture"], ""), new("fresh.txt", false, ["Fixture"], ""), reopenBeforeCompletion, delayedFailure);
        }
        Console.WriteLine("PASS file page lifecycle: detached results discarded; reopening before/after completion reads fresh Archives, Overwrite, Saves and Data after delayed success or failure");
        var target = shell.Profile.CurrentTarget;
        var archives = await shell.Profile.ReadArchives(target);
        var archivesRead = false;
        await CheckNative(window, new Mo2ArchivesView(async current => {
            var result = await shell.Profile.ReadArchives(current); archivesRead = true; return result;
        }) { ViewModel = new Mo2ArchivesPage(windows, shell.Profile) }, archives, () => archivesRead,
            "ArchivesStatus", text => text == "No archives reported by MO2." || text.EndsWith(" archives"));
        var overwrite = await shell.Profile.ReadOverwrite(target);
        var overwriteRead = false;
        await CheckNative(window, new Mo2OverwriteView(async current => {
            var result = await shell.Profile.ReadOverwrite(current); overwriteRead = true; return result;
        }) { ViewModel = new Mo2OverwritePage(windows, shell.Profile) }, overwrite, () => overwriteRead,
            "OverwriteStatus", text => text == "Overwrite is empty." || text.EndsWith("this game instance."));
        var saves = await shell.Profile.ReadSaves(target);
        var savesRead = false;
        await CheckNative(window, new Mo2SavesView(async current => {
            var result = await shell.Profile.ReadSaves(current); savesRead = true; return result;
        }) { ViewModel = new Mo2SavesPage(windows, shell.Profile) }, saves, () => savesRead,
            "SavesStatus", text => text == "No saves reported by MO2 for this profile." || text.EndsWith(" saves"));
        var data = await shell.Profile.ReadDataDirectory(target, "");
        var dataRead = false;
        await CheckNative(window, new Mo2DataView(async (current, directory) => {
            var result = await shell.Profile.ReadDataDirectory(current, directory); dataRead = true; return result;
        }) { ViewModel = new Mo2DataPage(windows, shell.Profile) }, data, () => dataRead,
            "DataStatus", text => text.EndsWith(" entries"), (a, b) => a.Name == b.Name && a.Directory == b.Directory &&
                a.Archive == b.Archive && a.Origins.SequenceEqual(b.Origins));
        if (target != shell.Profile.CurrentTarget) throw new Exception("Profile changed during native file page comparison");
        Console.WriteLine("PASS native Archives, Overwrite, Saves and Data tables match MO2 readback after lifecycle changes");
    }

    private static async Task CheckNative<T>(Window window, Control view, T[] expected, Func<bool> readCompleted, string statusName, Func<string, bool> ready, Func<T, T, bool>? equal = null)
    {
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 600, Height = 400, IsHitTestVisible = false };
        root.Children.Add(host);
        try {
            host.Children.Add(view);
            var until = DateTime.UtcNow.AddSeconds(15);
            while (!readCompleted() || !view.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Name == statusName && ready(x.Text ?? ""))) {
                if (DateTime.UtcNow > until) throw new Exception("Native file page did not finish reading");
                await Task.Delay(50);
            }
            var rows = view.GetVisualDescendants().OfType<TreeDataGrid>().Single().Rows!;
            if (rows.Count != expected.Length || !Enumerable.Range(0, rows.Count).All(i =>
                (equal ?? EqualityComparer<T>.Default.Equals)((T)rows[i].Model!, expected[i])))
                throw new Exception($"Native {typeof(T).Name} page differs from MO2 readback: table {rows.Count}, expected {expected.Length}");
        } finally { root.Children.Remove(host); }
    }

    private static async Task Check<T>(Window window, Func<Func<Mo2ProfileTarget, Task<T[]>>, Control> create,
        T oldRow, T freshRow, bool reopenBeforeCompletion, bool delayedFailure) where T : class
    {
        var pending = new TaskCompletionSource<T[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var view = create(_ => ++calls == 1 ? pending.Task : Task.FromResult(new[] { freshRow }));
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 600, Height = 400, IsHitTestVisible = false };
        root.Children.Add(host);
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (!ready()) { if (DateTime.UtcNow > until) throw new Exception("File page lifecycle did not settle"); await Task.Delay(25); }
        }
        try {
            host.Children.Add(view);
            await Wait(() => calls == 1 && view.GetVisualDescendants().OfType<TreeDataGrid>().Any());
            var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            host.Children.Remove(view);
            if (reopenBeforeCompletion) host.Children.Add(view);
            const string staleError = "Delayed fixture read failed";
            if (delayedFailure) pending.SetException(new IOException(staleError));
            else pending.SetResult([oldRow]);
            if (!reopenBeforeCompletion) {
                await Task.Delay(150);
                if (calls != 1 || table.Rows?.Count > 0 || view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == staleError))
                    throw new Exception("Detached file page published its delayed result or started another read");
                host.Children.Add(view);
            }
            await Wait(() => calls >= 2 && table.Rows?.Count == 1);
            if (!ReferenceEquals(table.Rows![0].Model, freshRow))
                throw new Exception("Reopened file page kept the previous activation's result");
            if (view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == staleError))
                throw new Exception("Reopened file page displayed the previous activation's error");
        } finally { pending.TrySetResult([oldRow]); root.Children.Remove(host); }
    }
}
