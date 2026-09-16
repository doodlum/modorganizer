using Avalonia.Controls;
using Avalonia.Input;
using System.Reactive.Linq;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// A row's menu is built when it is first opened, and not again.
//
// Every mod and plugin row carries MO2's own menu — thirty-three entries on a mod,
// nine on a plugin — and building that for every row of a list as the rows are
// realised would be paid for on every scroll. Mo2EntryMenu.Flyout builds on the
// first Opening and keeps what it built, so this checks both halves: nothing before
// it is opened, and the same objects when it is opened again.
//
// What the menus hold is not checked here. MO2_VERIFY_ROW_MENUS asks MO2 to build
// its own menu for the same row and compares entry for entry, both ways round,
// which is a better answer than any count written down here — and this check used
// to insist on exactly five items and a ContextMenu on the row, both of which the
// row-menu work replaced. It had been failing on "Mod row not attached" ever since,
// because the handle it looked for belonged to that older shape.
internal static class Mo2EntryMenuCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        var layout = Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT");
        if (layout is null || !Path.GetFullPath(layout).StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
            throw new Exception("Menu verification requires a temporary layout");
        var described = new List<string>();
        foreach (var (kind, rowName) in new[] { ("Mod", "ModRedesignRow"), ("Plugin", "PluginRedesignRow") }) {
            var item = kind == "Mod" ? shell.ProfileMenu.LeftMenuItemLoadout : shell.ProfileMenu.LeftMenuItemExternalChanges!;
            await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
                item.NavigateCommand.Execute(NexusMods.App.UI.Controls.Navigation.NavigationInformation.From(NexusMods.App.UI.WorkspaceSystem.NavigationInput.Default)));
            // The rows arrive with the profile rather than with the window, and this
            // runs a few seconds after it opens, so it waits for one rather than
            // assuming the list has filled.
            var until = DateTime.UtcNow.AddSeconds(60);
            Control? row;
            while ((row = window.GetVisualDescendants().OfType<Control>()
                       .FirstOrDefault(x => x.Name == rowName && x.ContextFlyout is MenuFlyout)) is null) {
                if (DateTime.UtcNow > until) throw new Exception(kind + " row never drew with a menu on it");
                await Task.Delay(50);
                window.UpdateLayout();
            }
            var menu = (MenuFlyout)row.ContextFlyout!;
            if (menu.Items.Count != 0) throw new Exception($"An unopened {kind.ToLowerInvariant()} menu had already built its entries");
            menu.ShowAt(row);
            await Task.Delay(150);
            var built = menu.Items.Count;
            if (built == 0) throw new Exception($"Opening a {kind.ToLowerInvariant()} menu built nothing");
            var first = menu.Items[0];
            menu.Hide();
            await Task.Delay(100);
            menu.ShowAt(row);
            await Task.Delay(150);
            if (menu.Items.Count != built || !ReferenceEquals(first, menu.Items[0]))
                throw new Exception($"Reopening a {kind.ToLowerInvariant()} menu built its entries again");
            menu.Hide();
            described.Add($"{kind.ToLowerInvariant()} rows build {built} entries on first open and reuse them");

            if (kind != "Mod") continue;
            // The endorsement icons, which the row draws from what MO2 reports. An
            // identical snapshot deliberately publishes no change, so the real
            // subscriptions are exercised without touching native profile state.
            var icons = window.GetVisualDescendants().OfType<NexusMods.UI.Sdk.Icons.UnifiedIcon>()
                .Where(icon => icon.Name == "ModEndorsementIcon")
                .Select(icon => (Icon: icon, Content: icon.Content)).ToArray();
            if (icons.Length == 0) throw new Exception("No endorsement icons to verify");
            var changed = typeof(Mo2LiveProfile).GetField("Changed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(shell.Profile) as Action ?? throw new Exception("Missing profile subscribers");
            changed();
            if (icons.Any(pair => pair.Icon.GetVisualRoot() is null || !ReferenceEquals(pair.Content, pair.Icon.Content)))
                throw new Exception("Unchanged endorsement icon was recreated during refresh");
            described.Add($"{icons.Length} endorsement icons kept what they had drawn across a refresh");
        }
        Console.WriteLine("PASS lazy entry menus: " + string.Join(", ", described));
    }
}
