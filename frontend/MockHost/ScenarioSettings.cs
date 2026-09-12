using NexusMods.App.UI.Controls.MarkdownRenderer;
using NexusMods.App.UI.Controls.Settings.SettingEntries;
using NexusMods.App.UI.Notifications;
using NexusMods.App.UI.Pages.Settings;
using NexusMods.App.UI.Settings;
using NexusMods.App.UI.Windows;
using NexusMods.Collections;
using NexusMods.CrossPlatform;
using NexusMods.Paths;
using NexusMods.Sdk.Settings;
using NexusMods.Sdk.Tracking;
using NexusMods.UI.Sdk.Icons;
using NexusMods.UI.Sdk.Settings;

namespace Mo2.Frontend;

internal static class ScenarioSettings
{
    public static FixturePageFactory Register(FixtureServices services, MemorySettings settings, IWindowManager windows)
    {
        services.Add<IFileSystem>(FileSystem.Shared);
        settings.Register<BehaviorSettings>();
        settings.Register<LanguageSettings>();
        settings.Register<LoggingSettings>();
        settings.Register<DownloadSettings>();
        settings.Register<TrackingSettings>();
        settings.Register<ExperimentalSettings>();
        settings.Set(new LanguageSettings { UICulture = new System.Globalization.CultureInfo("en") });
        services.Add<IEnumerable<SectionDescriptor>>(new[] {
            new SectionDescriptor(Sections.General, "General", () => IconValues.Desktop, ushort.MaxValue),
            new SectionDescriptor(Sections.Privacy, "Privacy", () => IconValues.ShieldHalfFull),
            new SectionDescriptor(Sections.GameSpecific, "Game specific", () => IconValues.Game, 3),
            new SectionDescriptor(Sections.Advanced, "Advanced", () => IconValues.School, 2),
            new SectionDescriptor(Sections.DeveloperTools, "Developer tools", () => IconValues.Code, 1),
            new SectionDescriptor(Sections.Experimental, "Experimental - Not currently supported", () => IconValues.WarningAmber),
        });
        services.Add<IInteractionControlFactory<BooleanContainerOptions>>(new SettingToggleFactory());
        services.Add<IInteractionControlFactory<SingleValueMultipleChoiceContainerOptions>>(new SettingComboBoxFactory());
        services.AddFactory<IMarkdownRendererViewModel>(() => new MarkdownRendererViewModel());
        return new FixturePageFactory("5f4a4e38-3b08-40d9-9ab3-d3a2a5f30003", "Settings", IconValues.CogOutline,
            () => new SettingsPageViewModel(services, settings, windows, new WindowNotificationService()));
    }
}
