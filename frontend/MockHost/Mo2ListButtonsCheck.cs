using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Every button My Mods and Plugins draw, pressed, with what pressing it did.
//
// The dead-control check asks whether each control's command can run, which says
// nothing about two things. A button can carry no command at all and act from a
// Click handler instead — most of these do — and such a button is invisible to a
// check that only looks at commands. And a command that can run is not a command
// that does anything: a button that says it opens a menu has to open one.
//
// So each button is pressed and the window looked at afterwards: a flyout or
// popup open, a dialog window up, or a change in what MO2 reports. A button that
// produces none of those is reported as doing nothing.
internal static class Mo2ListButtonsCheck
{
    // Buttons whose effect is deliberately not observable from here, with why.
    private static readonly Dictionary<string, string> Excused = new() {
        ["CloseTabButton"] = "closes the tab it is in, which the tab checks cover",
        ["AddTabButton1"] = "opens the new-tab page, which the workspace checks cover",
        ["AddTabButton2"] = "opens the new-tab page, which the workspace checks cover",
    };

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 600 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();
        using var turn = await Mo2CheckTurn.Take();
        var menu = live.ProfileMenu;
        var faults = new List<string>();
        var pressed = 0;

        // Searched from the page's own view rather than from a panel. The workspace
        // holds two panels at once, so reading "the selected panel" reported the
        // neighbour's toolbar twice over; and these pages build their bodies on a
        // later UI turn, so a panel found the moment its tab changes still holds
        // nothing but the panel's own chrome.
        var pages = new (string Page, NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel Item, Type View)[] {
            ("My Mods", menu.LeftMenuItemLoadout, typeof(Mo2ModsView)),
            ("Plugins", menu.LeftMenuItemExternalChanges!, typeof(Mo2PluginsView)),
        };

        foreach (var (page, item, viewType) in pages) {
            await Navigate(item);
            Avalonia.Controls.Control? view = null;
            for (var attempt = 0; attempt < 150 && view is null; attempt++) {
                await Task.Delay(100);
                window.UpdateLayout();
                view = window.GetVisualDescendants().OfType<Avalonia.Controls.Control>()
                    .FirstOrDefault(control => viewType.IsInstanceOfType(control) && control.Bounds.Height > 0);
            }
            if (view is null) { faults.Add($"{page} never drew its {viewType.Name}"); continue; }
            await Task.Delay(800);
            window.UpdateLayout();

            var buttons = view.GetVisualDescendants().OfType<Button>()
                .Where(button => button is not CheckBox and not RadioButton && button.IsEffectivelyVisible && button.Bounds.Width > 0)
                .GroupBy(button => button.Name ?? "(unnamed)")
                .Select(group => group.First())
                .ToArray();
            Console.WriteLine($"  {page}: {buttons.Length} distinct button(s)");

            foreach (var button in buttons) {
                var name = button.Name ?? "(unnamed)";
                if (Excused.TryGetValue(name, out var why)) { Console.WriteLine($"    {name}: excused, {why}"); continue; }
                var before = Snapshot(live, window, view);
                try {
                    // Raising Click only runs Click handlers. Button.OnClick is what
                    // opens an attached flyout and what runs a bound command, so a
                    // button that carries either is driven the way OnClick would.
                    if (button.Flyout is { } flyout) flyout.ShowAt(button);
                    else if (button.Command is { } command && command.CanExecute(button.CommandParameter))
                        command.Execute(button.CommandParameter);
                    else button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception error) { faults.Add($"{page}/{name} threw {error.GetType().Name}: {error.Message}"); continue; }
                pressed++;
                await Task.Delay(500);
                window.UpdateLayout();
                var effect = Describe(button, before, Snapshot(live, window, view));
                Console.WriteLine($"    {name} | {Label(button)} | enabled={button.IsEffectivelyEnabled} | {effect}");
                // A button that is greyed and does nothing is saying so; one that is
                // offered and does nothing is the fault.
                if (effect == "nothing" && button.IsEffectivelyEnabled)
                    faults.Add($"{page}/{name} ({Label(button)}) is enabled and did nothing");
                await Dismiss(window);
            }
        }

        if (faults.Count > 0) throw new Exception(string.Join("; ", faults));
        Console.WriteLine($"PASS list buttons: {pressed} button(s) on My Mods and Plugins each did something when pressed");
    }

    private sealed record State(int Windows, int Fields, string Status, int Mods, string Rows, int Controls, int Backups);

    private static State Snapshot(Mo2LiveWorkspace live, Window window, Avalonia.Controls.Control page) => new(
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows.Count(x => x.IsVisible) ?? 0,
        // An expanding search is an inline field appearing, not a popup.
        page.GetVisualDescendants().OfType<TextBox>().Count(box => box.IsEffectivelyVisible),
        live.Profile.Status,
        live.Profile.Mods.Count,
        string.Join(",", live.Profile.Mods.Select(mod => mod.Name + ":" + mod.State + ":" + mod.Priority).Take(40)),
        // A pane opening or closing inside the page, such as plugin details.
        page.GetVisualDescendants().OfType<Avalonia.Controls.Control>().Count(control => control.IsEffectivelyVisible),
        // Creating a backup writes a file and changes nothing on screen, so the
        // files themselves are what says it happened.
        CountBackups(live));

    private static string Describe(Button button, State before, State after)
    {
        // A flyout hosts its content in a popup root of its own, which is not a
        // visual descendant of this window, so the flyout itself is asked instead
        // of counting popups in the tree.
        if (button.Flyout is { IsOpen: true } || FlyoutBase.GetAttachedFlyout(button) is { IsOpen: true }) return "opened a menu";
        if (button.ContextFlyout is { IsOpen: true }) return "opened a menu";
        if (after.Windows > before.Windows) return "opened a dialog";
        if (after.Fields > before.Fields) return "opened its search field";
        if (after.Backups != before.Backups) return "wrote a backup";
        if (after.Rows != before.Rows) return "changed the list";
        if (after.Mods != before.Mods) return "changed the mod list";
        if (after.Controls != before.Controls) return "opened or closed a pane";
        if (after.Status != before.Status) return $"reported \"{after.Status}\"";
        return "nothing";
    }

    private static int CountBackups(Mo2LiveWorkspace live)
    {
        try {
            var profile = Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath);
            return Directory.Exists(profile)
                ? Directory.EnumerateFiles(profile).Count(file => Path.GetFileName(file).Contains(".txt."))
                : 0;
        } catch (IOException) { return 0; }
    }

    // Leave the window as it was found, so one button's menu is not the next
    // button's starting state.
    private static async Task Dismiss(Window window)
    {
        foreach (var button in window.GetVisualDescendants().OfType<Button>()) {
            if (button.Flyout is { IsOpen: true } flyout) flyout.Hide();
            if (FlyoutBase.GetAttachedFlyout(button) is { IsOpen: true } attached) attached.Hide();
        }
        var desktop = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        foreach (var extra in (desktop?.Windows ?? []).Where(x => x.IsVisible && !ReferenceEquals(x, window)).ToArray()) extra.Close();
        await Task.Delay(250);
    }

    private static string Label(Button button)
    {
        if (AutomationProperties.GetName(button) is { Length: > 0 } automation) return automation;
        if (button is StandardButton { Text: { Length: > 0 } text }) return text;
        if (ToolTip.GetTip(button) is string { Length: > 0 } tip) return tip.Split('\n')[0];
        if (button.Content is string { Length: > 0 } content) return content;
        return "(no label)";
    }

    private static Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel item) =>
        System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
            item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
}
