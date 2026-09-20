using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Documents;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// Opening a mod's information must put up the frontend's own dialog and leave
// MO2 silent. It used to ask MO2 for its details dialog, which a hidden host
// could never show and which held its event loop while it waited.
internal static class Mo2ModInfoCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 600 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();
        using var turn = await Mo2CheckTurn.Take();

        var mod = live.Profile.Mods.FirstOrDefault(entry => !entry.IsSeparator && !entry.IsOverwrite)
            ?? throw new Exception("No mod to show information for");
        if (live.Profile.ShowModInformation is not { } show)
            throw new Exception("The frontend has no mod information dialog; it would fall back to MO2's");

        var desktop = (IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!;
        var before = desktop.Windows.Count(x => x.IsVisible);
        var opening = show(mod.Id);

        Window? dialog = null;
        for (var attempt = 0; attempt < 80 && dialog is null; attempt++) {
            await Task.Delay(100);
            dialog = desktop.Windows.FirstOrDefault(x => x.IsVisible && !ReferenceEquals(x, window));
        }
        // The dialog draws its template before it binds its content, so a fixed
        // wait reads back placeholders. Wait for the body itself to arrive.
        for (var attempt = 0; attempt < 100; attempt++) {
            await Task.Delay(100);
            dialog?.UpdateLayout();
            if (dialog is not null && Shown(dialog).Contains("Version", StringComparison.Ordinal)) break;
        }
        var native = Mo2NativeWindows.Titles();
        if (dialog is null) throw new Exception($"No frontend dialog opened (MO2 windows: {string.Join(", ", native.DefaultIfEmpty("none"))})");
        if (native.Count > 0) throw new Exception($"MO2 put up its own window: {string.Join(", ", native)}");

        // What it is showing has to be this mod, not a placeholder.
        // The title is a property of the dialog rather than a text block in it, and
        // the body is selectable text, so both are read alongside ordinary labels.
        var shown = string.Join("\n", dialog.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? "")
            .Concat(dialog.GetVisualDescendants().OfType<SelectableTextBlock>().Select(block => block.Text ?? ""))
            .Append(dialog.Title ?? ""));
        Console.WriteLine("  dialog shows: " + shown.Replace("\n", " / ")[..Math.Min(300, shown.Length)]);
        if (!shown.Contains(mod.DisplayName, StringComparison.OrdinalIgnoreCase))
            throw new Exception($"The dialog does not name {mod.DisplayName}");
        foreach (var expected in new[] { "Version", "Category", "Priority" })
            if (!shown.Contains(expected, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"The dialog does not report {expected}");

        Console.WriteLine($"  dialog=\"{dialog.Title}\" for \"{mod.DisplayName}\", MO2 windows: none");
        foreach (var extra in desktop.Windows.Where(x => x.IsVisible && !ReferenceEquals(x, window)).ToArray()) extra.Close();
        await opening.WaitAsync(TimeSpan.FromSeconds(30));
        if (desktop.Windows.Count(x => x.IsVisible) != before) throw new Exception("The information dialog did not close");
        Console.WriteLine("PASS mod information: the frontend shows it in its own dialog and MO2 opens no window");
    }

    // The title is a property of the dialog rather than a text block in it, and
    // the body is selectable text, so both are read alongside ordinary labels.
    private static string Shown(Window dialog) => string.Join('\n',
        dialog.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? "")
            .Concat(dialog.GetVisualDescendants().OfType<SelectableTextBlock>().Select(block => block.Text ?? ""))
            .Append(dialog.Title ?? ""));
}
