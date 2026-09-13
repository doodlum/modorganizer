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
    private readonly TextBox _log = new() { Name = "Mo2LogOutput", IsReadOnly = true, AcceptsReturn = true, FontFamily = FontFamily.Parse("monospace"), FontSize = 12, TextWrapping = TextWrapping.NoWrap };
    private readonly ComboBox _files = new() { Name = "Mo2LogFile", MinWidth = 160, MaxWidth = 270 };
    private readonly ComboBox _level = new() { ItemsSource = new[] { "All messages", "Warnings and errors", "Errors" }, SelectedIndex = 0 };
    private readonly TextBox _filter = new() { Watermark = "Filter log messages", MinWidth = 140 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private string _text = "";
    private Mo2ProfileTarget? _target;
    private bool _reading, _updating;
    public Mo2LogsView()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(16) };
        root.Children.Add(new PageHeader { Title = "Logs", Description = "Live log output from MO2 and its extensions.", Icon = new AvaloniaSvg("avares://MockHost/Assets/Pictograms/logs.svg") });
        var bar = new WrapPanel { Margin = new Thickness(0,12,0,8) };
        bar.Children.Add(_files); bar.Children.Add(_level); bar.Children.Add(_filter);
        var refresh = new Button { Content = "Refresh" }; bar.Children.Add(refresh);
        foreach (var child in bar.Children) child.Margin = new Thickness(0,0,8,6);
        Grid.SetRow(bar,1); root.Children.Add(bar); Grid.SetRow(_status,2); root.Children.Add(_status); Grid.SetRow(_log,3); root.Children.Add(_log); Content = root;
        _level.SelectionChanged += (_,_) => Render(); _filter.TextChanged += (_,_) => Render();
        _files.SelectionChanged += async (_,_) => { if (!_updating) await Refresh(); }; refresh.Click += async (_,_) => await Refresh();
        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
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
        if (_reading || ViewModel is not { } model) return;
        _reading = true; var target = model.Profile.CurrentTarget; _target = target;
        try {
            var directory = model.Profile.LogsDirectory;
            if (!model.Profile.IsConnected || directory is null || !Directory.Exists(directory)) { _text = ""; Render(); _status.Text = "Connect to MO2 to read this instance’s logs."; return; }
            var files = Directory.EnumerateFiles(directory,"*.log").OrderByDescending(File.GetLastWriteTimeUtc).Select(Path.GetFileName).OfType<string>().ToArray();
            _updating = true;
            var selected = _files.SelectedItem as string;
            if (!(_files.ItemsSource as IEnumerable<string> ?? []).SequenceEqual(files)) _files.ItemsSource = files;
            _files.SelectedItem = selected is not null && files.Contains(selected) ? selected : files.FirstOrDefault();
            _updating = false;
            if (_files.SelectedItem is not string name) { _text = ""; Render(); _status.Text = "MO2 has not written a log yet."; return; }
            using var stream = new FileStream(Path.Combine(directory,name),FileMode.Open,FileAccess.Read,FileShare.ReadWrite | FileShare.Delete,4096,true);
            if (stream.Length > 256 * 1024) stream.Seek(-256 * 1024,SeekOrigin.End);
            using var reader = new StreamReader(stream,Encoding.UTF8,detectEncodingFromByteOrderMarks:true);
            var text = await reader.ReadToEndAsync();
            if (target != model.Profile.CurrentTarget) return;
            // Native debug logs can contain account headers or signed URLs.
            text = Regex.Replace(text,@"(?im)^.*(?:apikey|api.key|authorization|access.token|refresh.token).*$","[account details hidden]");
            text = Regex.Replace(text,@"(https?://[^\s?]+)\?[^\s]+","$1?[query hidden]");
            text = string.Join("\n",text.Split('\n').TakeLast(2000));
            if (_text != text) { _text = text; Render(); }
            _status.Text = model.Profile.GameName + " · " + name + " · Latest 2,000 lines";
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { _status.Text = "Log unavailable: " + error.Message; }
        finally { _reading = false; _updating = false; }
    }
    private void Render() {
        var lines = _text.Split('\n').Where(line => line.Contains(_filter.Text ?? "",StringComparison.OrdinalIgnoreCase));
        if (_level.SelectedIndex > 0) lines = lines.Where(line => line.Contains("error",StringComparison.OrdinalIgnoreCase) || line.Contains("critical",StringComparison.OrdinalIgnoreCase) || (_level.SelectedIndex == 1 && line.Contains("warn",StringComparison.OrdinalIgnoreCase)));
        _log.Text = string.Join("\n",lines); _log.CaretIndex = _log.Text.Length;
    }
}
