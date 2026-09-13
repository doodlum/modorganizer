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

internal interface IMo2ProfilesPage : IPageViewModelInterface { }
internal sealed class Mo2ProfilesPage : APageViewModel<IMo2ProfilesPage>, IMo2ProfilesPage
{
    public Mo2InstanceCatalog Catalog { get; }
    public Mo2LiveProfile Profile { get; }
    public Action ShowProfile { get; }
    public Mo2ProfilesPage(IWindowManager windows, Mo2InstanceCatalog catalog, Mo2LiveProfile profile, Action showProfile) : base(windows)
    { Catalog = catalog; Profile = profile; ShowProfile = showProfile; TabTitle = "MO2 instances"; TabIcon = IconValues.Package; }
}

internal sealed class Mo2ProfilesView : ReactiveUserControl<Mo2ProfilesPage>
{
    public Mo2ProfilesView()
    {
        var layout = new DockPanel { Margin = new Thickness(16) };
        var header = new StackPanel { Spacing = 10 };
        header.Children.Add(new TextBlock { Text = "MO2 instances", FontSize = 22 });
        header.Children.Add(new TextBlock { Text = "MO2 profiles across your game instances", TextWrapping = TextWrapping.Wrap });
        var actions = new WrapPanel();
        var add = new Button { Content = "Add MO2 instance…", Margin = new Thickness(0, 0, 8, 8) };
        var refresh = new Button { Content = "Refresh", Margin = new Thickness(0, 0, 8, 8) };
        var manage = new Button { Content = "Manage current instance’s profiles…", Margin = new Thickness(0, 0, 8, 8) };
        var originalUi = new Button { Name = "OriginalMo2Ui", Content = "Show original MO2", Margin = new Thickness(0, 0, 8, 8) };
        actions.Children.Add(originalUi);
        actions.Children.Add(add); actions.Children.Add(refresh); actions.Children.Add(manage); header.Children.Add(actions);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap }; header.Children.Add(error);
        DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
        var entries = new StackPanel { Spacing = 12 };
        layout.Children.Add(new ScrollViewer { Content = entries }); Content = layout;
        void Render()
        {
            if (ViewModel is not { } model) return;
            error.Text = model.Catalog.LoadError ?? "";
            manage.IsEnabled = !model.Profile.SelectingProfile && !model.Profile.Installing && model.Profile.ProfilePath.Length > 0;
            originalUi.IsEnabled = model.Profile.CanChangeOriginalUi;
            originalUi.Content = model.Profile.OriginalUiVisible == true ? "Hide original MO2" : "Show original MO2";
            entries.Children.Clear();
            foreach (var entry in model.Catalog.Read()) {
                var section = new StackPanel { Spacing = 8 };
                section.Children.Add(new TextBlock { Text = entry.Instance?.Game ?? "Unavailable instance", FontSize = 18 });
                section.Children.Add(new TextBlock { Text = entry.Registration.Directory, Opacity = 0.65, TextWrapping = TextWrapping.Wrap });
                var launcher = new Button { Content = entry.Registration.Launcher is null ? "Choose MO2 launcher…" : "Change launcher: " + Path.GetFileName(entry.Registration.Launcher),
                    IsEnabled = !model.Profile.SelectingProfile && !model.Profile.Installing };
                launcher.Click += async (_, _) => {
                    if (TopLevel.GetTopLevel(this) is not { } window) return;
                    var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose the script or executable that starts this MO2 instance", AllowMultiple = false });
                    if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
                    try { model.Catalog.SetLauncher(entry.Registration, path); Render(); }
                    catch (Exception exception) { error.Text = exception.Message; }
                };
                section.Children.Add(launcher);
                if (entry.Error is not null) section.Children.Add(new TextBlock { Text = entry.Error, TextWrapping = TextWrapping.Wrap });
                foreach (var profile in entry.Instance?.Profiles ?? []) {
                    var active = model.Profile.ProfilePath.Length > 0 && Mo2InstanceCatalog.LocalPath(model.Profile.ProfilePath) == Path.GetFullPath(profile.Directory);
                    var details = new StackPanel { Spacing = 4 };
                    details.Children.Add(new TextBlock { Text = profile.Name + (active ? " · Selected in MO2" : ""), FontSize = 16 });
                    details.Children.Add(new TextBlock { Text = $"{profile.ModEntries.Length} mod-list entries", Opacity = 0.65 });
                    var button = new Button { Content = details, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        IsEnabled = !model.Profile.SelectingProfile && !model.Profile.Installing };
                    button.Click += async (_, _) => {
                        if (await model.Profile.SelectProfile(entry.Registration, profile)) model.ShowProfile();
                    };
                    section.Children.Add(button);
                }
                entries.Children.Add(section);
            }
        }
        originalUi.Click += async (_, _) => { if (ViewModel is { } model) await model.Profile.SetOriginalUiVisible(model.Profile.OriginalUiVisible != true); };
        refresh.Click += (_, _) => Render();
        manage.Click += async (_, _) => { if (ViewModel is not null) { await ViewModel.Profile.ManageProfiles(); Render(); } };
        add.Click += async (_, _) => {
            if (ViewModel is null || TopLevel.GetTopLevel(this) is not { } window) return;
            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose MO2 instance", AllowMultiple = false });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
            try { ViewModel.Catalog.Add(path); error.Text = ""; Render(); }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        this.WhenActivated(disposables => {
            if (ViewModel is null) return;
            var profile = ViewModel.Profile;
            profile.Changed += Render;
            Disposable.Create(() => profile.Changed -= Render).DisposeWith(disposables);
            Render();
        });
    }
}
