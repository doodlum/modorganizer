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

internal interface IMo2DownloadsPage : IPageViewModelInterface { }
internal sealed class Mo2DownloadsPage : APageViewModel<IMo2DownloadsPage>, IMo2DownloadsPage
{
    public Mo2LiveProfile Profile { get; }
    public Mo2DownloadsPage(IWindowManager windows, Mo2LiveProfile profile) : base(windows)
    { Profile = profile; TabTitle = "Downloads"; TabIcon = IconValues.LibraryOutline; }
}

internal sealed class Mo2DownloadsView : ReactiveUserControl<Mo2DownloadsPage>
{
    public Mo2DownloadsView()
    {
        var layout = new DockPanel { Margin = new Thickness(16) };
        var header = new StackPanel { Spacing = 12 };
        header.Children.Add(new TextBlock { Text = "Downloads", FontSize = 22 });
        var import = new Button { Content = "Install archive…" };
        header.Children.Add(import);
        var link = new TextBox { Watermark = "Paste a Nexus file link", Name = "NexusFileLink" };
        var download = new Button { Content = "Download through MO2" };
        header.Children.Add(link); header.Children.Add(download);
        header.Children.Add(new TextBlock { Text = "Archives in this instance’s downloads folder", Margin = new Thickness(0, 8) });
        DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
        var rows = new StackPanel { Spacing = 8 };
        layout.Children.Add(new ScrollViewer { Content = rows });
        Content = layout;
        import.Click += async (_, _) => {
            if (ViewModel is null || TopLevel.GetTopLevel(this) is not { } window) return;
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                Title = "Install mod archive", AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Mod archives") { Patterns = ["*.zip", "*.7z", "*.rar", "*.fomod"] }],
            });
            if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) await ViewModel.Profile.InstallArchive(path);
        };
        download.Click += async (_, _) => { if (ViewModel is not null) await ViewModel.Profile.DownloadNexus(link.Text ?? ""); };
        this.WhenActivated(disposables => {
            if (ViewModel is null) return;
            var profile = ViewModel.Profile;
            void Refresh()
            {
                import.IsEnabled = download.IsEnabled = !profile.Installing;
                rows.Children.Clear();
                if (profile.Downloads.Count == 0) rows.Children.Add(new TextBlock { Text = "No downloads yet" });
                foreach (var archive in profile.Downloads) {
                    var row = new DockPanel { Margin = new Thickness(0, 4) };
                    var install = new Button { Content = "Install", IsEnabled = !archive.Partial && !profile.Installing, Margin = new Thickness(12, 0, 0, 0) };
                    install.Click += async (_, _) => await profile.InstallArchive(archive.Path);
                    DockPanel.SetDock(install, Dock.Right); row.Children.Add(install);
                    var text = new StackPanel { Spacing = 4 };
                    text.Children.Add(new TextBlock { Text = archive.Name, TextWrapping = TextWrapping.Wrap });
                    var state = archive.Paused ? "Paused" : archive.Partial ? "Downloading / incomplete" : archive.Installed ? "Previously installed" : "Downloaded";
                    text.Children.Add(new TextBlock { Text = $"{archive.Bytes / 1024d / 1024d:0.0} MB · {state}", Opacity = 0.65 });
                    row.Children.Add(text); rows.Children.Add(row);
                }
            }
            profile.Changed += Refresh;
            Disposable.Create(() => profile.Changed -= Refresh).DisposeWith(disposables);
            Refresh();
        });
    }
}
