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
            SelectedExecutable = _profile.Executables.FirstOrDefault() ?? "";
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
    public Mo2LaunchButton Model { get; }
    public ComboBox Executable { get; } = new() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
    public LaunchButtonView NativeButton { get; }
    public Mo2LaunchPanel(Mo2LiveProfile profile)
    {
        Model = new(profile);
        NativeButton = new LaunchButtonView { ViewModel = Model };
        Background = (IBrush)Application.Current!.FindResource("SurfaceLowBrush")!;
        CornerRadius = new CornerRadius(8); Padding = new Thickness(12);
        var contents = new StackPanel { Spacing = 8 };
        contents.Children.Add(Executable); contents.Children.Add(NativeButton); Child = contents;
        ToolTip.SetTip(Executable, "Choose an executable configured in MO2");
        Avalonia.Automation.AutomationProperties.SetName(Executable, "Executable");
        var updating = false;
        Executable.SelectionChanged += (_, _) => { if (!updating) Model.SelectedExecutable = Executable.SelectedItem as string ?? ""; };
        void Refresh()
        {
            updating = true;
            try {
                if (!(Executable.ItemsSource as IEnumerable<string> ?? []).SequenceEqual(profile.Executables)) Executable.ItemsSource = profile.Executables;
                Executable.SelectedItem = Model.SelectedExecutable;
                Executable.IsEnabled = !Model.IsBusy;
                NativeButton.IsEnabled = Model.CanLaunch;
            } finally { updating = false; }
        }
        Executable.SelectionChanged += (_, _) => NativeButton.IsEnabled = Model.CanLaunch;
        profile.Changed += Refresh;
        Refresh();
    }
}
