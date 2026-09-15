using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// The panel that lays out a page header's title and its actions on one line,
// and decides what gives way as the panel narrows:
//
//   1. title, description and pictogram beside the actions;
//   2. the words stand down and the pictogram alone marks the page;
//   3. the pictogram goes too and the actions take the whole line, still one row;
//   4. only then do the actions wrap onto a second row.
//
// The order matters: squeezing the actions into whatever is left beside the title
// turned them into a vertical strip of buttons one per row, next to a title nobody
// needed at that width.
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
    private readonly Control _actions;
    private Showing _shown = Showing.Words;
    private double _plateGap;
    private Control? _words;
    private Control? _plate;
    private TextBlock? _title;

    internal Mo2HeaderLine(Control header, Control actions)
    {
        Name = "PanelHeaderRow";
        _header = header; _actions = actions;
        Children.Add(header); Children.Add(actions);
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

        _actions.Measure(unlimited);
        var natural = _actions.DesiredSize.Width;

        var width = double.IsInfinity(availableSize.Width)
            ? natural + Mo2PanelChrome.TitleFloor : availableSize.Width;
        var room = width;

        FindParts();
        // The pictogram on its own still says which page this is, so it is what the
        // header falls back to before giving up the line entirely.
        var plateWidth = _plate is null ? 0 : (double.IsNaN(_plate.Width) ? _plate.DesiredSize.Width : _plate.Width)
            + _plate.Margin.Left + _plate.Margin.Right;
        // The pictogram sits beside the words, so the room the title needs includes it.
        _plateGap = plateWidth;
        // The title's own width on one line, not a fixed floor. A page whose actions
        // grow — Mods and Plugins do, the moment rows are selected — was left with
        // just over the floor and kept its words, wrapping "My Mods" into a column
        // two letters wide above five lines of description. Below what the title
        // needs to read as a title, the words stand down instead.
        if (_title is not null) _title.Measure(unlimited);
        var titleFloor = Math.Max(Mo2PanelChrome.TitleFloor,
            _title is null ? 0 : _title.DesiredSize.Width + _plateGap);
        _shown = natural <= room - titleFloor ? Showing.Words
            : plateWidth > 0 && natural <= room - plateWidth ? Showing.Pictogram
            : Showing.Nothing;
        // The words are taken out of the layout rather than squeezed: a pictogram
        // beside a title clipped to nothing reads as a bug, not as a smaller header.
        if (_words is not null && _words.IsVisible != (_shown == Showing.Words))
            _words.IsVisible = _shown == Showing.Words;

        // Past that, the actions take the line; past that again they wrap within it.
        if (natural > room) _actions.Measure(new Size(room, availableSize.Height));
        var actionsWidth = Math.Min(_actions.DesiredSize.Width, room);

        // The pictogram keeps its own width even when the actions have taken the rest,
        // so the stage that exists to show it can actually show it.
        var forHeader = Math.Max(_shown == Showing.Pictogram ? plateWidth : 0, room - actionsWidth);
        _header.Measure(new Size(Math.Max(0, forHeader), availableSize.Height));
        var height = Math.Max(_actions.DesiredSize.Height,
            _shown == Showing.Nothing ? 0 : _header.DesiredSize.Height);
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var room = finalSize.Width;
        // The actions give the pictogram its width back rather than taking the line
        // and leaving it nothing; that stage exists to keep the pictogram on screen.
        var plateWidth = _plate is null || _shown != Showing.Pictogram ? 0
            : (double.IsNaN(_plate.Width) ? _plate.DesiredSize.Width : _plate.Width) + _plate.Margin.Left + _plate.Margin.Right;
        var actionsWidth = Math.Min(_actions.DesiredSize.Width, Math.Max(0, room - plateWidth));

        // Everything on this line sits against its top edge, so the actions stay
        // level with the title rather than centring against a header that is three
        // lines tall.
        // Clamped at zero. A Rect with a negative width normalises to a box that
        // starts at minus that width, so a header asked for -28px was drawn 29px to
        // the left of the line — the pictogram ended up outside the panel's padding
        // rather than beside its actions.
        var headerWidth = Math.Max(0, Math.Min(_header.DesiredSize.Width, room - actionsWidth));
        _header.Arrange(_shown == Showing.Nothing
            ? default
            : new Rect(0, 0, headerWidth, Math.Min(_header.DesiredSize.Height, finalSize.Height)));
        _actions.Arrange(new Rect(room - actionsWidth, 0, actionsWidth,
            Math.Min(_actions.DesiredSize.Height, finalSize.Height)));
        return finalSize;
    }
}
