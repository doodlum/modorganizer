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
using System.Reactive;
using System.Reactive.Linq;
using NexusMods.App.UI.Controls;

namespace Mo2.Frontend;

internal interface IMo2ProfilesPage : IPageViewModelInterface { }
internal sealed class Mo2ProfilesPage : APageViewModel<IMo2ProfilesPage>, IMo2ProfilesPage
{
    public Mo2InstanceCatalog Catalog { get; }
    public Mo2LiveProfile Profile { get; }
    public Action ShowProfile { get; }
    public ReactiveCommand<bool, Unit> ManageNexusCommand { get; }
    public IObservable<string> AccountStatus { get; }
    public Mo2ProfilesPage(IWindowManager windows, Mo2InstanceCatalog catalog, Mo2LiveProfile profile, Action showProfile, Func<bool, Task> manageAccount, IObservable<string> accountStatus) : base(windows)
    {
        Catalog = catalog; Profile = profile; ShowProfile = showProfile; TabTitle = "Connections"; TabIcon = IconValues.Package;
        ManageNexusCommand = ReactiveCommand.CreateFromTask<bool>(manageAccount);
        AccountStatus = accountStatus;
    }
}

internal sealed class Mo2ProfilesView : ReactiveUserControl<Mo2ProfilesPage>
{
    public Mo2ProfilesView()
    {
        var layout = new DockPanel { Margin = new Thickness(16) };
        var header = new StackPanel { Spacing = 10 };
        // This page drew its own title and subtitle as two plain text blocks, which
        // made it the one page with no pictogram, no separator and no collapse. The
        // shared page header carries all three.
        var title = new NexusMods.App.UI.Controls.PageHeader.PageHeader {
            Title = "Connections", Description = "MO2 profiles across your game instances.",
            Icon = IconValues.Package };
        header.Children.Add(title);
        var actions = new WrapPanel();
        var add = new Button { Content = "Add MO2 instance…", Margin = new Thickness(0, 0, 8, 8) };
        var refresh = new Button { Content = "Refresh", Margin = new Thickness(0, 0, 8, 8) };
        var manage = new Button { Content = "Manage current instance’s profiles…", Margin = new Thickness(0, 0, 8, 8) };
        var originalUi = new Button { Name = "OriginalMo2Ui", Content = "Show original MO2", Margin = new Thickness(0, 0, 8, 8) };
        // MO2's own settings and notifications had no route from the frontend at
        // all. They open the host's dialogs rather than being reimplemented here,
        // so everything they write stays MO2's.
        var settings = new Button { Name = "OriginalMo2Settings", Content = "MO2 settings…", Margin = new Thickness(0, 0, 8, 8) };
        var hostMenu = new MenuFlyout();
        foreach (var (label, name) in new[] {
                     ("MO2 notifications…", "notifications"), ("Check for MO2 updates…", "update"),
                     ("Endorse Mod Organizer…", "endorse"), ("Visit Nexus…", "nexus"), ("MO2 help…", "help") }) {
            var entry = new MenuItem { Header = label };
            var action = name; var caption = label.TrimEnd((char)0x2026);
            entry.Click += async (_, _) => { if (ViewModel is { } model) await model.Profile.OpenOriginal(action, caption); };
            hostMenu.Items.Add(entry);
        }
        // The rest of MO2's own window entries, behind one control rather than five
        // buttons: they open the host's dialogs and are used rarely.
        var hostMore = new Button { Name = "OriginalMo2More", Content = "More MO2 actions", Margin = new Thickness(0, 0, 8, 8),
            Flyout = hostMenu };
        actions.Children.Add(originalUi);
        actions.Children.Add(settings); actions.Children.Add(hostMore);
        actions.Children.Add(add); actions.Children.Add(refresh); actions.Children.Add(manage); header.Children.Add(actions);
        var accountStatus = new TextBlock { Name = "NexusAccountStatus", TextWrapping = TextWrapping.Wrap };
        header.Children.Add(accountStatus);
        var nexusActions = new WrapPanel();
        var nexusConnect = new StandardButton { Name = "ConnectNexusInstances", Text = "Connect Nexus…", Margin = new Thickness(0, 0, 8, 8), CommandParameter = false };
        var nexusDisconnect = new StandardButton { Name = "DisconnectNexusInstances", Text = "Sign out of all instances", Margin = new Thickness(0, 0, 8, 8), CommandParameter = true };
        nexusActions.Children.Add(nexusConnect); nexusActions.Children.Add(nexusDisconnect); header.Children.Add(nexusActions);
        ToolTip.SetTip(nexusDisconnect, "Disconnect Nexus from every registered MO2 instance, including instances that are currently closed.");
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap }; header.Children.Add(error);
        DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
        var entries = new StackPanel { Spacing = 12 };
        layout.Children.Add(new ScrollViewer { Content = entries }); Content = layout;
        Mo2PanelChrome.ApplyDocked(this, layout, title);
        void Render()
        {
            if (ViewModel is not { } model) return;
            error.Text = model.Catalog.LoadError ?? "";
            manage.IsEnabled = !model.Profile.SelectingProfile && !model.Profile.Installing && model.Profile.ProfilePath.Length > 0;
            originalUi.IsEnabled = model.Profile.CanChangeOriginalUi;
            settings.IsEnabled = hostMore.IsEnabled = model.Profile.CanChangeOriginalUi;
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
        settings.Click += async (_, _) => { if (ViewModel is { } model) await model.Profile.OpenOriginal("settings", "MO2 settings"); };
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
            nexusConnect.Command = nexusDisconnect.Command = ViewModel.ManageNexusCommand;
            ViewModel.AccountStatus.Subscribe(text => accountStatus.Text = text).DisposeWith(disposables);
            var profile = ViewModel.Profile;
            profile.Changed += Render;
            Disposable.Create(() => profile.Changed -= Render).DisposeWith(disposables);
            Render();
        });
    }
}
