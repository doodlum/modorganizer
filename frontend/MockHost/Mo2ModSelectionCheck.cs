using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.MnemonicDB.Abstractions;

namespace Mo2.Frontend;

// Opt-in check for the Mods toolbar's selection group: hidden with nothing
// selected, showing an accurate count once rows are, and gone again after the
// clear action. Selection is driven through the real selection model, so this
// exercises the same path a click takes.
internal static class Mo2ModSelectionCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        // The page only exists once the bridge has connected and the workspace has
        // built its body, which takes considerably longer than the window opening.
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();
        for (var attempt = 0; attempt < 300 && !(live.ModsPage?.Adapter.SourceCount.Value > 0); attempt++) {
            await Task.Delay(100);
            if (attempt % 50 == 0)
                Console.WriteLine($"CHECK mods selection: connected={live.Profile.IsConnected} " +
                    $"profile={(live.Profile.ProfilePath.Length > 0)} mods={live.Profile.Mods.Count} " +
                    $"page={(live.ModsPage is null ? "null" : "present")} " +
                    $"rows={live.ModsPage?.Adapter.SourceCount.Value.ToString() ?? "-"} " +
                    $"views={window.GetVisualDescendants().OfType<Mo2ModsView>().Count()}");
        }
        if (!(live.ModsPage?.Adapter.SourceCount.Value > 0))
            throw new Exception("Mods page model never populated");
        // The one on screen, not merely the first in the tree: a retained workspace
        // can hold a second Mods body, and reading that one reports a page with no
        // toolbar at all.
        Mo2ModsView? view = null;
        for (var attempt = 0; attempt < 300 && view is null; attempt++) {
            await Task.Delay(100);
            view = window.GetVisualDescendants().OfType<Mo2ModsView>().FirstOrDefault(x => x.IsEffectivelyVisible)
                ?? window.GetVisualDescendants().OfType<Mo2ModsView>().FirstOrDefault();
        }
        if (view is null) throw new Exception("Mods page did not open");
        for (var attempt = 0; attempt < 80 && view.ViewModel?.Adapter.Source.Value.Items.Count() is null or 0; attempt++)
            await Task.Delay(100);

        var group = view.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == "ModSelectionGroup")
            ?? throw new Exception("No selection group in the Mods toolbar");
        var count = view.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(x => x.Name == "ModSelectionCount")
            ?? throw new Exception("Selection group has no count");
        if (view.ViewModel!.Adapter.Source.Value.Selection is not TreeDataGridRowSelectionModel<NexusMods.App.UI.Controls.CompositeItemModel<EntityId>> selection)
            throw new Exception("Mods table has no row selection model");

        selection.Clear();
        await Task.Delay(400);
        if (group.IsVisible) throw new Exception("Selection group is showing with nothing selected");

        selection.Select(new Avalonia.Controls.IndexPath(0));
        await Task.Delay(400);
        if (!group.IsVisible) throw new Exception("Selection group stayed hidden after selecting a row");
        if (count.Text != "1 selected") throw new Exception("Count read \"" + count.Text + "\" for one row");

        selection.Select(new Avalonia.Controls.IndexPath(1));
        selection.Select(new Avalonia.Controls.IndexPath(2));
        await Task.Delay(400);
        var selected = view.ViewModel.Adapter.SelectedModels.Count;
        if (count.Text != selected + " selected") throw new Exception($"Count read \"{count.Text}\" for {selected} rows");

        var clear = group.GetVisualDescendants().OfType<Button>().FirstOrDefault()
            ?? throw new Exception("Selection group has no clear action");
        clear.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(500);
        if (view.ViewModel.Adapter.SelectedModels.Count != 0) throw new Exception("Clear action left rows selected");
        if (group.IsVisible) throw new Exception("Selection group stayed visible after clearing");

        Console.WriteLine($"PASS mods selection group: hidden with no selection, \"1 selected\" for one row, " +
            $"\"{selected} selected\" for {selected}, and the clear action emptied the selection and hid the group");
    }
}
