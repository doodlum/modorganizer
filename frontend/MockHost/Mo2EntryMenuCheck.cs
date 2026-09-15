using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2EntryMenuCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        var layout = Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT");
        if (layout is null || !Path.GetFullPath(layout).StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
            throw new Exception("Menu verification requires a temporary layout");
        foreach (var kind in new[] { "Mod", "Plugin" }) {
            var item = kind == "Mod" ? shell.ProfileMenu.LeftMenuItemLoadout : shell.ProfileMenu.LeftMenuItemExternalChanges!;
            await item.NavigateCommand.Execute(NexusMods.App.UI.Controls.Navigation.NavigationInformation.From(NexusMods.App.UI.WorkspaceSystem.NavigationInput.Default));
            var until = DateTime.UtcNow.AddSeconds(30);
            Border? grip;
            while ((grip = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == kind + "DragHandle" && x.GetVisualAncestors().OfType<TreeDataGridRow>().FirstOrDefault()?.GetVisualDescendants().OfType<Button>().Any(button => button.Flyout is MenuFlyout) == true)) is null) {
                if (DateTime.UtcNow > until) throw new Exception(kind + " row not attached");
                await Task.Delay(50);
            }
            var row = grip.GetVisualAncestors().OfType<TreeDataGridRow>().First();
            if (kind == "Mod") {
                var icons = window.GetVisualDescendants().OfType<NexusMods.UI.Sdk.Icons.UnifiedIcon>()
                    .Where(icon => icon.Name == "ModEndorsementIcon")
                    .Select(icon => (Icon: icon, Content: icon.Content)).ToArray();
                if (icons.Length == 0) throw new Exception("No endorsement icons to verify");
                // Identical snapshots deliberately do not publish Changed. Exercise
                // the real subscriptions without changing native profile state.
                var changed = typeof(Mo2LiveProfile).GetField("Changed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(shell.Profile) as Action ?? throw new Exception("Missing profile subscribers");
                changed();
                if (icons.Any(pair => pair.Icon.GetVisualRoot() is null || !ReferenceEquals(pair.Content, pair.Icon.Content)))
                    throw new Exception("Unchanged endorsement icon was recreated during refresh");
                Console.WriteLine($"PASS endorsement icon refresh: {icons.Length} attached icons retained their rendered content after diagnostic Changed notification");
            }
            var menu = row.ContextMenu ?? throw new Exception("Missing context menu");
            if (menu.Items.Count != 0) throw new Exception("Unopened context menu constructed items eagerly");
            row.RaiseEvent(new ContextRequestedEventArgs()); await Task.Delay(100);
            if (!menu.IsOpen || menu.Items.Count != 5) throw new Exception("First context-menu open did not create actions");
            var first = menu.Items[0]; menu.Close(); row.RaiseEvent(new ContextRequestedEventArgs()); await Task.Delay(100);
            if (menu.Items.Count != 5 || !ReferenceEquals(first, menu.Items[0])) throw new Exception("Reopening recreated menu items");
            menu.Close();
            var button = row.GetVisualDescendants().OfType<Button>().First(x => x.Flyout is MenuFlyout);
            var flyout = (MenuFlyout)button.Flyout!;
            if (flyout.Items.Count != 0) throw new Exception("Unopened action flyout constructed items eagerly");
            flyout.ShowAt(button); await Task.Delay(100);
            if (!flyout.IsOpen || flyout.Items.Count != 5) throw new Exception("First action-flyout open did not create actions");
            flyout.Hide();
        }
        Console.WriteLine("PASS lazy entry menus: both lists open context menus and action flyouts, five actions each, items reused on reopen");
    }
}
