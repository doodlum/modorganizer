using System.Collections.ObjectModel;
using NexusMods.Abstractions.NexusModsLibrary.Models;
using NexusMods.Abstractions.NexusWebApi.Types;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.Notifications;
using NexusMods.App.UI.Pages;
using NexusMods.App.UI.Pages.LoadoutPage;
using NexusMods.App.UI.Pages.Sorting;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk;
using NexusMods.UI.Sdk.Icons;
using ObservableCollections;
using R3;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class ScenarioRules : AViewModel<ISortingSelectionViewModel>, ISortingSelectionViewModel
{
    public ReadOnlyObservableCollection<IViewModelInterface> RulesViewModels { get; }
    public IReadOnlyBindableReactiveProperty<bool> CanEdit { get; } = new BindableReactiveProperty<bool>(true);
    public R3.ReactiveCommand<NavigationInformation> OpenAllModsLoadoutPageCommand { get; } = new();
    public ScenarioRules(IServiceProvider services, NexusMods.Abstractions.Games.ISortOrderVariety order)
        => RulesViewModels = new(new ObservableCollection<IViewModelInterface> { new LoadOrderViewModel(services, order, default) });
}

internal sealed class ScenarioInstalledPage : APageViewModel<ILoadoutViewModel>, ILoadoutViewModel
{
    public bool IsMo2Profile { get; }
    public Mo2LiveProfile? LiveProfile { get; }
    public string EmptyStateTitleText => "No mods installed";
    public LoadoutTreeDataGridAdapter Adapter { get; }
    private readonly BindableReactiveProperty<int> _count = new();
    private readonly BindableReactiveProperty<int> _selected = new();
    public IReadOnlyBindableReactiveProperty<int> ItemCount => _count;
    public IReadOnlyBindableReactiveProperty<int> SelectionCount => _selected;
    public LoadoutPageSubTabs SelectedSubTab { get; }
    public bool HasRulesSection => true;
    public ISortingSelectionViewModel RulesSectionViewModel { get; }
    public bool IsCollection { get; }
    public bool EnableCollectionSharing => false;
    public IReadOnlyBindableReactiveProperty<bool> IsCollectionUploaded { get; } = new BindableReactiveProperty<bool>(false);
    public IReadOnlyBindableReactiveProperty<string> CollectionName { get; }
    public IReadOnlyBindableReactiveProperty<CollectionStatus> CollectionStatus { get; } = new BindableReactiveProperty<CollectionStatus>(NexusMods.Abstractions.NexusModsLibrary.Models.CollectionStatus.Unlisted);
    public IReadOnlyBindableReactiveProperty<RevisionStatus> RevisionStatus { get; } = new BindableReactiveProperty<RevisionStatus>();
    public IReadOnlyBindableReactiveProperty<RevisionNumber> RevisionNumber { get; } = new BindableReactiveProperty<RevisionNumber>();
    public IReadOnlyBindableReactiveProperty<DateTimeOffset> LastUploadedDate { get; } = new BindableReactiveProperty<DateTimeOffset>();
    public IReadOnlyBindableReactiveProperty<bool> HasOutstandingChanges { get; } = new BindableReactiveProperty<bool>(false);
    public R3.ReactiveCommand<NavigationInformation> CommandOpenLibraryPage { get; } = new();
    public R3.ReactiveCommand<NavigationInformation> CommandOpenFilesPage { get; } = new();
    public R3.ReactiveCommand<R3.Unit> CommandRemoveItem { get; }
    public R3.ReactiveCommand<R3.Unit> CommandDeselectItems { get; }
    public R3.ReactiveCommand<R3.Unit> CommandRenameGroup { get; }
    public R3.ReactiveCommand<R3.Unit> CommandShareCollection { get; } = new();
    public R3.ReactiveCommand<R3.Unit> CommandUploadDraftRevision { get; } = new();
    public R3.ReactiveCommand<R3.Unit> CommandUploadAndPublishRevision { get; } = new();
    public R3.ReactiveCommand<R3.Unit> CommandOpenRevisionUrl { get; } = new();
    public R3.ReactiveCommand<R3.Unit> CommandCopyRevisionUrl { get; } = new();
    public R3.ReactiveCommand<R3.Unit> CommandChangeVisibility { get; } = new();
    public R3.ReactiveCommand<R3.Unit> CommandDeleteGroup { get; } = new();

    public ScenarioInstalledPage(IServiceProvider services, IWindowManager windows, IInstalledModsSource mods, NexusMods.Abstractions.Games.ISortOrderVariety order,
        LoadoutPageSubTabs selected = LoadoutPageSubTabs.Mods, bool isCollection = false) : base(windows)
    {
        LiveProfile = mods as Mo2LiveProfile;
        IsMo2Profile = LiveProfile is not null;
        CollectionName = mods.CollectionName;
        CommandRenameGroup = new(async (_, token) => {
            if (mods is not ScenarioInstalledMods fixtureMods) return;
            var result = await windows.ShowDialog(LoadoutDialogs.RenameCollection(mods.CollectionName.Value), NexusMods.App.UI.Dialog.Enums.DialogWindowType.Modal);
            if (result.ButtonId == NexusMods.UI.Sdk.Dialog.ButtonDefinitionId.Accept && !string.IsNullOrWhiteSpace(result.InputText))
                fixtureMods.Rename(result.InputText.Trim());
        });
        IsCollection = isCollection;
        TabTitle = isCollection ? "My Mods" : "All"; TabIcon = isCollection ? IconValues.CollectionsOutline : IconValues.FormatAlignJustify;
        SelectedSubTab = selected;
        RulesSectionViewModel = new ScenarioRules(services, order);
        var filter = new LoadoutFilter { LoadoutId = default, CollectionGroupId = default };
        Adapter = IsMo2Profile ? new Mo2ModsAdapter(services) : new LoadoutTreeDataGridAdapter(services, filter);
        CommandDeselectItems = new(_ => Adapter.ClearSelection());
        CommandRemoveItem = new(_ => {
            mods.Remove(Adapter.SelectedModels.Select(x => NexusMods.Abstractions.Loadouts.LoadoutItemId.From(x.Key)).ToArray());
            Adapter.ClearSelection();
        });
        this.WhenActivated(disposables => {
            Adapter.Activate().AddTo(disposables);
            if (IsCollection) mods.CollectionName.Subscribe(name => TabTitle = name).AddTo(disposables);
            mods.CountLoadoutItems(filter).ToObservable().Subscribe(count => _count.Value = count).AddTo(disposables);
            Adapter.SelectedModels.ObserveCountChanged(notifyCurrentCount: true).Subscribe(count => _selected.Value = count).AddTo(disposables);
            Adapter.MessageSubject.Subscribe(message => message.Switch(
                toggle => mods.Toggle(toggle.Ids),
                collection => new WindowNotificationService().ShowToast("Collection: My Mods"),
                mod => new WindowNotificationService().ShowToast("Mod page scenario"),
                files => new WindowNotificationService().ShowToast("Mod files scenario"),
                remove => mods.Remove(remove.Ids))).AddTo(disposables);
        });
    }
}
