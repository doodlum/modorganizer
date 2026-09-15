using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.LogicalTree;
using NexusMods.App.UI.Controls;
using NexusMods.UI.Sdk.Icons;

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
            // The glyph too, not only the button around it: one of these pages built
            // its overflow by hand and got the icon's default size, so the same dots
            // were drawn at 24 on one list and 16 on the other.
            foreach (var glyph in action.GetSelfAndLogicalDescendants().OfType<UnifiedIcon>())
                glyph.Size = Mo2TableRow.GlyphSize;
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

    // The search control sits in the toolbar itself. The original markup puts it
    // inside a padded items control of its own, which made that page's search button
    // 32px tall against the other list's 24 — the same control, in a box only one of
    // them had. Taking it out is what makes both toolbars one row of one height.
    internal static void AddSearch(Toolbar toolbar, Control search)
    {
        if (search.Parent is ItemsControl wrapper && !ReferenceEquals(wrapper, toolbar)) {
            wrapper.Items.Remove(search);
            var at = toolbar.Items.IndexOf(wrapper);
            if (at >= 0) { toolbar.Items.RemoveAt(at); toolbar.Items.Insert(at, search); return; }
        } else if (search.Parent is Panel owner) owner.Children.Remove(search);
        if (!toolbar.Items.Contains(search)) toolbar.Items.Add(search);
    }
}
