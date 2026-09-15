using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.TopBar;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

// Opt-in check for the header sidebar toggle, matching Vortex's modern layout:
// the control sits in the header left of the page title, uses its
// left_panel_open/left_panel_close geometry and its Open/Collapse menu wording.
internal static class Mo2SidebarToggleCheck
{
    private static string? ReadPreference()
    {
        try { return File.ReadAllText(Mo2CollapsibleSidebar.StatePath); } catch (IOException) { return null; }
    }

    internal static async Task Run(Window window)
    {
        await Task.Delay(2000);
        var toggle = window.GetVisualDescendants().OfType<StandardButton>().SingleOrDefault(x => x.Name == "ToggleSidebarButton")
            ?? throw new Exception("Sidebar toggle button not found");
        // The column is Auto: the sidebar owns the animated width, so every read
        // has to wait for the transition to settle rather than sample it mid-flight.
        var sidebar = window.GetVisualDescendants().OfType<ContentControl>().Single(x => x.Name == "Mo2Sidebar");
        // Width is the animated property, so reading it straight after a click
        // returns an interpolated value. Wait for it to arrive instead.
        async Task<double> Settled(double? expected = null)
        {
            var last = double.NaN;
            for (var attempt = 0; attempt < 60; attempt++) {
                await Task.Delay(50);
                if (expected is { } target) { if (Math.Abs(sidebar.Width - target) < .01) return target; }
                else if (sidebar.Bounds.Width > 0 && Math.Abs(sidebar.Bounds.Width - last) < .01) return Math.Round(sidebar.Bounds.Width);
                last = sidebar.Bounds.Width;
            }
            throw new Exception(expected is { } want
                ? $"Sidebar width stayed at {sidebar.Width:F0} instead of reaching {want:F0}"
                : "Sidebar width never settled");
        }
        await Settled();
        var originalWidth = sidebar.Width;
        var original = ReadPreference();
        try {
            if (toggle.GetVisualAncestors().OfType<TopBarView>().FirstOrDefault() is null)
                throw new Exception("Toggle is not inside the top bar");
            var host = (Panel)toggle.Parent!;
            var title = host.Children.OfType<TextBlock>().FirstOrDefault(x => x.Name == "ActiveWorkspaceTitleTextBlock")
                ?? throw new Exception("Toggle is not beside the workspace title");
            if (host.Children.IndexOf(toggle) != 0 || host.Children.IndexOf(toggle) > host.Children.IndexOf(title))
                throw new Exception("Toggle does not lead the header, as Vortex places it");
            foreach (var icon in new[] { Mo2CollapsibleSidebar.OpenIcon, Mo2CollapsibleSidebar.CloseIcon }) {
                var geometry = icon.Value.AsT4.Geometry ?? throw new Exception("Icon has no geometry");
                if (geometry.Bounds.Width <= 0 || geometry.Bounds.Height <= 0)
                    throw new Exception("Vortex panel geometry did not parse into a drawable shape");
            }
            if (ReferenceEquals(Mo2CollapsibleSidebar.OpenIcon, Mo2CollapsibleSidebar.CloseIcon))
                throw new Exception("Both states share one icon");

            // The toggle, the workspace title and the window controls all sit in the
            // header and must share one centre line.
            double Centre(Visual control) => control.TranslatePoint(new Point(0, control.Bounds.Height / 2), window)!.Value.Y;
            var close = window.GetVisualDescendants().OfType<StandardButton>().Single(x => x.Name == "CloseButton");
            var centres = new[] { ("toggle", Centre(toggle)), ("title", Centre(title)), ("window controls", Centre(close)) };
            var spread = centres.Max(x => x.Item2) - centres.Min(x => x.Item2);
            if (spread > 1.5) throw new Exception("Header items are not on one line: " +
                string.Join(", ", centres.Select(x => $"{x.Item1} {x.Item2:F1}")));
            // A stretched text block centres its box while drawing its glyphs at the
            // top of it, so matching box centres alone does not mean matching text.
            if (title.Bounds.Height - title.DesiredSize.Height > 1)
                throw new Exception($"Title is stretched to {title.Bounds.Height:F1} against its {title.DesiredSize.Height:F1} text height, " +
                    "so its glyphs sit above the icons beside it");

            // The toggle draws a geometry icon while its neighbours draw icon-font
            // icons, which are sized by a different property. They must still match.
            var back = window.GetVisualDescendants().OfType<StandardButton>().Single(x => x.Name == "GoBackInHistory");
            double IconHeight(StandardButton button) => button.GetVisualDescendants().OfType<UnifiedIcon>()
                .Select(x => x.Bounds.Height).Where(x => x > 0).DefaultIfEmpty(0).Max();
            var toggleIcon = IconHeight(toggle);
            var neighbourIcon = IconHeight(back);
            if (toggleIcon <= 0 || neighbourIcon <= 0) throw new Exception("Header icons did not render");
            if (Math.Abs(toggleIcon - neighbourIcon) > 2)
                throw new Exception($"Toggle icon is {toggleIcon:F0}px against {neighbourIcon:F0}px on the buttons beside it");

            var states = new List<string>();
            var width = originalWidth;
            for (var step = 0; step < 3; step++) {
                var collapsed = width == 64;
                var expectedIcon = collapsed ? Mo2CollapsibleSidebar.OpenIcon : Mo2CollapsibleSidebar.CloseIcon;
                var expectedText = collapsed ? "Open menu" : "Collapse menu";
                if (!collapsed && width != 232) throw new Exception("Unexpected sidebar width " + width);
                if (!ReferenceEquals(toggle.LeftIcon, expectedIcon)) throw new Exception("Wrong panel icon while " + (collapsed ? "collapsed" : "expanded"));
                if ((ToolTip.GetTip(toggle) as string) != expectedText) throw new Exception("Tooltip is not Vortex's wording: " + ToolTip.GetTip(toggle));
                if (Avalonia.Automation.AutomationProperties.GetName(toggle) != expectedText) throw new Exception("Automation name is not Vortex's wording");
                states.Add((collapsed ? "collapsed" : "expanded") + "/" + expectedText);
                if (step == 2) break;
                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                width = await Settled(collapsed ? 232 : 64);
                if (sidebar.Bounds.Width <= 0) throw new Exception("Sidebar has no rendered width after the transition");
                if (ReadPreference() != (collapsed ? "false" : "true")) throw new Exception("Preference was not persisted as " + !collapsed);
            }
            if (states[0] != states[2]) throw new Exception("Two clicks did not restore the original state");
            Console.WriteLine($"PASS sidebar toggle: header placement before the title, Vortex panel geometry, {string.Join(" → ", states)}, preference persisted and restored");
        } finally {
            if (Math.Abs(sidebar.Width - originalWidth) > .01) {
                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Settled(originalWidth);
                Console.WriteLine($"Restored sidebar width {sidebar.Width} and preference {ReadPreference()} (was {original ?? "unset"})");
            }
        }
    }
}
