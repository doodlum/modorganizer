using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using System.Reactive.Disposables;

namespace Mo2.Frontend;

internal sealed record Mo2Archive(string Name,string Mod,bool Active,bool CanToggle);
internal interface IMo2ArchivesPage : IPageViewModelInterface { }
internal sealed class Mo2ArchivesPage : APageViewModel<IMo2ArchivesPage>, IMo2ArchivesPage
{
    public static readonly IconValue ArchiveIcon = new ProjektankerIcon("mdi-archive-outline");
    public Mo2LiveProfile Profile { get; }
    public Mo2ArchivesPage(IWindowManager windows,Mo2LiveProfile profile) : base(windows)
    { Profile = profile; TabTitle = "Archives"; TabIcon = ArchiveIcon; }
}
internal sealed class Mo2ArchivesView : ReactiveUserControl<Mo2ArchivesPage>
{
    private readonly TreeDataGrid _table = new() { Name = "ArchivesTable", ShowColumnHeaders = true };
    private readonly TextBox _filter = new() { Name = "ArchivesFilter", Watermark = "Filter archives", MinWidth = 120 };
    private readonly TextBlock _status = new() { Name = "ArchivesStatus", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _browse = new() { Content = "Browse…", Name = "BrowseArchive", IsEnabled = false };
    private Mo2Archive[] _archives = [];
    private Mo2ProfileTarget? _target;
    private bool _reading;
    public Mo2ArchivesView()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(24) };
        root.Children.Add(new PageHeader { Title = "Archives", Description = "Browse BSA and BA2 archives in this profile.", Icon = IconValues.PictogramLibrary });
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0,12,0,8) };
        bar.Children.Add(_filter); Grid.SetColumn(_browse,1); _browse.Margin = new Thickness(8,0); bar.Children.Add(_browse);
        var refresh = new Button { Content = "Refresh" }; Grid.SetColumn(refresh,2); bar.Children.Add(refresh);
        Grid.SetRow(bar,1); root.Children.Add(bar); Grid.SetRow(_status,2); root.Children.Add(_status); Grid.SetRow(_table,3); root.Children.Add(_table);
        _table.Classes.Add("MainListsStyling"); Content = root;
        _filter.TextChanged += (_,_) => Render(); refresh.Click += async (_,_) => await Refresh();
        _browse.Click += async (_,_) => {
            if (ViewModel is not { } model || _target is not { } target || _table.RowSelection?.SelectedItem is not Mo2Archive archive) return;
            await model.Profile.BrowseArchive(archive.Name,target); await Refresh();
        };
        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
            void Changed() { if (_target != model.Profile.CurrentTarget || !model.Profile.IsConnected) _ = Refresh(); UpdateActions(); }
            model.Profile.Changed += Changed;
            Disposable.Create(() => model.Profile.Changed -= Changed).DisposeWith(d);
            _ = Refresh();
        });
    }
    internal async Task Refresh()
    {
        if (_reading || ViewModel is not { } model) return;
        _reading = true; _target = model.Profile.CurrentTarget; var target = _target.Value;
        _archives = []; Render(); _status.Text = "Reading MO2 archives…";
        try {
            var archives = await model.Profile.ReadArchives(target);
            if (target != model.Profile.CurrentTarget) return;
            _archives = archives; Render();
        } catch (Exception error) { _status.Text = error.Message; }
        finally { _reading = false; UpdateActions(); if (target != model.Profile.CurrentTarget) await Refresh(); }
    }
    private void UpdateActions() => _browse.IsEnabled = !_reading && ViewModel?.Profile.CanChangeOriginalUi == true && _target == ViewModel.Profile.CurrentTarget && _table.RowSelection?.SelectedItem is Mo2Archive;
    private void Render()
    {
        var rows = _archives.Where(x => (x.Name + " " + x.Mod).Contains(_filter.Text ?? "",StringComparison.OrdinalIgnoreCase)).ToArray();
        var source = new FlatTreeDataGridSource<Mo2Archive>(rows);
        source.Columns.Add(new TextColumn<Mo2Archive,string>("Archive",x => x.Name,width:new GridLength(1,GridUnitType.Star)));
        source.Columns.Add(new TextColumn<Mo2Archive,string>("Mod",x => x.Mod,width:new GridLength(130)));
        source.Columns.Add(new TextColumn<Mo2Archive,string>("Loaded",x => x.Active ? "Yes" : "No",width:new GridLength(70)));
        source.RowSelection!.SelectionChanged += (_,_) => UpdateActions(); _table.Source = source;
        _status.Text = _archives.Length == 0 ? "No archives reported by MO2." : $"{rows.Length} of {_archives.Length} archives · Loading follows the game’s archive and plugin rules.";
        UpdateActions();
    }
}
