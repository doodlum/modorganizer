using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Mo2.Frontend;

// How much of a list MO2 fits on screen, and the sizes that follow from it.
//
// MO2 is a Qt desktop application: its lists draw a row per line of text, around
// 20px tall at 96dpi, and a window of this size shows forty of them. The theme this
// frontend borrows is built for a handful of large cards — 46px a row, 24px of
// padding inside every cell — so the same window showed eleven mods and the lists
// read as a different application to the one they replicate.
//
// The numbers live together because they have to agree: a row is only as short as
// the tallest thing in it, so the checkbox, the glyphs and the text all size from
// here rather than each picking their own.
internal static class Mo2Density
{
    // One line of a list. MO2's own is 20 at this font size; a pointer target of 22
    // keeps the count of rows on screen while staying usable on a touch screen.
    internal const double Row = 22;
    // The band the column headings sit in, one row plus the rule under them.
    internal const double Heading = 22;
    // MO2 draws its lists at the desktop's small font. 12px is the nearest this
    // theme gets while still hinting cleanly.
    internal const double FontSize = 12;
    // The enable box at the head of a mod or plugin row.
    internal const double Check = 16;
    // Glyph columns — conflicts, flags, content — and the icons in a row's text.
    internal const double Glyph = 14;
    // Side padding in a table cell. The theme uses 8, and 24 on the first column.
    internal static readonly Thickness Cell = new(4, 0);
    // The rounding on a row's hover, selection and separator bar. The theme rounds
    // by 8, which on a 22px line is most of the row.
    internal const double Corner = 4;

    // A colour MO2 reports for a row, as a brush. MO2 sends "#rrggbb" or nothing.
    internal static IBrush? Brush(string color) =>
        color.Length > 0 && Color.TryParse(color, out var parsed) ? new SolidColorBrush(parsed) : null;

    // MO2 draws a separator's name in whatever reads against the colour the user
    // gave it, rather than fixing on one foreground. Qt picks by luminance; so does
    // this, on the same threshold.
    internal static IBrush Ink(Color background) =>
        background.R * .299 + background.G * .587 + background.B * .114 > 150
            ? Brushes.Black : Brushes.White;

    // The density every table takes, applied once to the application rather than per
    // page: the pages that draw MO2's file lists build their tables from the shared
    // TreeDataGrid, and each one that set its own heights was a page that drifted.
    internal static void Install(Styles styles)
    {
        // The row itself, and the cells in it. The theme's first- and last-column
        // padding is what indents a list a third of an inch from its own panel.
        styles.Add(new Style(x => Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridRow>(x)) {
            Setters = {
                new Setter(Layoutable.HeightProperty, Row),
                new Setter(Layoutable.MinHeightProperty, Row),
            },
        });
        // The theme's own classes set the height again, inside a rule that names the
        // table as well as the row. A rule that names only the row does not replace
        // one that names both, so the file pages — every one of which asks for the
        // theme's Compact table — kept its 32px row.
        foreach (var table in new[] { "Compact", "MainListsStyling" })
            styles.Add(new Style(x => Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridRow>(
                Selectors.Descendant(Selectors.Class(Selectors.OfType<TreeDataGrid>(x), table)))) {
                Setters = {
                    new Setter(Layoutable.HeightProperty, Row),
                    new Setter(Layoutable.MinHeightProperty, Row),
                },
            });
        styles.Add(new Style(x => Selectors.Is<Avalonia.Controls.Primitives.TreeDataGridCell>(
            Selectors.Descendant(Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridRow>(x)))) {
            Setters = {
                new Setter(Avalonia.Controls.Primitives.TemplatedControl.PaddingProperty, Cell),
                new Setter(Avalonia.Controls.Primitives.TemplatedControl.FontSizeProperty, FontSize),
            },
        });
        styles.Add(new Style(x => Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridColumnHeader>(x)) {
            Setters = {
                new Setter(Layoutable.MinHeightProperty, Heading),
                new Setter(Layoutable.HeightProperty, Heading),
                new Setter(Avalonia.Controls.Primitives.TemplatedControl.PaddingProperty, Cell),
                new Setter(Avalonia.Controls.Primitives.TemplatedControl.FontSizeProperty, FontSize),
            },
        });
        // Text inside a cell, which the theme leaves at the application font size.
        styles.Add(new Style(x => Selectors.OfType<TextBlock>(
            Selectors.Descendant(Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridRow>(x)))) {
            Setters = { new Setter(TextBlock.FontSizeProperty, FontSize) },
        });
    }
}
