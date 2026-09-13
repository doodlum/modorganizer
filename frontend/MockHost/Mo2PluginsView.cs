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
        LayoutUpdated += (_, _) => {
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
            editor.ViewModel = ViewModel;
            var profile = ViewModel!.LiveProfile!;
            void UpdateSelection() {
                var selected = profile.Order.Plugins.Where(plugin => ViewModel.Adapter.SelectedModels.Any(row => row.Key.Equals(plugin.Key))).ToArray();
                foreach (var (button, enabled) in activationButtons)
                    button.IsEnabled = profile.IsConnected && !profile.SelectingProfile && selected.Any(x => x.CanToggle && x.IsActive != enabled);
                actions.IsEnabled = selected.Any(x => x.CanToggle);
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
