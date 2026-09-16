using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// A right-click menu on a table's rows. MO2 works every one of its lists this way —
// the downloads, the saves, the archives and the data tree all carry a menu and
// nothing else — so a page without one is a page whose actions are only reachable
// if the person knows to look at the header line.
//
// The mod and plugin rows build their own controls and so hang a flyout on the row
// itself (Mo2EntryMenu). These tables are drawn by TreeDataGrid from a column
// template, so the menu is attached to the table and the row is resolved when the
// menu is asked for — which is also what keeps it right after rows recycle.
internal static class Mo2RowMenu
{
    // Attach to a table whose rows carry a model of the given type. `build` is asked
    // for the menu each time it opens, against the row that was right-clicked, so a
    // menu never shows the actions of the row it was last opened on.
    //
    // MO2 right-clicking an unselected row acts on that row; this selects it first,
    // for the same reason.
    // `build` may return nulls and stray separators — the entries around them are
    // conditional in MO2 too — and Tidy takes them out on the way into the menu.
    // `allowEmpty` is for a table whose menu has entries even with no row under the
    // pointer: MO2's download menu keeps its whole-folder actions in that case.
    internal static void Attach<T>(TreeDataGrid table, Func<T?, IEnumerable<object?>> build, bool allowEmpty = false) where T : class
    {
        var menu = new ContextMenu();
        table.ContextMenu = menu;
        bool Fill()
        {
            menu.Items.Clear();
            var row = Target<T>(table);
            if (row is null && !allowEmpty) return false;
            foreach (var item in Tidy(build(row))) menu.Items.Add(item);
            return menu.Items.Count > 0;
        }
        menu.Opening += (_, args) => { if (!Fill()) args.Cancel = true; };
        // Filled again once it is up, so a menu opened by anything other than the
        // pointer — the keyboard's menu key, or a check driving the page — holds the
        // same entries a right-click would put there.
        menu.Opened += (_, _) => { if (menu.Items.Count == 0) Fill(); };
    }

    // Which row the menu belongs to: the one under the pointer, selected first so the
    // menu and the list agree about what is being acted on, falling back to whatever
    // is already selected when the press landed below the last row.
    private static T? Target<T>(TreeDataGrid table) where T : class
    {
        var pointer = table.GetVisualDescendants().OfType<TreeDataGridRow>()
            .FirstOrDefault(row => row.IsPointerOver && row.RowIndex >= 0 && row.RowIndex < (table.Rows?.Count ?? 0));
        if (pointer is not null && table.Rows![pointer.RowIndex].Model is T model) {
            if (!pointer.IsSelected) table.RowSelection?.Select(new IndexPath(pointer.RowIndex));
            return model;
        }
        return table.RowSelection?.SelectedItem as T;
    }

    // MO2 separates its download menu's row actions from its whole-list ones, and its
    // mod menu's in several places. A separator with nothing after it, or two in a
    // row, is what happens when the entries around it are conditional, so they are
    // dropped here rather than guarded at every call site.
    internal static IEnumerable<object> Tidy(IEnumerable<object?> items)
    {
        var pending = false; var any = false;
        foreach (var item in items) {
            if (item is null) continue;
            if (item is Separator) { pending = any; continue; }
            if (pending) { yield return new Separator(); pending = false; }
            any = true;
            yield return item;
        }
    }
}
