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
    // Set by the activation block to its own refresh, so MO2's Refresh button has
    // something to call from outside it.
    private Action? _qtRefresh;

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
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
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
        Grid.SetRow(native,2); layout.Children.Add(native);
        var unavailable = new TextBlock { Name = "DownloadsUnavailable", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24,8,24,8), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        layout.Children.Add(unavailable);
        // The game and profile these downloads belong to. Shown, where it used to be
        // a hidden marker for the stale-picker checks: the native page header that
        // carried it is stood down with the rest of the header line on a narrow
        // panel, which left this page with a band of nothing where the other pages
        // say what they are showing.
        var context = new TextBlock { Name = "DownloadProfileContext", Opacity = .6, FontSize = Mo2Density.FontSize,
            Margin = new Thickness(24,0,24,6), TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetRow(context, 1); layout.Children.Add(context);
        // MO2's own downloadTab furniture: a Refresh and a Query Metadata beside it.
        var qtBar = new StackPanel { Name = "DownloadsQtBar", Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6, Margin = new Thickness(24,0,24,8), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        // The list's own refresh is a local of the activation block, so the button is
        // pointed at whatever that block last set rather than at the page.
        qtBar.Children.Add(Mo2QtWidgets.Button("DownloadsRefreshButton", "Refresh", Mo2QtWidgets.DownloadsRefreshTip, "mdi-refresh",
            () => _qtRefresh?.Invoke()));
        qtBar.Children.Add(Mo2QtWidgets.Button("DownloadsQueryButton", Mo2QtWidgets.QueryMetadata, Mo2QtWidgets.QueryMetadata,
            "mdi-cloud-search-outline", () => { }));
        Grid.SetRow(qtBar, 0); layout.Children.Add(qtBar);
        // And the row MO2 puts under that list: the box that shows the downloads it
        // has been told to hide, and the field that narrows the list.
        var qtFilterBar = new Grid { Name = "DownloadsFilterBar", ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(24,6,24,8) };
        CheckBox? hiddenBox = null;
        TextBox? filterField = null;
        void ApplyFilter() => ViewModel?.Provider.SetFilter(filterField?.Text ?? "", hiddenBox?.IsChecked == true);
        hiddenBox = Mo2QtWidgets.Check("DownloadsHiddenFiles", Mo2QtWidgets.HiddenDownloads, Mo2QtWidgets.HiddenDownloadsTip,
            false, _ => ApplyFilter());
        qtFilterBar.Children.Add(hiddenBox);
        var qtFilter = Mo2QtWidgets.Filter("DownloadsQtFilter", Mo2QtWidgets.DownloadFilterTip, _ => ApplyFilter(), out filterField);
        qtFilter.MinWidth = 160;
        Grid.SetColumn(qtFilter, 2); qtFilterBar.Children.Add(qtFilter);
        Grid.SetRow(qtFilterBar, 3); layout.Children.Add(qtFilterBar);
        Content = layout;
        // The native downloads page draws its own header and toolbar; the shared
        // chrome puts them on one line with the separator, padding and compaction
        // every other page has.
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
            _qtRefresh = Refresh;
            System.Reactive.Disposables.Disposable.Create(() => _qtRefresh = null).DisposeWith(disposables);
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
                // MO2's own download columns (downloadlist.cpp): Status and Size beside
                // the name, then the four it keeps in the archive's .meta.
                columns.Add(NexusMods.App.UI.Controls.ColumnCreator.Create<NexusMods.Abstractions.Downloads.DownloadId, NexusMods.App.UI.Pages.Downloads.DownloadColumns.Status>(width: new GridLength(224)));
                columns.Add(new Avalonia.Controls.Models.TreeDataGrid.TextColumn<NexusMods.App.UI.Controls.CompositeItemModel<NexusMods.Abstractions.Downloads.DownloadId>, string>("Size", x => x.Get<NexusMods.App.UI.Controls.ValueComponent<string>>(Mo2DownloadProvider.BytesKey).Value.Value, width: new GridLength(90)));
                void Meta(string header, NexusMods.App.UI.Controls.ComponentKey key, double width) =>
                    columns.Add(new Avalonia.Controls.Models.TreeDataGrid.TextColumn<NexusMods.App.UI.Controls.CompositeItemModel<NexusMods.Abstractions.Downloads.DownloadId>, string>(
                        header, x => x.Get<NexusMods.App.UI.Controls.ValueComponent<string>>(key).Value.Value, width: new GridLength(width)));
                Meta("Filetime", Mo2DownloadProvider.FiletimeKey, 130);
                Meta("Mod name", Mo2DownloadProvider.ModNameKey, 150);
                Meta("Version", Mo2DownloadProvider.VersionKey, 80);
                Meta("Nexus ID", Mo2DownloadProvider.ModIdKey, 80);
                Meta("Source Game", Mo2DownloadProvider.SourceGameKey, 110);
                var previousWidth = double.NaN;
                // Each column past the status keeps its width until the panel is too
                // narrow to hold it, then collapses. Dropped rather than removed: the
                // list used to take columns out of the collection and put them back,
                // which only held while there were three of them and the one being
                // moved was known by index.
                (int Column, double Width, double Threshold)[] optional = [
                    (2, 90, 430), (3, 130, 900), (4, 150, 700), (5, 80, 1060), (6, 80, 1180), (7, 110, 1320)];
                resizeColumns = () => {
                    if (Bounds.Width <= 0 || Bounds.Width == previousWidth) return;
                    previousWidth = Bounds.Width;
                    // The native Downloads table measures star columns at their
                    // minimum. Allocate its remaining width explicitly instead.
                    var statusWidth = Bounds.Width < 460 ? 184d : 224d;
                    var taken = statusWidth;
                    foreach (var (column, width, threshold) in optional) {
                        if (column >= columns.Count) continue;
                        var show = Bounds.Width >= threshold;
                        columns.SetColumnWidth(column, new GridLength(show ? width : 0));
                        if (show) taken += width;
                    }
                    columns.SetColumnWidth(0, new GridLength(Math.Max(60, Bounds.Width - 48 - taken)));
                    if (columns.Count > 1) columns.SetColumnWidth(1, new GridLength(statusWidth));
                };
                resizeColumns();
            }).DisposeWith(disposables);
            Refresh();
        });
    }
}
