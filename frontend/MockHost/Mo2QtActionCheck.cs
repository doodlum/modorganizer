using Avalonia.Controls;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Everything MO2 offers from its menu bar and its toolbar, and where this frontend
// offers it.
//
// MO2's menus hold no widget of their own — they hold QActions — so the widget
// check walking src/mainwindow.ui says nothing about them. Manage Instances,
// Settings, Notifications, Endorse, Visit Nexus, Check for updates and MO2's own
// Help are all in that file and none of them was checked anywhere: whether they
// were reachable at all rested on a sentence in a document.
//
// So this reads MO2's own <action> list and requires every one of them to be
// answered: either found on screen, through the control this frontend offers it
// from, or recorded as Qt chrome with the reason. An action added to MO2's window
// fails this check until it is answered for.
internal static class Mo2QtActionCheck
{
    internal enum Kind
    {
        // Offered here. Found live, through the control named.
        Offered,
        // Qt's own window furniture: how big the toolbar icons are, whether the
        // menu bar is showing. This frontend draws no Qt toolbar, so there is
        // nothing for these to act on.
        Chrome,
    }

    // Page is the page to open first, or "" for something the window carries
    // whatever page is open. Entry is the menu entry to look for inside Control's
    // flyout, or "" when the control is the answer itself.
    internal sealed record Answer(Kind Kind, string Page, string Control, string Entry, string Note);

    private static Answer On(string page, string control, string entry = "", string note = "") =>
        new(Kind.Offered, page, control, entry, note);
    private static Answer Qt(string note) => new(Kind.Chrome, "", "", "", note);

    // MO2's action, and what answers for it here. Everything in src/mainwindow.ui's
    // action list appears exactly once.
    internal static readonly Dictionary<string, Answer> Answers = new() {
        ["actionInstallMod"] = On("my-mods", "ModsOverflowButton", "Add mod from archive…"),
        ["actionAdd_Profile"] = On("connections","Mo2ManageProfiles"),
        ["actionModify_Executables"] = On("", "ExecutablesListBox", Mo2LaunchPanel.EditEntry,
            "MO2's own first row of the executables box, which opens its Edit Executables dialog"),
        ["actionTool"] = On("tools", "Mo2ToolsMenuItem", "", "MO2's tool plugins, as a page of their own"),
        ["actionSettings"] = On("connections","OriginalMo2Settings", "", "opens MO2's own settings dialog"),
        ["actionNexus"] = On("connections","OriginalMo2More", "Visit Nexus…"),
        ["actionModPage"] = On("my-mods", "ModRedesignRow", "Visit on Nexus",
            "MO2 offers this for a mod it knows on Nexus, and so does the row menu here"),
        ["actionUpdate"] = On("connections","OriginalMo2More", "Check for MO2 updates…"),
        ["actionNotifications"] = On("connections","OriginalMo2More", "MO2 notifications…"),
        ["actionHelp"] = On("connections","OriginalMo2More", "MO2 help…"),
        ["actionEndorseMO"] = On("connections","OriginalMo2More", "Endorse Mod Organizer…"),
        ["actionChange_Game"] = On("connections","Mo2AddInstance"),
        ["actionExit"] = On("", "CloseButton", "", "the window's own close button"),
        ["actionViewLog"] = On("", "Mo2LogsMenuItem", "", "MO2's log, which MO2 docks and this frontend opens as a page"),
        ["action_Refresh"] = On("my-mods", "ModsListOptionsButton", "Refresh"),

        ["actionMainMenuToggle"] = Qt("shows and hides Qt's menu bar"),
        ["actionStatusBarToggle"] = Qt("shows and hides Qt's status bar"),
        ["actionToolBarMainToggle"] = Qt("shows and hides Qt's toolbar"),
        ["actionToolBarSmallIcons"] = Qt("sizes Qt's toolbar icons"),
        ["actionToolBarMediumIcons"] = Qt("sizes Qt's toolbar icons"),
        ["actionToolBarLargeIcons"] = Qt("sizes Qt's toolbar icons"),
        ["actionToolBarIconsOnly"] = Qt("captions Qt's toolbar buttons"),
        ["actionToolBarTextOnly"] = Qt("captions Qt's toolbar buttons"),
        ["actionToolBarIconsAndText"] = Qt("captions Qt's toolbar buttons"),
    };

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();

