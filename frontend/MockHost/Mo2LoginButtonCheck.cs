using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;

namespace Mo2.Frontend;

// Opt-in check for the header account button itself. The existing popup check calls
// Mo2NexusLoginPopup.Show directly, so the button, its command binding and
// LoginToNexus were never exercised — which is where a "login does nothing"
// report would live. This presses the real button and waits for the popup window.
internal static class Mo2LoginButtonCheck
{
    private static IReadOnlyList<Window> Windows =>
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows ?? [];

    internal static async Task Run(Window window)
    {
        await Task.Delay(3000);
        var login = window.GetVisualDescendants().OfType<StandardButton>().FirstOrDefault(x => x.Name == "LoginButton")
            ?? throw new Exception("No LoginButton in the header");
        var avatar = window.GetVisualDescendants().OfType<StandardButton>().FirstOrDefault(x => x.Name == "AvatarMenuItemButton");

        // NMA shows exactly one of these, driven by IsLoggedIn. If the account is
        // wrongly considered connected the button is hidden and nothing can be clicked.
        if (!login.IsVisible)
            throw new Exception($"LoginButton is hidden (avatar visible={avatar?.IsVisible}), so the account cannot be opened");
        if (login.Command is null) throw new Exception("LoginButton has no command bound");
        if (!login.Command.CanExecute(null)) throw new Exception("LoginButton command reports it cannot execute");

        var before = Windows.Count;
        // A real click runs Button.OnClick, which executes Command. Raising the routed
        // event only notifies handlers, so the command has to be executed directly.
        login.Command.Execute(null);

        Window? popup = null;
        for (var attempt = 0; attempt < 100 && popup is null; attempt++) {
            await Task.Delay(100);
            popup = Windows.FirstOrDefault(x => x.Title is "Nexus login" or "Nexus logout");
        }
        if (popup is null)
            throw new Exception($"Pressing the account button opened no login popup ({before} windows before, {Windows.Count} now)");

        popup.Close();
        await Task.Delay(500);
        Console.WriteLine($"PASS login button: visible with a bound command, and pressing it opened the \"{popup.Title}\" popup");
    }
}
