using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Closing tabs, from the tab strip, which is the only place this application puts
// an X.
//
// Upstream takes the X off a tab the moment its panel is down to one, because
// there a single-tab panel is closed by the floating X over its corner. That
// button is not drawn here, so following upstream would leave a panel that
// nothing could close, and a workspace whose tabs could only ever be added.
//
// So the rule checked here is: a tab can be closed unless it is the last tab of a
// panel that fills the workspace, where there is nothing behind it to close back
// to. The button is pressed rather than inspected, because a button that is shown
// and carries a command that cannot run looks right and does nothing.
internal static class Mo2TabCloseCheck
{
    public static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        var workspace = live.WorkspaceController.ActiveWorkspace;
        var panel = workspace.Panels.First();

        // The last tab of the only panel: no X, so the workspace cannot be emptied.
        while (panel.Tabs.Count > 1) panel.CloseTab(panel.Tabs.Last().Id);
        await Settle(window);
        if (workspace.Panels.Count != 1) throw new Exception("The workspace did not come down to one panel");
        if (Buttons(window).Length != 0)
            throw new Exception("The last tab of a full-screen panel still offers an X");
        Console.WriteLine("  one panel, one tab: no close button");

        // A second tab, and both become closable.
        panel.AddDefaultTab();
        await Settle(window);
        if (panel.Tabs.Any(tab => !tab.Header.CanClose))
            throw new Exception("A panel with two tabs has a tab that cannot be closed");
        var buttons = Buttons(window);
        if (buttons.Length != panel.Tabs.Count)
            throw new Exception($"{panel.Tabs.Count} tabs drew {buttons.Length} close buttons");

        // Pressed, not inspected: this is the part that was broken.
        var before = panel.Tabs.Count;
        var pressed = buttons.First(x => x.Command?.CanExecute(null) == true);
        pressed.Command!.Execute(pressed.CommandParameter);
        await Settle(window);
        if (panel.Tabs.Count != before - 1)
            throw new Exception($"Pressing a tab's X left {panel.Tabs.Count} tabs, expected {before - 1}");
        Console.WriteLine($"  two tabs: both closable, pressing one X closed it ({before} -> {panel.Tabs.Count})");

        // A single-tab panel beside another panel keeps its X, and pressing it
        // closes the panel, which is the job upstream gave the floating close.
        await workspace.AddPanelButtonViewModels.First().AddPanelCommand.Execute();
        await Settle(window);
        if (workspace.Panels.Count != 2) throw new Exception("A second panel did not open");
        var second = workspace.Panels.Last();
        while (second.Tabs.Count > 1) second.CloseTab(second.Tabs.Last().Id);
        await Settle(window);
        if (!second.Tabs.Single().Header.CanClose)
            throw new Exception("A single-tab panel beside another panel cannot be closed at all");
        second.CloseTab(second.Tabs.Single().Id);
        await Settle(window);
        if (workspace.Panels.Count != 1)
            throw new Exception("Closing a panel's last tab did not close the panel");
        Console.WriteLine("  two panels: the single-tab panel keeps its X, and closing that tab closes the panel");

        // A panel that shares the workspace is named by its tab strip and stands its
        // page header down. Coming back to one panel is what puts that header back,
        // and it used not to: the header was hidden by its wrapper, so the pass that
        // restores it had stopped finding it.
        var stack = window.GetVisualDescendants().OfType<StackPanel>()
            .FirstOrDefault(x => x.Name == "PanelHeaderStack")
            ?? throw new Exception("The remaining panel has no page header at all");
        var down = stack.Children.OfType<Control>().Where(x => !x.IsVisible).ToArray();
        if (down.Length != 0)
            throw new Exception($"The full-screen panel's header is still stood down ({down.Length} of {stack.Children.Count} parts hidden)");
        if (stack.GetVisualDescendants().OfType<PageHeader>().FirstOrDefault() is not { IsEffectivelyVisible: true })
            throw new Exception("The full-screen panel draws no page header");
        Console.WriteLine("  the panel left full-screen shows its page header again");

        Console.WriteLine("PASS tab close: every tab closes from the strip except the last tab of a full-screen panel");
    }

    private static StandardButton[] Buttons(Window window) => window.GetVisualDescendants()
        .OfType<StandardButton>().Where(x => x.Name == "CloseTabButton" && x.IsEffectivelyVisible).ToArray();

    // The rule is applied on the panel's layout pass, so the check reads the strip
    // only after the panel has had one.
    private static async Task Settle(Window window)
    {
        for (var attempt = 0; attempt < 20; attempt++) {
            window.UpdateLayout();
            await Task.Delay(50);
        }
    }
}
