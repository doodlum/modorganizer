using Avalonia.Controls;
using Avalonia.Layout;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Resources;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

// What a table shows while rows are selected: how many, a way to clear them, and
// the actions that apply to all of them at once. My Mods gets this from the
// original app's own toolbar; Plugins had nothing at all, so selecting plugins
// told you nothing and the only way out of a selection was the keyboard.
//
// Built here to the same shape as the original — the same wording, the same close
// icon, the same tertiary toolbar buttons, the same name so anything looking for
// one finds either — rather than invented, because the two tables sit side by side
// and a person moving between them should not have to learn the difference.
internal static class Mo2SelectionGroup
{
    // The original app's own group, rebuilt as the shared one: its buttons keep the
    // commands the native view bound to them and move into the same container
    // Plugins uses, so there is one group in the frontend rather than two that have
    // to be kept in step by hand.
    internal static Panel Adopt(ItemsControl native, out StandardButton deselect, Action clear)
    {
        var taken = native.Items.OfType<Control>().ToArray();
        native.Items.Clear();
        native.Name = "AdoptedContextControlGroup";
        native.IsVisible = false;
        var existing = taken.OfType<StandardButton>().FirstOrDefault(x => x.Name == "DeselectItemsButton");
        var group = Create(out var built, clear, taken.Where(x => !ReferenceEquals(x, existing)).ToArray());
        if (existing is null) { deselect = built; return group; }
        // The native close button, not a copy of it: whatever the original view bound
        // to it — and whatever this page re-pointed — still applies.
        group.Children.Remove(built);
        group.Children.Insert(0, existing);
        deselect = existing;
        return group;
    }

    // A panel rather than an items control, which is what the original markup used:
    // an items control builds its buttons the first time it is measured, and this one
    // starts hidden, so while nothing was selected the group had no buttons in it at
    // all — not merely invisible ones. Anything looking for the deselect or the
    // page's own multi-row actions found nothing until something happened to be
    // selected first.
    internal static Panel Create(out StandardButton deselect, Action clear, params Control[] actions)
    {
        var group = new StackPanel { Name = "ContextControlGroup", IsVisible = false,
            Orientation = Orientation.Horizontal, Spacing = 4 };
        deselect = new StandardButton {
            Name = "DeselectItemsButton", Type = StandardButton.Types.Tertiary, Size = StandardButton.Sizes.Toolbar,
            Fill = StandardButton.Fills.None, ShowIcon = StandardButton.ShowIconOptions.Left, LeftIcon = IconValues.Close,
        };
        ToolTip.SetTip(deselect, Language.Library_DeselectItemsButton_ToolTip);
        var button = deselect;
        deselect.Click += (_, _) => clear();
        group.Children.Add(deselect);
        foreach (var action in actions) group.Children.Add(action);
        return group;
    }

    // The same sentence the original writes, so the two tables do not describe the
    // same state in two different ways.
    internal static void Update(Panel group, StandardButton deselect, int count)
    {
        var wanted = count != 0;
        if (group.IsVisible != wanted) group.IsVisible = wanted;
        var text = count == 0 ? string.Empty : string.Format(Language.Library_DeselectItemsButton_Text, count);
        if (deselect.Text != text) deselect.Text = text;
    }
}
