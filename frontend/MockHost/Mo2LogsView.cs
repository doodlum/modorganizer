using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Text;
using System.Text.RegularExpressions;

namespace Mo2.Frontend;
internal interface IMo2LogsPage : IPageViewModelInterface { }
internal sealed class Mo2LogsPage : APageViewModel<IMo2LogsPage>, IMo2LogsPage
{
    public static readonly IconValue LogsIcon = new ProjektankerIcon("mdi-text-box-outline");
    public Mo2LiveProfile Profile { get; }
    public Mo2LogsPage(IWindowManager windows,Mo2LiveProfile profile) : base(windows)
    { Profile = profile; TabTitle = "Logs"; TabIcon = LogsIcon; }
}
internal sealed class Mo2LogsView : ReactiveUserControl<Mo2LogsPage>
{
    internal sealed record LogRead(string[] Files, string? Selected, string Text);
    private readonly Func<string, string?, Task<LogRead>> _read;
    private readonly TextBox _log = new() { Name = "Mo2LogOutput", IsReadOnly = true, AcceptsReturn = true, FontFamily = FontFamily.Parse("monospace"), FontSize = 12, TextWrapping = TextWrapping.NoWrap };
    private readonly ComboBox _files = new() { Name = "Mo2LogFile", MinWidth = 160, MaxWidth = 270 };
    private readonly ComboBox _level = new() { Name = "Mo2LogLevel", ItemsSource = new[] { "All messages", "Warnings and errors", "Errors" }, SelectedIndex = 0 };
    private readonly TextBox _filter;
    private readonly TextBlock _status = new() { Name = "Mo2LogStatus", TextWrapping = TextWrapping.Wrap };
    private string _text = "";
    private Mo2ProfileTarget? _target;
    private bool _reading, _updating, _active, _reload;
    private long _activation;
    internal bool IsReading => _reading;
    public Mo2LogsView() : this(ReadLogs) { }
    internal Mo2LogsView(Func<string, string?, Task<LogRead>> read)
    {
        _read = read;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(16) };
        var header = new PageHeader { Title = "Logs", Description = "Live log output from MO2 and its extensions.", Icon = new AvaloniaSvg("avares://NexusModsApp/Assets/Pictograms/logs.svg") };
        root.Children.Add(header);
        var bar = new WrapPanel { Margin = new Thickness(0,12,0,8) };
        bar.Children.Add(_files); bar.Children.Add(_level);
        foreach (var picker in new[] { _files, _level }) {
            picker.Width = 176; picker.MinWidth = 0; picker.MinHeight = 0;
            picker.Height = Mo2TableRow.ActionSize;
            picker.FontSize = Mo2Density.FontSize;
        }
        ToolTip.SetTip(_files, "Log file"); ToolTip.SetTip(_level, "Message severity");
        var field = Mo2QtWidgets.Filter("Mo2LogFilter", "Search log messages", _ => Render(), out _filter);
        var search = new Mo2ToolbarSearch(this, "LogsToolbarSearch", field, _filter, "Search log messages");
        // Shared icon action, matching every other panel's header line.
        // The action goes through IconButton: it marks Click handled, so a Click
        // handler attached afterwards would never run.
        var refresh = Mo2TableRow.IconButton("mdi-refresh", "Refresh logs", async () => await Refresh());
        foreach (var child in bar.Children) child.Margin = new Thickness(0,0,8,6);
        Grid.SetRow(bar,1); root.Children.Add(bar); Grid.SetRow(_status,2); root.Children.Add(_status); Grid.SetRow(_log,3); root.Children.Add(_log); Content = root;
        // File and severity remain visible while the message query expands in
        // the same toolbar control used by the installed lists.
        Mo2PanelChrome.Apply(this, root, header, Mo2ListToolbar.Pill("LogsToolbarActions", refresh), search);
        _level.SelectionChanged += (_,_) => Render();
        _files.SelectionChanged += async (_,_) => { if (!_updating) await Refresh(); };
        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
            _active = true; ++_activation;
            Disposable.Create(() => { _active = false; ++_activation; }).DisposeWith(d);
            void Changed() {
                if (_target == model.Profile.CurrentTarget && model.Profile.IsConnected) return;
                _text = ""; Render(); _status.Text = "Reading MO2 logs…"; _ = Refresh();
            }
            model.Profile.Changed += Changed;
            Disposable.Create(() => model.Profile.Changed -= Changed).DisposeWith(d);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += async (_,_) => await Refresh(); timer.Start();
            Disposable.Create(timer.Stop).DisposeWith(d); _ = Refresh();
        });
    }
    private async Task Refresh()
    {
        if (!_active || ViewModel is not { } model) return;
        if (_reading) { _reload = true; return; }
        _reading = true; _reload = false;
        var target = model.Profile.CurrentTarget;
        var activation = _activation;
        var directory = model.Profile.LogsDirectory;
        var connected = model.Profile.IsConnected;
        if (_target != target || !connected) {
            _text = ""; Render();
            _updating = true; _files.ItemsSource = Array.Empty<string>(); _updating = false;
        }
        _target = target;
        var selected = _files.SelectedItem as string;
        bool Current() => _active && activation == _activation && target == model.Profile.CurrentTarget &&
            connected == model.Profile.IsConnected && directory == model.Profile.LogsDirectory && selected == _files.SelectedItem as string;
        try {
            if (!connected || directory is null) { _text = ""; Render(); _status.Text = "Connect to MO2 to read this instance’s logs."; return; }
            // Enumeration, metadata, decoding and redaction all run off-thread.
            var result = await Task.Run(() => _read(directory, selected));
            if (!Current()) { if (_active) _reload = true; return; }
            _updating = true;
            if (!(_files.ItemsSource as IEnumerable<string> ?? []).SequenceEqual(result.Files)) _files.ItemsSource = result.Files;
            _files.SelectedItem = result.Selected;
            _updating = false;
            if (_text != result.Text) { _text = result.Text; Render(); }
            _status.Text = result.Selected is null ? "MO2 has not written a log yet." : model.Profile.GameName + " · " + result.Selected + " · Latest 2,000 lines";
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            if (Current()) _status.Text = "Log unavailable: " + error.Message;
            else if (_active) _reload = true;
        } finally {
            _reading = false; _updating = false;
            if (_active && _reload) { _reload = false; _ = Refresh(); }
        }
    }
    internal static async Task<LogRead> ReadLogs(string directory, string? selected)
    {
        var files = Directory.EnumerateFiles(directory,"*.log").OrderByDescending(File.GetLastWriteTimeUtc).Select(Path.GetFileName).OfType<string>().ToArray();
        var name = selected is not null && files.Contains(selected) ? selected : files.FirstOrDefault();
        if (name is null) return new(files, null, "");
        using var stream = new FileStream(Path.Combine(directory,name),FileMode.Open,FileAccess.Read,FileShare.ReadWrite | FileShare.Delete,4096,true);
        var truncated = stream.Length > 256 * 1024;
        if (truncated) stream.Seek(-256 * 1024,SeekOrigin.End);
        using var reader = new StreamReader(stream,Encoding.UTF8,detectEncodingFromByteOrderMarks:true);
        // The first bytes may be in the middle of a line (including a credential
        // whose identifying prefix was before the retained tail).
        if (truncated) await reader.ReadLineAsync().ConfigureAwait(false);
        var text = await reader.ReadToEndAsync().ConfigureAwait(false);
        text = Regex.Replace(text,@"(?im)^.*(?:apikey|api.key|authorization|access.token|refresh.token).*$","[account details hidden]");
        text = Regex.Replace(text,@"(https?://[^\s?]+)\?[^\s]+","$1?[query hidden]");
        return new(files, name, string.Join("\n",text.Split('\n').TakeLast(2000)));
    }
    private void Render() {
        var lines = _text.Split('\n').Where(line => line.Contains(_filter.Text ?? "",StringComparison.OrdinalIgnoreCase));
        if (_level.SelectedIndex > 0) lines = lines.Where(line => line.Contains("error",StringComparison.OrdinalIgnoreCase) || line.Contains("critical",StringComparison.OrdinalIgnoreCase) || (_level.SelectedIndex == 1 && line.Contains("warn",StringComparison.OrdinalIgnoreCase)));
        var text = string.Join("\n", lines);
        // Messages excluded by the active filters do not change the visible log.
        // Keep the reader's selection and position intact for those updates.
        if (_log.Text == text) return;
        _log.Text = text; _log.CaretIndex = text.Length;
    }
}
