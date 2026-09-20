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
    private readonly TextBox _search;
    private Mo2Tool[] _tools = [];
    private Mo2ProfileTarget? _target;
    private bool _loading;
    internal bool IsReading => _loading;
    private bool _active;
    private long _activation;
    private readonly Func<Task<Mo2Tool[]>>? _read;
    private bool _compact;
    private (string Name, string Icon, bool Pinned)[] _executableRows = [];
    private string _selectedExecutable = "";
    private static IEnumerable<(string Name, string Icon, bool Pinned)> ExecutableRows(Mo2LiveProfile profile) =>
        profile.Executables.Select(name => (name, profile.ExecutableIcons.GetValueOrDefault(name) ?? "", profile.PinnedExecutables.Contains(name)));
    private readonly List<string> _pins = [];
    private string PinsPath => Mo2ToolPins.Path;
    public Mo2ToolsView() : this(null) { }
    internal Mo2ToolsView(Func<Task<Mo2Tool[]>>? read)
    {
        _read = read;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Margin = new Thickness(24) };
        var header = new PageHeader { Title = "Tools", Description = "Programs and extension tools for the selected game.", Icon = new AvaloniaSvg("avares://NexusModsApp/Assets/Vortex/tools.svg") };
        root.Children.Add(header);
        var filter = Mo2QtWidgets.Filter("ToolsSearch", "Search tools", _ => Render(), out _search);
        var search = new Mo2ToolbarSearch(this, "ToolsToolbarSearch", filter, _search, "Search tools");
        var manage = Mo2ModRow.IconButton("mdi-pencil-outline", "Add or edit programs in MO2", async () => {
            if (ViewModel is not { } model) return;
            await model.Profile.RunTool(new Mo2Tool([], "Executable settings", "", "", true), model.Profile.CurrentTarget, true);
            await Refresh();
        });
        manage.Name = "ManageToolsButton";
        var refresh = Mo2ModRow.IconButton("mdi-refresh", "Refresh tools", async () => await Refresh());
        refresh.Name = "RefreshToolsButton";
        Grid.SetRow(_status,1); root.Children.Add(_status);
        var scroll = new ScrollViewer { Content = _rows, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll,2); root.Children.Add(scroll); Content = root;
        Mo2PanelChrome.Apply(this, root, header, Mo2ListToolbar.Pill("ToolsToolbarActions", manage, refresh), search);
        _status.IsVisible = false;
        _status.PropertyChanged += (_,args) => {
            if (args.Property == TextBlock.TextProperty) _status.IsVisible = !string.IsNullOrEmpty(_status.Text);
        };
        LayoutUpdated += (_,_) => {
            var compact = Bounds.Height < 350;
            if (compact == _compact) return;
            _compact = compact; Render();
        };
        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
            _active = true; ++_activation;
            try { _pins.Clear(); _pins.AddRange(Mo2ToolPins.Read()); }
            catch { _status.Text = "Saved tool pins could not be read."; }
            var wasConnected = model.Profile.IsConnected;
            void Changed() {
                manage.IsEnabled = model.Profile.CanChangeOriginalUi;
                var connectionChanged = wasConnected != model.Profile.IsConnected;
                wasConnected = model.Profile.IsConnected;
                if (_target != model.Profile.CurrentTarget || connectionChanged) _ = Refresh();
                else if (_selectedExecutable != model.Profile.SelectedExecutable || !_executableRows.SequenceEqual(ExecutableRows(model.Profile))) Render();
                else foreach (var button in _rows.Children.OfType<Border>().SelectMany(x => ((Grid)x.Child!).Children).OfType<Button>()) {
                    if (button.Name == "LaunchToolButton") button.IsEnabled = model.Profile.CanChangeOriginalUi && button.Tag is true;
                    else if (button.Name == "PinExecutableButton") button.IsEnabled = model.Profile.CanChangeOriginalUi;
                }
            }
            model.Profile.Changed += Changed;
            Disposable.Create(() => {
                _active = false; ++_activation; model.Profile.Changed -= Changed;
                manage.IsEnabled = false; Render();
            }).DisposeWith(d);
            void PinsChanged() { _pins.Clear(); _pins.AddRange(Mo2ToolPins.Read()); Render(); }
            Mo2ToolPins.Changed += PinsChanged;
            Disposable.Create(() => Mo2ToolPins.Changed -= PinsChanged).DisposeWith(d);
            _ = Refresh(); Changed();
        });
    }
    private async Task Refresh()
    {
        if (!_active || _loading || ViewModel is not { } model) return;
        var activation = _activation;
        _loading = true; var target = model.Profile.CurrentTarget; _target = target;
        var wasConnected = model.Profile.IsConnected;
        _tools = []; Render(); _status.Text = "Reading MO2 tools…";
        try {
            var tools = await (_read?.Invoke() ?? model.Profile.ReadTools());
            if (_active && activation == _activation && target == model.Profile.CurrentTarget) {
                _tools = tools; _status.Text = ""; Render();
            }
        } catch (Exception error) {
            if (_active && activation == _activation && target == model.Profile.CurrentTarget) _status.Text = error.Message;
        } finally {
            _loading = false;
            if (_active && (activation != _activation || target != model.Profile.CurrentTarget || wasConnected != model.Profile.IsConnected)) await Refresh();
        }
    }
    private void Render()
    {
        _rows.Children.Clear();
        if (!_active || ViewModel is not { } model) return;
        var profile = model.Profile; var target = profile.CurrentTarget;
        _executableRows = ExecutableRows(profile).ToArray();
        _selectedExecutable = profile.SelectedExecutable;
        if (target != _target || !profile.IsConnected) return;
        var all = profile.Executables.Select(name => (Key: "exe:" + name, Name: name, Description: "Launch with this profile’s mods", Enabled: true, Icon: profile.ExecutableIcons.GetValueOrDefault(name) ?? "", Run: (Func<Task>)(() => profile.CurrentTarget == target ? profile.Launch(name) : Task.CompletedTask)))
            .Concat(_tools.Select(tool => (Key: "tool:" + JsonSerializer.Serialize(tool.Id), Name: tool.Name, Description: tool.Description.Length > 0 ? tool.Description : tool.Group.Length > 0 ? tool.Group : "MO2 extension tool", Enabled: tool.Enabled, Icon: tool.Icon, Run: (Func<Task>)(() => profile.RunTool(tool, target))))).ToArray();
        string PinKey(string key) => target.Endpoint + "|" + key;
        // Whether a row counts as pinned: MO2's toolbar for an executable, this
        // page's own file for a tool plugin, which MO2 does not pin.
        bool Pinned(string key) => key.StartsWith("exe:", StringComparison.Ordinal)
            ? profile.PinnedExecutables.Contains(key[4..])
            : _pins.Contains(PinKey(key));
        var availableExtensionPins = all.Where(x => x.Key.StartsWith("tool:", StringComparison.Ordinal))
            .Select(x => PinKey(x.Key)).ToHashSet(StringComparer.Ordinal);
        int PreviousPin(string key) {
            for (var index = _pins.IndexOf(key) - 1; index >= 0; index--)
                if (availableExtensionPins.Contains(_pins[index])) return index;
            return -1;
        }
        var selected = "exe:" + profile.SelectedExecutable;
        var sections = new[] { "Default launcher", "Pinned tools", "Tools" };
        foreach (var section in sections) {
            var entries = all.Where(x => section == "Default launcher" ? x.Key == selected : section == "Pinned tools" ? x.Key != selected && Pinned(x.Key) : x.Key != selected && !Pinned(x.Key))
                .Where(x => (x.Name + " " + x.Description).Contains(_search.Text ?? "", StringComparison.OrdinalIgnoreCase));
            if (section == "Pinned tools") entries = entries.OrderBy(x => _pins.IndexOf(PinKey(x.Key)));
            var items = entries.ToArray(); if (items.Length == 0) continue;
            _rows.Children.Add(new TextBlock { Text = section, Opacity = .65, FontSize = Mo2Density.FontSize, Margin = new Thickness(0,_rows.Children.Count == 0 ? 0 : 6,0,2) });
            foreach (var entry in items) {
                // One MO2 list line and a little: this page lists the executables
                // MO2 keeps in a drop-down, and drew each of them as a 52px card.
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*,Auto,Auto,Auto"),
                    MinHeight = Mo2Density.Row + 4, Margin = new Thickness(6,1) };
                row.Children.Add(Mo2ToolIcons.Create(entry.Icon, _compact ? 18 : 20));
                var label = new TextBlock { Text = entry.Name, FontSize = Mo2Density.FontSize, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(6,0) };
                ToolTip.SetTip(label, entry.Name + "\n" + entry.Description); Grid.SetColumn(label,1); row.Children.Add(label);
                if (section == "Pinned tools" && entry.Key.StartsWith("tool:", StringComparison.Ordinal)) {
                    var up = new Button { Name = "MovePinnedToolEarlier", Content = "↑", FontSize = Mo2Density.FontSize, Padding = new Thickness(6,1), MinHeight = 0, Margin = new Thickness(1), IsEnabled = PreviousPin(PinKey(entry.Key)) >= 0 };
                    ToolTip.SetTip(up, "Move pinned tool earlier");
                    up.Click += (_,_) => {
                        var key = PinKey(entry.Key);
                        var index = _pins.IndexOf(key); var previous = PreviousPin(key);
                        if (index >= 0 && previous >= 0) {
                            (_pins[previous], _pins[index]) = (_pins[index], _pins[previous]); SavePins(); Render();
                        }
                    };
                    Grid.SetColumn(up,2); row.Children.Add(up);
                }
                // Pinning an executable is MO2's own Toolbar and Menu — MO2 keeps that
                // list, shows it on its toolbar and in its Run menu, and this page used
                // to keep a second one beside it. A tool plugin is still pinned here,
                // because MO2 pins no tool.
                var executable = entry.Key.StartsWith("exe:", StringComparison.Ordinal) ? entry.Key[4..] : null;
                var isPinned = Pinned(entry.Key);
                var pin = new Button { Name = executable is null ? "PinExtensionToolButton" : "PinExecutableButton", Content = isPinned ? "Unpin" : "Pin", FontSize = Mo2Density.FontSize, Padding = new Thickness(6,1), MinHeight = 0, Margin = new Thickness(1),
                    IsEnabled = executable is null || profile.CanChangeOriginalUi };
                if (executable is not null)
                    pin.Click += async (_,_) => { await profile.ToggleShortcut(executable, "Toolbar and Menu", target); Render(); };
                else
                    pin.Click += (_,_) => { if (!_pins.Remove(PinKey(entry.Key))) _pins.Add(PinKey(entry.Key)); SavePins(); Render(); };
                Grid.SetColumn(pin,3); row.Children.Add(pin);
                var launch = new Button { Content = "▶", Name = "LaunchToolButton", FontSize = Mo2Density.FontSize, Padding = new Thickness(6,1), MinHeight = 0, Tag = entry.Enabled, IsEnabled = entry.Enabled && profile.CanChangeOriginalUi, Margin = new Thickness(6,1,1,1) };
                ToolTip.SetTip(launch, "Open " + entry.Name); launch.Click += async (_,_) => await entry.Run();
                Grid.SetColumn(launch,4); row.Children.Add(launch);
                _rows.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#292C35")), CornerRadius = new CornerRadius(4), Child = row });
            }
        }
    }
    private void SavePins() { try { Directory.CreateDirectory(Path.GetDirectoryName(PinsPath)!); Mo2ToolPins.Save(_pins); } catch (Exception error) { _status.Text = "Could not save tool pins: " + error.Message; } }
}
