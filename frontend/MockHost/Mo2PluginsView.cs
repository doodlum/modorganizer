using ObservableCollections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using NexusMods.App.UI.Controls;
using NexusMods.Abstractions.Games;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
using NexusMods.App.UI.Pages.Sorting;
using ReactiveUI;
using R3;
using System.Reactive.Disposables;

namespace Mo2.Frontend;

// Keep the upstream ordering editor and add MO2's independent ESP activation.
internal sealed class Mo2PluginsView : ReactiveUserControl<ScenarioLoadOrderPage>
{
    public Mo2PluginsView()
    {
        var layout = new DockPanel();
        var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Thickness(8), Spacing = 6 };
        var activationButtons = new List<(Button Button, bool Enabled)>();
        foreach (var (label, enabled) in new[] { ("Enable selected", true), ("Disable selected", false) }) {
            var button = new Button { Content = label, Name = enabled ? "EnableSelectedPlugins" : "DisableSelectedPlugins" };
            button.Click += async (_, _) => { if (ViewModel is { } model) await model.SetSelectedActive(enabled); };
            actions.Children.Add(button); activationButtons.Add((button, enabled));
        }
        var details = new TextBlock { Name = "Mo2PluginDiagnostics", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(12) };
        var detailScroll = new ScrollViewer { Content = details, MaxHeight = 150, IsVisible = false };
        DockPanel.SetDock(detailScroll, Dock.Bottom); layout.Children.Add(detailScroll);
        actions.IsEnabled = false;
        DockPanel.SetDock(actions, Dock.Top); layout.Children.Add(actions);
        var editor = new LoadOrderView();
        var alert = editor.FindControl<Control>("LoadOrderAlert")!;
        // NMA's decorative winner-gradient looks like a second scrollbar.
        var winnerRail = editor.FindControl<Control>("TrophyBarColumnGrid")!;
        winnerRail.IsVisible = false;
        var help = new Button { Content = "?", Name = "PluginHelpButton" };
        ToolTip.SetTip(help,"Plugin load order help");
        help.Click += (_,_) => ViewModel?.ToggleAlertCommand.Execute().Subscribe();
        actions.Children.Add(help);
        LayoutUpdated += (_, _) => {
            // Use the same rectangle as Mods; NMA's order editor otherwise
            // crops its thumbnails to a different aspect ratio.
            foreach (var thumbnail in editor.GetVisualDescendants().OfType<Border>().Where(x => x.Name == "ParentBorder")) {
                if (thumbnail.Width != 46) thumbnail.Width = 46;
                if (thumbnail.Height != 26) thumbnail.Height = 26;
            }
            foreach (var row in editor.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.TreeDataGridRow>()) {
                if (row.MinHeight != 40) row.MinHeight = 40;
                if (row.Height != 40) row.Height = 40;
            }
            foreach (var text in editor.GetVisualDescendants().OfType<TextBlock>().Where(x => x.Name is "DisplayName" or "ModName")) {
                text.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
                ToolTip.SetTip(text,text.Text);
            }
            foreach (var image in editor.GetVisualDescendants().OfType<Image>().Where(x => x.Name == "ImageThumbnail"))
                if (image.Stretch != Avalonia.Media.Stretch.Uniform) image.Stretch = Avalonia.Media.Stretch.Uniform;
            // Keep the native column header and a complete plugin row visible
            // before allocating space to the optional selection details.
            var available = Bounds.Height - actions.DesiredSize.Height - alert.Bounds.Height - 112;
            detailScroll.MaxHeight = Math.Min(150, Math.Min(Bounds.Height * .25, Math.Max(0, available)));
            details.Margin = new Thickness(12, detailScroll.MaxHeight < 80 ? 4 : 12);
        };
        var chooseProfile = new TextBlock { Text = "Select a profile in My Loadouts to view its plugins.",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(24) };
        var body = new Grid(); body.Children.Add(editor); body.Children.Add(chooseProfile);
        layout.Children.Add(body); Content = layout;
        this.WhenActivated(disposables => {
            ViewModel!.Adapter.ViewHierarchical.Value = false;
            editor.ViewModel = ViewModel;
            var profile = ViewModel!.LiveProfile!;
            ViewModel.Adapter.Source.AsObservable().Subscribe(source => {
                if (source is not FlatTreeDataGridSource<CompositeItemModel<ISortItemKey>> table) return;
                table.Columns[0] = new TextColumn<CompositeItemModel<ISortItemKey>,int>("Order",x => x.Get<SharedComponents.IndexComponent>(LoadOrderColumns.IndexColumn.IndexComponentKey).Index.Value.Value, width:new GridLength(58), options:new TextColumnOptions<CompositeItemModel<ISortItemKey>> { CanUserSortColumn = false });
                table.Columns[1] = ColumnCreator.Create<ISortItemKey,LoadOrderColumns.DisplayNameColumn>(columnHeader:"Plugin",width:new GridLength(2,GridUnitType.Star),canUserSortColumn:false,canUserResizeColumn:true);
                table.Columns[2] = ColumnCreator.Create<ISortItemKey,LoadOrderColumns.ModNameColumn>(columnHeader:"Mod",width:new GridLength(1,GridUnitType.Star),canUserSortColumn:false,canUserResizeColumn:true);
            }).DisposeWith(disposables);
            void Highlight() => Mo2RowHighlights.Apply(editor,profile,plugins:true);
            EventHandler layout = (_,_) => Highlight(); editor.LayoutUpdated += layout;
            profile.HighlightsChanged += Highlight;
            System.Reactive.Disposables.Disposable.Create(() => { editor.LayoutUpdated -= layout; profile.HighlightsChanged -= Highlight; }).DisposeWith(disposables);
            void UpdateSelection() {
                var selected = profile.Order.Plugins.Where(plugin => ViewModel.Adapter.SelectedModels.Any(row => row.Key.Equals(plugin.Key))).ToArray();
                foreach (var (button, enabled) in activationButtons)
                    button.IsEnabled = profile.IsConnected && !profile.SelectingProfile && selected.Any(x => x.CanToggle && x.IsActive != enabled);
                actions.IsEnabled = profile.IsConnected;
                details.Text = string.Join("\n\n", selected.Select(x => $"{x.DisplayName} · {(x.IsActive ? "Enabled" : "Disabled")} · Mod index {(x.ModIndex.Length == 0 ? "—" : x.ModIndex)}\n{x.Diagnostics}"));
                if (selected.Length == 0) details.Text = "Select a plugin to view MO2’s diagnostics and mod index.";
                detailScroll.IsVisible = profile.ProfilePath.Length > 0 && selected.Length > 0;
            }
            void UpdateConnection() {
                var connected = profile.ProfilePath.Length > 0;
                editor.IsVisible = actions.IsVisible = connected;
                chooseProfile.IsVisible = !connected;
                UpdateSelection();
            }
            profile.Changed += UpdateConnection;
            System.Reactive.Disposables.Disposable.Create(() => profile.Changed -= UpdateConnection).DisposeWith(disposables);
            UpdateConnection();
            ViewModel!.Adapter.SelectedModels.ObserveChanged()
                .Subscribe(_ => UpdateSelection()).DisposeWith(disposables);
            System.Reactive.Disposables.Disposable.Create(() => editor.ViewModel = null).DisposeWith(disposables);
        });
    }
}
