using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.TopBar;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Mo2.Frontend;

internal static class Mo2SharedNexusLogoutCheck
{
    public static async Task Live(Mo2LiveWorkspace shell, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var keyFile = Environment.GetEnvironmentVariable("MO2_TEST_NEXUS_KEY_FILE") ?? throw new InvalidOperationException();
        var key = (await File.ReadAllTextAsync(keyFile)).Trim();
        if (key.Length == 0) throw new InvalidOperationException();
        var targets = shell.Catalog.Read().Select(x => x.Registration).ToArray();
        var before = await Mo2SharedNexusLogin.ReadStatus(targets);
        if (before.Total == 0 || before.Connected != before.Total) throw new InvalidOperationException("All instances must be connected before logout verification");
        var topView = desktop.MainWindow!.GetVisualDescendants().OfType<TopBarView>().Single();
        var top = (Mo2TopBar)topView.ViewModel!;
        async Task Wait(Func<bool> condition) {
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (!condition()) {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Logout UI verification did not finish");
                await Task.Delay(100);
            }
        }
        await Wait(() => top.IsLoggedIn);
        if (topView.FindControl<MenuItem>("SignOutMenuItem")!.Command is null) throw new InvalidOperationException("Sign out menu is not bound");
        try {
            Task operation;
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_PARTIAL_ACCOUNT") == "1") {
                var first = await new Mo2BridgeClient(targets[0].Endpoint).SendAsync("disconnectNexusAccount");
                if (!first.GetProperty("disconnected").GetBoolean()) throw new InvalidOperationException("Partial account setup failed");
                await Wait(() => !top.IsLoggedIn);
                var partial = await Mo2SharedNexusLogin.ReadStatus(targets);
                if (partial.Connected != partial.Total - 1) throw new InvalidOperationException("Partial native account state was not confirmed");
                shell.OpenConnections();
                await Wait(() => desktop.MainWindow!.GetVisualDescendants().OfType<Mo2ProfilesView>().Any());
                var connections = desktop.MainWindow!.GetVisualDescendants().OfType<Mo2ProfilesView>().Single();
                var button = connections.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.StandardButton>().Single(x => x.Name == "DisconnectNexusInstances");
                if (!button.IsEffectivelyVisible || !button.IsEnabled || !ReferenceEquals(button.Command, connections.ViewModel!.ManageNexusCommand)) throw new InvalidOperationException("Partial account recovery control unavailable");
                operation = connections.ViewModel.ManageNexusCommand.Execute(true).ToTask();
                Console.WriteLine("PASS Connections sign out control remains available with one disconnected native account");
            } else operation = top.LogoutCommand.Execute().ToTask();
            await Wait(() => {
                var popup = desktop.Windows.FirstOrDefault(x => x.Title == "Nexus logout");
                var title = popup?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(x => x.Name == "TitleTextBlock")?.Text;
                if (title?.StartsWith("Disconnected ", StringComparison.Ordinal) == true || title?.StartsWith("Logout could not", StringComparison.Ordinal) == true) popup!.Close();
                return operation.IsCompleted;
            });
            await operation;
            var after = await Mo2SharedNexusLogin.ReadStatus(targets);
            if (top.IsLoggedIn || top.Username is not null || after.Connected != 0) throw new InvalidOperationException("Logout retained an authenticated account");
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_PARTIAL_ACCOUNT") == "1") {
                var status = desktop.MainWindow!.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "NexusAccountStatus");
                if (status.Text != $"Nexus account · 0 of {after.Total} instances connected") throw new InvalidOperationException("Connections retained stale account status after logout");
            }
            Console.WriteLine($"PASS frontend sign out command disconnected {after.Total} native instances and cleared the top bar");
        } finally {
            var restored = await Mo2SharedNexusLogin.Apply(key, targets, _ => { }, default);
            if (restored.Connected != before.Total || restored.Failures.Count != 0) throw new InvalidOperationException("Original shared account restoration was not confirmed");
        }
        await Wait(() => top.IsLoggedIn);
        Console.WriteLine("PASS original login restored; periodic account refresh updated the running frontend");
    }

    public static async Task Run()
    {
        var one = new Mo2Registration("/fixture/one", "/one");
        var two = new Mo2Registration("/fixture/two", "/two");
        var calls = new List<string>();
        var result = await Mo2SharedNexusLogout.ApplyCore([one, two, one], _ => { }, default,
            r => { calls.Add("connect:" + r.Endpoint); return Task.CompletedTask; },
            r => { calls.Add("disconnect:" + r.Endpoint); return Task.FromResult(true); });
        if (result.Disconnected != 2 || result.Total != 2 || result.Failures.Count != 0 || !calls.SequenceEqual(new[] { "connect:/one", "disconnect:/one", "connect:/two", "disconnect:/two" }))
            throw new Exception("Logout skipped startup, duplicated targets or reordered operations");
        result = await Mo2SharedNexusLogout.ApplyCore([one, two], _ => { }, default, _ => Task.CompletedTask,
            r => r == one ? throw new IOException("private fixture") : Task.FromResult(true));
        if (result.Disconnected != 1 || result.Failures.Count != 1 || result.Failures.Any(x => x.Contains("private fixture"))) throw new Exception("Logout failure was not isolated");
        using var cancel = new CancellationTokenSource();
        calls.Clear();
        result = await Mo2SharedNexusLogout.ApplyCore([one, two], _ => { }, cancel.Token, _ => Task.CompletedTask,
            async r => { calls.Add(r.Endpoint); cancel.Cancel(); await Task.Delay(10); return true; });
        if (result.Disconnected != 1 || result.Failures.Count != 1 || !calls.SequenceEqual(new[] { "/one" })) throw new Exception("Logout cancellation interrupted a submitted change or changed another instance");
        Console.WriteLine("PASS shared logout startup, deduplication, partial failure and cancellation after submission");
    }
}
