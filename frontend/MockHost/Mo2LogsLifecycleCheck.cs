using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2LogsLifecycleCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell)
    {
        async Task Wait(Func<bool> ready, string message) {
            var until = DateTime.UtcNow.AddSeconds(15);
            while (!ready()) { if (DateTime.UtcNow > until) throw new Exception(message); await Task.Delay(25); }
        }
        await Wait(() => shell.Profile.IsConnected && shell.Profile.LogsDirectory is not null, "Log host unavailable");
        using var turn = await Mo2CheckTurn.Take();
        var host = new Grid();
        var window = new Window { Width = 650, Height = 650, Content = host, ShowInTaskbar = false };
        window.Show();
        Mo2LogsView Make(Func<string, string?, Task<Mo2LogsView.LogRead>> read) => new(read) {
            ViewModel = new Mo2LogsPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile)
        };
        TextBox Output(Mo2LogsView view) => view.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "Mo2LogOutput");
        TextBlock Status(Mo2LogsView view) => view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "Mo2LogStatus");
        async Task Drawn(Mo2LogsView view) => await Wait(() => view.GetVisualDescendants().OfType<TextBox>().Any(x => x.Name == "Mo2LogOutput"), "Logs controls not drawn");
        try {
            foreach (var failure in new[] { false, true })
            foreach (var earlyReopen in new[] { false, true }) {
                var pending = new TaskCompletionSource<Mo2LogsView.LogRead>(TaskCreationOptions.RunContinuationsAsynchronously);
                var reads = 0;
                var onUi = false;
                var view = Make((_, _) => {
                    if (Dispatcher.UIThread.CheckAccess()) onUi = true;
                    return Interlocked.Increment(ref reads) == 1 ? pending.Task : Task.FromResult(new Mo2LogsView.LogRead(["fresh.log"], "fresh.log", "fresh logs"));
                });
                host.Children.Add(view);
                await Drawn(view);
                var output = Output(view);
                var status = Status(view);
                await Wait(() => Volatile.Read(ref reads) == 1, "Initial log read did not start");
                host.Children.Remove(view);
                if (earlyReopen) host.Children.Add(view);
                if (failure) pending.SetException(new IOException("stale log error"));
                else pending.SetResult(new(["stale.log"], "stale.log", "stale logs"));
                if (!earlyReopen) {
                    await Wait(() => !view.IsReading, "Detached read did not finish");
                    if (reads != 1 || output.Text == "stale logs" || status.Text?.Contains("stale log error") == true)
                        throw new Exception("Detached logs published stale content or started another read");
                    host.Children.Add(view);
                }
                await Wait(() => output.Text == "fresh logs", "Reopened logs did not read fresh content");
                if (onUi || status.Text?.Contains("stale log error") == true)
                    throw new Exception("Logs ran I/O on the UI thread or published an old error");
                host.Children.Remove(view);
            }
            var delayed = new TaskCompletionSource<Mo2LogsView.LogRead>(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var addedMessages = "";
            var selectedView = Make((_, selected) => Interlocked.Increment(ref calls) == 2 ? delayed.Task :
                Task.FromResult(new Mo2LogsView.LogRead(["a.log", "b.log"], selected ?? "a.log", selected == "b.log" ? "[info] second\n[warning] retained\n[error] retained" + Volatile.Read(ref addedMessages) : "first logs")));
            host.Children.Add(selectedView);
            await Drawn(selectedView);
            await Wait(() => Output(selectedView).Text == "first logs", "File selector did not load");
            var staleDisplayed = false;
            Output(selectedView).TextChanged += (_, _) => { if (Output(selectedView).Text == "old first logs") staleDisplayed = true; };
            selectedView.GetVisualDescendants().OfType<Button>().Single(x => ToolTip.GetTip(x) as string == "Refresh logs")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => Volatile.Read(ref calls) >= 2, "Delayed refresh did not start");
            selectedView.GetVisualDescendants().OfType<ComboBox>().Single(x => x.Name == "Mo2LogFile").SelectedItem = "b.log";
            delayed.SetResult(new(["a.log", "b.log"], "a.log", "old first logs"));
            await Wait(() => Output(selectedView).Text?.Contains("[info] second") == true, "File change during refresh was lost");
            if (staleDisplayed) throw new Exception("Old file contents appeared after selecting another file");
            var level = selectedView.GetVisualDescendants().OfType<ComboBox>().Single(x => x.Name == "Mo2LogLevel");
            level.SelectedIndex = 1;
            if (Output(selectedView).Text != "[warning] retained\n[error] retained") throw new Exception("Warning filter differs");
            var selectedOutput = Output(selectedView);
            selectedOutput.CaretIndex = 9; selectedOutput.SelectionStart = 1; selectedOutput.SelectionEnd = 9;
            var start = selectedOutput.SelectionStart; var end = selectedOutput.SelectionEnd; var caret = selectedOutput.CaretIndex;
            var priorCalls = Volatile.Read(ref calls);
            Volatile.Write(ref addedMessages, "\n[info] excluded by the selected level");
            selectedView.GetVisualDescendants().OfType<Button>().Single(x => Equals(ToolTip.GetTip(x), "Refresh logs"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => Volatile.Read(ref calls) > priorCalls && !selectedView.IsReading, "Filtered log refresh did not complete");
            if (selectedOutput.SelectionStart != start || selectedOutput.SelectionEnd != end || selectedOutput.CaretIndex != caret)
                throw new Exception("Excluded messages disturbed the displayed log selection");
            level.SelectedIndex = 2;
            if (Output(selectedView).Text != "[error] retained") throw new Exception("Error filter differs");
            var filter = selectedView.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "Mo2LogFilter");
            filter.Text = "absent";
            await Wait(() => Output(selectedView).Text == "", "Text filter did not combine with level");
            var search = selectedView.GetVisualDescendants().OfType<Mo2ToolbarSearch>().Single();
            var searchButton = search.GetVisualDescendants().OfType<Button>().First();
            searchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => filter.IsEffectivelyVisible && filter.IsFocused, "Logs search did not open and focus");
            void Key(Key key, KeyModifiers modifiers = KeyModifiers.None) {
                var focused = window.FocusManager?.GetFocusedElement() as InputElement ?? throw new Exception("Logs search lost focus");
                var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };
                focused.RaiseEvent(args);
                if (!args.Handled) throw new Exception("Logs search shortcut was not handled");
            }
            Key(Avalonia.Input.Key.F, KeyModifiers.Control);
            await Wait(() => !filter.IsEffectivelyVisible && searchButton.IsFocused, "Logs search did not collapse");
            if (filter.Text != "absent") throw new Exception("Collapsing Logs search discarded its query");
            Key(Avalonia.Input.Key.F, KeyModifiers.Control);
            await Wait(() => filter.IsEffectivelyVisible && filter.IsFocused, "Logs search did not reopen");
            foreach (var width in new[] { 340d, 280d, 240d }) {
                selectedView.Width = width;
                await Task.Delay(200);
                foreach (var control in new Control[] { search,
                    selectedView.GetVisualDescendants().OfType<ComboBox>().Single(x => x.Name == "Mo2LogFile"), level }) {
                    var at = control.TranslatePoint(default, selectedView)!.Value;
                    if (Math.Abs(selectedView.Bounds.Width - width) > 1 || at.X < 23 || at.X + control.Bounds.Width > width - 23)
                        throw new Exception("Logs toolbar control loses padding at " + width + "px: " + control.Name);
                }
            }
            selectedView.Width = double.NaN;
            Key(Avalonia.Input.Key.Escape);
            await Wait(() => !filter.IsEffectivelyVisible && searchButton.IsFocused && Output(selectedView).Text == "[error] retained",
                "Escape did not restore severity-filtered logs");
            Console.WriteLine("PASS Logs toolbar: visible file/severity selectors; 340/280/240px padding; Ctrl+F retains query; Escape restores filtered messages and focus");
            host.Children.Remove(selectedView);
            await CheckReader();
            var nativeView = new Mo2LogsView { ViewModel = new Mo2LogsPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile) };
            host.Children.Add(nativeView);
            await Drawn(nativeView);
            await Wait(() => !nativeView.IsReading && Status(nativeView).Text?.Contains("Latest 2,000 lines") == true,
                "Current native log did not render");
            if (string.IsNullOrEmpty(Output(nativeView).Text) || Output(nativeView).Text!.Split('\n').Length > 2000)
                throw new Exception("Current native log was empty or exceeded the line limit");
            host.Children.Remove(nativeView);
            Console.WriteLine("PASS Logs lifecycle: off-thread reads; stale success/error discarded on close/reopen; file change during read retained; level/text filters; excluded messages preserve selection; bounded tail and credential redaction");
            Console.WriteLine("PASS Logs native: current host log rendered within the line limit; no log contents emitted by this check");
        } finally { host.Children.Clear(); window.Close(); }
    }

    private static async Task CheckReader()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mo2-log-reader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            await File.WriteAllTextAsync(Path.Combine(directory, "test.log"),
                "Authorization: " + new string('x', 300_000) + "\n[info] safe\napi_key=dummy-value\nhttps://example.invalid/file?token=dummy-value");
            var result = await Mo2LogsView.ReadLogs(directory, "test.log");
            if (result.Text != "[info] safe\n[account details hidden]\nhttps://example.invalid/file?[query hidden]")
                throw new Exception("Log tail exposed a partial line, account value or URL query");
            await File.WriteAllTextAsync(Path.Combine(directory, "test.log"), string.Join('\n', Enumerable.Range(0, 2500).Select(x => "line " + x)));
            result = await Mo2LogsView.ReadLogs(directory, "test.log");
            if (result.Text.Split('\n').Length != 2000 || !result.Text.StartsWith("line 500\n") || !result.Text.EndsWith("line 2499"))
                throw new Exception("Log reader did not retain the newest 2000 lines");
        } finally { Directory.Delete(directory, recursive: true); }
    }
}
