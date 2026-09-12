using ObservableCollections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
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
        var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Thickness(12), Spacing = 8 };
        foreach (var (label, enabled) in new[] { ("Enable selected", true), ("Disable selected", false) }) {
            var button = new Button { Content = label };
            button.Click += async (_, _) => { if (ViewModel is { } model) await model.SetSelectedActive(enabled); };
            actions.Children.Add(button);
        }
        var details = new TextBlock { Name = "Mo2PluginDiagnostics", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(12) };
        var detailScroll = new ScrollViewer { Content = details, MaxHeight = 180, IsVisible = false };
        DockPanel.SetDock(detailScroll, Dock.Bottom); layout.Children.Add(detailScroll);
        actions.IsEnabled = false;
        DockPanel.SetDock(actions, Dock.Top); layout.Children.Add(actions);
        var editor = new LoadOrderView();
        var chooseProfile = new TextBlock { Text = "Select a profile in My Loadouts to view its plugins.",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(24) };
        var body = new Grid(); body.Children.Add(editor); body.Children.Add(chooseProfile);
        layout.Children.Add(body); Content = layout;
        this.WhenActivated(disposables => {
            editor.ViewModel = ViewModel;
            var profile = ViewModel!.LiveProfile!;
            void UpdateSelection() {
                var selected = profile.Order.Plugins.Where(plugin => ViewModel.Adapter.SelectedModels.Any(row => row.Key.Equals(plugin.Key))).ToArray();
                actions.IsEnabled = selected.Any(x => x.CanToggle);
                details.Text = string.Join("\n\n", selected.Select(x => $"{x.DisplayName} · {(x.IsActive ? "Enabled" : "Disabled")} · Mod index {(x.ModIndex.Length == 0 ? "—" : x.ModIndex)}\n{x.Diagnostics}"));
                detailScroll.IsVisible = selected.Length > 0;
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
