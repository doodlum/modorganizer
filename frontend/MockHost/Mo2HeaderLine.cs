using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// The panel that lays out a page header on one line and decides what gives way as
// the panel narrows:
//
//   1. title, description and pictogram;
//   2. the words stand down and the pictogram alone marks the page;
//   3. the pictogram goes too and the line is given up.
//
// It used to lay the header out beside the page's actions, and to decide the order
// those two gave way in. There are no actions on this line any more — MO2 puts no
// toolbar on a tab, so a page's actions go in a row of their own under the
// separator, and the group kept here for them had been empty on every page for
// long enough that two checks asserted its emptiness and a third measured how its
// contents wrapped, which nothing could ever answer. An empty box measured and
// arranged on every layout pass of every page is a toolbar that no longer has a
// use.
//
// This is a layout container rather than a Grid with a LayoutUpdated handler
// adjusting widths afterwards. That handler wrote the decision back into the very
// properties the next measure read, and a wrapping toolbar reports whatever width
// it was last measured at — so the decision and the layout chased each other until
// Avalonia stopped the process with "Infinite layout loop detected". Deciding
// inside MeasureOverride, where the natural width is asked for directly, there is
// nothing to chase.
internal sealed class Mo2HeaderLine : Panel
{
    // How much of the header is on the line.
    internal enum Showing { Words, Pictogram, Nothing }

    private readonly Control _header;
    private Showing _shown = Showing.Words;
    private double _plateGap;
    private Control? _words;
    private Control? _plate;
    private TextBlock? _title;

    internal Mo2HeaderLine(Control header)
    {
        Name = "PanelHeaderRow";
        _header = header;
        Children.Add(header);
    }

    // How much of the header is on the line at the current width. Read by the
    // header's own collapse and by the checks, neither of which can tell the
    // difference between a pictogram standing in for a title and a missing header.
    internal Showing Shows => _shown;

    // The header's own parts. Found from the applied template, which does not exist
    // on the first measure, so this keeps looking until it does.
    private void FindParts()
    {
        if (_words is not null && _plate is not null && _title is not null) return;
        foreach (var child in _header.GetVisualDescendants().OfType<Control>()) {
            if (_plate is null && child is Border { Child: NexusMods.UI.Sdk.Icons.UnifiedIcon }) _plate = child;
            else if (_title is null && child is TextBlock { Name: "TitleTextBlock" } titleText) _title = titleText;
            else if (_words is null && child is StackPanel stack &&
                     stack.Children.OfType<TextBlock>().Any(x => x.Name == "TitleTextBlock")) _words = stack;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var unlimited = new Size(double.PositiveInfinity, availableSize.Height);

        var width = double.IsInfinity(availableSize.Width) ? Mo2PanelChrome.TitleFloor : availableSize.Width;
        var room = width;

        FindParts();
        // The pictogram on its own still says which page this is, so it is what the
        // header falls back to before giving up the line entirely.
        var plateWidth = _plate is null ? 0 : (double.IsNaN(_plate.Width) ? _plate.DesiredSize.Width : _plate.Width)
            + _plate.Margin.Left + _plate.Margin.Right;
        // The pictogram sits beside the words, so the room the title needs includes it.
        _plateGap = plateWidth;
        // The title's own width on one line, not a fixed floor. A page whose title
        // is long was left with just over the floor and kept its words, wrapping
        // "My Mods" into a column two letters wide above five lines of description.
        // Below what the title needs to read as a title, the words stand down.
        if (_title is not null) _title.Measure(unlimited);
        var titleFloor = Math.Max(Mo2PanelChrome.TitleFloor,
            _title is null ? 0 : _title.DesiredSize.Width + _plateGap);
        _shown = room >= titleFloor ? Showing.Words
            : plateWidth > 0 && room >= plateWidth ? Showing.Pictogram
            : Showing.Nothing;
        // The words are taken out of the layout rather than squeezed: a pictogram
        // beside a title clipped to nothing reads as a bug, not as a smaller header.
        if (_words is not null && _words.IsVisible != (_shown == Showing.Words))
            _words.IsVisible = _shown == Showing.Words;

        // The pictogram keeps its own width at the stage that exists to show it.
        var forHeader = Math.Max(_shown == Showing.Pictogram ? plateWidth : 0, room);
        _header.Measure(new Size(Math.Max(0, forHeader), availableSize.Height));
        var height = _shown == Showing.Nothing ? 0 : _header.DesiredSize.Height;
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var room = finalSize.Width;
        // The header sits against the line's top edge, so it stays level with
        // whatever is beside it rather than centring against a header three lines
        // tall. Clamped at zero: a Rect with a negative width normalises to a box
        // that starts at minus that width, so a header asked for -28px was drawn
        // 29px to the left of the line, outside the panel's padding.
        var headerWidth = Math.Max(0, Math.Min(_header.DesiredSize.Width, room));
        _header.Arrange(_shown == Showing.Nothing
            ? default
            : new Rect(0, 0, headerWidth, Math.Min(_header.DesiredSize.Height, finalSize.Height)));
        return finalSize;
    }
}
