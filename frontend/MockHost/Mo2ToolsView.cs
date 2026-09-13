using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Text.Json;

namespace Mo2.Frontend;

internal sealed record Mo2Tool(string[] Id, string Name, string Group, string Description, bool Enabled, string Icon = "");
internal interface IMo2ToolsPage : IPageViewModelInterface { }
internal sealed class Mo2ToolsPage : APageViewModel<IMo2ToolsPage>, IMo2ToolsPage
{
    public static readonly IconValue ToolIcon = new ProjektankerIcon("mdi-wrench-outline");
    public Mo2LiveProfile Profile { get; }
    public Mo2ToolsPage(IWindowManager windows, Mo2LiveProfile profile) : base(windows)
    { Profile = profile; TabTitle = "Tools"; TabIcon = ToolIcon; }
}

internal sealed class Mo2ToolsView : ReactiveUserControl<Mo2ToolsPage>
{
    private readonly StackPanel _rows = new() { Spacing = 8 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _search = new() { Watermark = "Filter tools", Name = "ToolsSearch" };
    private Mo2Tool[] _tools = [];
    private Mo2ProfileTarget? _target;
    private bool _loading;
    private string _executablesKey = "";
    private readonly List<string> _pins = [];
    private string PinsPath => Mo2ToolPins.Path;
    public Mo2ToolsView()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(24) };
        root.Children.Add(new PageHeader { Title = "Tools", Description = "Programs and extension tools for the selected game.", Icon = new AvaloniaSvg("avares://MockHost/Assets/Vortex/tools.svg") });
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, 16, 0, 12) };
        bar.Children.Add(_search);
        var manage = new Button { Content = "Add / edit…", Name = "ManageToolsButton", Margin = new Thickness(8,0,0,0) };
        ToolTip.SetTip(manage, "Configure programs in MO2");
        var refresh = new Button { Content = "Refresh", Margin = new Thickness(8,0,0,0) };
        Grid.SetColumn(manage,1); Grid.SetColumn(refresh,2); bar.Children.Add(manage); bar.Children.Add(refresh);
        Grid.SetRow(bar,1); root.Children.Add(bar); Grid.SetRow(_status,2); root.Children.Add(_status);
        var scroll = new ScrollViewer { Content = _rows, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll,3); root.Children.Add(scroll); Content = root;
        _search.TextChanged += (_,_) => Render();
        refresh.Click += async (_,_) => await Refresh();
        manage.Click += async (_,_) => {
            if (ViewModel is not { } model) return;
            await model.Profile.RunTool(new Mo2Tool([], "Executable settings", "", "", true), model.Profile.CurrentTarget, true);
            await Refresh();
        };
        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
            try { _pins.Clear(); _pins.AddRange(Mo2ToolPins.Read()); }
            catch { _status.Text = "Saved tool pins could not be read."; }
            void Changed() {
                manage.IsEnabled = model.Profile.CanChangeOriginalUi;
                if (_target != model.Profile.CurrentTarget || (!model.Profile.IsConnected && _tools.Length > 0)) _ = Refresh();
                else if (_executablesKey != string.Join("|", model.Profile.Executables.Prepend(model.Profile.SelectedExecutable))) Render();
                else foreach (var button in _rows.Children.OfType<Border>().SelectMany(x => ((Grid)x.Child!).Children).OfType<Button>().Where(x => x.Name == "LaunchToolButton"))
                    button.IsEnabled = model.Profile.CanChangeOriginalUi && button.Tag is true;
            }
            model.Profile.Changed += Changed;
            Disposable.Create(() => model.Profile.Changed -= Changed).DisposeWith(d);
            void PinsChanged() { _pins.Clear(); _pins.AddRange(Mo2ToolPins.Read()); Render(); }
            Mo2ToolPins.Changed += PinsChanged;
            Disposable.Create(() => Mo2ToolPins.Changed -= PinsChanged).DisposeWith(d);
            _ = Refresh(); Changed();
        });
    }
    private async Task Refresh()
    {
        if (_loading || ViewModel is not { } model) return;
        _loading = true; var target = model.Profile.CurrentTarget; _target = target;
        _tools = []; Render(); _status.Text = "Reading MO2 tools…";
        try { var tools = await model.Profile.ReadTools(); if (target == model.Profile.CurrentTarget) { _tools = tools; _status.Text = ""; Render(); } }
        catch (Exception error) { _status.Text = error.Message; }
        finally { _loading = false; if (target != model.Profile.CurrentTarget) await Refresh(); }
    }
    private void Render()
    {
        _rows.Children.Clear();
        if (ViewModel is not { } model) return;
        var profile = model.Profile; var target = profile.CurrentTarget;
        _executablesKey = string.Join("|", profile.Executables.Prepend(profile.SelectedExecutable));
        if (target != _target || !profile.IsConnected) return;
        var all = profile.Executables.Select(name => (Key: "exe:" + name, Name: name, Description: "Launch with this profile’s mods", Enabled: true, Icon: profile.ExecutableIcons.GetValueOrDefault(name) ?? "", Run: (Func<Task>)(() => profile.CurrentTarget == target ? profile.Launch(name) : Task.CompletedTask)))
            .Concat(_tools.Select(tool => (Key: "tool:" + JsonSerializer.Serialize(tool.Id), Name: tool.Name, Description: tool.Description.Length > 0 ? tool.Description : tool.Group.Length > 0 ? tool.Group : "MO2 extension tool", Enabled: tool.Enabled, Icon: tool.Icon, Run: (Func<Task>)(() => profile.RunTool(tool, target))))).ToArray();
        string PinKey(string key) => target.Endpoint + "|" + key;
        var selected = "exe:" + profile.SelectedExecutable;
        var sections = new[] { "Default launcher", "Pinned tools", "Tools" };
        foreach (var section in sections) {
            var entries = all.Where(x => section == "Default launcher" ? x.Key == selected : section == "Pinned tools" ? x.Key != selected && _pins.Contains(PinKey(x.Key)) : x.Key != selected && !_pins.Contains(PinKey(x.Key)))
                .Where(x => (x.Name + " " + x.Description).Contains(_search.Text ?? "", StringComparison.OrdinalIgnoreCase));
            if (section == "Pinned tools") entries = entries.OrderBy(x => _pins.IndexOf(PinKey(x.Key)));
            var items = entries.ToArray(); if (items.Length == 0) continue;
            _rows.Children.Add(new TextBlock { Text = section, Opacity = .65, Margin = new Thickness(0,14,0,2) });
            foreach (var entry in items) {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("40,*,Auto,Auto,Auto"), MinHeight = 52, Margin = new Thickness(8,4) };
                row.Children.Add(Mo2ToolIcons.Create(entry.Icon, 32));
                var label = new TextBlock { Text = entry.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(8,0) };
                ToolTip.SetTip(label, entry.Name + "\n" + entry.Description); Grid.SetColumn(label,1); row.Children.Add(label);
                if (section == "Pinned tools") {
                    var up = new Button { Content = "↑", Margin = new Thickness(2), IsEnabled = _pins.IndexOf(PinKey(entry.Key)) > 0 };
                    ToolTip.SetTip(up, "Move pinned tool earlier"); up.Click += (_,_) => { var i = _pins.IndexOf(PinKey(entry.Key)); if (i > 0) { (_pins[i-1],_pins[i]) = (_pins[i],_pins[i-1]); SavePins(); Render(); } };
                    Grid.SetColumn(up,2); row.Children.Add(up);
                }
                var pin = new Button { Content = _pins.Contains(PinKey(entry.Key)) ? "Unpin" : "Pin", Margin = new Thickness(2) };
                pin.Click += (_,_) => { if (!_pins.Remove(PinKey(entry.Key))) _pins.Add(PinKey(entry.Key)); SavePins(); Render(); };
                Grid.SetColumn(pin,3); row.Children.Add(pin);
                var launch = new Button { Content = "▶", Name = "LaunchToolButton", Tag = entry.Enabled, IsEnabled = entry.Enabled && profile.CanChangeOriginalUi, Margin = new Thickness(8,2,2,2) };
                ToolTip.SetTip(launch, "Open " + entry.Name); launch.Click += async (_,_) => await entry.Run();
                Grid.SetColumn(launch,4); row.Children.Add(launch);
                _rows.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#292C35")), CornerRadius = new CornerRadius(4), Child = row });
            }
        }
    }
    private void SavePins() { try { Directory.CreateDirectory(Path.GetDirectoryName(PinsPath)!); Mo2ToolPins.Save(_pins); } catch (Exception error) { _status.Text = "Could not save tool pins: " + error.Message; } }
}
