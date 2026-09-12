using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using NexusMods.App.UI.Notifications;
using NexusMods.App.UI.Pages;
using NexusMods.App.UI.Pages.LibraryPage;
using NexusMods.App.UI.Pages.LibraryPage.Collections;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ObservableCollections;
using R3;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class ScenarioLibraryPage : APageViewModel<ILibraryViewModel>, ILibraryViewModel
{
    public LibraryTreeDataGridAdapter Adapter { get; }
    public ReadOnlyObservableCollection<ICollectionCardViewModel> Collections { get; } = new(new());
    public ReadOnlyObservableCollection<InstallationTarget> InstallationTargets { get; } = new(new ObservableCollection<InstallationTarget> { new(default, "My Mods") });
    private InstallationTarget? _target;
    public InstallationTarget? SelectedInstallationTarget { get => _target; set => this.RaiseAndSetIfChanged(ref _target, value); }
    public string EmptyLibrarySubtitleText => "Add mods to your library to install them.";
    public R3.ReactiveCommand<R3.Unit> UpdateAllCommand { get; } = new();
    public R3.ReactiveCommand<R3.Unit> RefreshUpdatesCommand { get; } = new(_ => new WindowNotificationService().ShowToast("No updates available"));
    public R3.ReactiveCommand<R3.Unit> InstallSelectedItemsCommand { get; }
    public R3.ReactiveCommand<R3.Unit> InstallSelectedItemsWithAdvancedInstallerCommand { get; } = new();
    public R3.ReactiveCommand<R3.Unit> UpdateSelectedItemsCommand { get; } = new();
    public R3.ReactiveCommand<R3.Unit> UpdateAndKeepOldSelectedItemsCommand { get; } = new();
    public R3.ReactiveCommand<R3.Unit> RemoveSelectedItemsCommand { get; }
    public R3.ReactiveCommand<R3.Unit> DeselectItemsCommand { get; }
    private int _selectionCount;
    public int SelectionCount { get => _selectionCount; private set => this.RaiseAndSetIfChanged(ref _selectionCount, value); }
    public int UpdatableSelectionCount => 0;
    public bool HasAnyUpdatesAvailable => false;
    public bool IsUpdatingAll => false;
    public R3.ReactiveCommand<R3.Unit> OpenFilePickerCommand { get; }
    public R3.ReactiveCommand<R3.Unit> OpenNexusModsCommand { get; } = new(_ => new WindowNotificationService().ShowToast("Browse mods scenario"));
    public R3.ReactiveCommand<R3.Unit> OpenNexusModsCollectionsCommand { get; } = new(_ => new WindowNotificationService().ShowToast("Browse collections scenario"));
    public IStorageProvider? StorageProvider { get; set; }
    public ScenarioLibraryPage(IServiceProvider services, IWindowManager windows, ScenarioLibrary library, ScenarioInstalledMods installed) : base(windows)
    {
        TabTitle = "Library"; TabIcon = IconValues.LibraryOutline;
        SelectedInstallationTarget = InstallationTargets[0];
        var local = new FixtureServices(services);
        local.Add<IEnumerable<ILibraryDataProvider>>([new ScenarioLibraryProvider(library, installed)]);
        Adapter = new LibraryTreeDataGridAdapter(local, new LibraryFilter(default, null!));
        InstallSelectedItemsCommand = new(_ => library.Install(Adapter.SelectedModels.Select(x => x.Key), installed));
        RemoveSelectedItemsCommand = new(_ => { library.Remove(Adapter.SelectedModels.Select(x => x.Key)); Adapter.ClearSelection(); });
        DeselectItemsCommand = new(_ => Adapter.ClearSelection());
        OpenFilePickerCommand = new(async (_, token) => {
            if (StorageProvider is null) return;
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Add mods", AllowMultiple = true });
            // This frontend scenario imports metadata only, never file content.
            foreach (var file in files) library.Add(Path.GetFileNameWithoutExtension(file.Name));
        });
        this.WhenActivated(disposables => {
            Adapter.Activate().AddTo(disposables);
            Adapter.SelectedModels.ObserveCountChanged(notifyCurrentCount: true).Subscribe(count => SelectionCount = count).AddTo(disposables);
            Adapter.MessageSubject.Subscribe(message => message.Switch(
                install => library.Install(install.Ids.Select(x => x.Value), installed),
                replace => { }, keep => { }, changelog => { }, modPage => { }, hide => { },
                delete => library.Remove(delete.Ids.Select(x => x.Value)))).AddTo(disposables);
        });
    }
}
