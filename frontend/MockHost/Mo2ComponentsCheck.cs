using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.Alerts;
using NexusMods.App.UI.Controls.PageHeader;

namespace Mo2.Frontend;

// Opt-in check for the shared-control gallery: opens it from the home menu,
// confirms the shared controls actually render, and exercises the dismissible
// hint without leaving the user's preference changed.
internal static class Mo2ComponentsCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window, string? screenshot)
    {
        await Task.Delay(1500);
        live.ShowHome();
        live.OpenComponents();
        Mo2ComponentsView? view = null;
        for (var attempt = 0; attempt < 60 && view is null; attempt++) {
            await Task.Delay(100);
            view = window.GetVisualDescendants().OfType<Mo2ComponentsView>().FirstOrDefault();
        }
        if (view is null) throw new Exception("Components page did not open");
        await Task.Delay(1500);

        var sections = view.GetVisualDescendants().OfType<Border>().Where(x => x.Name == "ComponentSection").ToArray();
        if (sections.Length < 10) throw new Exception("Expected the full section list, found " + sections.Length);
        if (sections.Any(x => x.Bounds.Width <= 0)) throw new Exception("A section has no width");

        var buttons = view.GetVisualDescendants().OfType<StandardButton>().ToArray();
        if (buttons.Length < 45) throw new Exception("Button matrix is incomplete: " + buttons.Length);
        if (!buttons.Any(x => !x.IsEnabled)) throw new Exception("No disabled button sample");
        if (view.GetVisualDescendants().OfType<Toolbar>().Count() < 2) throw new Exception("Toolbar samples missing");
        if (!view.GetVisualDescendants().OfType<PageHeader>().Any()) throw new Exception("Page header sample missing");
        if (!view.GetVisualDescendants().OfType<EmptyState>().Any(x => x.IsActive)) throw new Exception("Empty state sample missing");
        var alerts = view.GetVisualDescendants().OfType<Alert>().ToArray();
        if (alerts.Count(x => x.Name != "UsageAlert") < 4) throw new Exception("Expected one sample per alert severity");

        var usage = alerts.SingleOrDefault(x => x.Name == "UsageAlert") ?? throw new Exception("Did you know component missing");
        var settings = usage.AlertSettings ?? throw new Exception("Did you know hint has no persisted settings");
        var dismissedBefore = settings.IsDismissed;
        try {
            if (dismissedBefore) { settings.ShowAlert(); await Task.Delay(100); }
            if (!usage.IsVisible) throw new Exception("Restored hint stayed hidden");
            usage.DismissCommand.Execute().Subscribe();
            await Task.Delay(100);
            if (!settings.IsDismissed || usage.IsVisible) throw new Exception("Dismissing the hint did not hide it");
            settings.ShowAlert();
            await Task.Delay(100);
            if (settings.IsDismissed || !usage.IsVisible) throw new Exception("Restoring the hint did not show it again");
        } finally {
            if (dismissedBefore) settings.DismissAlert(); else settings.ShowAlert();
        }

        if (screenshot is not null) {
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
            bitmap.Render(window);
            bitmap.Save(screenshot);
        }
        Console.WriteLine($"PASS components gallery: {sections.Length} sections, {buttons.Length} StandardButton samples, " +
            "shared Toolbar/PageHeader/EmptyState/Alert render, hint dismissed and restored, preference left unchanged");
    }
}
