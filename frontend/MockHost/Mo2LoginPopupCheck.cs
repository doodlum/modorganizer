using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia;
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
        Console.WriteLine($"PASS real browser SSO received a credential, applied it through the NMA overlay to {result.Total} instances, and confirmed native account readback");
    }

    // The login is an overlay inside the shell window, as it is in NMA, so what
    // has to fit is the window rather than a popup of its own.
    internal static NexusLoginOverlayView? Overlay(Window owner) =>
        owner.GetVisualDescendants().OfType<NexusLoginOverlayView>().FirstOrDefault(x => x.IsEffectivelyVisible);

    public static async Task Run(Window owner, Mo2LiveWorkspace shell)
    {
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
                    NexusLoginOverlayView? view = null;
                    try {
                        for (var i = 0; i < 40; i++) {
                            view = Overlay(owner);
                            if (view is not null && view.Bounds.Height > 0) break;
                            await Task.Delay(50);
                        }
                        await Task.Delay(100);
                        owner.UpdateLayout();
                        Console.WriteLine($"CHECK login overlay geometry: owner {owner.ClientSize}, overlay {view?.Bounds.Size}");
                        if (view is null || view.Bounds.Width > owner.ClientSize.Width || view.Bounds.Height > owner.ClientSize.Height)
                            throw new Exception($"Login overlay does not fit owner {owner.ClientSize}: overlay {view?.Bounds.Size}");
                        var cancel = view.FindControl<StandardButton>("CancelButton")!;
                        if (cancel.Command is null || !cancel.Command.CanExecute(null)) throw new Exception("Native cancel button not bound");
                        foreach (var button in new[] { view.FindControl<StandardButton>("CopyButton")!, cancel }) {
                            button.BringIntoView();
                            await Task.Delay(100);
                            var point = ((Visual)button).TranslatePoint(default, owner);
                            if (point is null || point.Value.X < 0 || point.Value.Y < 0 ||
                                point.Value.X + button.Bounds.Width > owner.ClientSize.Width + 1 ||
                                point.Value.Y + button.Bounds.Height > owner.ClientSize.Height + 1)
                                throw new Exception("Login Copy/Cancel control is not reachable inside the window");
                        }
                        cancel.Command.Execute(null);
                        await pending.WaitAsync(TimeSpan.FromSeconds(5));
                        if (Overlay(owner) is not null) throw new Exception("Login overlay did not close after cancellation");
                    } finally {
                        if (Overlay(owner) is { } leftover) {
                            leftover.FindControl<StandardButton>("CancelButton")!.Command?.Execute(null);
                            await pending.WaitAsync(TimeSpan.FromSeconds(5));
                        }
                    }
                }
            }
        } finally { owner.Width = originalWidth; owner.Height = originalHeight; owner.WindowState = originalState; }
        Console.WriteLine("PASS NMA login overlay fits 1280x750 and 480x320 windows; bound Cancel closes cleanly and reopens");
    }
}
