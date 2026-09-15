using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// How much of each list the window actually shows, measured where it shows it.
//
// MO2 fits about forty rows in a window this size; the theme this frontend borrows
// draws cards, and every page inherited them. Row heights are set in four places —
// the theme, the deferred placeholder, the row's own content and the page's
// headings — so a page can be given the shorter row and still draw the tall one,
// which is exactly what happened and what nothing was measuring.
internal static class Mo2DensityCheck
{
    // A row is allowed to be a little taller than the metric — a focus ring, a
    // border — but not half as tall again.
    private const double Slack = 4;

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();

        var menu = live.ProfileMenu;
        var pages = new (string Name, Func<Task> Open)[] {
            ("my-mods", () => Navigate(menu.LeftMenuItemLoadout)),
            ("plugins", () => Navigate(menu.LeftMenuItemExternalChanges!)),
            ("archives", () => Navigate(menu.ArchivesItem)),
            ("data", () => Navigate(menu.DataItem)),
            ("saves", () => Navigate(menu.SavesItem)),
            ("overwrite", () => Navigate(menu.OverwriteItem)),
            ("external-files", () => Navigate(menu.ExternalFilesItem)),
            ("downloads", () => Navigate(menu.LeftMenuItemLibrary)),
            ("tools", () => Navigate(menu.ToolsItem)),
            ("logs", () => Navigate(menu.LogsItem)),
        };

        using var turn = await Mo2CheckTurn.Take();
        var panels = live.WorkspaceController.ActiveWorkspace.Panels;
        var selected = panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
        var restore = selected?.SelectedTab.Contents.ViewModel is ScenarioLoadOrderPage
            ? menu.LeftMenuItemExternalChanges! : menu.LeftMenuItemLoadout;

        var faults = new List<string>();
        var measured = new List<string>();
        foreach (var (name, open) in pages) {
            await open();
            double tallest = 0;
            var rows = 0;
            for (var attempt = 0; attempt < 60; attempt++) {
                await Task.Delay(100);
                window.UpdateLayout();
                // Rows that are actually on screen in the panel that was navigated;
                // a table being rebuilt briefly has none, and one off screen has no
                // height to compare. Scoped to that panel: the window shows two at
                // once, so measuring the whole window reported the neighbouring Mods
                // list under every page's name.
                var drawn = Panel(live, window).GetVisualDescendants().OfType<TreeDataGridRow>()
                    .Where(x => x.IsEffectivelyVisible && x.Bounds.Height > 0).ToArray();
                if (drawn.Length == 0) continue;
                rows = drawn.Length;
                tallest = drawn.Max(x => x.Bounds.Height);
                if (Environment.GetEnvironmentVariable("MO2_DENSITY_DIAGNOSTIC") == "1" && attempt == 0) {
                    var row = drawn[0];
                    var height = Avalonia.Diagnostics.AvaloniaObjectExtensions.GetDiagnostic(row, Avalonia.Layout.Layoutable.HeightProperty);
                    var min = Avalonia.Diagnostics.AvaloniaObjectExtensions.GetDiagnostic(row, Avalonia.Layout.Layoutable.MinHeightProperty);
                    Console.WriteLine($"DENSITY {name}: height={height.Value} ({height.Priority}) min={min.Value} ({min.Priority}) " +
                        $"bounds={row.Bounds.Height} content={(row.GetVisualChildren().FirstOrDefault() as Control)?.Bounds.Height}");
                }
                if (tallest <= Mo2Density.Row + Slack) break;
            }
            if (rows == 0) { measured.Add($"{name} has no rows to measure"); continue; }
            measured.Add($"{name} {rows}×{tallest:F0}px");
            if (tallest > Mo2Density.Row + Slack)
                faults.Add($"{name} draws {tallest:F0}px rows, not MO2's {Mo2Density.Row}");
        }

        await Navigate(restore);
        await Task.Delay(600);

        if (faults.Count > 0) Console.WriteLine("FAIL MO2 density: " + string.Join("; ", faults) +
            " — measured " + string.Join(", ", measured));
        else Console.WriteLine($"PASS MO2 density: every page's rows are within {Slack}px of MO2's " +
            $"{Mo2Density.Row}px line — " + string.Join(", ", measured));

        // The panel the navigation went to, which is the one holding the page being
        // measured.
        static Avalonia.Visual Panel(Mo2LiveWorkspace live, Window window)
        {
            var panels = live.WorkspaceController.ActiveWorkspace.Panels;
            var chosen = panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
            return window.GetVisualDescendants().OfType<Mo2DeferredPanel>()
                .FirstOrDefault(x => ReferenceEquals(x.ViewModel, chosen)) ?? (Avalonia.Visual)window;
        }

        static async Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel? item)
        {
            if (item is null) return;
            await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
                item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
            await Task.Delay(300);
        }
    }
}
