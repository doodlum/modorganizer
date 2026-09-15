using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls.ApplicationLifetimes;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Overlays.Login;
namespace Mo2.Frontend;
internal static class Mo2LoginPopupCheck
{
    public static async Task Live(Window owner, Mo2LiveWorkspace shell)
    {
        var received = false;
        var browserOpened = false;
        var result = await Mo2NexusLoginPopup.Show(owner, shell, async (open, cancellation) => {
            var key = await Mo2NexusSso.Authorize(uri => { browserOpened = true; open(uri); }, cancellation);
            received = true;
            return key;
        });
        if (!received || !browserOpened || result is null || result.Total == 0 || result.Connected != result.Total || result.Failures.Count != 0)
            throw new Exception("Browser SSO did not confirm the shared account connection");
        var state = await Mo2SharedNexusLogin.ReadStatus(shell.Catalog.Read().Select(x => x.Registration).ToArray());
        if (state.Total != result.Total || state.Connected != state.Total || state.Failures.Count != 0)
            throw new Exception("Native account readback did not confirm browser SSO");
        Console.WriteLine($"PASS real browser SSO received a credential, applied it through the NMA popup to {result.Total} instances, and confirmed native account readback");
    }

    public static async Task Run(Window owner, Mo2LiveWorkspace shell)
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!;
        var originalState = owner.WindowState;
        var originalWidth = owner.Width;
        var originalHeight = owner.Height;
        try {
            owner.WindowState = WindowState.Normal;
            await Task.Delay(200);
            foreach (var size in new[] { new Avalonia.Size(1280, 750), new Avalonia.Size(480, 320) }) {
                owner.WindowState = WindowState.Normal;
                await Task.Delay(200);
                owner.Width = size.Width; owner.Height = size.Height;
                var until = DateTime.UtcNow.AddSeconds(10);
                while (Math.Abs(owner.ClientSize.Width - size.Width) > 1 || Math.Abs(owner.ClientSize.Height - size.Height) > 1) {
                    if (DateTime.UtcNow > until) {
                        Console.WriteLine($"CHECK owner resize failed: requested {size}, actual {owner.ClientSize}, state {owner.WindowState}");
                        throw new Exception("Login owner resize did not settle");
                    }
                    await Task.Delay(25);
                }
                for (var attempt = 0; attempt < 2; attempt++) {
                    var pending = Mo2NexusLoginPopup.Show(owner, shell, async (_,token) => { await Task.Delay(Timeout.Infinite,token); return "unreachable"; });
                    Window? popup = null;
                    try {
                        for (var i = 0; i < 40; i++) {
                            popup = desktop.Windows.FirstOrDefault(x => x.Title == "Nexus login" && x.IsVisible);
                            if (popup is not null && popup.Bounds.Height > 0) break;
                            await Task.Delay(50);
                        }
                        await Task.Delay(100);
                        Console.WriteLine($"CHECK login popup geometry: owner {owner.ClientSize}, popup {popup?.ClientSize}");
                        if (popup is null || popup.ClientSize.Width > owner.ClientSize.Width || popup.ClientSize.Height > owner.ClientSize.Height)
                            throw new Exception($"Login popup does not fit owner {owner.ClientSize}: popup {popup?.ClientSize}");
                        var view = popup.GetVisualDescendants().OfType<NexusLoginOverlayView>().Single();
                        var cancel = view.FindControl<StandardButton>("CancelButton")!;
                        if (cancel.Command is null || !cancel.Command.CanExecute(null)) throw new Exception("Native cancel button not bound");
                        foreach (var button in new[] { view.FindControl<StandardButton>("CopyButton")!, cancel }) {
                            button.BringIntoView();
                            await Task.Delay(100);
                            var point = button.TranslatePoint(default, popup);
                            if (point is null || point.Value.X < 0 || point.Value.Y < 0 ||
                                point.Value.X + button.Bounds.Width > popup.ClientSize.Width + 1 ||
                                point.Value.Y + button.Bounds.Height > popup.ClientSize.Height + 1)
                                throw new Exception("Login Copy/Cancel control is not reachable inside the popup");
                        }
                        cancel.Command.Execute(null);
                        await pending.WaitAsync(TimeSpan.FromSeconds(5));
                        if (popup.IsVisible) throw new Exception("Login popup did not close after cancellation");
                    } finally {
                        if (popup?.IsVisible == true) {
                            popup.GetVisualDescendants().OfType<NexusLoginOverlayView>().Single()
                                .FindControl<StandardButton>("CancelButton")!.Command?.Execute(null);
                            await pending.WaitAsync(TimeSpan.FromSeconds(5));
                        }
                    }
                }
            }
        } finally { owner.Width = originalWidth; owner.Height = originalHeight; owner.WindowState = originalState; }
        Console.WriteLine("PASS NMA login popup fits 1280x750 and 480x320 owners; bound Cancel closes cleanly and reopens");
    }
}
