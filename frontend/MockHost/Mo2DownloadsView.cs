using ObservableCollections;
using R3;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using System.Reactive.Disposables;

namespace Mo2.Frontend;

internal sealed class Mo2DownloadsView : ReactiveUserControl<Mo2DownloadsPage>
{
    internal async Task ChooseArchive(Func<Task<string?>> choose)
    {
        if (ViewModel is not { } model || !model.Profile.CanUseDownloads) return;
        var profile = model.Profile;
        var target = profile.CurrentTarget;
        if (await choose() is { } path) await profile.InstallArchive(path, target);
    }
    public Mo2DownloadsView()
    {
        var native = new NexusMods.App.UI.Pages.Downloads.DownloadsPageView();
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        var actions = new WrapPanel { Margin = new Thickness(24,12,24,0) };
        var import = new Button { Content = "Install archive…", Name = "InstallArchiveButton", Margin = new Thickness(0,0,8,4) };
        var install = new Button { Content = "Install selected", Name = "InstallSelectedDownload", Margin = new Thickness(0,0,8,4) };
        var nexus = new Button { Content = "Nexus link…", Margin = new Thickness(0,0,0,4) };
        var link = new TextBox { Watermark = "Paste a Nexus file link", Name = "NexusFileLink", MinWidth = 260 };
        var download = new Button { Content = "Download through MO2", Name = "DownloadNexusButton", Margin = new Thickness(0,8,0,0) };
        var flyoutContent = new StackPanel(); flyoutContent.Children.Add(link); flyoutContent.Children.Add(download);
        nexus.Flyout = new Flyout { Content = flyoutContent };
        actions.Children.Add(import); actions.Children.Add(install); actions.Children.Add(nexus);
        layout.Children.Add(actions); Grid.SetRow(native,2); layout.Children.Add(native);
        var unavailable = new TextBlock { Name = "DownloadsUnavailable", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24,8,24,8), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        Grid.SetRow(unavailable,1); layout.Children.Add(unavailable);
        // Retained as a context marker for stale-picker checks; visible context is the native page header.
        var context = new TextBlock { Name = "DownloadProfileContext", IsVisible = false };
        layout.Children.Add(context); Content = layout;
        import.Click += async (_, _) => {
            if (ViewModel is null || TopLevel.GetTopLevel(this) is not { } window) return;
            await ChooseArchive(async () => {
                var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                    Title = "Install mod archive", AllowMultiple = false,
                    FileTypeFilter = [new FilePickerFileType("Mod archives") { Patterns = ["*.zip", "*.7z", "*.rar", "*.fomod"] }],
                });
                return files.FirstOrDefault()?.TryGetLocalPath();
            });
        };
        install.Click += async (_,_) => {
            if (ViewModel is not { } model) return;
            var target = model.Profile.CurrentTarget;
            foreach (var file in model.Selected.Where(x => !x.Partial).ToArray()) await model.Profile.InstallArchive(file.Path, target);
        };
        download.Click += async (_, _) => { if (ViewModel is not null) { nexus.Flyout.Hide(); await ViewModel.Profile.DownloadNexus(link.Text ?? ""); } };
        this.WhenActivated(disposables => {
            if (ViewModel is not { } model) return;
            native.ViewModel = model;
            var profile = model.Profile;
            void Refresh()
            {
                import.IsEnabled = download.IsEnabled = nexus.IsEnabled = profile.CanUseDownloads;
                install.IsEnabled = profile.CanUseDownloads && model.Selected.Any(x => !x.Partial);
                context.Text = model.HeaderDescription;
                unavailable.IsVisible = !profile.IsConnected;
                unavailable.Text = profile.ProfilePath.Length == 0 ? "Select a profile to view downloads." : "Downloads are unavailable. Reconnect to MO2 to refresh this folder.";
                foreach (var name in new[] { "PauseAllButton", "ResumeAllButton", "PauseSelectedButton", "ResumeSelectedButton", "CancelSelectedButton" }) {
                    var button = native.FindControl<NexusMods.App.UI.Controls.StandardButton>(name)!;
                    button.ShowLabel = Bounds.Width > 650;
                    button.IsVisible = !name.Contains("Selected") || model.Selected.Any(x => x.Partial && (name.StartsWith("Cancel") || x.Paused == name.StartsWith("Resume")));
                    button.IsEnabled = profile.CanUseDownloads && (name.Contains("Selected") ? model.Selected : profile.Downloads).Any(x => x.Partial && (name.StartsWith("Cancel") || x.Paused == name.StartsWith("Resume")));
                    ToolTip.SetTip(button, name.Replace("Button", "").Replace("All", " all").Replace("Selected", " selected"));
                }
            }
            profile.Changed += Refresh; SizeChanged += Resized;
            void Resized(object? sender, SizeChangedEventArgs args) => Refresh();
            System.Reactive.Disposables.Disposable.Create(() => { profile.Changed -= Refresh; SizeChanged -= Resized; }).DisposeWith(disposables);
            model.Adapter.SelectedModels.ObserveChanged().Subscribe(_ => Refresh()).DisposeWith(disposables);
            model.Adapter.Source.Subscribe(source => {
                var columns = source switch {
                    FlatTreeDataGridSource<NexusMods.App.UI.Controls.CompositeItemModel<NexusMods.Abstractions.Downloads.DownloadId>> flat => flat.Columns,
                    HierarchicalTreeDataGridSource<NexusMods.App.UI.Controls.CompositeItemModel<NexusMods.Abstractions.Downloads.DownloadId>> tree => tree.Columns,
                    _ => null
                };
                if (columns is null) return;
                columns.Clear();
                columns.Add(new Avalonia.Controls.Models.TreeDataGrid.TemplateColumn<NexusMods.App.UI.Controls.CompositeItemModel<NexusMods.Abstractions.Downloads.DownloadId>>("Name",
                    new Avalonia.Controls.Templates.FuncDataTemplate<NexusMods.App.UI.Controls.CompositeItemModel<NexusMods.Abstractions.Downloads.DownloadId>>((item, _) => {
                        var label = new TextBlock { Text = item?.Get<NexusMods.App.UI.Controls.NameComponent>(NexusMods.App.UI.Pages.Downloads.DownloadColumns.Name.NameComponentKey).Value.Value ?? "", TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                        ToolTip.SetTip(label, label.Text); return label;
                    }), width: new GridLength(180)));
                columns.Add(new Avalonia.Controls.Models.TreeDataGrid.TextColumn<NexusMods.App.UI.Controls.CompositeItemModel<NexusMods.Abstractions.Downloads.DownloadId>, string>("Downloaded", x => x.Get<NexusMods.App.UI.Controls.ValueComponent<string>>(Mo2DownloadProvider.BytesKey).Value.Value, width: new GridLength(90)));
                columns.Add(NexusMods.App.UI.Controls.ColumnCreator.Create<NexusMods.Abstractions.Downloads.DownloadId, NexusMods.App.UI.Pages.Downloads.DownloadColumns.Status>(width: new GridLength(155)));
            }).DisposeWith(disposables);
            Refresh();
        });
    }
}