        var menu = live.ProfileMenu;
        var open = new Dictionary<string, Func<Task>> {
            ["my-mods"] = () => Navigate(menu.LeftMenuItemLoadout),
            // The profile sidebar's own, before the home workspace is opened: the
            // two sidebars swap, and MO2's tool plugins are an entry on the profile
            // one.
            ["tools"] = () => Navigate(menu.ToolsItem),
            // MO2's own host actions live on Connections, which is the home
            // workspace's page rather than one of the profile sidebar's: the profile
            // sidebar's Profiles item opens this instance's loadouts.
            ["connections"] = () => { live.OpenConnections(); return Task.CompletedTask; },
        };

        using var turn = await Mo2CheckTurn.Take();
        // MO2's executables box is on screen always; this frontend can fold it into
        // a flyout. Compared with the sidebar open, and put back as it was found.
        using var sidebar = await Mo2SidebarState.Open(window, live);
        var faults = new List<string>();
        var described = new List<string>();
        var unexercised = new List<string>();

        var source = Mo2QtWidgetSource.Locate();
        var actions = Mo2QtWidgetSource.Actions(source);
        if (actions.Length == 0) throw new Exception("No action was read from " + source);

        // Both ways round, as the widget check does: an action MO2 has that nothing
        // answers for is the fault this exists to catch, and an answer for an action
        // MO2 has dropped is an expectation that has stopped meaning anything.
        var unanswered = actions.Where(x => !Answers.ContainsKey(x.Name)).Select(x => $"{x.Name} (\"{x.Text}\")").ToArray();
        if (unanswered.Length > 0)
            faults.Add($"MO2 offers {unanswered.Length} action(s) nothing answers for: {string.Join(", ", unanswered)}");
        var stale = Answers.Keys.Where(x => !actions.Any(a => a.Name == x)).ToArray();
        if (stale.Length > 0)
            faults.Add($"{stale.Length} answer(s) name an action MO2 has dropped: {string.Join(", ", stale)}");
        // Everything below reads an answer by name. An action with none is already a
        // fault above; looking one up anyway would end the run in a
        // KeyNotFoundException and report that instead of what was actually wrong.
        actions = actions.Where(x => Answers.ContainsKey(x.Name)).ToArray();

        // And that MO2's menu bar is accounted for entry by entry, rather than as a
        // total: a menu whose actions are all answered somewhere else still has to
        // have every one of its own entries answered.
        foreach (var host in new[] { "menuFile", "menuView", "menuTools", "menuRun", "menuHelp", "menuToolbars", "toolBar" }) {
            var carried = Mo2QtWidgetSource.Carries(host, source);
            var lost = carried.Where(x => !Answers.ContainsKey(x) && !x.StartsWith("menu", StringComparison.Ordinal)).ToArray();
            if (lost.Length > 0) faults.Add($"MO2's {host} carries {string.Join(", ", lost)}, which nothing answers for");
        }

        var panels = live.WorkspaceController.ActiveWorkspace.Panels;
        var selected = panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
        var restore = selected?.SelectedTab.Contents.ViewModel is ScenarioLoadOrderPage
            ? menu.LeftMenuItemExternalChanges! : menu.LeftMenuItemLoadout;

        // The window's own, first: these do not depend on which page is open, and the
        // run row moves out of the window's tree when the sidebar is collapsed, so it
        // is reached through the workspace that holds it.
        foreach (var action in actions.Where(x => Answers[x.Name] is { Kind: Kind.Offered, Page: "" })) {
            var answer = Answers[action.Name];
            var found = Find(Roots(window, live), answer.Control);
            if (found is null) faults.Add($"{action.Name} (\"{action.Text}\") has no {answer.Control} in the window");
            else if (answer.Entry.Length > 0 && !await Carries(found, answer.Entry))
                faults.Add($"{action.Name}: {answer.Control} does not offer \"{answer.Entry}\"");
            else described.Add($"{action.Text} from {answer.Control}");
        }

