using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.MnemonicDB.Abstractions;

namespace Mo2.Frontend;

// What selecting mods does, driven through the real selection model.
//
// This checked the Mods toolbar's selection group — hidden with nothing selected,
// counting rows once there were some, gone again after its clear action. That
// group went when the mod pane was cut back to MO2's widgets, because MO2 has no
// equivalent: MO2 works a selection from the row's own right-click menu, and so
// does this list. The check was left pointing at it and failed on the first line
// of every run, which nothing saw, because no sweep had ever named it.
//
// Its subject was never the group. It is the selection, which this still drives —
// and it now fails if the group comes back, which is the same rule the other
// checks over removed controls were put back under.
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

        // No selection group, and nothing of one left behind. MO2 draws none, so a
        // page that grows one back has grown a control MO2 has not got.
        foreach (var name in new[] { "ContextControlGroup", "AdoptedContextControlGroup", "ModSelectionGroup", "DeselectItemsButton" })
            if (view.GetVisualDescendants().OfType<Control>().Any(x => x.Name == name))
                throw new Exception($"{name} is drawn, and MO2 has no selection group — a selection is worked from the row's menu");
        if (view.ViewModel!.Adapter.Source.Value.Selection is not TreeDataGridRowSelectionModel<NexusMods.App.UI.Controls.CompositeItemModel<EntityId>> selection)
            throw new Exception("Mods table has no row selection model");

        selection.Clear();
        await Task.Delay(400);
        if (view.ViewModel.Adapter.SelectedModels.Count != 0) throw new Exception("Clearing left rows selected");

        selection.Select(new Avalonia.Controls.IndexPath(0));
        await Task.Delay(400);
        if (view.ViewModel.Adapter.SelectedModels.Count != 1)
            throw new Exception($"Selecting one row left {view.ViewModel.Adapter.SelectedModels.Count} selected");

        selection.Select(new Avalonia.Controls.IndexPath(1));
        selection.Select(new Avalonia.Controls.IndexPath(2));
        await Task.Delay(400);
        var selected = view.ViewModel.Adapter.SelectedModels.Count;
        if (selected != 3) throw new Exception($"Selecting three rows left {selected} selected");

        selection.Clear();
        await Task.Delay(400);
        if (view.ViewModel.Adapter.SelectedModels.Count != 0) throw new Exception("Clearing left rows selected");

        Console.WriteLine("PASS mods selection: the table's own selection model took one row and then three, and " +
            "clearing emptied it, with no selection group anywhere on the page — MO2 draws none, and works a " +
            "selection from the row's own menu");
    }
}
