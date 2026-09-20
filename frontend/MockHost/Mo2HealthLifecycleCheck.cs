using System.Reflection;

namespace Mo2.Frontend;

internal static class Mo2HealthLifecycleCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell)
    {
        using var turn = await Mo2CheckTurn.Take();
        await Mo2NotificationLifecycleCheck.Run(shell);
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(10);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception("Health lifecycle check did not settle");
                await Task.Delay(25);
            }
        }
        foreach (var fail in new[] { false, true }) {
            var profile = new Mo2LiveProfile("first-host");
            void Set(string name, object value) => typeof(Mo2LiveProfile).GetProperty(name)!.SetValue(profile, value);
            Set(nameof(profile.ProfilePath), "same-profile-path");
            var pending = new TaskCompletionSource<(string Title, string Details)[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reads = 0;
            var page = new Mo2HealthPage(new FixtureWindows { ActiveWindow = shell }, shell, profile,
                () => ++reads == 1 ? pending.Task : Task.FromResult(new[] { ("Current warning", "Current details") }));
            var active = page.Activator.Activate();
            try {
                await Wait(() => reads == 1);
                active.Dispose();
                var before = page.DiagnosticEntries.Select(x => x.Title).ToArray();
                if (fail) pending.SetException(new Exception("Old diagnostic failure"));
                else pending.SetResult([("Old warning", "Old details")]);
                await Task.Delay(100);
                if (reads != 1 || !before.SequenceEqual(page.DiagnosticEntries.Select(x => x.Title)))
                    throw new Exception("Detached Health Check accepted a late result or initiated another read");
                active = page.Activator.Activate();
                await Wait(() => page.HasResult && page.DiagnosticEntries.Any(x => x.Title == "Current warning"));
                if (reads != 2) throw new Exception("Reopened Health Check did not read once");
                var currentEntries = page.DiagnosticEntries;
                await page.Refresh();
                if (!currentEntries.SequenceEqual(page.DiagnosticEntries))
                    throw new Exception("Unchanged diagnostic refresh replaced its report objects");
                Set(nameof(profile.Endpoint), "other-host-with-identical-warning");
                await page.Refresh();
                if (currentEntries.SequenceEqual(page.DiagnosticEntries))
                    throw new Exception("Identical diagnostics retained another host's report objects");
                Set(nameof(profile.Endpoint), "first-host");
            } finally { active.Dispose(); }

            var oldHost = new TaskCompletionSource<(string Title, string Details)[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            reads = 0;
            var switched = new Mo2HealthPage(new FixtureWindows { ActiveWindow = shell }, shell, profile,
                () => ++reads == 1 ? oldHost.Task : Task.FromResult(new[] { ("Second host warning", "Second host details") }));
            using var switchedActive = switched.Activator.Activate();
            await Wait(() => reads == 1);
            Set(nameof(profile.Endpoint), "second-host");
            ((Action?)typeof(Mo2LiveProfile).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(profile))?.Invoke();
            if (fail) oldHost.SetException(new Exception("Wrong host failure"));
            else oldHost.SetResult([("Wrong host warning", "Wrong host details")]);
            await Wait(() => switched.HasResult && switched.DiagnosticEntries.Any(x => x.Title == "Second host warning"));
            if (reads != 2) throw new Exception("Same-path host switch did not request its own diagnostics");
        }
        Console.WriteLine("PASS Health lifecycle: detached success/error ignored, reopen refreshes, same-path host changes discard old success/error and read current diagnostics");
        foreach (var fail in new[] { false, true })
        foreach (var earlyReopen in new[] { false, true }) {
            var profile = new Mo2LiveProfile("details-host");
            typeof(Mo2LiveProfile).GetProperty(nameof(profile.ProfilePath))!.SetValue(profile, "details-profile");
            typeof(Mo2LiveProfile).GetField("_lastSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(profile, "fixture");
            var diagnostic = new Mo2HealthEntry("Warning", "Original details",
                NexusMods.Abstractions.Diagnostics.DiagnosticSeverity.Warning, shell).Diagnostic;
            var pending = new TaskCompletionSource<(string Title, string Details)[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reads = 0;
            var details = new Mo2HealthDetails(new FixtureWindows { ActiveWindow = shell }, profile,
                new Mo2HealthDetailsContext(diagnostic, profile.Endpoint, profile.ProfilePath),
                () => ++reads == 1 ? pending.Task : Task.FromResult(new[] { ("Warning", "Fresh details") }));
            var active = details.Activator.Activate();
            try {
                await Wait(() => reads == 1);
                active.Dispose();
                var before = details.MarkdownRendererViewModel.Contents;
                if (earlyReopen) active = details.Activator.Activate();
                if (fail) pending.SetException(new Exception("Old details error"));
                else pending.SetResult([("Warning", "Old details")]);
                if (!earlyReopen) {
                    await Task.Delay(100);
                    if (reads != 1 || details.MarkdownRendererViewModel.Contents != before)
                        throw new Exception("Detached diagnostic details accepted a late result or initiated a read");
                    active = details.Activator.Activate();
                }
                await Wait(() => details.HasResult && details.MarkdownRendererViewModel.Contents == "Fresh details");
                if (reads != 2) throw new Exception("Reopened details did not refresh once after the prior read");
            } finally { active.Dispose(); }
        }
        Console.WriteLine("PASS Health details lifecycle: late success/error ignored while closed; reopening before or after completion reads fresh details once");
    }
}
