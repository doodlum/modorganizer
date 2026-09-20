using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using NexusMods.App.UI.Controls;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

// The three pages that show the contents of a folder — External Files, Data and
// Overwrite — are the same page three times over: a table of files, a search box
// beneath the header, a line of status under it, and icon actions on the header
// line. They were written separately and drifted accordingly. External Files was
// the one that matched the original app, so its pieces are the ones here and the
// other two are built from them.
//
// Data rendered its folders as a "▸ " typed in front of the name and styled its
// table MainListsStyling, which NMA reserves for mod lists; Overwrite formatted
// its own sizes in B and KB only, showed a row of labelled buttons no other page
// has, and offered no way to hide a column.
internal static class Mo2FolderPage
{
    internal static string NameHeader => SharedColumns.NameWithFileIcon.GetColumnHeader();
    // NMA styles its file trees Compact and reserves MainListsStyling for mod
    // lists. All three of these are file trees.
    internal static TreeDataGrid Table(string name)
    {
        var table = new TreeDataGrid { Name = name, ShowColumnHeaders = true };
        table.Classes.Add("Compact");
        return table;
    }

    internal static TextBox Search(string name, string watermark) =>
        new() { Name = name, Watermark = watermark, MinWidth = 60 };

    internal static TextBlock Status(string name) =>
        new() { Name = name, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };

    // The name of a file or folder, with the icon that says which it is. Data used
    // to draw a text arrow instead, so its rows did not line up with either of the
    // other two pages.
    internal static Control NameCell(string name, bool folder, string? tip = null, bool link = false)
    {
        var cell = new Grid { Name = "FolderNameCell", ColumnDefinitions = new ColumnDefinitions("24,*"), MinWidth = 0 };
        cell.Children.Add(new UnifiedIcon {
            Value = new ProjektankerIcon(folder ? "mdi-folder-outline" : link ? "mdi-file-link-outline" : "mdi-file-outline"),
            Size = 16, Opacity = .7,
        });
        var label = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(label, 1);
        cell.Children.Add(label);
        if (tip is { Length: > 0 }) ToolTip.SetTip(cell, tip);
        return cell;
    }

    // A column of names built from the cell above, so the three tables cannot end
    // up with the icon in one and not in another.
    internal static TemplateColumn<T> NameColumn<T>(Func<T, (string Name, bool Folder, string? Tip, bool Link)> describe,
        string? header = null) where T : class =>
        new(header ?? SharedColumns.NameWithFileIcon.GetColumnHeader(),
            new FuncDataTemplate<T>((row, _) => {
                if (row is null) return new Control();
                var (name, folder, tip, link) = describe(row);
                return NameCell(name, folder, tip, link);
            }), width: new GridLength(1, GridUnitType.Star));

    // One width for the second column on all three pages, and one way of writing a
    // size. Overwrite counted in B and KB only, so a 40MB generated file read as
    // "41277.5 KB" where the same file in External Files read as "40.31 MB".
    //
    // Wide enough to hold the longest size a file can have with the scrollbar gutter
    // taken out of it: when this column is the last one, the table keeps about 11px
    // back for the scrollbar, and at 90 that clipped "40.32 MB" to "40.32 …".
    internal const double SizeColumnWidth = 104;

    internal static TextColumn<T, string> SizeColumn<T>(Func<T, string> text, string? header = null) where T : class =>
        new(header ?? SharedColumns.ItemSizeOverGamePath.GetColumnHeader(), x => text(x), new GridLength(SizeColumnWidth),
            new TextColumnOptions<T> { MinWidth = new GridLength(0) });

    // Fixed metadata gives way before filenames become unreadable. Keep column
    // identities rather than indices: user-hidden columns are removed from the
    // source, so saved indices can resize the wrong column or leave its bounds.
    internal static Action MetadataFitter<T>(TreeDataGrid table, HierarchicalTreeDataGridSource<T> source,
        params (IColumn<T> Column, double Width, double Threshold)[] optional) where T : class
    {
        double previousWidth = -1;
        IColumn<T>[] previousColumns = [];
        return Fit;
        void Fit() {
            var width = table.Bounds.Width;
            if (width <= 0) return;
            // Ordinary layout passes must not reset a user's column resize or
            // rebuild metadata choices while width and columns are stable.
            if (width == previousWidth && source.Columns.SequenceEqual(previousColumns)) return;
            previousWidth = width; previousColumns = source.Columns.ToArray<IColumn<T>>();
            var shown = optional.Where(x => source.Columns.Contains(x.Column) && width >= x.Threshold).ToList();
            while (shown.Count > 0 && width - 12 - shown.Sum(x => x.Width) < Mo2TableRow.NameFloor)
                shown.Remove(shown.MaxBy(x => x.Threshold));
            foreach (var item in optional) {
                for (var index = 0; index < source.Columns.Count; index++) {
                    if (!ReferenceEquals(source.Columns[index], item.Column)) continue;
                    source.Columns.SetColumnWidth(index, new GridLength(shown.Any(x => ReferenceEquals(x.Column, item.Column)) ? item.Width : 0));
                    break;
                }
            }
        }
    }

    // Matches the B / KB / MB steps the reference screenshot shows.
    internal static string SizeText(long bytes) => bytes switch {
        < 1024 => bytes + " B",
        < 1024 * 1024 => $"{bytes / 1024d:0.##} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024):0.##} MB",
        _ => $"{bytes / (1024d * 1024 * 1024):0.##} GB",
    };
}
