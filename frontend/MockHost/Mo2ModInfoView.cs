using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using NexusMods.App.UI.Controls;
using ReactiveUI;

namespace Mo2.Frontend;

// MO2's mod information panel, drawn with this application's own controls.
//
// The tabs are MO2's and in MO2's order: what the mod carries as text, as ini, as
// pictures; which of its plugins the game can see; what it fights with; and the
// three things about it that are the user's — its categories, what Nexus knows
// about it, and the note kept against it. The last tab is the folder itself.
//
// A tab with nothing in it says so. MO2 shows an empty list; a sentence is clearer
// about the difference between "this mod has no ini files" and "this panel could
// not read the mod".
internal sealed class Mo2ModInfoView : ReactiveUserControl<Mo2ModInfoPage>
{
    private static object Resource(string key) => Application.Current!.FindResource(key)!;
    private static IBrush Brush(string key) => (IBrush)Resource(key);

    private static TextBlock Muted(string text) => new() {
        Text = text, Opacity = .7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 8, 4, 4),
    };

    public Mo2ModInfoView()
    {
        this.WhenActivated(_ => { if (ViewModel is { } page) Content = Build(page); });
    }

    private Control Build(Mo2ModInfoPage page)
    {
        var tabs = new TabControl { Name = "Mo2ModInfoTabs", Margin = new Thickness(0, 8, 0, 0) };
        void Tab(string header, Func<Control> content) =>
            tabs.Items.Add(new TabItem { Header = header, Content = content(), Name = "Mo2ModInfoTab" });

        Tab("Text Files", () => FileTab(page.TextFiles(), "This mod carries no readme or other text file.", Text));
        Tab("INI Files", () => FileTab(page.IniFiles(), "This mod carries no ini files.", Text));
        Tab("Images", () => FileTab(page.Images(), "This mod carries no images.", Picture));
        Tab("Optional Plugins", () => Plugins(page));
        Tab("Conflicts", () => Conflicts(page));
        Tab("Categories", () => Categories(page));
        Tab("Nexus Info", () => Nexus(page));
        Tab("Notes", () => Notes(page));
        Tab("Filetree", () => Filetree(page));

        if (!page.FolderExists)
            return new StackPanel { Children = { Muted($"MO2 keeps this mod at {page.Folder}, which is not there."), tabs } };
        return tabs;
    }

    // A list of files beside whatever the chosen one is shown as. MO2 splits these
    // tabs the same way, with the list on the left and the file on the right.
    private static Control FileTab(Mo2ModInfoPage.Item[] items, string empty, Func<Mo2ModInfoPage.Item, Control> show)
    {
        if (items.Length == 0) return Muted(empty);
        var list = new ListBox {
            Name = "Mo2ModInfoFileList", ItemsSource = items.Select(x => x.Display).ToArray(),
            SelectedIndex = 0, MinWidth = 180, MaxWidth = 320,
        };
        var host = new ContentControl { Name = "Mo2ModInfoFileContent", Margin = new Thickness(12, 0, 0, 0) };
        void Render() {
            var index = list.SelectedIndex;
            host.Content = index >= 0 && index < items.Length ? show(items[index]) : null;
        }
        list.SelectionChanged += (_, _) => Render();
        Render();

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(list, 0); grid.Children.Add(list);
        Grid.SetColumn(host, 1); grid.Children.Add(host);
        return grid;
    }

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
        return new TextBox {
            Name = "Mo2ModInfoText", Text = body, IsReadOnly = true, AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap, FontFamily = FontFamily.Parse("monospace"), FontSize = 12,
        };
    }

    private static Control Picture(Mo2ModInfoPage.Item item)
    {
        // .dds is the format most game textures are in and nothing here decodes it,
        // which MO2 also treats as a special case — it has a switch for showing them
        // at all. The file is named rather than drawn.
        if (Path.GetExtension(item.Path).Equals(".dds", StringComparison.OrdinalIgnoreCase))
            return Muted($"{item.Display} is a DDS texture, which this panel does not draw.");
        try {
            return new Image { Source = new Bitmap(item.Path), Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        } catch (Exception error) { return Muted($"{item.Display} could not be shown: {error.Message}"); }
    }

    // MO2's Optional Plugins tab: two lists and the two buttons that move a plugin
    // between them, because a plugin in the optional folder is one the game cannot
    // load at all.
    private static Control Plugins(Mo2ModInfoPage page)
    {
        var available = new ListBox { Name = "Mo2ModInfoAvailablePlugins", MinHeight = 120 };
        var optional = new ListBox { Name = "Mo2ModInfoOptionalPlugins", MinHeight = 120 };
        var status = new TextBlock { Opacity = .7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

        Mo2ModInfoPage.Item[] left = [], right = [];
        void Fill() {
            left = page.AvailablePlugins(); right = page.OptionalPlugins();
            available.ItemsSource = left.Select(x => x.Display).ToArray();
            optional.ItemsSource = right.Select(x => x.Display).ToArray();
            status.Text = left.Length == 0 && right.Length == 0
                ? "This mod carries no plugins." : $"{left.Length} the game can load, {right.Length} kept out of its way.";
        }
        Fill();

        void Move(bool toOptional) {
            var list = toOptional ? available : optional;
            var items = toOptional ? left : right;
            if (list.SelectedIndex < 0 || list.SelectedIndex >= items.Length) return;
            var failure = page.MovePlugin(items[list.SelectedIndex], toOptional);
            Fill();
            if (failure is not null) status.Text = failure;
        }

        var hide = new StandardButton { Name = "Mo2ModInfoHidePlugin", Text = "Make unavailable →", Type = StandardButton.Types.Tertiary,
            Command = ReactiveCommand.Create(() => Move(true)) };
        var showIt = new StandardButton { Name = "Mo2ModInfoShowPlugin", Text = "← Make available", Type = StandardButton.Types.Tertiary,
            Command = ReactiveCommand.Create(() => Move(false)) };

        var buttons = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0), Children = { hide, showIt } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*") };
        Grid.SetColumn(available, 0); grid.Children.Add(available);
        Grid.SetColumn(buttons, 1); grid.Children.Add(buttons);
        Grid.SetColumn(optional, 2); grid.Children.Add(optional);

        return new StackPanel { Children = {
            new TextBlock { Text = "Plugins the game can load, and plugins kept out of its way.", Opacity = .7, Margin = new Thickness(0,0,0,8) },
            grid, status } };
    }

    private static Control Conflicts(Mo2ModInfoPage page)
    {
        var summary = page.Mod.Conflicts is { Length: > 0 } text ? text : "MO2 reports no conflicts for this mod.";
        var found = page.Conflicts();
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = summary, TextWrapping = TextWrapping.Wrap, Opacity = .85 });
        if (found.Length == 0) { panel.Children.Add(Muted("No file of this mod is provided by another enabled mod.")); return panel; }

        var winning = found.Where(x => x.Winning).ToArray();
        var losing = found.Where(x => !x.Winning).ToArray();
        void Section(string title, Mo2ModInfoPage.Conflict[] rows) {
            if (rows.Length == 0) return;
            panel.Children.Add(new TextBlock { Text = title, Theme = (ControlTheme)Resource("HeadingXSSemiTheme"), Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(new ListBox { MinHeight = 100, MaxHeight = 260,
                ItemsSource = rows.Select(x => $"{x.File}  —  {x.Other}").ToArray() });
        }
        // MO2's own wording: the mod later in the order is the one whose file the
        // game sees.
        Section($"Winning file conflicts ({winning.Length})", winning);
        Section($"Losing file conflicts ({losing.Length})", losing);
        return new ScrollViewer { Content = panel };
    }

    private static Control Categories(Mo2ModInfoPage page)
    {
        var all = page.Categories();
        if (all.Length == 0) return Muted("MO2 has no categories set up for this instance.");

        var boxes = new List<(string Name, CheckBox Box)>();
        var list = new StackPanel { Spacing = 2 };
        foreach (var category in all) {
            var box = new CheckBox { Content = category.Name, IsChecked = category.Chosen,
                Margin = new Thickness(category.Depth * 16, 0, 0, 0), FontSize = Mo2Density.FontSize };
            boxes.Add((category.Name, box));
            list.Children.Add(box);
        }

        var status = new TextBlock { Opacity = .7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        var save = new StandardButton { Name = "Mo2ModInfoSaveCategories", Text = "Save categories", Type = StandardButton.Types.Primary,
            Command = ReactiveCommand.CreateFromTask(async () => {
                var chosen = boxes.Where(x => x.Box.IsChecked == true).Select(x => x.Name).ToArray();
                status.Text = "Saving…";
                await page.SaveCategories(chosen);
                page.Reread();
                status.Text = page.Profile.Status;
            }) };

        return new DockPanel { Children = {
            Docked(new StackPanel { Spacing = 8, Children = { save, status } }, Dock.Bottom),
            new ScrollViewer { Content = list, Name = "Mo2ModInfoCategories" } } };
    }

    private static Control Docked(Control control, Dock side)
    { DockPanel.SetDock(control, side); return control; }

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
        var panel = new StackPanel { Spacing = 4 };
        foreach (var (label, value) in rows) {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*") };
            var name = new TextBlock { Text = label, Opacity = .7, FontSize = Mo2Density.FontSize };
            var body = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, FontSize = Mo2Density.FontSize };
            Grid.SetColumn(name, 0); grid.Children.Add(name);
            Grid.SetColumn(body, 1); grid.Children.Add(body);
            panel.Children.Add(grid);
        }
        return new ScrollViewer { Content = panel, Name = "Mo2ModInfoNexus" };
    }

    private static Control Notes(Mo2ModInfoPage page)
    {
        var box = new TextBox { Name = "Mo2ModInfoNotes", Text = page.Mod.Notes, AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap, MinHeight = 160, Watermark = "Anything worth remembering about this mod." };
        var status = new TextBlock { Opacity = .7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        var save = new StandardButton { Name = "Mo2ModInfoSaveNotes", Text = "Save notes", Type = StandardButton.Types.Primary,
            Command = ReactiveCommand.CreateFromTask(async () => {
                status.Text = "Saving…";
                await page.SaveNotes(box.Text ?? "");
                page.Reread();
                status.Text = page.Profile.Status;
            }) };
        return new DockPanel { Children = {
            Docked(new StackPanel { Spacing = 8, Children = { save, status } }, Dock.Bottom), box } };
    }

    private static Control Filetree(Mo2ModInfoPage page)
    {
        var tree = page.Tree();
        if (tree.Length == 0) return Muted("This mod's folder is empty, or could not be read.");
        var view = new TreeView { Name = "Mo2ModInfoTree", ItemsSource = tree };
        view.ItemTemplate = new Avalonia.Controls.Templates.FuncTreeDataTemplate<Mo2ModInfoPage.Node>(
            (node, _) => new TextBlock { Text = node.Name, FontSize = Mo2Density.FontSize,
                Foreground = node.IsFolder ? Brush("NeutralModerateBrush") : Brush("NeutralStrongBrush") },
            node => node.Children);
        return view;
    }

    private static string Or(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
