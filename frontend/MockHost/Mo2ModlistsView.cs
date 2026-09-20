using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;

namespace Mo2.Frontend;

// The Wabbajack browser.
//
// One row per modlist, because the thing being chosen is a whole setup rather than
// a mod: the name, who made it, which game it is for, how much of the disk it will
// take and what it is. The filter above defaults to the games found on this PC,
// which is the only set a person can act on today.
internal sealed class Mo2ModlistsView : ReactiveUserControl<Mo2ModlistsPage>
{
    private readonly ComboBox _game = new() { Name = "Mo2ModlistGame", MinWidth = 220 };
    private readonly CheckBox _installable = new() { Name = "Mo2ModlistInstallable", Content = "Only lists Mod Organizer can manage", IsChecked = true };
    private readonly TextBlock _status = new() { Name = "Mo2ModlistStatus", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly StackPanel _rows = new() { Name = "Mo2ModlistRows", Spacing = 8 };
    private readonly TextBox _filter;
    private bool _updating;

    public Mo2ModlistsView()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(16) };
        var header = new PageHeader {
            Title = "Modlists",
            Description = "Wabbajack modlists, installed here as their own Mod Organizer instance.",
            Icon = Mo2ModlistsPage.ModlistsIcon,
        };
        root.Children.Add(header);

        var bar = new WrapPanel { Margin = new Thickness(0, 12, 0, 8) };
        _game.Height = Mo2TableRow.ActionSize; _game.MinHeight = 0; _game.FontSize = Mo2Density.FontSize;
        ToolTip.SetTip(_game, "Which game's modlists to show");
        bar.Children.Add(_game);
        bar.Children.Add(_installable);
        foreach (var child in bar.Children) child.Margin = new Thickness(0, 0, 8, 6);
        _installable.VerticalAlignment = VerticalAlignment.Center;

        var field = Mo2QtWidgets.Filter("Mo2ModlistFilter", "Search modlists", _ => Push(), out _filter);
        var search = new Mo2ToolbarSearch(this, "ModlistsToolbarSearch", field, _filter, "Search modlists");
        var refresh = Mo2TableRow.IconButton("mdi-refresh", "Refresh the Wabbajack gallery",
            async () => { if (ViewModel is { } model) await model.Load(refresh: true); });

        Grid.SetRow(bar, 1); root.Children.Add(bar);
        Grid.SetRow(_status, 2); root.Children.Add(_status);
        var scroll = new ScrollViewer { Content = _rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 3); root.Children.Add(scroll);
        Content = root;
        Mo2PanelChrome.Apply(this, root, header, Mo2ListToolbar.Pill("ModlistsToolbarActions", refresh), search);

        _game.SelectionChanged += (_, _) => { if (!_updating && ViewModel is { } model && _game.SelectedItem is string choice) model.Game = choice; };
        _installable.IsCheckedChanged += (_, _) => { if (!_updating && ViewModel is { } model) model.OnlyInstallable = _installable.IsChecked == true; };

        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
            void Sync() {
                _updating = true;
                if (!ReferenceEquals(_game.ItemsSource, model.Games)) _game.ItemsSource = model.Games;
                if (_game.SelectedItem as string != model.Game) _game.SelectedItem = model.Game;
                if (_installable.IsChecked != model.OnlyInstallable) _installable.IsChecked = model.OnlyInstallable;
                _status.Text = model.Status;
                _updating = false;
                Render();
            }
            Sync();
            model.Visible.CollectionChanged += (_, _) => Render();
            model.WhenAnyValue(x => x.Status, x => x.Game, x => x.Busy).Subscribe(_ => Sync()).DisposeWith(d);
        });
    }

    private void Push()
    {
        if (ViewModel is { } model) model.Search = _filter.Text ?? "";
    }

    private void Render()
    {
        if (ViewModel is not { } model) return;
        _rows.Children.Clear();
        foreach (var list in model.Visible) _rows.Children.Add(Row(model, list));
    }

    private static object Resource(string key) => Application.Current!.FindResource(key)!;

    private Control Row(Mo2ModlistsPage model, Mo2Modlist list)
    {
        var art = new Image { Width = 72, Height = 72, Stretch = Stretch.UniformToFill };
        if (list.Game is { } game) art.Source = Mo2GameArt.SquareIcon(game);
        _ = Mo2ModlistArt.Load(list, bitmap => { if (bitmap is not null) art.Source = bitmap; });

        var title = new TextBlock { Text = list.Title, Theme = (ControlTheme)Resource("HeadingXSSemiTheme"), TextTrimming = TextTrimming.CharacterEllipsis };
        var byline = new TextBlock {
            Text = $"{list.Author} · {list.Game ?? list.WabbajackGame}" +
                   (list.Version is { Length: > 0 } ? $" · v{list.Version}" : "") +
                   (list.TotalSize > 0 ? $" · {list.TotalSize / 1024d / 1024 / 1024:0.0} GB installed" : ""),
            Opacity = .7, FontSize = Mo2Density.FontSize, TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var about = new TextBlock {
            Text = list.Description, Opacity = .85, FontSize = Mo2Density.FontSize,
            TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 4, 0, 0),
        };

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(title); text.Children.Add(byline); text.Children.Add(about);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (list.ReadmeUrl is { Length: > 0 })
            actions.Children.Add(new StandardButton {
                Name = "ModlistReadmeButton", Text = "Read more", Type = StandardButton.Types.Tertiary,
                Command = ReactiveCommand.Create(() => model.OpenReadme(list)),
            });
        var installed = model.Installed(list);
        actions.Children.Add(new StandardButton {
            Name = "ModlistInstallButton",
            Text = installed ? "Installed" : "Install",
            Type = installed ? StandardButton.Types.Tertiary : StandardButton.Types.Primary,
            Command = ReactiveCommand.CreateFromTask(() => model.Install(list),
                canExecute: System.Reactive.Linq.Observable.Return(!installed && list.Supported)),
        });

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(12) };
        Grid.SetColumn(art, 0); art.Margin = new Thickness(0, 0, 12, 0); grid.Children.Add(art);
        Grid.SetColumn(text, 1); grid.Children.Add(text);
        Grid.SetColumn(actions, 2); grid.Children.Add(actions);

        return new Border {
            Name = "Mo2ModlistRow",
            Child = grid,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = (IBrush)Resource("StrokeTranslucentSubduedBrush"),
            Background = (IBrush)Resource("SurfaceTranslucentLowBrush"),
            Tag = list.NamespacedName,
        };
    }
}
