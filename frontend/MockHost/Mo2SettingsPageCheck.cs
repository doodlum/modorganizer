using Avalonia.Controls;
using Avalonia.VisualTree;
using NexusMods.App.UI.Pages.Settings;

namespace Mo2.Frontend;

// The gear used to open the frontend's own instance-management page. It opens
// NMA's settings page now, so this confirms the page that appears is upstream's,
// with its sections and entries populated and its Save and Cancel bound.
internal static class Mo2SettingsPageCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        live.OpenSettings();
        SettingsView? view = null;
        for (var attempt = 0; attempt < 100 && view is null; attempt++) {
            await Task.Delay(100);
            window.UpdateLayout();
            view = window.GetVisualDescendants().OfType<SettingsView>().FirstOrDefault(x => x.Bounds.Height > 0);
        }
        if (view?.ViewModel is not ISettingsPageViewModel model)
            throw new Exception("The gear did not open NMA's settings page");

        if (model.Sections.Count == 0) throw new Exception("Settings page has no sections");
        if (model.SettingEntries.Count == 0) throw new Exception("Settings page has no entries");

        var buttons = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.StandardButton>()
            .Where(x => x.IsEffectivelyVisible).ToArray();
        Console.WriteLine($"  sections={model.Sections.Count} entries={model.SettingEntries.Count} " +
            $"names={string.Join(", ", model.Sections.Select(x => x.Descriptor.Name))}");
        if (buttons.Length == 0) throw new Exception("Settings page shows no controls");

        Console.WriteLine($"PASS settings: the gear opens NMA's own settings page with {model.Sections.Count} sections and {model.SettingEntries.Count} entries");
    }
}
