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
    internal static Grid Columns() => new() { MinWidth = 0, ColumnDefinitions = new ColumnDefinitions(
        $"{Mo2TableRow.GripColumn},{Mo2TableRow.StatusColumn},*,80,130,84,{Mo2TableRow.ActionsColumn}") };
    // Columns the user has switched off through the Mods view-options action. The
    // responsive widths below still apply: a column shows only when it both fits and
    // has not been hidden. Status, Mod name and Actions are not optional.
    internal static readonly HashSet<int> HiddenColumns = [];
    internal static readonly (int Column, string Name)[] OptionalColumns =
        [(3, "Version"), (4, "Category"), (5, "Endorsed")];

    internal static void Fit(Grid grid, double width)
    {
        Mo2TableRow.Fit(grid, width, [(3, 80, 610), (4, 130, 800), (5, 84, 940)], HiddenColumns.Contains);
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
        Add(row, grip, 0);
        // Just the enable box. It used to be paired with a caret opening the same
        // actions, which the row already offers twice over — from its grip and from
        // the caret beside the remove action — so the third copy only crowded the
        // checkbox it sat against.
        var toggle = Activation("ModActivationToggle", mod.Name, (mod.State & 6) != 0, mod.CanManage && profile.CanChangeOriginalUi,
            async () => { await Run(() => profile.ToggleMod(mod.Id)); return (profile.FindMod(mod.Id)?.State & 6) != 0; },
            () => target == profile.CurrentTarget && profile.CanChangeOriginalUi && profile.FindMod(mod.Id)?.CanManage == true);
        Add(row, toggle, 1);
        var title = Mo2TableRow.Cell(mod.DisplayName, (mod.State & 6) != 0 ? .85 : .5);
        ToolTip.SetTip(title, string.Join("\n", new[] { mod.DisplayName, mod.Version, mod.Category, mod.Conflicts, mod.Flags }.Where(s => s.Length > 0))); Add(row, title, 2);
        var version = Mo2TableRow.Cell(mod.Version, .6);
        // An available update is a filled pill with a download glyph, as the reference
        // shows, in place of the plain version text.
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
        var category = Mo2TableRow.Cell(mod.Category, .6);
        var versionCell = new Grid(); versionCell.Children.Add(version); versionCell.Children.Add(updatePill); Add(row, versionCell, 3); Add(row, category, 4);
        var isEndorsed = (mod.State & 0x10) != 0;
        var endorsed = new UnifiedIcon { Name = "ModEndorsementIcon", Value = new ProjektankerIcon(isEndorsed ? "mdi-thumb-up" : "mdi-thumb-up-outline"), Size = 18,
            Opacity = mod.NexusId <= 0 ? 0 : (mod.State & 0x10) != 0 ? 1 : .45 };
        ToolTip.SetTip(endorsed, (mod.State & 0x10) != 0 ? "Endorsed on Nexus Mods" : "MO2 does not report this mod as endorsed");
        Add(row, endorsed, 5);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var remove = IconButton("mdi-trash-can-outline", "Remove mod…", () => { if (target == profile.CurrentTarget && profile.CanChangeOriginalUi) profile.Remove([NexusMods.Abstractions.Loadouts.LoadoutItemId.From(mod.Id)]); });
        remove.IsEnabled = mod.CanManage; actions.Children.Add(remove);
        var menu = Mo2EntryMenu.Flyout(Actions);
        var more = IconButton("mdi-menu-down", "Mod actions", () => { }); more.Flyout = menu; actions.Children.Add(more); Add(row, actions, 6);
        void Refresh() {
            var ready = RefreshActions();
            toggle.IsChecked = (mod.State & 6) != 0;
            toggle.IsEnabled = remove.IsEnabled = ready && mod.CanManage;
            title.Text = mod.DisplayName; title.Opacity = toggle.IsChecked == true ? .85 : .5;
            version.Text = mod.Version; category.Text = mod.Category;
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
