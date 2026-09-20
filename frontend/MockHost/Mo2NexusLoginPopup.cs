using Avalonia.Controls;
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
    // The view locator hands this exact instance back, so the flow below can keep
    // updating its title and progress for as long as the overlay is open.
    internal NexusLoginOverlayView? View { get; init; }
}

internal static class Mo2NexusLoginPopup
{
    public static async Task<Mo2LoginResult?> Show(Window owner, Mo2LiveWorkspace shell, Func<Action<Uri>, CancellationToken, Task<string>>? authorize = null, bool logout = false)
    {
        // NMA shows this view as an overlay inside its own window rather than as a
        // separate one, so the app stays visible and dimmed behind it.
        var overlays = shell.Overlays ?? throw new InvalidOperationException("The shell window has no overlay host");
        using var cancellation = new CancellationTokenSource();
        var view = new NexusLoginOverlayView();
        if (logout) view.FindControl<TextBlock>("CaptionTextBlock")!.GetVisualAncestors().OfType<Border>().First().IsVisible = false;
        var title = view.FindControl<TextBlock>("TitleTextBlock")!;
        title.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        // The shared overlay is a fixed desktop width. Release it so the existing
        // layout wraps inside narrow windows; the host scrolls when they are short.
        if (view.Content is Border { Child: StackPanel layout }) layout.Width = double.NaN;
        void Fit() {
            var width = Math.Max(1, owner.ClientSize.Width - 24);
            view.MaxWidth = Math.Min(600, width);
            view.MaxHeight = Math.Max(1, owner.ClientSize.Height - 24);
            title.MaxWidth = Math.Max(1, Math.Min(450, width - 148));
        }
        Fit();
        owner.PropertyChanged += OnOwnerChanged;
        void OnOwnerChanged(object? _, Avalonia.AvaloniaPropertyChangedEventArgs args) {
            if (args.Property == Avalonia.Controls.TopLevel.ClientSizeProperty) Fit();
        }

        var finished = false;
        Mo2LoginResult? result = null;
        Mo2NexusLoginPopupModel? model = null;
        void Cancel() {
            cancellation.Cancel();
            if (finished) model?.Close();
            else title.Text = "Cancelling; finishing any submitted account change…";
        }
        model = new Mo2NexusLoginPopupModel(Cancel) { View = view };
        view.ViewModel = model;
        title.Text = logout ? "Disconnecting Nexus from MO2 instances…" : "Connecting to Nexus Mods…";
        void Finished() {
            finished = true;
            foreach (var ring in view.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.ProgressRing.ProgressRing>()) ring.IsVisible = false;
        }
        void OnKey(object? _, Avalonia.Input.KeyEventArgs args) {
            if (args.Key == Avalonia.Input.Key.Escape) { Cancel(); args.Handled = true; }
        }
        owner.KeyDown += OnKey;
        overlays.Controller.Enqueue(model);
        try {
            if (logout) {
                var targets = shell.Catalog.Registrations;
                var disconnected = await Mo2SharedNexusLogout.Apply(targets, text => title.Text = text, cancellation.Token);
                result = new(0, disconnected.Total, null, disconnected.Failures);
                Finished();
                if (disconnected.Disconnected == disconnected.Total && disconnected.Failures.Count == 0) model.Close();
                else title.Text = $"Disconnected {disconnected.Disconnected} of {disconnected.Total} instances. " + string.Join(" ", disconnected.Failures);
            } else {
                var key = await (authorize ?? Mo2NexusSso.Authorize)(uri => {
                    model.Uri = uri; title.Text = "Please click \"Authorise\" on the website";
                    shell.DesktopInterop.OpenUri(uri);
                }, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                var registrations = shell.Catalog.Registrations.DistinctBy(x => x.Endpoint).ToArray();
                result = await Mo2SharedNexusLogin.Apply(key, registrations, text => title.Text = text, cancellation.Token);
                Finished();
                if (result.Connected == result.Total && result.Failures.Count == 0) model.Close();
                else title.Text = $"Connected {result.Connected} of {result.Total} instances. " + string.Join(" ", result.Failures);
            }
        } catch (OperationCanceledException) {
            Finished(); model.Close();
        } catch (Exception) {
            Finished(); title.Text = logout ? "Logout could not complete. Close this overlay and check the account status." : "Login could not complete. Close this overlay and try again.";
        }
        await model.CompletionTask;
        owner.KeyDown -= OnKey;
        owner.PropertyChanged -= OnOwnerChanged;
        return result;
    }
}
