using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using NexusMods.App.UI.Controls;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;

namespace Mo2.Frontend;

// MO2's mod information panel, drawn with this application's own controls.
//
// The layout is MO2's: nine tabs across the top in MO2's order, and inside each
// one the arrangement MO2 gives it — the file tabs list on the left and show on
// the right, the plugin tab has the two lists with the buttons that move a plugin
// between them, conflicts are split into the ones this mod wins and the ones it
// loses. What changes is only what it is drawn with: this application's type
// scale, brushes and buttons rather than Avalonia's defaults, and its empty state
// for a tab with nothing in it.
internal sealed class Mo2ModInfoView : ReactiveUserControl<Mo2ModInfoPage>
{
    private static object Resource(string key) => Application.Current!.FindResource(key)!;
    private static IBrush Brush(string key) => (IBrush)Resource(key);
    private static ControlTheme Type(string key) => (ControlTheme)Resource(key);

    private static TextBlock Body(string text, string? brush = null) => new() {
        Text = text, Theme = Type("BodySMNormalTheme"), TextWrapping = TextWrapping.Wrap,
        Foreground = Brush(brush ?? "NeutralSubduedBrush"),
    };

    private static Control Empty(string header, string subtitle) => new EmptyState {
        IsActive = true, Header = header, Subtitle = Body(subtitle),
        HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center,
    };

    // The surface a section's content sits on, so the panel reads as panels do here
    // rather than as a box with a border drawn round it.
    private static Border Surface(Control content) => new() {
        Child = content, Padding = new Thickness(12), CornerRadius = new CornerRadius(8),
        Background = Brush("SurfaceLowBrush"), BorderThickness = new Thickness(1),
        BorderBrush = Brush("StrokeTranslucentWeakBrush"),
    };

    public Mo2ModInfoView()
    {
        this.WhenActivated(_ => { if (ViewModel is { } page) Content = Build(page); });
    }

    private Control Build(Mo2ModInfoPage page)
    {
        // MO2 puts these across the top, in this order, and so does this. The
        // headers are drawn tighter than the theme draws a tab by default: nine of
        // them at the width this dialog opens at wrapped onto a second row, which
        // MO2 never does.
        // Bounded, because the dialog this opens in sizes itself to its content up to
        // a limit and then stops: a tab taller than that limit had its own footer
        // pushed out of the window, which is how the Save button under the categories
        // went missing. Fixing the height puts the scrolling inside the tab, where a
        // long list belongs, and keeps the footer in view.
        var tabs = new TabControl { Name = "Mo2ModInfoTabs", Height = 372 };
        tabs.Styles.Add(new Style(x => x.OfType<TabItem>()) { Setters = {
            new Setter(TemplatedControl.PaddingProperty, new Thickness(8, 4)),
            new Setter(TemplatedControl.FontSizeProperty, 12d),
            new Setter(Layoutable.MinHeightProperty, 0d),
        } });

        void Tab(string header, Func<Control> content) =>
            tabs.Items.Add(new TabItem { Header = header, Name = "Mo2ModInfoTab",
                Content = new ContentControl { Content = content(), Margin = new Thickness(0, 8, 0, 0) } });

        Tab("Text Files", () => FileSection(page.TextFiles(), "No text files", "This mod carries no readme or other text file.", Text));
        Tab("INI Files", () => FileSection(page.IniFiles(), "No ini files", "This mod carries nothing the game reads as settings.", Text));
        Tab("Images", () => FileSection(page.Images(), "No images", "This mod carries no pictures of itself.", Picture));
        Tab("Optional Plugins", () => Plugins(page));
        Tab("Conflicts", () => Conflicts(page));
        Tab("Categories", () => Categories(page));
        Tab("Nexus Info", () => Nexus(page));
        Tab("Notes", () => Notes(page));
        Tab("Filetree", () => Filetree(page));
        tabs.SelectedIndex = 0;

        if (page.FolderExists) return tabs;
        return new DockPanel { LastChildFill = true, Children = {
            Docked(Body($"MO2 keeps this mod at {page.Folder}, which is not there.", "NeutralModerateBrush"), Dock.Top),
            tabs } };
    }

