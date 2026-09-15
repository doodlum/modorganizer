using Avalonia;
using Avalonia.Controls;

namespace Mo2.Frontend;

// The panel that lays out a page header's title, its actions and the maximise
// action on one line, and decides what gives way as the panel narrows:
//
//   1. title and actions side by side;
//   2. the title stands down and the actions take the whole line, still one row;
//   3. only then do the actions wrap onto a second row.
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
    // Space between the actions and the maximise action beside them.
    private const double Gap = 4;

    private readonly Control _header;
    private readonly Control _actions;
    private readonly Control _maximise;
    private bool _titleShown = true;

    internal Mo2HeaderLine(Control header, Control actions, Control maximise)
    {
        Name = "PanelHeaderRow";
        _header = header; _actions = actions; _maximise = maximise;
        Children.Add(header); Children.Add(actions); Children.Add(maximise);
    }

    // Whether the title is on the line at the current width. Read by the header's
    // own collapse, which has no other way to know.
    internal bool TitleShown => _titleShown;

    protected override Size MeasureOverride(Size availableSize)
    {
        var unlimited = new Size(double.PositiveInfinity, availableSize.Height);
        _maximise.Measure(unlimited);
        var reserve = _maximise.IsVisible ? _maximise.DesiredSize.Width + Gap : 0;

        // What the actions want on one row, asked for directly.
        _actions.Measure(unlimited);
        var natural = _actions.DesiredSize.Width;

        var width = double.IsInfinity(availableSize.Width)
            ? natural + reserve + Mo2PanelChrome.TitleFloor : availableSize.Width;
        var room = Math.Max(0, width - reserve);

        // Not written back as IsVisible: another handler reads the header's visibility
        // from the window's layout and writes its own properties in response, and the
        // two passes then trigger each other. A header that is not shown is simply
        // arranged at nothing, which is a layout result rather than a state change.
        _titleShown = natural <= room - Mo2PanelChrome.TitleFloor;

        // Past that, the actions take the line; past that again they wrap within it.
        if (natural > room) _actions.Measure(new Size(room, availableSize.Height));
        var actionsWidth = Math.Min(_actions.DesiredSize.Width, room);

        _header.Measure(new Size(Math.Max(0, room - actionsWidth), availableSize.Height));
        var height = Math.Max(_maximise.DesiredSize.Height,
            Math.Max(_actions.DesiredSize.Height, _titleShown ? _header.DesiredSize.Height : 0));
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var reserve = _maximise.IsVisible ? _maximise.DesiredSize.Width + Gap : 0;
        var room = Math.Max(0, finalSize.Width - reserve);
        var actionsWidth = Math.Min(_actions.DesiredSize.Width, room);

        // Everything on this line sits against its top edge, so the actions stay
        // level with the title rather than centring against a header that is three
        // lines tall.
        _header.Arrange(_titleShown
            ? new Rect(0, 0, Math.Max(0, room - actionsWidth), Math.Min(_header.DesiredSize.Height, finalSize.Height))
            : default);
        _actions.Arrange(new Rect(room - actionsWidth, 0, actionsWidth,
            Math.Min(_actions.DesiredSize.Height, finalSize.Height)));
        if (_maximise.IsVisible)
            _maximise.Arrange(new Rect(finalSize.Width - _maximise.DesiredSize.Width, 0,
                _maximise.DesiredSize.Width, Math.Min(_maximise.DesiredSize.Height, finalSize.Height)));
        return finalSize;
    }
}
