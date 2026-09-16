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
    //
    // MO2 also heads Author, Nexus ID, Source Game, Installation and Priority. Those
    // five are not drawn here: the first four say where a mod came from rather than
    // what it does in this profile, and MO2's own priority is the order the list is
    // already in — the row's place in it is the column. What they hold is still
    // MO2's and still reported: a mod's Information... entry carries all of it.
    internal const int Name = 0, Conflicts = 1, Flags = 2, Content = 3, Category = 4,
        Uploader = 5, Version = 6, Notes = 7;

    // Widths for MO2's own text size rather than the theme's: a column sized for
    // 14px text and 24px of padding took a third more room than the text in it
    // needs, and the panel ran out of room three columns earlier than MO2 does.
    internal static Grid Columns() => new() { MinWidth = 0, ColumnDefinitions = new ColumnDefinitions(
        "*,24,24,72,92,92,64,92") };

    internal static readonly (int Column, string Name)[] Headers = [
        (Name, "Mod Name"), (Conflicts, "Conflicts"), (Flags, "Flags"), (Content, "Content"),
        (Category, "Category"), (Uploader, "Uploader"), (Version, "Version"), (Notes, "Notes")];

    // Columns the user has switched off. The responsive widths below still apply: a
    // column shows only when it both fits and has not been hidden. Only the name is
    // not optional, matching MO2, which lets every other column be turned off.
    internal static readonly HashSet<int> HiddenColumns = [];
    internal static readonly (int Column, string Name)[] OptionalColumns =
        Headers.Where(x => x.Column != Name).ToArray();

    // Dropped from the right as the panel narrows, in reverse order of how much they
    // say about a mod: the glyph columns and the priority survive longest because
    // they are what the list is read by.
    // The widths above, and the panel width each column needs before it is worth
    // the room. Lowered with them: at MO2's density the same panel holds three more
    // of MO2's columns than these thresholds used to let through.
    private static readonly (int Column, double Width, double Threshold)[] Optional = [
        (Conflicts, 24, 250), (Flags, 24, 280), (Content, 72, 470), (Category, 92, 390),
        (Uploader, 92, 1070), (Version, 64, 330), (Notes, 92, 610)];

    // Room the category filter takes out of the pane when it is showing. The column
    // fitting below measures the panel, not the list, so without this the columns
    // believed they had the whole width and the name column — the one column that is
    // not optional — was squeezed to nothing to keep the others.
    internal static double SideWidth;

    internal static void Fit(Grid grid, double width) =>
        Mo2TableRow.Fit(grid, Math.Max(0, width - SideWidth), Optional, HiddenColumns.Contains,
            chrome: Mo2TableRow.RailWidth + Mo2TableRow.StatusColumn + 2 * Mo2PanelChrome.Padding);

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
        var toggle = new ToggleButton { Name = name, Tag = tag, Width = Mo2Density.Check, Height = Mo2Density.Check, MinWidth = 0, MinHeight = 0,
            Padding = new Thickness(0), IsChecked = active, IsEnabled = enabled, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        toggle.Template = new FuncControlTemplate<ToggleButton>((control, _) => {
            var check = new UnifiedIcon { Value = new ProjektankerIcon("mdi-check"), Size = Mo2Density.Check - 2, Foreground = Brush.Parse("#FB923C"), Opacity = control.IsChecked == true ? 1 : 0 };
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
        // The control this row ends up being, so a menu entry can reach the page it
        // is on — the dialogs belong to the page, not to a row.
        Control? owner = null;
        async Task Rename() {
            if (owner?.GetVisualAncestors().OfType<Mo2ModsView>().FirstOrDefault()?.ViewModel is not { } page) return;
            if (mod.IsSeparator) await page.RenameSeparatorDialog(mod.Name);
            else await page.RenameModDialog(mod.Name);
        }
        var actionSets = new List<object[]>();
        object[] Actions() {
            var items = Mo2ModMenu.Build(profile, adapter, () => mod, target, Run, Rename);
            actionSets.Add(items); RefreshActions(); return items;
        }
        bool RefreshActions() {
            var latest = target == profile.CurrentTarget ? profile.FindMod(mod.Id) : null;
            var ready = latest is not null && profile.CanChangeOriginalUi;
            if (latest is not null) mod = latest;
            // The menu is built for the mod as it then was; a row whose mod has since
            // changed hands or gone is left with nothing to take. Its shape follows
            // MO2's conditions and is settled when it opens, so what changes here is
            // whether its entries can be taken at all.
            foreach (var items in actionSets)
                foreach (var item in items.OfType<MenuItem>()) item.IsEnabled = ready && item.Tag is not false;
            return ready;
        }
        var grip = Mo2EntryMenu.Create("Mod", mod.Name, Actions);
        grip.Width = Mo2TableRow.GripWidth;
        if (mod.IsSeparator) {
            var group = new Grid { ColumnDefinitions = new ColumnDefinitions($"{Mo2TableRow.GripWidth},{Mo2TableRow.ActionSize},18,Auto,*"),
                Margin = new Thickness(0), Height = Mo2Density.Row };
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
            var tagIcon = new UnifiedIcon { Value = new ProjektankerIcon("mdi-tag-outline"), Size = Mo2Density.Glyph, Opacity = .65 };
            Add(group, tagIcon, 2);
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
            var highlight = new Border { Name = "SeparatorHighlight", CornerRadius = new CornerRadius(Mo2Density.Corner), IsHitTestVisible = false,
                Background = (IBrush)Application.Current!.FindResource("SurfaceMidBrush")! };
            var separatorContent = new Grid(); separatorContent.Children.Add(highlight); separatorContent.Children.Add(group);
            // Full width, like the reference draws a category: the entries beside it no
            // longer carry a surface of their own, so what marks out how wide an entry
            // is now is the band its hover and selection paint — the whole row. Inset
            // 16px, as this was, the bar sat visibly narrower than everything under it.
            // Its own surface is the one thing on the list that keeps one, which is what
            // separates a category from the entries it holds.
            var separator = new Border { Name = "ModSeparatorBar", CornerRadius = new CornerRadius(Mo2Density.Corner), Margin = new Thickness(0),
                Background = (IBrush)Application.Current!.FindResource("SurfaceMidBrush")!, Child = separatorContent };
            // The colour the user gave the separator in MO2, which MO2 paints the whole
            // row in. Without it every separator was the same grey bar and the colours
            // a list is organised by — the reason for colouring them at all — were lost
            // on the way across the bridge.
            void Paint() {
                var brush = Mo2Density.Brush(mod.Color);
                separator.Background = brush ?? (IBrush)Application.Current!.FindResource("SurfaceMidBrush")!;
                var ink = brush is SolidColorBrush solid ? Mo2Density.Ink(solid.Color) : null;
                // Cleared rather than set to null when the separator has no colour of
                // its own: a local null is a brush that paints nothing, which would
                // leave the name and the count invisible rather than in the theme's ink.
                void Write(AvaloniaObject part, AvaloniaProperty property) {
                    if (ink is null) part.ClearValue(property); else part.SetValue(property, ink);
                }
                Write(groupTitle, TextBlock.ForegroundProperty);
                Write(count, TextBlock.ForegroundProperty);
                Write(expand, TemplatedControl.ForegroundProperty);
                Write(tagIcon, TemplatedControl.ForegroundProperty);
            }
            TreeDataGridRow? host = null;
            void Highlight() => highlight.Background = host?.IsSelected == true
                ? (IBrush)Application.Current!.FindResource("SurfaceTranslucentMidBrush")!
                : host?.IsPointerOver == true ? (IBrush)Application.Current!.FindResource("SurfaceTranslucentLowBrush")! : Brushes.Transparent;
            void RowChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
                if (e.Property == TreeDataGridRow.IsSelectedProperty || e.Property == Avalonia.Input.InputElement.IsPointerOverProperty) Highlight();
            }
            separator.AttachedToVisualTree += (_,_) => { host = separator.FindAncestorOfType<TreeDataGridRow>(); if (host is not null) host.PropertyChanged += RowChanged; Highlight(); };
            separator.DetachedFromVisualTree += (_,_) => { if (host is not null) host.PropertyChanged -= RowChanged; host = null; };
            void RefreshSeparator() {
                if (target != profile.CurrentTarget) return;
                RefreshActions(); groupTitle.Text = mod.DisplayName;
                count.Text = adapter.SeparatorCount(mod).ToString();
                Paint();
            }
            Paint();
            owner = separator;
            separator.AttachedToVisualTree += (_,_) => { profile.Changed += RefreshSeparator; RefreshSeparator(); };
            separator.DetachedFromVisualTree += (_,_) => profile.Changed -= RefreshSeparator;
            return separator;
        }
        var row = Columns(); row.Name = "ModRedesignRow"; owner = row;
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
        var uploader = Mo2TableRow.Cell(mod.Uploader, .6);
        var notes = Mo2TableRow.Cell(mod.Notes, .6);
        if (mod.Notes.Length > 0) ToolTip.SetTip(notes, mod.Notes);
        // MO2 paints an ordinary mod's colour behind its Notes cell rather than across
        // the row, which is where a user who colours mods rather than separators looks
        // for it.
        var notesCell = new Border { Name = "ModNotesCell", Child = notes, Margin = new Thickness(0, 1), CornerRadius = new CornerRadius(3) };
        void PaintNotes() {
            var brush = Mo2Density.Brush(mod.NotesColor);
            notesCell.Background = brush;
            if (brush is SolidColorBrush solid) notes.Foreground = Mo2Density.Ink(solid.Color);
            else notes.ClearValue(TextBlock.ForegroundProperty);
            notes.Opacity = brush is null ? .6 : 1;
        }
        PaintNotes();
        // MO2 offers its colour actions on an ordinary mod only where the colour is
        // shown — a right-click in the Notes column, not anywhere on the row
        // (modlistcontextmenu.cpp, addRegularActions). A separator's colour is on its
        // own menu instead, because a separator has no Notes cell to paint.
        if (!mod.IsSeparator && !mod.IsOverwrite)
            notesCell.ContextFlyout = Mo2EntryMenu.Flyout(() => [
                Mo2EntryMenu.ColorMenu("Select Color...", mod.Color.Length > 0 || mod.NotesColor.Length > 0,
                    color => Run(() => profile.SetModColor(mod.Name, color, target)))]);

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
        Add(row, category, Category); Add(row, uploader, Uploader);
        Add(row, versionCell, Version); Add(row, notesCell, Notes);
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
            uploader.Text = mod.Uploader;
            notes.Text = mod.Notes; PaintNotes();
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
