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
        // Other checks drive this same table from the same window opening.
        using var turn = await Mo2CheckTurn.Take();
        for (var attempt = 0; attempt < 80 && view.ViewModel?.Adapter.Source.Value.Items.Count() is null or 0; attempt++)
            await Task.Delay(100);

        // The group that is actually on the toolbar, found the way anything else would
        // find it. Looking it up through the native view's name scope returns the one
        // the original markup declared, which is now emptied into the shared group and
        // never shown — so this read as passing while checking a control nobody sees.
        var group = view.GetVisualDescendants().OfType<Panel>().FirstOrDefault(x => x.Name == "ContextControlGroup")
            ?? throw new Exception("No selection group in the Mods toolbar");
        var deselect = view.NativeView.FindControl<NexusMods.App.UI.Controls.StandardButton>("DeselectItemsButton")
            ?? throw new Exception("Selection group has no deselect action");
        if (!ReferenceEquals(deselect.Parent, group))
            throw new Exception("The deselect action is not in the group that is shown");
        if (view.GetVisualDescendants().OfType<Border>().Any(x => x.Name == "ModSelectionGroup"))
            throw new Exception("A second selection group is still on the toolbar");
        if (view.GetVisualDescendants().OfType<Control>().Count(x => x.Name == "ContextControlGroup") != 1)
            throw new Exception("More than one selection group is on the page");
        if (view.ViewModel!.Adapter.Source.Value.Selection is not TreeDataGridRowSelectionModel<NexusMods.App.UI.Controls.CompositeItemModel<EntityId>> selection)
            throw new Exception("Mods table has no row selection model");

        selection.Clear();
        await Task.Delay(400);
        if (group.IsVisible) throw new Exception("Selection group is showing with nothing selected");

        selection.Select(new Avalonia.Controls.IndexPath(0));
        await Task.Delay(400);
        if (!group.IsVisible) throw new Exception("Selection group stayed hidden after selecting a row");
        if (deselect.Text?.Contains('1') != true) throw new Exception("Deselect action read \"" + deselect.Text + "\" for one row");

        selection.Select(new Avalonia.Controls.IndexPath(1));
        selection.Select(new Avalonia.Controls.IndexPath(2));
        await Task.Delay(400);
        var selected = view.ViewModel.Adapter.SelectedModels.Count;
        if (deselect.Text?.Contains(selected.ToString()) != true)
            throw new Exception($"Deselect action read \"{deselect.Text}\" for {selected} rows");

        deselect.Command?.Execute(null);
        await Task.Delay(500);
        if (view.ViewModel.Adapter.SelectedModels.Count != 0) throw new Exception("Clear action left rows selected");
        if (group.IsVisible) throw new Exception("Selection group stayed visible after clearing");

        Console.WriteLine($"PASS mods selection group: the native group and no second one beside it, hidden with " +
            $"nothing selected, reading 1 for one row and {selected} for {selected}, and its deselect action emptied " +
            "the selection and hid the group");
    }
}
