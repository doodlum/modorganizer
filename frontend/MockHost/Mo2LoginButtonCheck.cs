using Avalonia.Controls;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;

namespace Mo2.Frontend;

// Opt-in check for the header account button itself. The existing overlay check
// calls Mo2NexusLoginPopup.Show directly, so the button, its command binding and
// LoginToNexus were never exercised — which is where a "login does nothing"
// report would live. This presses the real button and waits for the overlay.
internal static class Mo2LoginButtonCheck
{
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

        // A real click runs Button.OnClick, which executes Command. Raising the routed
        // event only notifies handlers, so the command has to be executed directly.
        login.Command.Execute(null);

        // The login now opens in NMA's own overlay layer inside this window rather
        // than in a window of its own.
        NexusMods.App.UI.Overlays.Login.NexusLoginOverlayView? overlay = null;
        for (var attempt = 0; attempt < 100 && overlay is null; attempt++) {
            await Task.Delay(100);
            overlay = Mo2LoginPopupCheck.Overlay(window);
        }
        if (overlay is null)
            throw new Exception("Pressing the account button opened no login overlay");

        var scrim = overlay.GetVisualAncestors().OfType<Border>().FirstOrDefault(x => x.Name == "OverlayBorder");
        if (scrim is null || !scrim.IsVisible)
            throw new Exception("The login overlay is not inside the shell overlay layer, so the app behind it is not dimmed");

        overlay.FindControl<StandardButton>("CancelButton")!.Command?.Execute(null);
        for (var attempt = 0; attempt < 50 && Mo2LoginPopupCheck.Overlay(window) is not null; attempt++) await Task.Delay(100);
        Console.WriteLine("PASS login button: visible with a bound command, and pressing it opened the NMA login overlay over a dimmed window");
    }
}
