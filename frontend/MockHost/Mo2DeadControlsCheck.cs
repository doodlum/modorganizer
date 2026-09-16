using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Controls that are drawn and can never be used.
//
// This frontend is built on another application's views, and it reaches into them
// and re-points what it can. What it cannot re-point was left where it was, bound
// to a command that answers "no" for good: five entries in the top bar's menus —
// Nexus profile, Premium, Forums, the changelog and the welcome message — were
// drawn greyed on every run, for every user, forever. A greyed entry says "not
// now"; these mean "never", and the two are not the same thing to read.
//
// So: every command a drawn control is bound to is asked whether it can run. One
// that cannot is only allowed here if it is named below with the condition that
// would let it — a sign-out that needs an account, MO2's log folder that needs a
// connected instance. Anything else is a control to take out rather than to grey.
internal static class Mo2DeadControlsCheck
{
    // Bound to a command that is off right now, for a reason that can change.
    // Named with the condition, so "it is disabled" is never the whole answer.
    private static readonly Dictionary<string, string> Conditional = new() {
        ["SignOutMenuItem"] = "there is a Nexus account signed in",
        ["ViewAppLogsMenuItem"] = "an MO2 instance is connected and has a log folder",
        ["GoBackInHistory"] = "there is a page behind this one",
        ["GoForwardInHistory"] = "a page has been gone back from",
        ["CloseTabButton"] = "the panel holds a tab that may be closed",
    };

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();

        var menu = live.ProfileMenu;
        using var turn = await Mo2CheckTurn.Take();
        // The run row is folded into a flyout when the sidebar is closed, and a
        // control in a closed flyout cannot be judged either way.
        using var sidebar = await Mo2SidebarState.Open(window, live);

        var panels = live.WorkspaceController.ActiveWorkspace.Panels;
        var selected = panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
        var restore = selected?.SelectedTab.Contents.ViewModel is ScenarioLoadOrderPage
            ? menu.LeftMenuItemExternalChanges! : menu.LeftMenuItemLoadout;

        var faults = new List<string>();
        var examined = 0;
        var excused = new List<string>();

        // The window's own chrome first, then every page, because a page's controls
        // exist only while it is open.
        var pages = new (string Name, Func<Task> Open)[] {
            ("the window", () => Task.CompletedTask),
            ("my-mods", () => Navigate(menu.LeftMenuItemLoadout)),
            ("plugins", () => Navigate(menu.LeftMenuItemExternalChanges!)),
            ("data", () => Navigate(menu.DataItem)),
            ("archives", () => Navigate(menu.ArchivesItem)),
            ("saves", () => Navigate(menu.SavesItem)),
            ("downloads", () => Navigate(menu.LeftMenuItemLibrary)),
            ("tools", () => Navigate(menu.ToolsItem)),
            ("logs", () => Navigate(menu.LogsItem)),
            ("overwrite", () => Navigate(menu.OverwriteItem)),
            ("external-files", () => Navigate(menu.ExternalFilesItem)),
        };