    private static Control Docked(Control control, Dock side)
    { DockPanel.SetDock(control, side); return control; }

    // A list of files beside whatever the chosen one is shown as, which is the shape
    // MO2 gives these three sections too.
    private static Control FileSection(Mo2ModInfoPage.Item[] items, string header, string subtitle, Func<Mo2ModInfoPage.Item, Control> show)
    {
        if (items.Length == 0) return Empty(header, subtitle);
        var list = Listing(items.Select(x => x.Display));
        list.Width = 220;
        var host = new ContentControl { Margin = new Thickness(12, 0, 0, 0) };
        void Render() {
            var index = list.SelectedIndex;
            host.Content = index >= 0 && index < items.Length ? show(items[index]) : null;
        }
        list.SelectionChanged += (_, _) => Render();
        list.SelectedIndex = 0;
        Render();

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(list, 0); grid.Children.Add(list);
        Grid.SetColumn(host, 1); grid.Children.Add(host);
        return grid;
    }

    // One list style for the whole panel, at the density the rest of the application
    // draws rows at rather than Avalonia's default.
    private static ListBox Listing(IEnumerable<string> items) => new() {
        Name = "Mo2ModInfoList", ItemsSource = items.ToArray(),
        Background = Brushes.Transparent, BorderThickness = new Thickness(0),
        ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((text, _) =>
            new TextBlock { Text = text, Theme = Type("BodySMNormalTheme"),
                TextTrimming = TextTrimming.CharacterEllipsis }, true),
        // At the density the rest of the application draws rows at; the theme's own
        // list item is built for a page, not for a panel inside a dialog.
        Styles = { new Style(x => x.OfType<ListBoxItem>()) { Setters = {
            new Setter(TemplatedControl.PaddingProperty, new Thickness(8, 3)),
            new Setter(Layoutable.MinHeightProperty, 0d),
        } } },
    };

    private static Control Text(Mo2ModInfoPage.Item item)
    {
        string body;
        try {
            var info = new FileInfo(item.Path);
            // A mod's log can be enormous and nobody reads the middle of one in a
            // panel; MO2 loads the file, this loads as much of it as is useful.
            body = info.Length > 512 * 1024
                ? File.ReadAllText(item.Path)[..(256 * 1024)] + "\n\n… (shown to 256KB of " + info.Length / 1024 + "KB)"
                : File.ReadAllText(item.Path);
        } catch (Exception error) { body = "This file could not be read: " + error.Message; }
        return Surface(new TextBox {
            Name = "Mo2ModInfoText", Text = body, IsReadOnly = true, AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap, FontFamily = FontFamily.Parse("monospace"), FontSize = 12,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
        });
    }

