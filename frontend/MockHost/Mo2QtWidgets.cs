using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

// The widgets MO2 puts in a tab alongside its list, drawn in the Nexus theme with
// Material icons. Captions and tooltips are MO2's own, from src/mainwindow.ui, so a
// page reads the way the tab it replicates reads.
//
// These live together rather than in each page because MO2 reuses the same few:
// every tab that can be re-read has a Refresh, three of them have a filter field,
// and the wording is the wording MO2 chose.
internal static class Mo2QtWidgets
{
    // MO2's own strings. Named after the widget they come from so the spelling can
    // be traced back rather than guessed at.
    internal const string SortTip = "Sort the plugins using LOOT.";
    internal const string RestoreTip = "Restore a backup.";
    internal const string SaveTip = "Create a backup.";
    internal const string ActiveLabel = "Active:";
    internal const string PluginFilterTip = "Filter the list of plugins.";
    internal const string DataRefreshTip = "Refresh the data structure.";
    internal const string DataFilterTip = "Filter the Data tree.";
    internal const string ConflictsOnly = "Conflicts only";
    internal const string ConflictsOnlyTip = "Filter the list so that only conflicts are displayed.";
    internal const string FromArchives = "Archives";
    internal const string FromArchivesTip = "Filter the list so that files from archives are shown.";
    internal const string HiddenFiles = "Hidden files";
    internal const string HiddenFilesTip = "Filter the list so that hidden files are shown.";
    internal const string DownloadsRefreshTip = "Refresh the downloads.";
    internal const string QueryMetadata = "Query Metadata";
    internal const string QueryMetadataTip = "Ask MO2 to look up what its downloads are missing on Nexus Mods.";
    internal const string ArchivesNote = "Currently detected archives.";
    internal const string FiltersGroup = "Filters";
    internal const string FiltersClear = "Clear";
    internal const string FiltersEdit = "Edit...";
    // The tooltip MO2 puts on the And/Or radios, which is what its own filter list
    // uses to explain how ticks combine.
    internal const string FiltersEditTip = "Display mods that match all selected categories.";
    // Edit... has no tooltip of MO2's own; this says what MO2's button does.
    internal const string FiltersEditAction = "Edit MO2's categories.";
    internal const string ModFilterTip = "Filter the list of mods.";
    internal const string ProfileLabel = "Profile";
    internal const string ProfileTip = "Pick a module collection";
    internal const string ListOptionsTip = "Open list options...";
    internal const string OpenFolderTip = "Show Open Folders menu...";
    internal const string RestoreModsTip = "Restore Backup...";
    internal const string SaveModsTip = "Create Backup";
    internal const string FilterLabel = "Filter";
    internal const string ClearAllFilters = "Clear all Filters";
    internal const string DisplayCategoriesTip = "Show or hide the filter list.";
    internal const string FiltersAnd = "And";
    internal const string FiltersAndTip = "Display mods that match all selected categories.";
    internal const string FiltersOr = "Or";
    internal const string FiltersOrTip = "Display mods that match at least one of the selected categories";
    internal const string SeparatorsTip = "Filter: only show the separators that match the current filters\n" +
        "Show: always show separators\nHide: never show separators";
    internal static readonly string[] SeparatorModes = ["Filter separators", "Show separators", "Hide separators"];
    internal static readonly string[] GroupModes = ["No groups", "Categories", "Nexus IDs"];
    internal const string HiddenDownloads = "Hidden files";
    internal const string HiddenDownloadsTip = "Show downloads marked as hidden.";
    internal const string DownloadFilterTip = "Filter the list of downloads.";
    internal const string SavesFilterTip = "Filter the list of saves.";
    internal const string ArchiveFilterTip = "Filter the list of archives.";

