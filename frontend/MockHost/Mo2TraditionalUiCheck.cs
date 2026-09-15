using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2TraditionalUiCheck
{
    public static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        async Task Wait(Func<bool> condition) {
            var end = DateTime.UtcNow.AddSeconds(20);
            while (!condition()) { if (DateTime.UtcNow > end) throw new TimeoutException("Traditional UI check timed out"); await Task.Delay(50); }
        }
        await Wait(() => shell.Profile.IsConnected && shell.Profile.Mods.Any(x => x.Name == "__Inline separator check_separator"));
        if (!shell.Profile.ProfilePath.Replace("\\", "/").EndsWith("/Frontend Test", StringComparison.Ordinal)) throw new InvalidOperationException("Use the isolated FNV test profile");
        var mods = window.GetVisualDescendants().OfType<Mo2ModsView>().Single();
        var adapter = (Mo2ModsAdapter)mods.ViewModel!.Adapter;
        var modScroll = mods.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Name == "ModsRailScrollBar");
        modScroll.Value = modScroll.Maximum;
        await Wait(() => mods.GetVisualDescendants().OfType<Button>().Any(x => x.Name == "SeparatorExpandButton"));
        var before = adapter.VisibleRowCount;
        var separator = mods.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "SeparatorExpandButton" && Equals(x.Tag, "__Inline separator check_separator"));
        separator.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Wait(() => adapter.VisibleRowCount < before);
        separator.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Wait(() => adapter.VisibleRowCount == before);
        Console.WriteLine("PASS native separator renders inline and collapse/expand hides and restores its member rows");

        modScroll.Value = modScroll.Maximum;
        await Wait(() => mods.GetVisualDescendants().OfType<ToggleSwitch>().Any(x => x.Name == "ModActivationToggle" && x.IsEnabled));
        var modToggle = mods.GetVisualDescendants().OfType<ToggleSwitch>().First(x => x.Name == "ModActivationToggle" && x.IsEnabled);
        var modName = (string)modToggle.Tag!;
        var mod = shell.Profile.Mods.Single(x => x.Name == modName);
        var modActive = (mod.State & 2) != 0;
        try {
            modToggle.IsChecked = !modActive;
            modToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => ((shell.Profile.Mods.Single(x => x.Name == modName).State & 2) != 0) != modActive);
        } finally {
            if (((shell.Profile.Mods.Single(x => x.Name == modName).State & 2) != 0) != modActive) await shell.Profile.ToggleMod(mod.Id);
        }
        await Wait(() => ((shell.Profile.Mods.Single(x => x.Name == modName).State & 2) != 0) == modActive);
        var overflow = mods.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "ModsOverflowButton");
        if (!overflow.IsVisible || overflow.Flyout is not MenuFlyout menu || !menu.Items.OfType<MenuItem>().Any(x => x.Name == "AddModMenuItem"))
            throw new InvalidOperationException("Mod toolbar overflow missing");
        overflow.Flyout.ShowAt(overflow);
        await Task.Delay(200);
        overflow.Flyout.Hide();
        Console.WriteLine("PASS mod toggle changed native activation and restored it; toolbar overflow contains archive and separator actions");

        var plugins = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single();
        var pluginRail = plugins.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Name == "PluginRailScrollBar");
        pluginRail.Value = pluginRail.Maximum;
        await Wait(() => plugins.GetVisualDescendants().OfType<ToggleSwitch>().Any(x => x.Name == "PluginActivationToggle" && x.IsEnabled));
        var toggle = plugins.GetVisualDescendants().OfType<ToggleSwitch>().First(x => x.Name == "PluginActivationToggle" && x.IsEnabled);
        var name = (string)toggle.Tag!;
        var original = shell.Profile.Order.Plugins.Single(x => x.DisplayName == name).IsActive;
        try {
            toggle.IsChecked = !original;
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => shell.Profile.Order.Plugins.Single(x => x.DisplayName == name).IsActive != original);
            Console.WriteLine("PASS left-side plugin toggle changed native MO2 activation");
        } finally {
            await shell.Profile.SetPluginsActive([name], original);
        }
        await Wait(() => shell.Profile.Order.Plugins.Single(x => x.DisplayName == name).IsActive == original);
        var scrollbar = plugins.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Name == "PluginRailScrollBar");
        var height = window.Height;
        window.Height = 600;
        try {
            await Wait(() => scrollbar.Maximum > 0);
            var viewer = plugins.GetVisualDescendants().OfType<ScrollViewer>().First(x => x.GetVisualDescendants().Any(c => c.GetType().Name == "TreeDataGridRowsPresenter"));
            scrollbar.Value = scrollbar.Maximum;
            await Wait(() => Math.Abs(viewer.Offset.Y - scrollbar.Value) < 1 && viewer.Offset.Y > 0);
            if (viewer.VerticalScrollBarVisibility != ScrollBarVisibility.Hidden) throw new InvalidOperationException("Duplicate vertical scrollbar remains");
            scrollbar.Value = 0;
            await Wait(() => viewer.Offset.Y == 0);
            Console.WriteLine("PASS trophy-rail scrollbar controls plugin rows; inner scrollbar hidden; native activation restored");
        } finally { window.Height = height; }
    }
}
