using Avalonia.Controls;
using Avalonia.VisualTree;
using System.Reflection;

namespace Mo2.Frontend;

internal static class Mo2ToolsLifecycleCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        using var turn = await Mo2CheckTurn.Take();
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
            // An isolated profile exercises notifications without changing the
            // user's pins, executables, installation state or native instance.
            var profile = new Mo2LiveProfile("");
            void Set(string name, object value) => typeof(Mo2LiveProfile).GetProperty(name)!.SetValue(profile, value);
            void Notify() => ((Action?)typeof(Mo2LiveProfile).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(profile))?.Invoke();
            typeof(Mo2LiveProfile).GetField("_lastSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(profile, "fixture");
            Set(nameof(profile.ProfilePath), "/tools-presentation-fixture");
            Set(nameof(profile.OriginalUiVisible), false);
            Set(nameof(profile.Executables), new[] { "Primary", "Secondary" });
            Set(nameof(profile.SelectedExecutable), "Primary");
            var presentation = new Mo2ToolsView(() => Task.FromResult(Array.Empty<Mo2Tool>())) {
                ViewModel = new Mo2ToolsPage(new FixtureWindows { ActiveWindow = shell }, profile)
            };
            host.Children.Add(presentation);
            await Wait(() => !presentation.IsReading && presentation.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == "Secondary"));
            Grid Row() => presentation.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Text == "Secondary")
                .GetVisualAncestors().OfType<Grid>().First();
            Button Pin() => Row().Children.OfType<Button>().Single(x => x.Content is "Pin" or "Unpin");
            var faults = new List<string>();
            var unchanged = Row(); Notify();
            if (!ReferenceEquals(unchanged, Row())) faults.Add("unchanged notifications rebuild tool rows");
            Set(nameof(profile.PinnedExecutables), new[] { "Secondary" }); Notify();
            if (!Equals(Pin().Content, "Unpin") || !presentation.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == "Pinned tools"))
                faults.Add("native pin changes are not reflected");
            using (var iconStream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://NexusModsApp/Assets/AppIcon.png"))) {
                using var bytes = new MemoryStream(); iconStream.CopyTo(bytes);
                var encoded = Convert.ToBase64String(bytes.ToArray());
                Set(nameof(profile.ExecutableIcons), new Dictionary<string,string> { ["Secondary"] = encoded }); Notify();
                var expected = ((Image)Mo2ToolIcons.Create(encoded, 20)).Source;
                if (!Row().Children.OfType<Image>().Any(x => ReferenceEquals(x.Source, expected))) faults.Add("native icon changes are not reflected");
            }
            Set(nameof(profile.Installing), true); Notify();
            if (Pin().IsEnabled || Row().Children.OfType<Button>().Single(x => x.Name == "LaunchToolButton").IsEnabled)
                faults.Add("native executable actions remain enabled while busy");
            Set(nameof(profile.Installing), false); Notify();
            if (!Pin().IsEnabled || !Row().Children.OfType<Button>().Single(x => x.Name == "LaunchToolButton").IsEnabled)
                faults.Add("native executable actions do not recover after busy state");
            Set(nameof(profile.PinnedExecutables), Array.Empty<string>()); Notify();
            if (!Equals(Pin().Content, "Pin")) faults.Add("native unpin changes are not reflected");
            host.IsHitTestVisible = true;
            await Mo2ToolbarSearchCheck.Run(window, presentation);
            host.IsHitTestVisible = false;
            host.Children.Remove(presentation);
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_TOOL_PIN_ORDER") == "1") {
                Set(nameof(profile.PinnedExecutables), new[] { "Secondary" });
                await Mo2ToolPinOrderCheck.Run(host, shell, profile);
                Set(nameof(profile.PinnedExecutables), Array.Empty<string>());
            }
            Set(nameof(profile.Tools), new[] { new Mo2Tool(["extension"], "Original tool", "", "", false) });
            var toolReads = 0;
            var launcher = new Mo2LaunchPanel(profile, () => ["|tool:[\"extension\"]"], () => { toolReads++; return Task.CompletedTask; });
            host.Children.Add(launcher);
            launcher.Executable.SelectedItem = "Secondary";
            Notify();
            if (launcher.Model.SelectedExecutable != "Secondary")
                faults.Add("unchanged native selection overwrites a local launcher choice");
            Set(nameof(profile.SelectedExecutable), "Secondary"); Notify();
            Set(nameof(profile.SelectedExecutable), "Primary"); Notify();
            if (launcher.Model.SelectedExecutable != "Primary" || !Equals(launcher.Executable.SelectedItem, "Primary"))
                faults.Add("launcher ignores a changed native executable selection");
            Button[] Shortcuts() => launcher.GetVisualDescendants().OfType<Button>().Where(x => x.Name == "PinnedToolShortcut").ToArray();
            await Wait(() => Shortcuts().Length == 1);
            if (toolReads != 1) faults.Add("launcher does not load pinned extension availability on its own");
            if (Shortcuts()[0].IsEnabled) faults.Add("disabled extension shortcut can be launched");
            Set(nameof(profile.Tools), new[] { new Mo2Tool(["extension"], "Renamed tool", "", "", true) }); Notify();
            if (!Equals(ToolTip.GetTip(Shortcuts()[0]), "Renamed tool") || !Shortcuts()[0].IsEnabled)
                faults.Add("extension shortcut rename/enabled state is stale");
            var unchangedShortcut = Shortcuts()[0]; Notify();
            if (!ReferenceEquals(unchangedShortcut, Shortcuts()[0])) faults.Add("unchanged notifications rebuild shortcuts");
            if (toolReads != 1) faults.Add("unchanged notifications repeatedly query native tools");
            Set(nameof(profile.Installing), true); Notify();
            if (Shortcuts()[0].IsEnabled) faults.Add("extension shortcut remains enabled while busy");
            Set(nameof(profile.Installing), false); Notify();
            if (!Shortcuts()[0].IsEnabled) faults.Add("extension shortcut does not recover after busy state");
            if (toolReads != 2) faults.Add("pinned extension availability is not refreshed after busy state");
            Set(nameof(profile.Tools), Array.Empty<Mo2Tool>()); Notify();
            if (Shortcuts()[0].IsEnabled) faults.Add("removed extension shortcut can be launched");
            Set(nameof(profile.PinnedExecutables), new[] { "Secondary" });
            Set(nameof(profile.Executables), new[] { "Primary" }); Notify();
            if (Shortcuts().Single(x => Equals(ToolTip.GetTip(x), "Secondary")).IsEnabled)
                faults.Add("missing executable shortcut can be launched");
            host.Children.Remove(launcher);
            var detachedReads = toolReads;
            var detachedItems = launcher.Executable.ItemsSource;
            Set(nameof(profile.Executables), new[] { "Primary", "Reopened" });
            Set(nameof(profile.Installing), true); Notify();
            Set(nameof(profile.Installing), false); Notify();
            if (toolReads != detachedReads || !ReferenceEquals(detachedItems, launcher.Executable.ItemsSource))
                faults.Add("detached launcher still refreshes tools or executable choices");
            host.Children.Add(launcher);
            await Wait(() => (launcher.Executable.ItemsSource as IEnumerable<string> ?? []).Contains("Reopened"));
            if (toolReads != detachedReads + 1)
                faults.Add("reopened launcher does not refresh pinned extension availability once");
            host.Children.Remove(launcher);
            Set(nameof(profile.PinnedExecutables), Array.Empty<string>());
            Set(nameof(profile.Tools), new[] { new Mo2Tool(["extension"], "Pending tool", "", "", true) });
            var toolRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var pendingLauncher = new Mo2LaunchPanel(profile, () => ["|tool:[\"extension\"]"], () => toolRead.Task);
            host.Children.Add(pendingLauncher);
            Button PendingShortcut() => pendingLauncher.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PinnedToolShortcut");
            if (PendingShortcut().IsEnabled) faults.Add("shortcut trusts old tool availability while its read is pending");
            toolRead.SetResult();
            await Wait(() => PendingShortcut().IsEnabled);
            host.Children.Remove(pendingLauncher);
            foreach (var manualRefresh in new[] { true, false }) {
                var recoveryReads = 0;
                var recoveryLauncher = new Mo2LaunchPanel(profile, () => ["|tool:[\"extension\"]"], () =>
                    ++recoveryReads == 1 ? Task.FromException(new IOException("Controlled pinned-tools read failure")) : Task.CompletedTask);
                host.Children.Add(recoveryLauncher);
                Button RecoveringShortcut() => recoveryLauncher.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PinnedToolShortcut");
                if (RecoveringShortcut().IsEnabled || recoveryReads != 1) faults.Add("failed pinned-tool read did not disable its shortcut");
                if (manualRefresh) {
                    // ReadTools replaces this list on success and then notifies.
                    Set(nameof(profile.Tools), new[] { new Mo2Tool(["extension"], "Recovered tool", "", "", true) });
                } else Set(nameof(profile.ProfilePath), "/tools-recovered-profile");
                Notify();
                if (!RecoveringShortcut().IsEnabled || recoveryReads != (manualRefresh ? 1 : 2))
                    faults.Add(manualRefresh ? "successful Tools refresh leaves pinned shortcuts disabled during backoff" :
                        "old profile's failed-read backoff delays the new profile's tools");
                host.Children.Remove(recoveryLauncher);
            }
            var shortcutReads = new List<TaskCompletionSource<Mo2LiveProfile.Mo2Shortcut[]>>();
            var shortcutLauncher = new Mo2LaunchPanel(profile, () => [], readShortcuts: _ => {
                var read = new TaskCompletionSource<Mo2LiveProfile.Mo2Shortcut[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                shortcutReads.Add(read); return read.Task;
            });
            host.Children.Add(shortcutLauncher);
            var shortcutButton = shortcutLauncher.ShortcutMenu!;
            var shortcutMenu = (MenuFlyout)shortcutButton.Flyout!;
            shortcutMenu.ShowAt(shortcutButton);
            await Wait(() => shortcutReads.Count == 1);
            shortcutMenu.Hide();
            shortcutMenu.ShowAt(shortcutButton);
            await Wait(() => shortcutReads.Count == 2);
            shortcutReads[1].SetResult([new("Current response", false, true)]);
            await Wait(() => shortcutMenu.Items.OfType<MenuItem>().Any(x => Equals(x.Header, "Add to Current response")));
            shortcutReads[0].SetResult([new("Stale response", false, true)]);
            await Task.Delay(100);
            if (!shortcutMenu.Items.OfType<MenuItem>().Any(x => Equals(x.Header, "Add to Current response")))
                faults.Add("old shortcut response replaces a reopened menu");
            shortcutMenu.Hide();
            shortcutMenu.ShowAt(shortcutButton);
            await Wait(() => shortcutReads.Count == 3);
            shortcutLauncher.Executable.SelectedItem = "Reopened";
            shortcutReads[2].SetResult([new("Wrong executable", false, true)]);
            await Task.Delay(100);
            if (shortcutMenu.IsOpen || shortcutMenu.Items.OfType<MenuItem>().Any(x => Equals(x.Header, "Add to Wrong executable")))
                faults.Add("shortcut menu retains actions after executable selection changes");
            host.Children.Remove(shortcutLauncher);
            if (faults.Count > 0) throw new Exception(string.Join("; ", faults));
            Console.WriteLine("PASS shortcut menu lifecycle: old responses cannot replace reopened menus; executable changes close and invalidate pending actions");
            Console.WriteLine("PASS Tools lifecycle: delayed success/failure discarded after detach; reopen reads fresh tools; native pin/icon changes update rows; busy actions disable and recover; unchanged notifications retain rows; pinned shortcuts follow rename, enabled state and removal; failed reads recover immediately on manual refresh or profile change");

            if (Environment.GetEnvironmentVariable("MO2_VERIFY_PINNED_TOOLS_NATIVE") == "1") {
                var nativeTools = await shell.Profile.ReadTools();
                if (nativeTools.Length == 0) throw new Exception("Native pinned-tool check requires installed extension tools");
                var fresh = new Mo2LiveProfile(shell.Profile.Endpoint);
                await fresh.Refresh();
                if (!fresh.IsConnected || fresh.CurrentTarget != shell.Profile.CurrentTarget || fresh.Tools.Count != 0)
                    throw new Exception("Fresh launch profile is not connected with an empty tool cache");
                var nativePins = nativeTools.Select(tool => fresh.Endpoint + "|tool:" + System.Text.Json.JsonSerializer.Serialize(tool.Id)).ToArray();
                // Only the pin source is injected. Availability comes through the
                // production native reader, without opening Tools or running one.
                var nativeLauncher = new Mo2LaunchPanel(fresh, () => nativePins);
                host.Children.Add(nativeLauncher);
                Button[] NativeShortcuts() => nativeLauncher.GetVisualDescendants().OfType<Button>()
                    .Where(x => x.Name == "PinnedToolShortcut").ToArray();
                await Wait(() => fresh.Tools.Count == nativeTools.Length && nativeTools.All(tool =>
                    NativeShortcuts().Any(button => Equals(ToolTip.GetTip(button), tool.Name) && button.IsEnabled == tool.Enabled)));
                var before = NativeShortcuts();
                await fresh.ReadTools();
                if (!before.SequenceEqual(NativeShortcuts())) throw new Exception("Unchanged native tool refresh rebuilt pinned buttons");
                host.Children.Remove(nativeLauncher);
                Console.WriteLine($"PASS native pinned tools: {nativeTools.Length} extension shortcuts resolve names and availability from an empty cache through the live bridge; unchanged reread retains buttons; no launch or pin write");
            }
        } finally { root.Children.Remove(host); }
    }
}
