using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.Abstractions.Games;
using NexusMods.MnemonicDB.Abstractions;

namespace Mo2.Frontend;

internal static class Mo2RowHighlights
{
    private static readonly IBrush Linked = Brush.Parse("#66514385");
    // The colour a row takes when it belongs with what is selected in the other
    // table. Named so a check can look for exactly this rather than for "a purple
    // sort of colour", which also matched the grey a row goes under the pointer.
    internal static Color LinkedColour => ((ISolidColorBrush)Linked).Color;
    // Match MO2's selection-relative semantics: a threat to the selected mod
    // is red; a mod whose files the selection overwrites is green.
    private static readonly IBrush OverwritesSelection = Brush.Parse("#66893D43");
    private static readonly IBrush OverwrittenBySelection = Brush.Parse("#664F7D43");
    private static readonly IBrush Separator = Brushes.Transparent;
    // Keeping a table's highlights up to date: repaint when the profile says the
    // links changed, and again on every layout pass because rows are recycled as the
    // list scrolls and a recycled row arrives carrying the last row's colour. Both
    // lists wrote this out separately, with their own handlers to unsubscribe.
    public static IDisposable Attach(Control host, Control table, Mo2LiveProfile profile, bool plugins)
    {
        void Highlight() => Apply(table, profile, plugins);
        EventHandler layout = (_, _) => Highlight();
        host.LayoutUpdated += layout;
        profile.HighlightsChanged += Highlight;
        Highlight();
        return System.Reactive.Disposables.Disposable.Create(() => {
            host.LayoutUpdated -= layout;
            profile.HighlightsChanged -= Highlight;
        });
    }

    public static void Apply(Control view,Mo2LiveProfile profile,bool plugins)
    {
        foreach (var row in FindRows(view)) {
            var visuals = RowDecorations(row).ToArray();
            row.Height = row.MinHeight = 40;
            row.CornerRadius = new Avalonia.CornerRadius(8);
            row.BorderThickness = new Avalonia.Thickness(0);
            foreach (var cell in visuals.OfType<TreeDataGridTemplateCell>()) cell.Padding = new Avalonia.Thickness(0);
            var table = row.FindAncestorOfType<TreeDataGrid>();
            var model = table?.Rows is { } rows && row.RowIndex >= 0 && row.RowIndex < rows.Count ? rows[row.RowIndex].Model : null;
            var isSeparator = !plugins && model is CompositeItemModel<EntityId> separatorModel && profile.FindMod(separatorModel.Key)?.IsSeparator == true;
            if (!plugins) foreach (var presenter in visuals.OfType<TreeDataGridCellsPresenter>()) {
                if (isSeparator) presenter.Background = Brushes.Transparent;
                else presenter.ClearValue(TemplatedControl.BackgroundProperty);
            }
            IBrush? brush = null;
            if (plugins && model is CompositeItemModel<ISortItemKey> plugin)
                brush = profile.Order.FindPlugin(plugin.Key) is { } entry && profile.LinkedPlugins.Contains(entry.DisplayName) ? Linked : null;
            else if (!plugins && model is CompositeItemModel<EntityId> mod) {
                var name = profile.FindMod(mod.Key)?.Name ?? "";
                // Linked covers both directions: the mod a selected Data file came
                // from, and the mod a selected plugin was installed by.
                brush = profile.IsDataSourceHighlighted(name) || profile.LinkedMods.Contains(name) ? Linked : profile.WinningMods.Contains(name) ? OverwritesSelection : profile.LosingMods.Contains(name) ? OverwrittenBySelection : name.EndsWith("_separator",StringComparison.OrdinalIgnoreCase) ? Separator : null;
            }
            // Cleared, not painted transparent. A local value beats a style, so writing
            // Transparent here left the mod rows unable to show the selected and
            // hovered fills the shared row styles give them — selecting a mod barely
            // marked it while selecting a plugin filled the row.
            if (brush is null) row.ClearValue(TemplatedControl.BackgroundProperty);
            else if (!ReferenceEquals(row.Background,brush)) row.Background = brush;
            if (plugins) {
                foreach (var border in visuals.OfType<Border>()) {
                    if (border.Name == "RowOuterBorder") border.Padding = new Avalonia.Thickness(0);
                    if (border.Name == "RowBorder") {
                        border.Background = brush ?? Brushes.Transparent;
                        border.CornerRadius = new Avalonia.CornerRadius(8);
                        border.BorderThickness = new Avalonia.Thickness(0);
                        border.BoxShadow = default;
                    }
                }
            }
        }
    }
    private static IEnumerable<Avalonia.Visual> RowDecorations(Avalonia.Visual root)
    {
        foreach (var child in root.GetVisualChildren()) {
            yield return child;
            // Entry bodies contain their own labels, buttons and icons. The
            // native cells/presenters/borders decorated here are outside them.
            if (child is not Mo2DeferredRow)
                foreach (var nested in RowDecorations(child)) yield return nested;
        }
    }
    private static IEnumerable<TreeDataGridRow> FindRows(Avalonia.Visual root)
    {
        foreach (var child in root.GetVisualChildren()) {
            if (child is TreeDataGridRow row) yield return row;
            else foreach (var nested in FindRows(child)) yield return nested;
        }
    }
}