    private static Control Picture(Mo2ModInfoPage.Item item)
    {
        // .dds is the format most game textures are in and nothing here decodes it,
        // which MO2 also treats as a special case — it has a switch for showing them
        // at all. The file is named rather than drawn.
        if (Path.GetExtension(item.Path).Equals(".dds", StringComparison.OrdinalIgnoreCase))
            return Empty("DDS texture", $"{item.Display} is a game texture, which this panel does not draw.");
        try {
            return Surface(new Image { Source = new Bitmap(item.Path), Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        } catch (Exception error) { return Empty("Could not be shown", $"{item.Display}: {error.Message}"); }
    }

    // MO2's Optional Plugins: two lists and the two buttons that move a plugin
    // between them, because a plugin in the optional folder is one the game cannot
    // load at all.
    private static Control Plugins(Mo2ModInfoPage page)
    {
        Mo2ModInfoPage.Item[] left = page.AvailablePlugins(), right = page.OptionalPlugins();
        if (left.Length == 0 && right.Length == 0)
            return Empty("No plugins", "This mod adds no esp, esm or esl for the game to load.");

        var available = Listing(left.Select(x => x.Display));
        var optional = Listing(right.Select(x => x.Display));
        var status = Body("");

        void Fill() {
            left = page.AvailablePlugins(); right = page.OptionalPlugins();
            available.ItemsSource = left.Select(x => x.Display).ToArray();
            optional.ItemsSource = right.Select(x => x.Display).ToArray();
            status.Text = $"{left.Length} the game can load, {right.Length} kept out of its way.";
        }
        Fill();

        void Move(bool toOptional) {
            var list = toOptional ? available : optional;
            var items = toOptional ? left : right;
            if (list.SelectedIndex < 0 || list.SelectedIndex >= items.Length) { status.Text = "Choose a plugin first."; return; }
            var failure = page.MovePlugin(items[list.SelectedIndex], toOptional);
            Fill();
            if (failure is not null) status.Text = failure;
        }

        Control Column(string heading, ListBox list) {
            var stack = new DockPanel { LastChildFill = true };
            stack.Children.Add(Docked(new TextBlock { Text = heading, Theme = Type("BodySMSemiTheme"),
                Foreground = Brush("NeutralStrongBrush"), Margin = new Thickness(0, 0, 0, 6) }, Dock.Top));
            stack.Children.Add(Surface(list));
            return stack;
        }

        var buttons = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0),
            Children = {
                new StandardButton { Name = "Mo2ModInfoHidePlugin", Text = "Hide", Type = StandardButton.Types.Tertiary,
                    Size = StandardButton.Sizes.Small, Command = ReactiveCommand.Create(() => Move(true)) },
                new StandardButton { Name = "Mo2ModInfoShowPlugin", Text = "Restore", Type = StandardButton.Types.Tertiary,
                    Size = StandardButton.Sizes.Small, Command = ReactiveCommand.Create(() => Move(false)) },
            } };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*") };
        var loadable = Column("The game loads", available);
        var hidden = Column("Kept out of its way", optional);
        Grid.SetColumn(loadable, 0); grid.Children.Add(loadable);
        Grid.SetColumn(buttons, 1); grid.Children.Add(buttons);
        Grid.SetColumn(hidden, 2); grid.Children.Add(hidden);

        return WithFooter(grid, status);
    }

