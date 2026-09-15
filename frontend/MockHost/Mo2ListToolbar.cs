using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using NexusMods.App.UI.Controls;

namespace Mo2.Frontend;

// The row of controls both installed lists put on their header line: the page's
// own actions gathered into one pill, the search control, and the group that
// appears while rows are selected.
//
// Both pages wrote this out separately, down to the pill's colour and corner
// radius being typed twice, which is how they ended up with the same intent and
// different behaviour. The pieces are here so that changing one changes both.
internal static class Mo2ListToolbar
{
    // The size of an action inside the pill. Smaller than a header action on its
    // own, because several of them sit together inside one rounded plate.
    internal const double PillActionSize = 24;

    private static readonly IBrush PillBackground = Brush.Parse("#29292E");

    // A page's primary actions, gathered into one plate so they read as a unit
    // rather than as several loose buttons on the header line.
    internal static Border Pill(string name, params Control[] actions)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var action in actions) {
            if (action is Button button) { button.Width = button.Height = PillActionSize; button.MinHeight = 0; }
            row.Children.Add(action);
        }
        return new Border {
            Name = name, Background = PillBackground, CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0), Margin = new Thickness(0, 0, 8, 0), Child = row,
        };
    }

    // The toolbar itself. It wraps rather than clipping, because the panel chrome
    // puts it on the header line and a narrow panel has to give it a second row.
    internal static Toolbar Create(string name)
    {
        var toolbar = new Toolbar { Name = name, Margin = new Thickness(0) };
        Wrap(toolbar);
        return toolbar;
    }

    // An existing toolbar — the original app's, on My Mods — laid out the same way.
    internal static void Wrap(Toolbar toolbar) =>
        toolbar.ItemsPanel = new FuncTemplate<Panel?>(() => new WrapPanel { Orientation = Orientation.Horizontal });
}