        foreach (var (name, open) in pages) {
            await open();
            await Task.Delay(700);
            window.UpdateLayout();
            // The top bar's two menus are markup rather than built on opening, but
            // their entries are in no visual tree until the menu is shown — so a
            // walk of the window judged everything except the entries this check
            // was written for. Opened here, judged, and closed again.
            var open_menus = new List<MenuFlyout>();
            if (name == "the window")
                foreach (var host in new[] { "HelpButton", "AvatarMenuItemButton" })
                    if (window.GetVisualDescendants().OfType<Button>().FirstOrDefault(x => x.Name == host)?.Flyout is MenuFlyout flyout) {
                        flyout.ShowAt(window);
                        await Task.Delay(500);
                        open_menus.Add(flyout);
                    }
            window.UpdateLayout();
            foreach (var control in Drawn(window, open_menus)) {
                var command = control switch {
                    MenuItem item => item.Command,
                    Button button => button.Command,
                    _ => null,
                };
                if (command is null) continue;
                var parameter = control switch {
                    MenuItem item => item.CommandParameter,
                    Button button => button.CommandParameter,
                    _ => null,
                };
                // A NavigationInformation command answers about the navigation it is
                // given rather than about null, and reads as dead when asked wrongly.
                if (parameter is null && command.GetType().GetGenericArguments().FirstOrDefault() == typeof(NavigationInformation))
                    parameter = NavigationInformation.From(NavigationInput.Default);
                examined++;
                if (command.CanExecute(parameter)) continue;
                var id = control.Name ?? Describe(control);
                if (Conditional.TryGetValue(id, out var condition)) {
                    if (!excused.Any(x => x.StartsWith(id, StringComparison.Ordinal)))
                        excused.Add($"{id} is off until {condition}");
                    continue;
                }
                var fault = $"{name}: {id} is drawn bound to a command that cannot run";
                if (!faults.Contains(fault)) faults.Add(fault);
            }
            // And the rows the controls sit in. A toolbar, a pill or an action row
            // with nothing in it is still measured and arranged on every layout pass
            // of every page, and still takes the spacing its parent gives it — a gap
            // on the header line that nobody can use and nobody can see the cause
            // of.
            foreach (var container in Drawn(window, open_menus)
                         .Where(x => x is Toolbar || x.Name is { } id &&
                             (id.EndsWith("Toolbar", StringComparison.Ordinal) || id.EndsWith("Actions", StringComparison.Ordinal) ||
                              id.EndsWith("ActionRow", StringComparison.Ordinal)))) {
                var held = container switch {
                    ItemsControl items => items.Items.Count,
                    Panel panel => panel.Children.Count(x => x.IsVisible),
                    ContentControl { Content: not null } => 1,
                    Decorator { Child: not null } => 1,
                    _ => -1,
                };
                if (Environment.GetEnvironmentVariable("MO2_TRACE_DEAD_CONTROLS") == "1")
                    Console.WriteLine($"CONTAINER {name}: {container.Name ?? container.GetType().Name} ({container.GetType().Name}) holds {held}");
                if (held != 0) continue;
                var fault = $"{name}: {container.Name ?? container.GetType().Name} is drawn holding nothing";
                if (!faults.Contains(fault)) faults.Add(fault);
            }
            foreach (var flyout in open_menus) flyout.Hide();
            await Task.Delay(200);
        }

        await Navigate(restore);
        await Task.Delay(600);

        if (examined == 0) faults.Add("no control bound to a command was found, so nothing was judged");
        if (faults.Count > 0) Console.WriteLine("FAIL dead controls: " + string.Join("; ", faults));
        else Console.WriteLine($"PASS dead controls: all {examined} control(s) drawn with a command across " +
            $"{pages.Length - 1} pages and the window can run it" +
            (excused.Count > 0 ? ", except " + string.Join(", ", excused) : ""));

        // Drawn, not merely built: a control the theme keeps in the tree and never
        // shows — the account badges for a role this frontend's user never has —
        // cannot be read by anyone and is not what this check is about. An open
        // menu's entries live in a popup of their own rather than under the window.
        static Control[] Drawn(Window window, IEnumerable<MenuFlyout> open) =>
            window.GetVisualDescendants().OfType<Control>()
                .Concat(open.SelectMany(flyout => flyout.Items.OfType<Control>()))
                .Where(x => x.IsEffectivelyVisible)
                .ToArray();

        static string Describe(Control control) =>
            control is ContentControl { Content: string text } && text.Length > 0
                ? $"{control.GetType().Name} (\"{text}\")"
                : control is MenuItem { Header: string header } && header.Length > 0
                    ? $"MenuItem (\"{header}\")" : control.GetType().Name;

        static async Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel? item)
        {
            if (item is null) return;
            await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
                item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
            await Task.Delay(300);
        }
    }
}