    // A push button as MO2 labels them: the caption it draws, the tooltip it
    // explains itself with, and a Material icon in place of MO2's own.
    internal static Button Button(string name, string caption, string tip, string icon, Action click)
    {
        var button = new Button {
            Name = name, Padding = new Thickness(6, 2), Background = Brushes.Transparent, FontSize = Mo2Density.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                new UnifiedIcon { Value = new ProjektankerIcon(icon), Size = Mo2TableRow.GlyphSize,
                    VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = caption, VerticalAlignment = VerticalAlignment.Center },
            } },
        };
        ToolTip.SetTip(button, tip);
        Avalonia.Automation.AutomationProperties.SetName(button, caption);
        button.Click += (_, e) => { e.Handled = true; click(); };
        return button;
    }

    // A push button MO2 draws as an icon alone: the list options, the open-folders
    // menu and the two backup buttons above its mod list all carry no caption.
    internal static Button Icon(string name, string tip, string icon, Action click)
    {
        var button = Mo2TableRow.IconButton(icon, tip, click);
        button.Name = name;
        return button;
    }

    // MO2's displayCategoriesBtn: a checkable button that shows and hides the filter
    // list beside the mod list. Drawn as a toggle rather than a push button because
    // that is what it is — it stays down while the list is showing.
    internal static ToggleButton Toggle(string name, string tip, string icon, bool active, Action<bool> changed)
    {
        var toggle = new ToggleButton {
            Name = name, Width = Mo2TableRow.ActionSize, Height = Mo2TableRow.ActionSize, MinWidth = 0, MinHeight = 0,
            Padding = new Thickness(2), IsChecked = active, VerticalAlignment = VerticalAlignment.Center,
            Content = new UnifiedIcon { Value = new ProjektankerIcon(icon), Size = Mo2TableRow.GlyphSize },
        };
        ToolTip.SetTip(toggle, tip);
        Avalonia.Automation.AutomationProperties.SetName(toggle, tip);
        toggle.IsCheckedChanged += (_, _) => changed(toggle.IsChecked == true);
        return toggle;
    }

    // One of MO2's drop-downs: the profile box, the grouping box and the separators
    // box are all a plain list of choices with the first one taken.
    internal static ComboBox Choice(string name, string tip, IEnumerable<string> items, int selected, Action<int> changed)
    {
        var box = new ComboBox { Name = name, MinWidth = 0, Padding = new Thickness(8, 2),
            FontSize = Mo2Density.FontSize, VerticalAlignment = VerticalAlignment.Center };
        foreach (var item in items) box.Items.Add(item);
        box.SelectedIndex = Math.Min(selected, box.Items.Count - 1);
        ToolTip.SetTip(box, tip);
        Avalonia.Automation.AutomationProperties.SetName(box, tip);
        box.SelectionChanged += (_, _) => changed(box.SelectedIndex);
        return box;
    }

    // MO2's And/Or pair, which decides whether a mod has to carry every ticked
    // category or only one of them.
    internal static RadioButton Radio(string name, string caption, string tip, string group, bool active, Action<bool> changed)
    {
        var radio = new RadioButton { Name = name, Content = caption, GroupName = group, IsChecked = active,
            MinWidth = 0, FontSize = Mo2Density.FontSize, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(radio, tip);
        radio.IsCheckedChanged += (_, _) => changed(radio.IsChecked == true);
        return radio;
    }

    // A caption MO2 puts beside a widget: "Profile", "Filter", "Active:".
    internal static TextBlock Caption(string name, string text) =>
        new() { Name = name, Text = text, Opacity = .6, FontSize = Mo2Density.FontSize,
            VerticalAlignment = VerticalAlignment.Center };

    // One of MO2's filter checkboxes, which narrow what its Data tree lists.
    internal static CheckBox Check(string name, string caption, string tip, bool active, Action<bool> changed)
    {
        var box = new CheckBox { Name = name, Content = caption, IsChecked = active, FontSize = Mo2Density.FontSize,
            VerticalAlignment = VerticalAlignment.Center, MinWidth = 0 };
        ToolTip.SetTip(box, tip);
        box.IsCheckedChanged += (_, _) => changed(box.IsChecked == true);
        return box;
    }

    // MO2's filter field. It uses a LineEditClear — a text box that carries its own
    // clear action — which is what the inner button here stands in for.
    internal static Grid Filter(string name, string tip, Action<string> changed, out TextBox box)
    {
        box = new TextBox { Name = name, Watermark = "Filter", MinWidth = 60, FontSize = Mo2Density.FontSize,
            Padding = new Thickness(6, 1), MinHeight = 0, Height = Mo2TableRow.ActionSize };
        ToolTip.SetTip(box, tip);
        var field = box;
        var clear = Mo2TableRow.IconButton("mdi-close", "Clear the filter", () => field.Text = "");
        clear.Name = name + "Clear";
        clear.IsVisible = false;
        box.TextChanged += (_, _) => { clear.IsVisible = (field.Text ?? "").Length > 0; changed(field.Text ?? ""); };
        var host = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        host.Children.Add(box);
        Grid.SetColumn(clear, 1); host.Children.Add(clear);
        return host;
    }

    // MO2 counts the active plugins beside its list on an LCD readout. The count is
    // what the tab is read for; the readout itself is a Qt affectation, so it is a
    // plain figure here.
    //
    // MO2 names the caption beside it too — activeModslabel and activePluginsLabel —
    // so it carries a name here as well: a caption nothing can look up is a caption
    // no check can find missing.
    internal static StackPanel Counter(string name, out TextBlock value)
    {
        value = new TextBlock { Name = name, Text = "0", FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center };
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center, Children = {
                new TextBlock { Name = name.Replace("Counter", "Label"), Text = ActiveLabel, Opacity = .6,
                    VerticalAlignment = VerticalAlignment.Center },
                value,
            } };
    }

    // The note MO2 puts above its archive list, which links out to what an archive
    // is. The link itself goes nowhere here; the sentence is the part that tells a
    // reader what the list below is.
    internal static TextBlock Note(string name, string text) =>
        new() { Name = name, Text = text, Opacity = .6, TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };

    // MO2's category filter: a checkable list beside the mod list, where ticking
    // categories narrows it to mods in them. MO2 keeps a tree, because its categories
    // nest; the ones this frontend gets from MO2 are a flat set of names, so this is
    // the same control over the categories there actually are.
    internal static Border Categories(string name, out ItemsControl items, out ScrollViewer scroller, params Control[] footer)
    {
        items = new ItemsControl { Name = name };
        scroller = new ScrollViewer { Content = items, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        // MO2's group box: its title, the filter tree, and under it the two rows of
        // controls that decide what the ticks in the tree mean.
        var body = new DockPanel();
        body.Children.Add(Head(new TextBlock { Text = FiltersGroup, FontWeight = FontWeight.SemiBold,
            FontSize = Mo2Density.FontSize, Margin = new Thickness(0, 0, 0, 4) }));
        foreach (var control in footer) { DockPanel.SetDock(control, Dock.Bottom); body.Children.Add(control); }
        body.Children.Add(scroller);
        return new Border {
            Name = name + "Group", CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 4),
            BorderThickness = new Thickness(1), BorderBrush = Application.Current?.FindResource("StrokeTranslucentModerateBrush") as IBrush,
            Child = body,
        };
    }

    private static Control Head(Control control) { DockPanel.SetDock(control, Dock.Top); return control; }

    // One row of that list: the category, and how many mods are in it, as MO2 shows.
    internal static CheckBox Category(string category, int count, bool active, Action<bool> changed)
    {
        var box = new CheckBox { Content = $"{category} ({count})", IsChecked = active, MinWidth = 0,
            FontSize = Mo2Density.FontSize, MinHeight = 0, Margin = new Thickness(0) };
        box.Tag = category;
        ToolTip.SetTip(box, FiltersEditTip);
        box.IsCheckedChanged += (_, _) => changed(box.IsChecked == true);
        return box;
    }
}
