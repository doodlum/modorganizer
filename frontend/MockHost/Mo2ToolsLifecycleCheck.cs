using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2ToolsLifecycleCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(15);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception("Tools lifecycle check did not settle");
                await Task.Delay(25);
            }
        }
        await Wait(() => shell.Profile.IsConnected && !shell.Profile.SelectingProfile);
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 600, Height = 400, IsHitTestVisible = false };
        root.Children.Add(host);
        try {
            foreach (var failure in new[] { false, true })
            foreach (var earlyReopen in new[] { false, true }) {
                var pending = new TaskCompletionSource<Mo2Tool[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var reads = 0;
                var view = new Mo2ToolsView(() => ++reads == 1 ? pending.Task : Task.FromResult(new[] {
                    new Mo2Tool(["fixture"], "Fresh lifecycle tool", "", "", true)
                })) { ViewModel = new Mo2ToolsPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile) };
                bool HasText(string text) => view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text);
                host.Children.Add(view); await Wait(() => reads == 1);
                host.Children.Remove(view);
                if (earlyReopen) host.Children.Add(view);
                if (failure) pending.SetException(new InvalidOperationException("Stale tools failure"));
                else pending.SetResult([new(["old"], "Stale lifecycle tool", "", "", true)]);
                if (!earlyReopen) {
                    await Wait(() => !view.IsReading);
                    if (reads != 1 || HasText("Stale lifecycle tool") || HasText("Stale tools failure"))
                        throw new Exception("Detached Tools view applied an old response or started another read");
                    if (view.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "LaunchToolButton" && b.IsEnabled))
                        throw new Exception("Detached Tools view retained enabled launch actions");
                    host.Children.Add(view);
                }
                await Wait(() => reads == 2 && HasText("Fresh lifecycle tool"));
                if (HasText("Stale lifecycle tool") || HasText("Stale tools failure"))
                    throw new Exception("Reopened Tools view shows stale content");
                host.Children.Remove(view);
            }
            Console.WriteLine("PASS Tools lifecycle: delayed success/failure discarded after detach; reopen before/after completion reads fresh tools");
        } finally { root.Children.Remove(host); }
    }
}
