using NexusMods.App.UI.Overlays;
using NexusMods.App.UI;
using R3;
using ReactiveUI;

namespace Mo2.Frontend;

// NMA greets a first run with this overlay: log in, opt into telemetry, and the
// community links. It is shown here as NMA shows it, against MO2's own shared
// login rather than an NMA account session.
internal sealed class Mo2WelcomeOverlay : AOverlayViewModel<IWelcomeOverlayViewModel>, IWelcomeOverlayViewModel
{
    private static string SeenPath => Mo2ConfigPaths.Combine("welcome-shown");
    private static string TelemetryPath => Mo2ConfigPaths.Combine("allow-telemetry");

    public R3.ReactiveCommand CommandOpenDiscord { get; }
    public R3.ReactiveCommand CommandOpenForum { get; }
    public R3.ReactiveCommand CommandOpenGitHub { get; }
    public R3.ReactiveCommand CommandOpenPrivacyPolicy { get; }
    public R3.ReactiveCommand<R3.Unit> CommandLogIn { get; }
    public R3.ReactiveCommand<R3.Unit> CommandLogOut { get; }
    public R3.ReactiveCommand CommandClose { get; }

    private readonly BindableReactiveProperty<bool> _isLoggedIn = new();
    public IReadOnlyBindableReactiveProperty<bool> IsLoggedIn => _isLoggedIn;
    public BindableReactiveProperty<bool> AllowTelemetry { get; }

    private Mo2WelcomeOverlay(Mo2LiveWorkspace shell)
    {
        var open = shell.DesktopInterop;
        CommandOpenDiscord = new R3.ReactiveCommand(_ => open.OpenUri(ConstantLinks.DiscordUri));
        CommandOpenForum = new R3.ReactiveCommand(_ => open.OpenUri(ConstantLinks.ForumsUri));
        CommandOpenGitHub = new R3.ReactiveCommand(_ => open.OpenUri(ConstantLinks.GitHubUri));
        CommandOpenPrivacyPolicy = new R3.ReactiveCommand(_ => open.OpenUri(ConstantLinks.PrivacyPolicyUri));

        AllowTelemetry = new BindableReactiveProperty<bool>(File.Exists(TelemetryPath));

        // Both route through MO2's shared sign-in, which applies one Nexus account
        // to every registered instance, so the overlay reflects the same state the
        // header does.
        CommandLogIn = IsLoggedIn.AsObservable().Select(static signedIn => !signedIn).ToReactiveCommand<R3.Unit>(
            executeAsync: async (_, _) => await shell.LoginToNexus(),
            initialCanExecute: false);
        CommandLogOut = IsLoggedIn.AsObservable().ToReactiveCommand<R3.Unit>(
            executeAsync: async (_, _) => await shell.LoginToNexus(logout: true),
            initialCanExecute: false);

        CommandClose = new R3.ReactiveCommand(_ => {
            // Remembered, but nothing in this build reads it: MO2 has no tracking
            // backend here, so the preference is kept rather than acted on.
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(TelemetryPath)!);
                if (AllowTelemetry.Value) File.WriteAllText(TelemetryPath, "1");
                else File.Delete(TelemetryPath);
            } catch (IOException) { }
            Close();
        });

        if (shell.TopBar is { } bar)
            bar.WhenAnyValue(x => x.IsLoggedIn).Subscribe(signedIn => _isLoggedIn.Value = signedIn);
    }

    // Shown once, as NMA does, and never during a verification run: those drive the
    // window themselves and an overlay in front of it would fail them for the wrong
    // reason. MO2_WELCOME=1 forces it, MO2_WELCOME=0 suppresses it.
    internal static void ShowIfFirstRun(Mo2LiveWorkspace shell)
    {
        var forced = Environment.GetEnvironmentVariable("MO2_WELCOME");
        if (forced == "0") return;
        if (forced != "1") {
            if (File.Exists(SeenPath)) return;
            if (Environment.GetEnvironmentVariable("MO2_SCREENSHOT") is { Length: > 0 }) return;
            if (Environment.GetEnvironmentVariables().Keys.OfType<string>()
                .Any(name => name.StartsWith("MO2_VERIFY_", StringComparison.Ordinal))) return;
        }
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(SeenPath)!);
            File.WriteAllText(SeenPath, DateTime.UtcNow.ToString("O"));
        } catch (IOException) { }
        if (shell.Overlays is not { } overlays) return;
        overlays.Controller.Enqueue(new Mo2WelcomeOverlay(shell));
    }
}
