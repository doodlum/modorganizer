using ObservableCollections;
using R3;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using NexusMods.App.UI.Controls;
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
        // The native plugin API gives us downloaded bytes, but no total size.
        // Keep NMA's progress styling without implying a known percentage.
        native.Styles.Add(new Style(selector => selector.OfType<ProgressBar>().Class("DownloadBar")) {
            Setters = { new Setter(ProgressBar.MinWidthProperty, 0d), new Setter(ProgressBar.MaxWidthProperty,
                new Avalonia.Data.Binding("Bounds.Width") {
                    RelativeSource = new Avalonia.Data.RelativeSource(Avalonia.Data.RelativeSourceMode.FindAncestor) { AncestorType = typeof(Mo2DownloadsView) },
                    Converter = new Avalonia.Data.Converters.FuncValueConverter<double, double>(width => width < 460 ? 60 : 120)
                }), new Setter(ProgressBar.IsIndeterminateProperty,
                new Avalonia.Data.Binding("Status.Value") {
                    Converter = new Avalonia.Data.Converters.FuncValueConverter<NexusMods.Sdk.Jobs.JobStatus, bool>(status => status == NexusMods.Sdk.Jobs.JobStatus.Running)
                }), new Setter(ProgressBar.ProgressTextFormatProperty,
                new Avalonia.Data.Binding("Status.Value") {
                    Converter = new Avalonia.Data.Converters.FuncValueConverter<NexusMods.Sdk.Jobs.JobStatus, string>(status => status switch {
                        NexusMods.Sdk.Jobs.JobStatus.Failed => "Failed",
                        NexusMods.Sdk.Jobs.JobStatus.Paused => "Paused",
                        _ => "Downloading"
                    })
                }) }
        });
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var actions = new ItemsControl();
        StandardButton ActionButton(string text, string name, IconValue icon) {
            var button = new StandardButton { Text = text, Name = name, LeftIcon = icon,
                Type = StandardButton.Types.Tertiary, Fill = StandardButton.Fills.None,
                Size = StandardButton.Sizes.Toolbar, ShowIcon = StandardButton.ShowIconOptions.Left };
            ToolTip.SetTip(button, text); return button;
        }
        var import = ActionButton("Install archive…", "InstallArchiveButton", IconValues.Add);
        var install = ActionButton("Install selected", "InstallSelectedDownload", IconValues.DownloadDone);
        var delete = ActionButton("Delete selected…", "DeleteSelectedDownload", IconValues.DeleteOutline);
        var nexus = ActionButton("Nexus link…", "OpenNexusLinkButton", IconValues.Link);
        var link = new TextBox { Watermark = "Paste a Nexus file link", Name = "NexusFileLink", MinWidth = 260 };
        var download = new Button { Content = "Download through MO2", Name = "DownloadNexusButton", Margin = new Thickness(0,8,0,0) };
        var flyoutContent = new StackPanel(); flyoutContent.Children.Add(link); flyoutContent.Children.Add(download);
        var nexusFlyout = new Flyout { Content = flyoutContent };
        nexus.Flyout = nexusFlyout;
        Mo2ProfileTarget? linkTarget = null;
        nexusFlyout.Opened += (_, _) => linkTarget = ViewModel?.Profile.CurrentTarget;
        nexusFlyout.Closed += (_, _) => linkTarget = null;
        actions.Items.Add(import); actions.Items.Add(install); actions.Items.Add(delete); actions.Items.Add(nexus);
        native.GetLogicalDescendants().OfType<Toolbar>().Single().Items.Insert(1, actions);
        Grid.SetRow(native,1); layout.Children.Add(native);
        var unavailable = new TextBlock { Name = "DownloadsUnavailable", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24,8,24,8), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        layout.Children.Add(unavailable);
        // Retained as a context marker for stale-picker checks; visible context is the native page header.
        var context = new TextBlock { Name = "DownloadProfileContext", IsVisible = false };
        layout.Children.Add(context); Content = layout;
        // The native downloads page draws its own header and toolbar; the shared
        // chrome puts them on one line with the separator, padding, compaction and
        // maximise action every other page has.
        Mo2PanelChrome.Adopt(native);
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
        delete.Click += async (_, _) => {
            if (ViewModel is not { } model) return;
            var target = model.Profile.CurrentTarget;
            foreach (var file in model.Selected.Where(x => x.CanControl("delete")).ToArray())
                await model.Profile.ControlDownload(file.Path, "delete", target);
        };
        download.Click += async (_, _) => {
            if (ViewModel is not { } model || linkTarget is not { } target) return;
            nexusFlyout.Hide();
            await model.Profile.DownloadNexus(link.Text ?? "", target);
        };
        this.WhenActivated(disposables => {
            if (ViewModel is not { } model) return;
            native.ViewModel = model;
            var profile = model.Profile;
            System.Reactive.Disposables.Disposable.Create(() => { nexusFlyout.Hide(); linkTarget = null; }).DisposeWith(disposables);
            Action? resizeColumns = null;
            void Refresh()
            {
                if (linkTarget is { } target && (target != profile.CurrentTarget || !profile.CanUseDownloads)) nexusFlyout.Hide();
                import.IsEnabled = download.IsEnabled = nexus.IsEnabled = profile.CanUseDownloads;
                install.IsEnabled = profile.CanUseDownloads && model.Selected.Any(x => !x.Partial);
                delete.IsEnabled = profile.CanUseDownloads && model.Selected.Any(x => x.CanControl("delete"));
                delete.IsVisible = model.Selected.Any(x => x.CanControl("delete"));
                import.ShowLabel = install.ShowLabel = delete.ShowLabel = nexus.ShowLabel = Bounds.Width >= 900;
                context.Text = model.HeaderDescription;
                unavailable.IsVisible = !profile.IsConnected;
                unavailable.Text = profile.ProfilePath.Length == 0 ? "Select a profile to view downloads." : "Downloads are unavailable. Reconnect to MO2 to refresh this folder.";
                foreach (var name in new[] { "PauseAllButton", "ResumeAllButton", "PauseSelectedButton", "ResumeSelectedButton", "CancelSelectedButton" }) {
                    var button = native.FindControl<NexusMods.App.UI.Controls.StandardButton>(name)!;
                    button.ShowLabel = Bounds.Width > 650;
                    var operation = name.StartsWith("Cancel") ? "cancel" : name.StartsWith("Resume") ? "resume" : "pause";
                    button.IsVisible = !name.Contains("Selected") || model.Selected.Any(x => x.CanControl(operation));
                    button.IsEnabled = profile.CanUseDownloads && (name.Contains("Selected") ? model.Selected : profile.Downloads).Any(x => x.CanControl(operation));
                    ToolTip.SetTip(button, name.Replace("Button", "").Replace("All", " all").Replace("Selected", " selected"));
                }
            }
            profile.Changed += Refresh; SizeChanged += Resized;
            void Resized(object? sender, SizeChangedEventArgs args) { Refresh(); resizeColumns?.Invoke(); }
            System.Reactive.Disposables.Disposable.Create(() => { profile.Changed -= Refresh; SizeChanged -= Resized; }).DisposeWith(disposables);
            model.Adapter.SelectedModels.ObserveChanged().Subscribe(_ => Refresh()).DisposeWith(disposables);
            model.Adapter.Source.Subscribe(source => {
                resizeColumns = null;
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
                columns.Add(NexusMods.App.UI.Controls.ColumnCreator.Create<NexusMods.Abstractions.Downloads.DownloadId, NexusMods.App.UI.Pages.Downloads.DownloadColumns.Status>(width: new GridLength(224)));
                var bytesColumn = columns[1];
                var previousWidth = double.NaN;
                resizeColumns = () => {
                    if (Bounds.Width <= 0 || Bounds.Width == previousWidth) return;
                    previousWidth = Bounds.Width;
                    // The native Downloads table measures star columns at their
                    // minimum. Allocate its remaining width explicitly instead.
                    var compact = Bounds.Width < 460;
                    var bytesWidth = compact ? 0 : 90;
                    var statusWidth = compact ? 184 : 224;
                    columns.SetColumnWidth(0, new GridLength(Math.Max(60, Bounds.Width - 48 - bytesWidth - statusWidth)));
                    if (compact && columns.Count == 3) columns.RemoveAt(1);
                    else if (!compact && columns.Count == 2) columns.Insert(1, bytesColumn);
                    if (!compact) columns.SetColumnWidth(1, new GridLength(bytesWidth));
                    columns.SetColumnWidth(compact ? 1 : 2, new GridLength(statusWidth));
                };
                resizeColumns();
            }).DisposeWith(disposables);
            Refresh();
        });
    }
}
