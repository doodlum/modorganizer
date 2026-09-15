using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal static class Mo2ModRow
{
    // MO2's own mod list columns, in the order its model declares them
    // (src/modlist.h, EColumn) and under the headers it gives them
    // (modlist.cpp, headerData). The enable box sits in the name column as it does
    // in MO2, and there is no actions column: MO2 puts a row's actions on its
    // right-click menu rather than drawing buttons on every row.
    internal const int Name = 0, Conflicts = 1, Flags = 2, Content = 3, Category = 4,
        Author = 5, Uploader = 6, NexusId = 7, SourceGame = 8, Version = 9,
        Installation = 10, Priority = 11, Notes = 12;

    internal static Grid Columns() => new() { MinWidth = 0, ColumnDefinitions = new ColumnDefinitions(
        "*,30,30,88,104,104,104,74,104,78,120,56,110") };

    internal static readonly (int Column, string Name)[] Headers = [
        (Name, "Mod Name"), (Conflicts, "Conflicts"), (Flags, "Flags"), (Content, "Content"),
        (Category, "Category"), (Author, "Author"), (Uploader, "Uploader"), (NexusId, "Nexus ID"),
        (SourceGame, "Source Game"), (Version, "Version"), (Installation, "Installation"),
        (Priority, "Priority"), (Notes, "Notes")];

    // Columns the user has switched off. The responsive widths below still apply: a
    // column shows only when it both fits and has not been hidden. Only the name is
    // not optional, matching MO2, which lets every other column be turned off.
    internal static readonly HashSet<int> HiddenColumns = [];
    internal static readonly (int Column, string Name)[] OptionalColumns =
        Headers.Where(x => x.Column != Name).ToArray();

    // Dropped from the right as the panel narrows, in reverse order of how much they
    // say about a mod: the glyph columns and the priority survive longest because
    // they are what the list is read by.
    private static readonly (int Column, double Width, double Threshold)[] Optional = [
        (Conflicts, 30, 300), (Flags, 30, 330), (Content, 88, 620), (Category, 104, 500),
        (Author, 104, 1120), (Uploader, 104, 1360), (NexusId, 74, 900), (SourceGame, 104, 1240),
        (Version, 78, 420), (Installation, 120, 1020), (Priority, 56, 380), (Notes, 110, 780)];

    // Room the category filter takes out of the pane when it is showing. The column
    // fitting below measures the panel, not the list, so without this the columns
    // believed they had the whole width and the name column — the one column that is
    // not optional — was squeezed to nothing to keep the others.
    internal static double SideWidth;

    internal static void Fit(Grid grid, double width) =>
        Mo2TableRow.Fit(grid, Math.Max(0, width - SideWidth), Optional, HiddenColumns.Contains);

    // One of MO2's glyph columns. MO2 draws a small icon per condition and spells the
    // conditions out in the tooltip; the icon is shown only when there is something to
    // say, so an ordinary mod's row stays quiet rather than carrying three grey marks.
    private static UnifiedIcon Glyph(string name, string detail, string icon) =>
        Detail(new UnifiedIcon { Name = name, Value = new ProjektankerIcon(icon), Size = 14,
            Opacity = detail.Length > 0 ? .85 : 0, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center }, detail);

    private static UnifiedIcon Detail(UnifiedIcon icon, string detail)
    {
        if (detail.Length > 0) ToolTip.SetTip(icon, detail);
        return icon;
    }
    // Kept as thin forwarders: both tables share one implementation in Mo2TableRow.
    internal static void Add(Grid grid, Control control, int column) => Mo2TableRow.Add(grid, control, column);
    internal static Button IconButton(string icon, string tip, Action click) => Mo2TableRow.IconButton(icon, tip, click);
    internal static ToggleButton Activation(string name, string tag, bool active, bool enabled, Func<Task<bool>> change, Func<bool>? canChange = null)
    {
        var toggle = new ToggleButton { Name = name, Tag = tag, Width = 30, Height = 28, MinWidth = 0, MinHeight = 0,
            Padding = new Thickness(0), IsChecked = active, IsEnabled = enabled, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        toggle.Template = new FuncControlTemplate<ToggleButton>((control, _) => {
            var check = new UnifiedIcon { Value = new ProjektankerIcon("mdi-check"), Size = 18, Foreground = Brush.Parse("#FB923C"), Opacity = control.IsChecked == true ? 1 : 0 };
            void Refresh() => check.Opacity = control.IsChecked == true ? 1 : 0;
            void Changed(object? sender, AvaloniaPropertyChangedEventArgs e) { if (e.Property == ToggleButton.IsCheckedProperty) Refresh(); }
            check.AttachedToVisualTree += (_, _) => { control.PropertyChanged += Changed; Refresh(); };
            check.DetachedFromVisualTree += (_, _) => control.PropertyChanged -= Changed;
            return new Border { CornerRadius = new CornerRadius(4), BorderBrush = Brush.Parse("#414147"), BorderThickness = new Thickness(1), Background = Brushes.Transparent, Child = check };
        });
        ToolTip.SetTip(toggle, "Enable or disable " + tag);
        Avalonia.Automation.AutomationProperties.SetName(toggle, "Enable or disable " + tag);
        toggle.Click += async (_, e) => {
            e.Handled = true; toggle.IsEnabled = false;
            try { toggle.IsChecked = await change(); } finally { toggle.IsEnabled = canChange?.Invoke() ?? enabled; }
        };
        return toggle;
    }
    internal static Control Create(Mo2LiveProfile profile, Mo2ModsAdapter adapter, Mo2LiveMod mod)
    {
        Mo2UiLatencyProbe.Count("Mod rows created");
        var target = profile.CurrentTarget;
        Task Run(Func<Task> action) => target == profile.CurrentTarget && profile.CanChangeOriginalUi ? action() : Task.CompletedTask;
        var actionSets = new List<MenuItem[]>();
        MenuItem[] Actions() {
            MenuItem[] items = [
            Mo2EntryMenu.Action("View files and conflicts…", () => Run(() => profile.ShowModDetails(mod.Id))),
            Mo2EntryMenu.Action((mod.State & 2) != 0 ? "Disable" : "Enable", () => Run(() => profile.ToggleMod(mod.Id)), mod.CanManage && !mod.IsSeparator),
            Mo2EntryMenu.Action("Move earlier", () => Run(() => adapter.Move(mod.Id, -1)), mod.CanManage),
            Mo2EntryMenu.Action("Move later", () => Run(() => adapter.Move(mod.Id, 1)), mod.CanManage),
            Mo2EntryMenu.Action("Remove…", () => Run(() => { profile.Remove([NexusMods.Abstractions.Loadouts.LoadoutItemId.From(mod.Id)]); return Task.CompletedTask; }), mod.CanManage)
            ];
            actionSets.Add(items); RefreshActions(); return items;
        }
        bool RefreshActions() {
            var latest = target == profile.CurrentTarget ? profile.FindMod(mod.Id) : null;
            var ready = latest is not null && profile.CanChangeOriginalUi;
            if (latest is not null) mod = latest;
            foreach (var items in actionSets) {
                items[0].IsEnabled = ready;
                items[1].Header = (mod.State & 2) != 0 ? "Disable" : "Enable";
                items[1].IsEnabled = ready && mod.CanManage && !mod.IsSeparator;
                items[2].IsEnabled = items[3].IsEnabled = items[4].IsEnabled = ready && mod.CanManage;
            }
            return ready;
        }
        var grip = Mo2EntryMenu.Create("Mod", mod.Name, Actions);
        grip.Width = Mo2TableRow.GripWidth;
        if (mod.IsSeparator) {
            var group = new Grid { ColumnDefinitions = new ColumnDefinitions("24,28,24,Auto,*"), Margin = new Thickness(0,2), MinHeight = 32 };
            Add(group, grip, 0);
            var expand = IconButton(adapter.IsCollapsed(mod.Name) ? "mdi-chevron-right" : "mdi-chevron-down", "Expand or collapse separator", () => {
                adapter.ToggleSeparator(mod.Name);
                if (group.Children.OfType<Button>().FirstOrDefault()?.Content is UnifiedIcon icon)
                    icon.Value = new ProjektankerIcon(adapter.IsCollapsed(mod.Name) ? "mdi-chevron-right" : "mdi-chevron-down");
            });
            expand.Name = "SeparatorExpandButton"; expand.Tag = mod.Name;
            // A disclosure chevron, not an action: match the weight of the tag icon
            // beside it rather than the full-size row action icons.
            if (expand.Content is UnifiedIcon chevron) { chevron.Size = 14; chevron.Opacity = .65; }
            Add(group, expand, 1);
            Add(group, new UnifiedIcon { Value = new ProjektankerIcon("mdi-tag-outline"), Size = 16, Opacity = .65 }, 2);
            var groupTitle = new TextBlock { Text = mod.DisplayName, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(6,0) };
            groupTitle.Name = "SeparatorTitle";
            groupTitle.Tag = mod.Name;
            ToolTip.SetTip(groupTitle, "Double-click to rename separator");
            groupTitle.DoubleTapped += async (_, e) => {
                e.Handled = true;
                if (target == profile.CurrentTarget && groupTitle.GetVisualAncestors().OfType<Mo2ModsView>().FirstOrDefault()?.ViewModel is { } page)
                    await page.RenameSeparatorDialog(mod.Name);
            };
            Add(group, groupTitle, 3);
            var count = new TextBlock { Name = "SeparatorModCount", Text = adapter.SeparatorCount(mod).ToString(), Opacity = .5, Margin = new Thickness(12,0,16,0), VerticalAlignment = VerticalAlignment.Center };
            Add(group, count, 4);
            group.SizeChanged += (_, _) => groupTitle.MaxWidth = Math.Max(0, group.Bounds.Width - 130);
            // The reference draws a category as a filled, full-width rounded bar
            // rather than a bare row; conflict highlighting still paints over this.
            var highlight = new Border { Name = "SeparatorHighlight", CornerRadius = new CornerRadius(8), IsHitTestVisible = false,
                Background = (IBrush)Application.Current!.FindResource("SurfaceMidBrush")! };
            var separatorContent = new Grid(); separatorContent.Children.Add(highlight); separatorContent.Children.Add(group);
            // Full width, like the reference draws a category: the entries beside it no
            // longer carry a surface of their own, so what marks out how wide an entry
            // is now is the band its hover and selection paint — the whole row. Inset
            // 16px, as this was, the bar sat visibly narrower than everything under it.
            // Its own surface is the one thing on the list that keeps one, which is what
            // separates a category from the entries it holds.
            var separator = new Border { Name = "ModSeparatorBar", CornerRadius = new CornerRadius(8), Margin = new Thickness(0),
                Background = (IBrush)Application.Current!.FindResource("SurfaceMidBrush")!, Child = separatorContent };
            TreeDataGridRow? owner = null;
            void Highlight() => highlight.Background = owner?.IsSelected == true
                ? (IBrush)Application.Current!.FindResource("SurfaceTranslucentMidBrush")!
                : owner?.IsPointerOver == true ? (IBrush)Application.Current!.FindResource("SurfaceTranslucentLowBrush")! : Brushes.Transparent;
            void RowChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
                if (e.Property == TreeDataGridRow.IsSelectedProperty || e.Property == Avalonia.Input.InputElement.IsPointerOverProperty) Highlight();
            }
            separator.AttachedToVisualTree += (_,_) => { owner = separator.FindAncestorOfType<TreeDataGridRow>(); if (owner is not null) owner.PropertyChanged += RowChanged; Highlight(); };
            separator.DetachedFromVisualTree += (_,_) => { if (owner is not null) owner.PropertyChanged -= RowChanged; owner = null; };
            void RefreshSeparator() {
                if (target != profile.CurrentTarget) return;
                RefreshActions(); groupTitle.Text = mod.DisplayName;
                count.Text = adapter.SeparatorCount(mod).ToString();
            }
            separator.AttachedToVisualTree += (_,_) => { profile.Changed += RefreshSeparator; RefreshSeparator(); };
            separator.DetachedFromVisualTree += (_,_) => profile.Changed -= RefreshSeparator;
            return separator;
        }
        var row = Columns(); row.Name = "ModRedesignRow";
        // Name column: the enable box then the mod's name, as MO2 draws its own name
        // column. The row's actions are on its right-click menu, again as MO2 has it,
        // so no column is spent on buttons that repeat what the menu already offers.
        var toggle = Activation("ModActivationToggle", mod.Name, (mod.State & 6) != 0, mod.CanManage && profile.CanChangeOriginalUi,
            async () => { await Run(() => profile.ToggleMod(mod.Id)); return (profile.FindMod(mod.Id)?.State & 6) != 0; },
            () => target == profile.CurrentTarget && profile.CanChangeOriginalUi && profile.FindMod(mod.Id)?.CanManage == true);
        var title = Mo2TableRow.Cell(mod.DisplayName, (mod.State & 6) != 0 ? .85 : .5);
        ToolTip.SetTip(title, string.Join("\n", new[] { mod.DisplayName, mod.Version, mod.Category, mod.Conflicts, mod.Flags }.Where(s => s.Length > 0)));
        var nameCell = new Grid { ColumnDefinitions = new ColumnDefinitions($"{Mo2TableRow.StatusColumn},*") };
        Mo2TableRow.Add(nameCell, toggle, 0); Mo2TableRow.Add(nameCell, title, 1);
        Add(row, nameCell, Name);
        row.ContextFlyout = Mo2EntryMenu.Flyout(Actions);

        // Conflicts, Flags and Content are glyph columns in MO2, each summarising
        // what its tooltip spells out. Material icons stand in for MO2's own.
        var conflicts = Glyph("ModConflictIcon", mod.Conflicts, "mdi-swap-vertical-bold");
        var flags = Glyph("ModFlagIcon", mod.Flags, "mdi-flag");
        var content = Glyph("ModContentIcon", mod.Content, "mdi-package-variant-closed");
        Add(row, conflicts, Conflicts); Add(row, flags, Flags); Add(row, content, Content);

        var category = Mo2TableRow.Cell(mod.Category, .6);
        var author = Mo2TableRow.Cell(mod.Author, .6);
        var uploader = Mo2TableRow.Cell(mod.Uploader, .6);
        var nexusId = Mo2TableRow.Cell(mod.NexusId > 0 ? mod.NexusId.ToString() : "", .6);
        var sourceGame = Mo2TableRow.Cell(mod.SourceGame, .6);
        var installation = Mo2TableRow.Cell(mod.InstallTime, .6);
        var priority = Mo2TableRow.Cell(mod.PriorityText, .6);
        var notes = Mo2TableRow.Cell(mod.Notes, .6);
        if (mod.Notes.Length > 0) ToolTip.SetTip(notes, mod.Notes);

        var version = Mo2TableRow.Cell(mod.Version, .6);
        // MO2 marks an available update on the version itself rather than in a column
        // of its own, so the newer version is shown beside the installed one.
        var updatePill = new Border { Name = "ModUpdatePill", CornerRadius = new CornerRadius(6), Padding = new Thickness(6,1),
            Background = Brush.Parse("#1D4ED8"), VerticalAlignment = VerticalAlignment.Center, Margin = Mo2TableRow.CellMargin,
            HorizontalAlignment = HorizontalAlignment.Left, IsVisible = mod.HasUpdate,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
                new UnifiedIcon { Value = new ProjektankerIcon("mdi-cloud-download"), Size = 14 },
                new TextBlock { Text = mod.NewestVersion, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 } } } };
        if (mod.HasUpdate) {
            version.IsVisible = false;
            ToolTip.SetTip(updatePill, $"Update available: {mod.Version} → {mod.NewestVersion}");
        }
        var versionCell = new Grid(); versionCell.Children.Add(version); versionCell.Children.Add(updatePill);
        Add(row, category, Category); Add(row, author, Author); Add(row, uploader, Uploader);
        Add(row, nexusId, NexusId); Add(row, sourceGame, SourceGame); Add(row, versionCell, Version);
        Add(row, installation, Installation); Add(row, priority, Priority); Add(row, notes, Notes);
        var isEndorsed = (mod.State & 0x10) != 0;
        // MO2 shows endorsement as one of the flag glyphs rather than a column, so it
        // rides along with the flags it belongs among.
        var endorsed = new UnifiedIcon { Name = "ModEndorsementIcon", Value = new ProjektankerIcon(isEndorsed ? "mdi-thumb-up" : "mdi-thumb-up-outline"), Size = 14,
            Opacity = mod.NexusId <= 0 ? 0 : isEndorsed ? 1 : .45, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(endorsed, isEndorsed ? "Endorsed on Nexus Mods" : "MO2 does not report this mod as endorsed");
        Add(row, endorsed, Flags);
        void Refresh() {
            var ready = RefreshActions();
            toggle.IsChecked = (mod.State & 6) != 0;
            toggle.IsEnabled = ready && mod.CanManage;
            title.Text = mod.DisplayName; title.Opacity = toggle.IsChecked == true ? .85 : .5;
            version.Text = mod.Version; category.Text = mod.Category;
            author.Text = mod.Author; uploader.Text = mod.Uploader;
            nexusId.Text = mod.NexusId > 0 ? mod.NexusId.ToString() : "";
            sourceGame.Text = mod.SourceGame; installation.Text = mod.InstallTime;
            priority.Text = mod.PriorityText; notes.Text = mod.Notes;
            ToolTip.SetTip(title, string.Join("\n", new[] { mod.DisplayName, mod.Version, mod.Category, mod.Conflicts, mod.Flags }.Where(s => s.Length > 0)));
            var nextEndorsed = (mod.State & 0x10) != 0;
            if (nextEndorsed != isEndorsed) {
                isEndorsed = nextEndorsed;
                endorsed.Value = new ProjektankerIcon(isEndorsed ? "mdi-thumb-up" : "mdi-thumb-up-outline");
            }
            endorsed.Opacity = mod.NexusId <= 0 ? 0 : (mod.State & 0x10) != 0 ? 1 : .45;
            ToolTip.SetTip(endorsed, (mod.State & 0x10) != 0 ? "Endorsed on Nexus Mods" : "MO2 does not report this mod as endorsed");
        }
        row.AttachedToVisualTree += (_,_) => { profile.Changed += Refresh; Refresh(); };
        row.DetachedFromVisualTree += (_,_) => profile.Changed -= Refresh;
        void Resize() => Fit(row, row.GetVisualAncestors().OfType<NexusMods.App.UI.WorkspaceSystem.PanelView>().FirstOrDefault()?.Bounds.Width ?? row.Bounds.Width);
        row.LayoutUpdated += (_, _) => Resize();
        return row;
    }
}
