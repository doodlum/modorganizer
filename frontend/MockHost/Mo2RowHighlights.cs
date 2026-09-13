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
    private static readonly IBrush Winner = Brush.Parse("#664F7D43");
    private static readonly IBrush Loser = Brush.Parse("#66893D43");
    private static readonly IBrush Separator = Brush.Parse("#443E5268");
    public static void Apply(Control view,Mo2LiveProfile profile,bool plugins)
    {
        foreach (var row in view.GetVisualDescendants().OfType<TreeDataGridRow>()) {
            var table = row.FindAncestorOfType<TreeDataGrid>();
            var model = table?.Rows is { } rows && row.RowIndex >= 0 && row.RowIndex < rows.Count ? rows[row.RowIndex].Model : null;
            IBrush? brush = null;
            if (plugins && model is CompositeItemModel<ISortItemKey> plugin)
                brush = profile.Order.FindPlugin(plugin.Key) is { } entry && profile.LinkedPlugins.Contains(entry.DisplayName) ? Linked : null;
            else if (!plugins && model is CompositeItemModel<EntityId> mod) {
                var name = profile.FindMod(mod.Key)?.Name ?? "";
                brush = profile.WinningMods.Contains(name) ? Winner : profile.LosingMods.Contains(name) ? Loser : name.EndsWith("_separator",StringComparison.OrdinalIgnoreCase) ? Separator : null;
            }
            if (brush is null) row.ClearValue(TemplatedControl.BackgroundProperty);
            else if (!ReferenceEquals(row.Background,brush)) row.Background = brush;
        }
    }
}
