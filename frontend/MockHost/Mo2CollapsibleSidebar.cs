using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.LeftMenu.Items;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal static class Mo2CollapsibleSidebar
{
    // Vortex's own left_panel_open/left_panel_close geometry, copied from its
    // renderer icon-paths.ts so the toggle draws the same symbol we do.
    private static readonly Geometry PanelOpen = StreamGeometry.Parse(
        "M12.5 8v8l4-4-4-4ZM5 21q-0.825 0-1.4125-0.5875T3 19v-14q0-0.825 0.5875-1.4125T5 3h14q0.825 0 1.4125 0.5875T21 5v14q0 0.825-0.5875 1.4125T19 21H5Zm3-2v-14H5v14h3Zm2 0h9v-14H10v14Zm-2 0H5h3Z");
    private static readonly Geometry PanelClose = StreamGeometry.Parse(
        "M16.5 16v-8L12.5 12l4 4ZM5 21q-0.825 0-1.4125-0.5875T3 19v-14q0-0.825 0.5875-1.4125T5 3h14q0.825 0 1.4125 0.5875T21 5v14q0 0.825-0.5875 1.4125T19 21H5Zm3-2v-14H5v14h3Zm2 0h9v-14H10v14Zm-2 0H5h3Z");
    internal static readonly IconValue OpenIcon = new AvaloniaPathIcon(PanelOpen);
    internal static readonly IconValue CloseIcon = new AvaloniaPathIcon(PanelClose);
    internal static string StatePath => Mo2ConfigPaths.Combine("sidebar-collapsed.json");
    internal static void Attach(Grid layout, ContentControl sidebar, Control gameMenu, Control homeMenu, Mo2LaunchPanel launcher, Mo2LiveProfile profile, Panel toggleHost)
    {
        var collapsed = false;
        try { collapsed = System.Text.Json.JsonSerializer.Deserialize<bool>(File.ReadAllText(StatePath)); } catch (IOException) { } catch (System.Text.Json.JsonException) { }
        // Vortex keeps this control in the header, left of the page title,
        // rather than at the foot of the menu it collapses.
        var toggle = new StandardButton { Name = "ToggleSidebarButton", Type = StandardButton.Types.Tertiary,
            Fill = StandardButton.Fills.None, Size = StandardButton.Sizes.Medium, ShowLabel = false,
            ShowIcon = StandardButton.ShowIconOptions.Left, VerticalAlignment = VerticalAlignment.Center };
        toggleHost.Children.Insert(0, toggle);
        // UnifiedIcon binds Width/Height to a PathIcon but not Size, and the theme
        // sizes icons through Size. A geometry icon therefore draws at its natural
        // size and reads larger than the icon-font buttons beside it.
        toggle.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => {
            foreach (var icon in toggle.GetVisualDescendants().OfType<UnifiedIcon>()) {
                icon.Width = icon.Height = 20; icon.Size = 20;
            }
        }, DispatcherPriority.Loaded);
        // Vortex animates its menu between widths. GridLength is not animatable, so
        // the column follows the sidebar's own transitioned width instead.
        layout.ColumnDefinitions[1].Width = GridLength.Auto;
        sidebar.Transitions = new Transitions { new DoubleTransition {
            Property = Layoutable.WidthProperty, Duration = TimeSpan.FromMilliseconds(160), Easing = new CubicEaseOut() } };
        var originalLaunchParent = (Grid)launcher.Parent!;
        var compactLaunch = new StackPanel { Spacing = 4, Margin = new Thickness(0,8) };
        var launch = new Button { Name = "CompactLaunchButton", Command = launcher.Model.Command, Height = 32,
            Content = new UnifiedIcon { Value = new ProjektankerIcon("mdi-play"), Size = 20 } };
        var tools = new Button { Name = "CompactLaunchToolsButton", Height = 32,
            Content = new UnifiedIcon { Value = new ProjektankerIcon("mdi-menu-down"), Size = 20 } };
        ToolTip.SetTip(tools, "Choose launch tool and pinned tools");
        var toolFlyout = new Flyout { Placement = PlacementMode.RightEdgeAlignedBottom };
        tools.Flyout = toolFlyout;
        compactLaunch.Children.Add(tools); compactLaunch.Children.Add(launch);
        var expandedGroups = new Dictionary<Expander,(bool Expanded, double MinWidth, Thickness Padding)>();
        void ApplyMenu(Control menu) {
            // Vortex separates its menu buttons by a hair; NMA's own stacks set no
            // spacing, which reads as cramped next to both apps.
            foreach (var stack in menu.GetLogicalDescendants().OfType<StackPanel>().Where(x => x.Children.Any(child => child is LeftMenuItemView)))
                stack.Spacing = 2;
            foreach (var item in menu.GetLogicalDescendants().OfType<LeftMenuItemView>()) {
                var button = item.FindControl<Button>("NavButton");
                var icon = item.FindControl<UnifiedIcon>("LeftIcon");
                if (button is null || icon is null) continue;
                item.FindControl<Control>("LabelTextBlock")!.IsVisible = !collapsed;
                item.FindControl<Control>("RightContentControl")!.IsVisible = !collapsed;
                // Clearing restores the theme's own nav-button metrics. Writing a
                // captured value back would set a local value instead, which beats
                // the style and permanently shrank the expanded rows.
                if (collapsed) { button.Padding = new Thickness(8); icon.Margin = new Thickness(0); }
                else { button.ClearValue(TemplatedControl.PaddingProperty); icon.ClearValue(Layoutable.MarginProperty); }
                ToolTip.SetTip(item, collapsed ? item.FindControl<TextBlock>("LabelTextBlock")!.Text : null);
            }
            foreach (var expander in menu.GetLogicalDescendants().OfType<Expander>()) {
                if (collapsed) {
                    expandedGroups.TryAdd(expander, (expander.IsExpanded, expander.MinWidth, expander.Padding));
                    expander.IsExpanded = true; expander.MinWidth = 0; expander.Padding = new Thickness(0);
                } else if (expandedGroups.Remove(expander, out var original)) {
                    expander.IsExpanded = original.Expanded; expander.MinWidth = original.MinWidth; expander.Padding = original.Padding;
                }
                // Vortex retains section icons but removes section headings in compact mode.
                var header = expander.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault(button => button.Name == "ExpanderHeader");
                if (header is not null) header.IsVisible = !collapsed;
            }
        }
        void Apply() {
            sidebar.Width = collapsed ? 64 : 232;
            // Reuse the two icon values: assigning a fresh IconValue rebuilds
            // UnifiedIcon's inner control on every Changed notification.
            toggle.LeftIcon = collapsed ? OpenIcon : CloseIcon;
            ToolTip.SetTip(toggle, collapsed ? "Open menu" : "Collapse menu");
            Avalonia.Automation.AutomationProperties.SetName(toggle, collapsed ? "Open menu" : "Collapse menu");
            toolFlyout.Hide();
            if (collapsed && ReferenceEquals(launcher.Parent, originalLaunchParent)) {
                originalLaunchParent.Children.Remove(launcher);
                launcher.Width = 240; toolFlyout.Content = launcher;
                Grid.SetRow(compactLaunch, 1); originalLaunchParent.Children.Add(compactLaunch);
            } else if (!collapsed && originalLaunchParent.Children.Contains(compactLaunch)) {
                originalLaunchParent.Children.Remove(compactLaunch); toolFlyout.Content = null;
                launcher.Width = double.NaN; originalLaunchParent.Children.Add(launcher);
            }
            ApplyMenu(gameMenu); ApplyMenu(homeMenu);
        }
        toggle.Click += (_,_) => {
            collapsed = !collapsed; Apply();
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
                File.WriteAllText(StatePath + ".tmp", System.Text.Json.JsonSerializer.Serialize(collapsed));
                File.Move(StatePath + ".tmp", StatePath, true);
            } catch (IOException) { ToolTip.SetTip(toggle, "Sidebar changed; preference could not be saved."); }
        };
        gameMenu.AttachedToVisualTree += (_,_) => Dispatcher.UIThread.Post(() => ApplyMenu(gameMenu), DispatcherPriority.Loaded);
        homeMenu.AttachedToVisualTree += (_,_) => Dispatcher.UIThread.Post(() => ApplyMenu(homeMenu), DispatcherPriority.Loaded);
        void LaunchTip() => ToolTip.SetTip(launch, "Launch " + launcher.Model.SelectedExecutable);
        profile.Changed += LaunchTip; LaunchTip();
        sidebar.DetachedFromVisualTree += (_,_) => { if (sidebar.GetVisualRoot() is null) toolFlyout.Hide(); };
        if (Environment.GetEnvironmentVariable("MO2_TRACE_SIDEBAR") == "1") {
            gameMenu.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_,e) => {
                Console.WriteLine($"Sidebar input source={e.Source?.GetType().Name} modifiers={e.KeyModifiers}");
                foreach (var item in gameMenu.GetLogicalDescendants().OfType<LeftMenuItemView>()) {
                    var b = item.FindControl<Button>("NavButton")!;
                    Console.WriteLine($"Sidebar item {item.Name}: bounds={b.Bounds} enabled={b.IsEffectivelyEnabled} visible={b.IsEffectivelyVisible} at={b.TranslatePoint(default,layout)}");
                }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
        }
        Apply();
    }
}
