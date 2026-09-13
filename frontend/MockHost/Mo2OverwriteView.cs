using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using System.Reactive.Disposables;

namespace Mo2.Frontend;

internal sealed record Mo2OverwriteFile(string Path, long Bytes);
internal interface IMo2OverwritePage : IPageViewModelInterface { }
internal sealed class Mo2OverwritePage : APageViewModel<IMo2OverwritePage>, IMo2OverwritePage
{
    public Mo2LiveProfile Profile { get; }
    public Mo2OverwritePage(IWindowManager windows, Mo2LiveProfile profile) : base(windows)
    { Profile = profile; TabTitle = "Overwrite"; TabIcon = IconValues.Folder; }
}
internal sealed class Mo2OverwriteView : ReactiveUserControl<Mo2OverwritePage>
{
    private readonly TreeDataGrid _table = new() { Name = "OverwriteFiles", ShowColumnHeaders = true };
    private readonly TextBlock _status = new() { Name = "OverwriteStatus", TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _search = new() { Name = "OverwriteSearch", Watermark = "Filter generated files" };
    private readonly List<Button> _actions = [];
    private Mo2OverwriteFile[] _files = [];
    private Mo2ProfileTarget? _target;
    private bool _loading, _loaded;
    public Mo2OverwriteView()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), Margin = new Thickness(24) };
        root.Children.Add(new PageHeader { Title = "Overwrite", Icon = IconValues.Folder,
            Description = "Generated files that take priority over installed mods." });
        var text = new TextBlock { Text = "Move these files into a mod to organise them. Actions below apply to the entire Overwrite folder.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,12), Opacity = .75 };
        Grid.SetRow(text,1); root.Children.Add(text);
        var bar = new WrapPanel();
        var inspect = new Button { Content = "Open files…", Name = "OpenOverwriteFiles" };
        inspect.Click += async (_,_) => { if (ViewModel is { } model && _target == model.Profile.CurrentTarget && model.Profile.Mods.SingleOrDefault(x => x.IsOverwrite) is { } mod) { await model.Profile.ShowModDetails(mod.Id); await Refresh(); } };
        bar.Children.Add(inspect); _actions.Add(inspect);
        foreach (var (label, operation) in new[] { ("Create mod…","create"), ("Move to mod…","move"), ("Sync to mods…","sync"), ("Clear…","clear") }) {
            var button = new Button { Content = label, Name = "Overwrite_" + operation, Tag = operation };
            button.Click += async (_,_) => { if (ViewModel is not { } model || _target is not { } target) return; await model.Profile.OverwriteAction(operation,target); await Refresh(); };
            bar.Children.Add(button); _actions.Add(button);
        }
        var refresh = new Button { Content = "Refresh", Name = "RefreshOverwrite" }; refresh.Click += async (_,_) => await Refresh(); bar.Children.Add(refresh);
        foreach (var button in bar.Children) button.Margin = new Thickness(0,0,8,8);
        Grid.SetRow(bar,2); root.Children.Add(bar);
        var filter = new StackPanel { Spacing = 8, Margin = new Thickness(0,4,0,8) }; filter.Children.Add(_search); filter.Children.Add(_status);
        Grid.SetRow(filter,3); root.Children.Add(filter); Grid.SetRow(_table,4); root.Children.Add(_table);
        _table.Classes.Add("MainListsStyling"); Content = root;
        _search.TextChanged += (_,_) => Render();
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
        if (_loading || ViewModel is not { } model) return;
        _loading = true; _loaded = false; _target = model.Profile.CurrentTarget; var target = _target;
        _files = []; Render(); _status.Text = "Reading MO2 Overwrite…"; UpdateActions();
        try { var files = await model.Profile.ReadOverwrite(target!.Value); if (target == model.Profile.CurrentTarget) { _files = files; _loaded = true; Render(); } }
        catch (Exception error) { _status.Text = error.Message; }
        finally { _loading = false; UpdateActions(); if (target != model.Profile.CurrentTarget) await Refresh(); }
    }
    private void UpdateActions()
    {
        foreach (var button in _actions) button.IsEnabled = _loaded && !_loading && ViewModel?.Profile.CanChangeOriginalUi == true && (button.Tag is null || _files.Length > 0);
    }
    private void Render()
    {
        var visible = _files.Where(x => x.Path.Contains(_search.Text ?? "",StringComparison.OrdinalIgnoreCase)).ToArray();
        var source = new FlatTreeDataGridSource<Mo2OverwriteFile>(visible);
        source.Columns.Add(new TextColumn<Mo2OverwriteFile,string>("File",x => x.Path,width:new GridLength(1,GridUnitType.Star)));
        source.Columns.Add(new TextColumn<Mo2OverwriteFile,string>("Size",x => $"{x.Bytes / 1024d:0.#} KB",width:new GridLength(85)));
        _table.Source = source;
        _status.Text = !_loaded ? "" : _files.Length == 0 ? "Overwrite is empty." : $"{visible.Length} of {_files.Length} files · Stored by MO2 for this game instance.";
    }
}
