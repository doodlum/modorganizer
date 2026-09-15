using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.Alerts;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.Sdk.Settings;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using System.Reactive.Disposables;

namespace Mo2.Frontend;

internal interface IMo2ComponentsPage : IPageViewModelInterface { }
internal sealed class Mo2ComponentsPage : APageViewModel<IMo2ComponentsPage>, IMo2ComponentsPage
{
    public static readonly IconValue ComponentsIcon = new ProjektankerIcon("mdi-shape-outline");
    public ISettingsManager Settings { get; }
    public Mo2ComponentsPage(IWindowManager windows, ISettingsManager settings) : base(windows)
    { Settings = settings; TabTitle = "Components"; TabIcon = ComponentsIcon; }
}

// "Did you know?" hint: the shared Alert already self-hides and persists its
// dismissal through AlertSettingsWrapper, so a panel only supplies a stable key
// and the text. Dismissal survives restart through Mo2AlertPreferences.
internal static class Mo2UsageAlert
{
    internal static Alert Create(ISettingsManager settings, string key, string body, string title = "Did you know?") =>
        new() { Name = "UsageAlert", Severity = Alert.SeverityOptions.Info, Title = title, Body = body,
            ShowDismiss = true, ShowActions = false, AlertSettings = new AlertSettingsWrapper(settings, key) };
}

// Gallery of the shared NexusMods.App.UI controls this frontend builds panels from,
// grouped to follow the design reference sheets. Sections whose reference component
// has no shared control yet say so, so the gap stays visible instead of being
// reimplemented per panel.
internal sealed class Mo2ComponentsView : ReactiveUserControl<Mo2ComponentsPage>
{
    private static object Resource(string key) => Application.Current!.FindResource(key)!;
    private static ControlTheme Text(string key) => (ControlTheme)Resource(key);
    private static IBrush Brush(string key) => (IBrush)Resource(key);

