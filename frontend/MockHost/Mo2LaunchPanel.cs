using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NexusMods.Abstractions.Loadouts;
using NexusMods.App.UI.LeftMenu.Items;
using NexusMods.Sdk.Jobs;
using NexusMods.UI.Sdk;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class Mo2LaunchButton : AViewModel<ILaunchButtonViewModel>, ILaunchButtonViewModel
{
    private readonly Mo2LiveProfile _profile;
    private readonly BehaviorSubject<bool> _enabled = new(false);
    private readonly BehaviorSubject<bool> _running = new(false);
    private string _endpoint = "";
    private string _selected = "";
    public string SelectedExecutable { get => _selected; set { this.RaiseAndSetIfChanged(ref _selected, value); UpdateAvailability(); } }
    public bool IsBusy => _profile.Launching || _profile.Installing || _profile.SelectingProfile || _profile.ManagingMod;
    public bool CanLaunch => !IsBusy && _profile.IsConnected && _profile.ProfilePath.Length > 0 && _profile.Executables.Contains(SelectedExecutable);
    public LoadoutId LoadoutId { get; set; }
    public ReactiveCommand<Unit, Unit> Command { get; set; }
    public IObservable<bool> IsRunningObservable => _running.DistinctUntilChanged();
    public string Label => _profile.Launching ? "RUNNING" : "PLAY";
    public Percent? Progress => null;
    public Mo2LaunchButton(Mo2LiveProfile profile)
    {
        _profile = profile;
        Command = ReactiveCommand.CreateFromTask(() => profile.Launch(SelectedExecutable), _enabled.DistinctUntilChanged());
        profile.Changed += Refresh;
        Refresh();
    }
    private void UpdateAvailability() => _enabled.OnNext(CanLaunch);
    private void Refresh()
    {
        if (_endpoint != _profile.Endpoint || !_profile.Executables.Contains(SelectedExecutable))
            SelectedExecutable = _profile.Executables.Contains(_profile.SelectedExecutable) ? _profile.SelectedExecutable : _profile.Executables.FirstOrDefault() ?? "";
        _endpoint = _profile.Endpoint;
        UpdateAvailability();
        _running.OnNext(_profile.Launching);
        this.RaisePropertyChanged(nameof(Label));
    }
}