    // A section's own content with a row under it that stays put. A DockPanel whose
    // fill child is added first pushes the rest off the bottom, which is how the
    // Save buttons here went missing.
    private static Control WithFooter(Control content, params Control[] footer)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        foreach (var control in footer) row.Children.Add(control);
        var dock = new DockPanel { LastChildFill = true };
        dock.Children.Add(Docked(row, Dock.Bottom));
        dock.Children.Add(content);
        return dock;
    }

    private static Control Conflicts(Mo2ModInfoPage page)
    {
        var found = page.Conflicts();
        if (found.Length == 0)
            return Empty("No conflicts", page.Mod.Conflicts is { Length: > 0 } text
                ? text : "No file of this mod is provided by another enabled mod.");

        var winning = found.Where(x => x.Winning).ToArray();
        var losing = found.Where(x => !x.Winning).ToArray();
        var panel = new StackPanel { Spacing = 12 };
        void Section(string title, Mo2ModInfoPage.Conflict[] rows) {
            if (rows.Length == 0) return;
            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(new TextBlock { Text = $"{title} ({rows.Length})", Theme = Type("BodySMSemiTheme"),
                Foreground = Brush("NeutralStrongBrush") });
            stack.Children.Add(Surface(Listing(rows.Select(x => $"{x.File}   —   {x.Other}"))));
            panel.Children.Add(stack);
        }
        // MO2's own wording: the mod later in the order is the one whose file the
        // game sees.
        Section("Winning file conflicts", winning);
        Section("Losing file conflicts", losing);
        return new ScrollViewer { Content = panel };
    }

    private static Control Categories(Mo2ModInfoPage page)
    {
        var all = page.Categories();
        if (all.Length == 0) return Empty("No categories", "This instance has no categories set up to give a mod.");

        var boxes = new List<(string Name, CheckBox Box)>();
        var list = new StackPanel { Spacing = 0 };
        foreach (var category in all) {
            var box = new CheckBox { Content = new TextBlock { Text = category.Name, Theme = Type("BodySMNormalTheme") },
                IsChecked = category.Chosen, Margin = new Thickness(category.Depth * 16, 0, 0, 0), MinHeight = 0 };
            boxes.Add((category.Name, box));
            list.Children.Add(box);
        }

        var status = Body("");
        var save = new StandardButton { Name = "Mo2ModInfoSaveCategories", Text = "Save categories",
            Type = StandardButton.Types.Tertiary, Fill = StandardButton.Fills.Weak, Size = StandardButton.Sizes.Small,
            Command = ReactiveCommand.CreateFromTask(async () => {
                var chosen = boxes.Where(x => x.Box.IsChecked == true).Select(x => x.Name).ToArray();
                status.Text = "Saving…";
                await page.SaveCategories(chosen);
                page.Reread();
                status.Text = page.Profile.Status;
            }) };

        return WithFooter(Surface(new ScrollViewer { Content = list, Name = "Mo2ModInfoCategories" }), save, status);
    }

    private static Control Nexus(Mo2ModInfoPage page)
    {
        var mod = page.Mod;
        var rows = new (string Label, string Value)[] {
            ("Mod name", mod.DisplayName),
            ("Nexus ID", mod.NexusId > 0 ? mod.NexusId.ToString() : "not a Nexus mod"),
            ("Version", Or(mod.Version, "unknown")),
            ("Latest version", Or(mod.NewestVersion, "not checked")),
            ("Author", Or(mod.Author, "unknown")),
            ("Uploaded by", Or(mod.Uploader, "unknown")),
            ("Category", Or(mod.Category, "none")),
            ("Endorsed", Or(mod.Endorsed, "not endorsed")),
            ("Tracked", Or(mod.Tracked, "not tracked")),
            ("Installed", Or(mod.InstallTime, "unknown")),
            ("Source game", Or(mod.SourceGame, "this game")),
            ("Web page", Or(mod.Url, "none")),
        };
        var panel = new StackPanel { Spacing = 6 };
        foreach (var (label, value) in rows) {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*") };
            var name = new TextBlock { Text = label, Theme = Type("BodySMNormalTheme"), Foreground = Brush("NeutralSubduedBrush") };
            var body = new TextBlock { Text = value, Theme = Type("BodySMNormalTheme"),
                Foreground = Brush("NeutralStrongBrush"), TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(name, 0); grid.Children.Add(name);
            Grid.SetColumn(body, 1); grid.Children.Add(body);
            panel.Children.Add(grid);
        }
        return Surface(new ScrollViewer { Content = panel, Name = "Mo2ModInfoNexus" });
    }

    private static Control Notes(Mo2ModInfoPage page)
    {
        var box = new TextBox { Name = "Mo2ModInfoNotes", Text = page.Mod.Notes, AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap, Watermark = "Anything worth remembering about this mod.",
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
        var status = Body("");
        var save = new StandardButton { Name = "Mo2ModInfoSaveNotes", Text = "Save notes",
            Type = StandardButton.Types.Tertiary, Fill = StandardButton.Fills.Weak, Size = StandardButton.Sizes.Small,
            Command = ReactiveCommand.CreateFromTask(async () => {
                status.Text = "Saving…";
                await page.SaveNotes(box.Text ?? "");
                page.Reread();
                status.Text = page.Profile.Status;
            }) };
        return WithFooter(Surface(box), save, status);
    }

    private static Control Filetree(Mo2ModInfoPage page)
    {
        var tree = page.Tree();
        if (tree.Length == 0) return Empty("Nothing here", "This mod's folder is empty, or could not be read.");
        var view = new TreeView { Name = "Mo2ModInfoTree", ItemsSource = tree,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        view.ItemTemplate = new Avalonia.Controls.Templates.FuncTreeDataTemplate<Mo2ModInfoPage.Node>(
            (node, _) => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                new UnifiedIcon { Value = new ProjektankerIcon(node.IsFolder ? "mdi-folder-outline" : "mdi-file-outline"),
                    Size = 14, Foreground = Brush("NeutralSubduedBrush"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = node.Name, Theme = Type("BodySMNormalTheme"),
                    Foreground = Brush(node.IsFolder ? "NeutralStrongBrush" : "NeutralModerateBrush") } } },
            node => node.Children);
        return Surface(view);
    }

    private static string Or(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