        foreach (var page in open.Keys) {
            var wanted = actions.Where(x => Answers[x.Name] is { Kind: Kind.Offered } answer && answer.Page == page).ToArray();
            if (wanted.Length == 0) continue;
            await open[page]();
            foreach (var action in wanted) {
                var answer = Answers[action.Name];
                // A page is built when it is first shown, so the frame right after
                // navigating is still the one before it.
                Control? found = null;
                for (var attempt = 0; attempt < 60 && found is null; attempt++) {
                    await Task.Delay(100);
                    window.UpdateLayout();
                    found = Find(Roots(window, live), answer.Control);
                }
                // A row menu needs a row MO2 reports on Nexus. Where this host has
                // none, that says so rather than passing quietly.
                if (found is null && answer.Control == "ModRedesignRow") {
                    unexercised.Add($"{action.Text}: no row was on screen to open a menu on");
                    continue;
                }
                if (found is null) { faults.Add($"{action.Name} (\"{action.Text}\") has no {answer.Control} on {page}"); continue; }
                if (answer.Entry.Length == 0) { described.Add($"{action.Text} from {answer.Control}"); continue; }
                // MO2 offers Browse Mod Page only for a mod it knows on Nexus, so
                // the first row on screen is not the question — whether any row
                // offers it is. Asking one row and calling the answer "this host has
                // none" would have read as agreement on a host where it was simply
                // the wrong row.
                if (answer.Control == "ModRedesignRow") {
                    var rows = Roots(window, live).SelectMany(root => root.GetVisualDescendants().OfType<Control>())
                        .Where(x => x.Name == "ModRedesignRow" && x.ContextFlyout is MenuFlyout).ToArray();
                    var offering = 0;
                    foreach (var row in rows) if (await Carries(row, answer.Entry)) offering++;
                    if (offering > 0) described.Add($"{action.Text} from {offering} of {rows.Length} mod row(s)");
                    else unexercised.Add($"{action.Text}: none of the {rows.Length} mod row(s) on screen is one " +
                        "MO2 reports as known on Nexus, so the entry MO2 gates on that was not exercised");
                    continue;
                }
                if (await Carries(found, answer.Entry)) described.Add($"{action.Text} from {answer.Control}");
                else faults.Add($"{action.Name}: {answer.Control} does not offer \"{answer.Entry}\"");
            }
        }

        await Navigate(restore);
        await Task.Delay(600);

        var chrome = actions.Count(x => Answers[x.Name].Kind == Kind.Chrome);
        if (faults.Count > 0) Console.WriteLine("FAIL MO2 actions: " + string.Join("; ", faults));
        else Console.WriteLine($"PASS MO2 actions: all {actions.Length} actions MO2 names in {Path.GetFileName(source)} " +
            $"are accounted for — {described.Count} offered here ({string.Join(", ", described)}), " +
            $"{chrome} are Qt's own toolbar and menu-bar furniture" +
            (unexercised.Count > 0 ? ", and " + string.Join("; ", unexercised) : ""));

        // Where a control is looked for. The run row sits in the profile sidebar,
        // which moves into a flyout when the sidebar is collapsed; the workspace
        // holds it so it can be read wherever it currently is.
        static IEnumerable<Control> Roots(Window window, Mo2LiveWorkspace live) =>
            live.LaunchPanel is { } panel ? [window, panel] : [window];

        static Control? Find(IEnumerable<Control> roots, string name) =>
            roots.SelectMany(root => root.GetVisualDescendants().OfType<Control>().Prepend(root))
                .FirstOrDefault(x => x.Name == name);

        // Whether a control offers an entry: a drop-down's rows, or the menu behind
        // a button. Menus that build themselves when opened have to be opened, so
        // this shows and hides rather than reading Items as they stand.
        static async Task<bool> Carries(Control control, string entry)
        {
            if (control is ComboBox box) return box.Items.OfType<object>().Any(x => x?.ToString() == entry);
            var flyout = control.ContextFlyout as MenuFlyout ?? (control as Button)?.Flyout as MenuFlyout;
            if (flyout is null) return false;
            flyout.ShowAt(control);
            await Task.Delay(500);
            var carried = flyout.Items.OfType<MenuItem>().Any(x => x.Header?.ToString() == entry);
            flyout.Hide();
            await Task.Delay(200);
            return carried;
        }

        static async Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel? item)
        {
            if (item is null) return;
            await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
                item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
            await Task.Delay(300);
        }
    }
}