// Native NMA launch button, placed where its Apply/Play control normally lives.
// MO2's virtual file system requires no separate Apply action.
internal sealed class Mo2LaunchPanel : Border
{
    // MO2's own first row of executablesListBox, under MO2's own wording
    // (MainWindow::refreshExecutablesList). Choosing it opens MO2's Edit Executables
    // dialog and puts the previous choice back, which is what
    // on_executablesListBox_currentIndexChanged does. The frontend could reach that
    // dialog from Tools and from nowhere else; MO2 puts it here, where the executable
    // about to be run is chosen.
    internal const string EditEntry = "<Edit...>";
    public Mo2LaunchButton Model { get; }
    public ComboBox Executable { get; } = new() { Name = "ExecutablesListBox", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
    public Button? ShortcutMenu { get; private set; }
    public LaunchButtonView NativeButton { get; }
    public Mo2LaunchPanel(Mo2LiveProfile profile)
    {
        Model = new(profile);
        NativeButton = new LaunchButtonView { ViewModel = Model };
        Background = (IBrush)Application.Current!.FindResource("SurfaceLowBrush")!;
        CornerRadius = new CornerRadius(8); Padding = new Thickness(12);
        var contents = new StackPanel { Spacing = 8 };
        var pins = new WrapPanel { Name = "PinnedToolShortcuts" };
        contents.Children.Add(pins);
        // MO2's linkButton, beside the box the executable is chosen in. Its three
        // entries are MO2's own, and each is worded by what MO2 says it would do —
        // MO2 draws a remove icon where the shortcut is already there and an add icon
        // where it is not, which is the same answer in words.
        var link = new Button { Name = "ShortcutMenuButton", Content = new TextBlock { Text = "Shortcut…" },
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, Padding = new Thickness(6, 2) };
        ToolTip.SetTip(link, "Add or remove a shortcut to the chosen executable");
        var linkMenu = new MenuFlyout { Placement = Avalonia.Controls.PlacementMode.Top };
        link.Flyout = linkMenu;
        // Read when it is opened, not held: MO2 is the only thing that knows whether
        // a shortcut is still there, and it can be removed outside this window.
        linkMenu.Opening += async (_, _) => {
            linkMenu.Items.Clear();
            var chosen = Model.SelectedExecutable;
            var target = profile.CurrentTarget;
            linkMenu.Items.Add(new MenuItem { Header = "Asking MO2…", IsEnabled = false });
            var entries = await profile.ReadShortcuts(chosen);
            if (profile.CurrentTarget != target) return;
            linkMenu.Items.Clear();
            if (entries.Length == 0) {
                linkMenu.Items.Add(new MenuItem { Header = "MO2 offers no shortcut for this executable", IsEnabled = false });
                return;
            }
            foreach (var entry in entries) {
                var item = new MenuItem {
                    Header = (entry.Exists ? "Remove from " : "Add to ") + entry.Text,
                    IsEnabled = entry.Enabled && profile.CanChangeOriginalUi,
                };
                var name = entry.Text;
                item.Click += async (_, _) => await profile.ToggleShortcut(chosen, name, target);
                linkMenu.Items.Add(item);
            }
        };
        var runRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 0) };
        runRow.Children.Add(Executable);
        link.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(link, 1); runRow.Children.Add(link);
        contents.Children.Add(runRow); contents.Children.Add(NativeButton); Child = contents;
        ShortcutMenu = link;
        ToolTip.SetTip(Executable, "Choose an executable configured in MO2");
        Avalonia.Automation.AutomationProperties.SetName(Executable, "Executable");
        var updating = false;
        string pinsKey = "";
        void RefreshPins() {
            string[] saved;
            try { saved = Mo2ToolPins.Read().Where(x => x.StartsWith(profile.Endpoint + "|",StringComparison.Ordinal)).ToArray(); }
            catch { saved = []; }
            var key = string.Join("|", saved) + profile.Endpoint + string.Join("|",profile.ExecutableIcons.Values) + string.Join("|",profile.Tools.Select(x => x.Icon));
            if (key != pinsKey) {
                pinsKey = key; pins.Children.Clear();
                var target = profile.CurrentTarget;
                foreach (var pin in saved) {
                    var value = pin[(profile.Endpoint.Length + 1)..];
                    var executable = value.StartsWith("exe:") ? value[4..] : null;
                    string[] id;
                    try { id = executable is null ? System.Text.Json.JsonSerializer.Deserialize<string[]>(value[5..]) ?? [] : []; } catch { continue; }
                    var tool = profile.Tools.FirstOrDefault(x => x.Id.SequenceEqual(id));
                    var name = executable ?? tool?.Name ?? id.LastOrDefault() ?? "Tool";
                    var icon = executable is not null ? profile.ExecutableIcons.GetValueOrDefault(executable) ?? "" : tool?.Icon ?? "";
                    var button = new Button { Name = "PinnedToolShortcut", Content = Mo2ToolIcons.Create(icon,28), Padding = new Thickness(3), Margin = new Thickness(0,0,4,4) };
                    ToolTip.SetTip(button,name);
                    button.Click += async (_,_) => {
                        if (profile.CurrentTarget.Endpoint != target.Endpoint) return;
                        if (executable is not null) await profile.Launch(executable);
                        else await profile.RunTool(tool ?? new Mo2Tool(id,name,"","",true),profile.CurrentTarget);
                    };
                    pins.Children.Add(button);
                }
            }
            pins.IsVisible = pins.Children.Count > 0;
            foreach (var button in pins.Children.OfType<Button>()) button.IsEnabled = profile.CanChangeOriginalUi;
        }
        AttachedToVisualTree += (_,_) => { Mo2ToolPins.Changed -= RefreshPins; Mo2ToolPins.Changed += RefreshPins; RefreshPins(); };
        DetachedFromVisualTree += (_,_) => Mo2ToolPins.Changed -= RefreshPins;
        Executable.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((name,_) => {
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(Mo2ToolIcons.Create(profile.ExecutableIcons.GetValueOrDefault(name ?? "") ?? "",22));
            row.Children.Add(new TextBlock { Text = name, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            return row;
        });
        Executable.SelectionChanged += async (_, _) => {
            if (updating) return;
            if (Executable.SelectedItem as string == EditEntry) {
                // Put the previous choice back before handing over, not after: MO2's
                // dialog is modal and this call waits on it, and a combo left reading
                // "<Edit...>" for as long as that dialog is open is not what MO2 shows.
                var previous = Model.SelectedExecutable;
                updating = true;
                try { Executable.SelectedItem = previous; } finally { updating = false; }
                if (!profile.CanChangeOriginalUi) return;
                await profile.RunTool(new Mo2Tool([], "Executable settings", "", "", true), profile.CurrentTarget, true);
                await profile.Refresh();
                return;
            }
            Model.SelectedExecutable = Executable.SelectedItem as string ?? "";
        };
        void Refresh()
        {
            RefreshPins();
            updating = true;
            try {
                // MO2's own order: <Edit...> first, then the executables it is not
                // hiding.
                var wanted = new[] { EditEntry }.Concat(profile.Executables).ToArray();
                if (!(Executable.ItemsSource as IEnumerable<string> ?? []).SequenceEqual(wanted)) Executable.ItemsSource = wanted;
                Executable.SelectedItem = Model.SelectedExecutable;
                Executable.IsEnabled = !Model.IsBusy;
                // MO2 greys its linkButton out with the rest of the pane, and it has
                // nothing to make a shortcut to until an executable is chosen.
                link.IsEnabled = profile.CanChangeOriginalUi && Model.SelectedExecutable.Length > 0;
                NativeButton.IsEnabled = Model.CanLaunch;
            } finally { updating = false; }
        }
        Executable.SelectionChanged += (_, _) => NativeButton.IsEnabled = Model.CanLaunch;
        profile.Changed += Refresh;
        Refresh();
    }
}