    private static TextBlock Heading(string text) =>
        new() { Text = text, Theme = Text("HeadingXSSemiTheme") };
    private static TextBlock Note(string text) =>
        new() { Text = text, Theme = Text("BodySMNormalTheme"), Foreground = Brush("NeutralSubduedBrush"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
    private static TextBlock Caption(string text) =>
        new() { Text = text, Theme = Text("BodySMNormalTheme"), Foreground = Brush("NeutralSubduedBrush"),
            VerticalAlignment = VerticalAlignment.Center, MinWidth = 128 };

    // Every section uses the same surface, padding and corner radius so the page
    // itself demonstrates the consistency the panels are meant to share.
    private static Border Section(string title, string? note, params Control[] rows)
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(Heading(title));
        if (note is not null) stack.Children.Add(Note(note));
        foreach (var row in rows) stack.Children.Add(row);
        return new Border { Name = "ComponentSection", Background = Brush("SurfaceLowBrush"),
            BorderBrush = Brush("StrokeTranslucentWeakBrush"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(16), Child = stack };
    }

    private static Control Row(string caption, params Control[] items)
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        foreach (var item in items) wrap.Children.Add(item);
        var row = new DockPanel { LastChildFill = true };
        var label = Caption(caption); DockPanel.SetDock(label, Dock.Left);
        row.Children.Add(label); row.Children.Add(wrap);
        return row;
    }

    private static StandardButton Button(StandardButton.Types type, StandardButton.Fills fill,
        StandardButton.Sizes size, string text = "Button", bool enabled = true,
        StandardButton.ShowIconOptions icon = StandardButton.ShowIconOptions.None) =>
        new() { Type = type, Fill = fill, Size = size, Text = text, IsEnabled = enabled,
            ShowIcon = icon, LeftIcon = IconValues.Add, RightIcon = IconValues.ChevronDown };

    private readonly ContentControl _usageHost = new();
    private AlertSettingsWrapper? _usage;

    public Mo2ComponentsView()
    {
        // The hint needs the page's settings manager, which arrives with the view model.
        this.WhenActivated(disposables => {
            if (ViewModel is { } model && _usageHost.Content is null) {
                var alert = Mo2UsageAlert.Create(model.Settings, "components-gallery",
                    "You can select multiple rows with Ctrl+click or Shift+click, then act on all of them at once.");
                _usage = alert.AlertSettings; _usageHost.Content = alert;
            }
            System.Reactive.Disposables.Disposable.Empty.DisposeWith(disposables);
        });
        var sections = new StackPanel { Spacing = 16 };
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(24) };
        root.Children.Add(new PageHeader { Title = "Components", Icon = Mo2ComponentsPage.ComponentsIcon,
            Description = "Shared NexusMods app controls this frontend builds its panels from." });
        var scroll = new ScrollViewer { Content = sections, Margin = new Thickness(0, 16, 0, 0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll); Content = root;

        foreach (var type in new[] { StandardButton.Types.Primary, StandardButton.Types.Secondary, StandardButton.Types.Tertiary })
            sections.Children.Add(Section($"Button · {type}",
                type == StandardButton.Types.Primary
                    ? "StandardButton covers the reference button matrix: type × fill × size, with icon placement and disabled state."
                    : null,
                Row("Weak fill",
                    Button(type, StandardButton.Fills.Weak, StandardButton.Sizes.Small),
                    Button(type, StandardButton.Fills.Weak, StandardButton.Sizes.Medium),
                    Button(type, StandardButton.Fills.Weak, StandardButton.Sizes.Toolbar)),
                Row("Strong fill",
                    Button(type, StandardButton.Fills.Strong, StandardButton.Sizes.Small),
                    Button(type, StandardButton.Fills.Strong, StandardButton.Sizes.Medium),
                    Button(type, StandardButton.Fills.Strong, StandardButton.Sizes.Toolbar)),
                Row("No fill",
                    Button(type, StandardButton.Fills.None, StandardButton.Sizes.Small),
                    Button(type, StandardButton.Fills.None, StandardButton.Sizes.Medium),
                    Button(type, StandardButton.Fills.None, StandardButton.Sizes.Toolbar)),
                Row("Icons",
                    Button(type, StandardButton.Fills.Weak, StandardButton.Sizes.Medium, icon: StandardButton.ShowIconOptions.Left),
                    Button(type, StandardButton.Fills.Weak, StandardButton.Sizes.Medium, icon: StandardButton.ShowIconOptions.Right),
                    Button(type, StandardButton.Fills.Weak, StandardButton.Sizes.Medium, icon: StandardButton.ShowIconOptions.Both),
                    Button(type, StandardButton.Fills.Weak, StandardButton.Sizes.Medium, icon: StandardButton.ShowIconOptions.IconOnly)),
                Row("Disabled",
                    Button(type, StandardButton.Fills.Weak, StandardButton.Sizes.Medium, enabled: false),
                    Button(type, StandardButton.Fills.Strong, StandardButton.Sizes.Medium, enabled: false),
                    Button(type, StandardButton.Fills.None, StandardButton.Sizes.Medium, enabled: false))));

        var search = new TextBox { Watermark = "Search…", MinWidth = 180 };
        var toolbar = new Toolbar { Name = "ComponentToolbar" };
        toolbar.Items.Add(search);
        toolbar.Items.Add(Button(StandardButton.Types.Tertiary, StandardButton.Fills.None, StandardButton.Sizes.Toolbar, "Add", icon: StandardButton.ShowIconOptions.Left));
        toolbar.Items.Add(Button(StandardButton.Types.Tertiary, StandardButton.Fills.None, StandardButton.Sizes.Toolbar, "Update all", icon: StandardButton.ShowIconOptions.Left));
        var selection = new Toolbar { Name = "ComponentSelectionToolbar" };
        selection.Items.Add(Button(StandardButton.Types.Tertiary, StandardButton.Fills.None, StandardButton.Sizes.Toolbar, "1 selected", icon: StandardButton.ShowIconOptions.Left));
        selection.Items.Add(new Divider { Width = 1, Height = 20 });
        selection.Items.Add(Button(StandardButton.Types.Tertiary, StandardButton.Fills.None, StandardButton.Sizes.Toolbar, "Install", icon: StandardButton.ShowIconOptions.Left));
        selection.Items.Add(Button(StandardButton.Types.Tertiary, StandardButton.Fills.None, StandardButton.Sizes.Toolbar, "Delete", icon: StandardButton.ShowIconOptions.Left));
        sections.Children.Add(Section("Toolbar",
            "Shared Toolbar sticks above the page content and carries collective actions. Selecting rows adds a section limited to bulk actions; per-row actions stay in the row menu.",
            Row("Default", toolbar), Row("Item selected", selection)));

        sections.Children.Add(Section("Page header",
            "Shared PageHeader. The reference also specifies a compact drop-shadow variant on scroll and a 1408px content cap; neither is implemented yet.",
            new PageHeader { Title = "Mods", Description = "Manage the mods installed for this game.", Icon = IconValues.CollectionsOutline }));

        sections.Children.Add(Section("Did you know",
            "A dismissible hint beside the thing it explains. Dismissing keeps it dismissed for that key, across restarts.",
            _usageHost,
            new StandardButton { Text = "Restore this hint", Type = StandardButton.Types.Tertiary,
                Fill = StandardButton.Fills.Weak, Size = StandardButton.Sizes.Small, ShowIcon = StandardButton.ShowIconOptions.None,
                HorizontalAlignment = HorizontalAlignment.Left, Command = ReactiveUI.ReactiveCommand.Create(() => _usage?.ShowAlert()) }));

        var alerts = new StackPanel { Spacing = 8 };
        foreach (var severity in new[] { Alert.SeverityOptions.Info, Alert.SeverityOptions.Success, Alert.SeverityOptions.Warning, Alert.SeverityOptions.Error })
            alerts.Children.Add(new Alert { Severity = severity, Title = severity + " alert",
                Body = "Body text explaining the condition and what to do about it.", ShowDismiss = false });
        sections.Children.Add(Section("Alerts", null, alerts));

        sections.Children.Add(Section("Empty state",
            "Shared EmptyState, used where the reference sheets show a NoResults block.",
            new EmptyState { IsActive = true, Header = "You don't have any installed mods",
                Icon = IconValues.CollectionsOutline, Height = 180,
                Subtitle = new TextBlock { Text = "But don't worry, I know a place…", Foreground = Brush("NeutralSubduedBrush") } }));

        sections.Children.Add(Section("Inputs",
            "TextBox and ComboBox are unthemed Avalonia defaults: the reference input-group states (label, required-field error, disabled) and the Select/Picker forms have no shared control yet.",
            Row("Text box", new TextBox { Watermark = "Placeholder", MinWidth = 180 },
                new TextBox { Text = "Filled", MinWidth = 180 },
                new TextBox { Text = "Disabled", MinWidth = 180, IsEnabled = false }),
            Row("Select", new ComboBox { PlaceholderText = "Select label", MinWidth = 180,
                ItemsSource = new[] { "Select label", "Second option" } })));

        sections.Children.Add(Section("Toggles",
            "ToggleSwitch and CheckBox are unthemed Avalonia defaults; the reference toggle sizes are not implemented yet.",
            Row("Toggle", new ToggleSwitch { IsChecked = true }, new ToggleSwitch(), new ToggleSwitch { IsEnabled = false }),
            Row("Check box", new CheckBox { IsChecked = true }, new CheckBox(), new CheckBox { IsEnabled = false })));

        var tooltipped = Button(StandardButton.Types.Tertiary, StandardButton.Fills.Weak, StandardButton.Sizes.Medium, "Hover me");
        ToolTip.SetTip(tooltipped, "Tooltip label");
        sections.Children.Add(Section("Tooltips",
            "Themed tooltips exist; the reference keyboard-shortcut chips do not.",
            Row("Tooltip", tooltipped)));

        sections.Children.Add(Section("Progress", null,
            Row("Indeterminate", new NexusMods.App.UI.Controls.ProgressRing.ProgressRing { Width = 28, Height = 28 },
                new NexusMods.App.UI.Controls.Spinner.Spinner { Width = 28, Height = 28 },
                new ProgressBar { IsIndeterminate = true, Width = 180 }),
            Row("Determinate", new ProgressBar { Value = 60, Width = 180 })));

        sections.Children.Add(Section("Separators", null,
            Row("Divider", new Divider { Width = 180, Height = 1 })));

        var labelRows = new StackPanel { Spacing = 8 };
        foreach (var fill in new[] { Mo2Label.Fills.Weak, Mo2Label.Fills.Strong, Mo2Label.Fills.Outline }) {
            var chips = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
            foreach (var brand in Enum.GetValues<Mo2Label.Brands>()) chips.Children.Add(new Mo2Label(brand.ToString(), brand, fill));
            labelRows.Children.Add(Row(fill.ToString(), chips));
        }
        sections.Children.Add(Section("Labels",
            "The reference chip, added here because the shared library has no label control.", labelRows));

        var tabs = new TabControl { Name = "ComponentTabs", Height = 96 };
        foreach (var title in new[] { "Installed", "Available", "Updates" })
            tabs.Items.Add(new TabItem { Header = title, Content = new TextBlock { Text = title + " content",
                Foreground = Brush("NeutralSubduedBrush"), Margin = new Thickness(0, 8) } });
        tabs.SelectedIndex = 0;
        var secondary = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
        foreach (var (title, index) in new[] { ("All", 0), ("Enabled", 1), ("Disabled", 2) })
            secondary.Children.Add(new StandardButton { Text = title, Size = StandardButton.Sizes.Small,
                Type = StandardButton.Types.Tertiary, ShowIcon = StandardButton.ShowIconOptions.None,
                Fill = index == 0 ? StandardButton.Fills.Weak : StandardButton.Fills.None });
        sections.Children.Add(Section("Tabs",
            "Primary tabs use the themed TabControl; the secondary pill row is built from StandardButtons.",
            tabs, Row("Secondary", secondary)));

        var options = new Mo2ColumnToggle("components-gallery", () => { });
        options.Action.HorizontalAlignment = HorizontalAlignment.Left;
        sections.Children.Add(Section("View options",
            "The same column chooser the table panels put beside their header actions: a checkable column list and Reset to default.",
            Row("Columns", options.Action)));
    }
}
