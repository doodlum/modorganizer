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
    internal static void Attach<T>(TreeDataGrid table, Func<T?, IEnumerable<object?>> build, bool allowEmpty = false,
        Func<T?, Task<Mo2MenuEntry[]?>>? availability = null) where T : class
    {
        var menu = new ContextMenu();
        table.ContextMenu = menu;
        // Select the pointed-at row only for an actual right-button press.
        // Keyboard/programmatic opening must keep the current selection even
        // when the stationary pointer happens to be over another row.
        table.AddHandler(InputElement.PointerPressedEvent, (_, args) => {
            if (!args.GetCurrentPoint(table).Properties.IsRightButtonPressed || args.Source is not Control source) return;
            var row = source as TreeDataGridRow ?? source.GetVisualAncestors().OfType<TreeDataGridRow>().FirstOrDefault();
            if (row is { IsSelected: false } && row.RowIndex >= 0 && row.RowIndex < (table.Rows?.Count ?? 0))
                SelectRow(table, row.RowIndex);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
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
        long version = 0;
        menu.Closed += (_, _) => ++version;
        menu.Opened += async (_, _) => {
            Fill();
            var request = ++version;
            if (availability is null) return;
            var row = Target<T>(table);
            var items = menu.Items.OfType<MenuItem>().Select(item => (Item: item, Enabled: item.IsEnabled)).ToArray();
            try {
                var pending = availability(row);
                if (!pending.IsCompleted) foreach (var (item, _) in items) item.IsEnabled = false;
                var native = await pending;
                if (!menu.IsOpen || request != version || !ReferenceEquals(row, Target<T>(table))) return;
                foreach (var (item, enabled) in items) {
                    var matching = native?.FirstOrDefault(x => x.Text == item.Header as string);
                    item.IsEnabled = enabled && (native is null || matching?.Enabled == true);
                }
            } catch (Exception error) {
                if (!menu.IsOpen || request != version) return;
                foreach (var (item, _) in items) {
                    item.IsEnabled = false;
                    ToolTip.SetTip(item, "Could not read MO2 action availability: " + error.Message);
                }
            }
        };
    }

    private static T? Target<T>(TreeDataGrid table) where T : class =>
        table.RowSelection?.SelectedItem as T;

    // Visual row offsets are not model paths once a Data folder is expanded.
    internal static void SelectRow(TreeDataGrid table, int rowIndex)
    {
        table.RowSelection?.Clear();
        table.RowSelection?.Select(table.Rows!.RowIndexToModelIndex(rowIndex));
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
