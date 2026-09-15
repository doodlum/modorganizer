using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using NexusMods.App.UI.Overlays;
using NexusMods.App.UI.Overlays.Login;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class Mo2NexusLoginPopupModel(Action cancel) : AOverlayViewModel<INexusLoginOverlayViewModel>, INexusLoginOverlayViewModel
{
    private Uri? _uri;
    public Uri? Uri { get => _uri; set => this.RaiseAndSetIfChanged(ref _uri, value); }
    public R3.ReactiveCommand Cancel { get; } = new(_ => cancel());
}

internal static class Mo2NexusLoginPopup
{
    public static async Task<Mo2LoginResult?> Show(Window owner, Mo2LiveWorkspace shell, Func<Action<Uri>, CancellationToken, Task<string>>? authorize = null, bool logout = false)
    {
        using var cancellation = new CancellationTokenSource();
        var view = new NexusLoginOverlayView();
        if (logout) view.FindControl<TextBlock>("CaptionTextBlock")!.GetVisualAncestors().OfType<Border>().First().IsVisible = false;
        var title = view.FindControl<TextBlock>("TitleTextBlock")!;
        title.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        var width = Math.Min(600, Math.Max(1, owner.ClientSize.Width - 24));
        var maxHeight = Math.Max(1, owner.ClientSize.Height - 24);
        // The shared overlay has a fixed desktop width. Let its existing layout
        // wrap inside this popup, with scrolling when the owner is short.
        if (view.Content is Border { Child: StackPanel layout }) layout.Width = double.NaN;
        title.MaxWidth = Math.Max(1, Math.Min(450, width - 148));
        var scroll = new ScrollViewer { Content = view, MaxHeight = maxHeight,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var finished = false;
        var window = new Window { Title = logout ? "Nexus logout" : "Nexus login", Width = width, MaxHeight = maxHeight, SizeToContent = SizeToContent.Height,
            CanResize = false, SystemDecorations = SystemDecorations.None, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = scroll, Icon = owner.Icon };
        void Cancel() {
            cancellation.Cancel();
            if (finished) window.Close();
            else title.Text = "Cancelling; finishing any submitted account change…";
        }
        var model = new Mo2NexusLoginPopupModel(Cancel);
        view.ViewModel = model;
        title.Text = logout ? "Disconnecting Nexus from MO2 instances…" : "Connecting to Nexus Mods…";
        Mo2LoginResult? result = null;
        window.KeyDown += (_,args) => { if (args.Key == Avalonia.Input.Key.Escape) { Cancel(); args.Handled = true; } };
        void Finished() {
            finished = true;
            foreach (var ring in view.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.ProgressRing.ProgressRing>()) ring.IsVisible = false;
        }
        window.Closing += (_,args) => { if (!finished) { args.Cancel = true; Cancel(); } };
        window.Opened += async (_,_) => {
            try {
                if (logout) {
                    var targets = shell.Catalog.Registrations;
                    var disconnected = await Mo2SharedNexusLogout.Apply(targets, text => title.Text = text, cancellation.Token);
                    result = new(0, disconnected.Total, null, disconnected.Failures);
                    Finished();
                    if (disconnected.Disconnected == disconnected.Total && disconnected.Failures.Count == 0) window.Close();
                    else title.Text = $"Disconnected {disconnected.Disconnected} of {disconnected.Total} instances. " + string.Join(" ", disconnected.Failures);
                    return;
                }
                var key = await (authorize ?? Mo2NexusSso.Authorize)(uri => {
                    model.Uri = uri; title.Text = "Please click Authorize in your browser";
                    shell.DesktopInterop.OpenUri(uri);
                }, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                var registrations = shell.Catalog.Registrations.DistinctBy(x => x.Endpoint).ToArray();
                result = await Mo2SharedNexusLogin.Apply(key, registrations, text => title.Text = text, cancellation.Token);
                Finished();
                if (result.Connected == result.Total && result.Failures.Count == 0) window.Close();
                else title.Text = $"Connected {result.Connected} of {result.Total} instances. " + string.Join(" ", result.Failures);
            } catch (OperationCanceledException) {
                Finished(); window.Close();
            } catch (Exception) {
                Finished(); title.Text = logout ? "Logout could not complete. Close this window and check the account status." : "Login could not complete. Close this window and try again.";
            }
        };
        await window.ShowDialog(owner);
        return result;
    }
}
