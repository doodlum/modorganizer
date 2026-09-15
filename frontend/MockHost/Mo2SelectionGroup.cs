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
    internal static ItemsControl Create(out StandardButton deselect, Action clear, params Control[] actions)
    {
        var group = new ItemsControl { Name = "ContextControlGroup", IsVisible = false };
        group.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(
            () => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 });
        deselect = new StandardButton {
            Name = "DeselectItemsButton", Type = StandardButton.Types.Tertiary, Size = StandardButton.Sizes.Toolbar,
            Fill = StandardButton.Fills.None, ShowIcon = StandardButton.ShowIconOptions.Left, LeftIcon = IconValues.Close,
        };
        ToolTip.SetTip(deselect, Language.Library_DeselectItemsButton_ToolTip);
        var button = deselect;
        deselect.Click += (_, _) => clear();
        group.Items.Add(deselect);
        foreach (var action in actions) group.Items.Add(action);
        return group;
    }

    // The same sentence the original writes, so the two tables do not describe the
    // same state in two different ways.
    internal static void Update(ItemsControl group, StandardButton deselect, int count)
    {
        var wanted = count != 0;
        if (group.IsVisible != wanted) group.IsVisible = wanted;
        var text = count == 0 ? string.Empty : string.Format(Language.Library_DeselectItemsButton_Text, count);
        if (deselect.Text != text) deselect.Text = text;
    }
}
