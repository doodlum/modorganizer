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
        actions.IsEnabled = false;
        DockPanel.SetDock(actions, Dock.Top); layout.Children.Add(actions);
        var editor = new LoadOrderView(); layout.Children.Add(editor); Content = layout;
        this.WhenActivated(disposables => {
            editor.ViewModel = ViewModel;
            ViewModel!.Adapter.SelectedModels.ObserveCountChanged(notifyCurrentCount: true)
                .Subscribe(count => actions.IsEnabled = count > 0).DisposeWith(disposables);
            System.Reactive.Disposables.Disposable.Create(() => editor.ViewModel = null).DisposeWith(disposables);
        });
    }
}
