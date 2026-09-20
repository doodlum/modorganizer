using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// The information panel, drawn and photographed.
//
// Its tabs are checked elsewhere for what they read; this is for how they look,
// which is the part a list of assertions cannot judge. Every tab is selected in
// turn, given a moment to lay out, and captured, so the panel can be compared
// against the rest of the application rather than against a description of it.
internal static class Mo2ModInfoShotCheck
{
    public static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        var output = Environment.GetEnvironmentVariable("MO2_SCREENSHOT");
        if (output is not { Length: > 0 }) throw new Exception("Set MO2_SCREENSHOT to where the captures should go");

        // The panel is drawn from what MO2 has reported, so there is nothing to draw
        // until the bridge has answered.
        for (var attempt = 0; attempt < 900 && !(live.Profile.IsConnected && live.Profile.Mods.Count > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();

        var mod = live.Profile.Mods.FirstOrDefault(x => x.IsRegular && x.Name == "Just Assorted Mods")
            ?? live.Profile.Mods.FirstOrDefault(x => x.IsRegular)
            ?? throw new Exception("This profile has no ordinary mod to open the panel on");

        if (live.Profile.ShowModInformation is not { } show) throw new Exception("The frontend has no information panel");
        var showing = show(mod.Id);
        TabControl? tabs = null;
        for (var attempt = 0; attempt < 80 && tabs is null; attempt++) {
            await Task.Delay(100);
            tabs = Windows().SelectMany(x => x.GetVisualDescendants().OfType<TabControl>())
                .FirstOrDefault(x => x.Name == "Mo2ModInfoTabs");
        }
        if (tabs is null) throw new Exception("The information panel did not open");
        var host = tabs.GetVisualAncestors().OfType<Window>().First();
        Console.WriteLine($"  panel open, {tabs.Items.Count} tabs, window {host.Bounds.Width:0}x{host.Bounds.Height:0}");

        for (var index = 0; index < tabs.Items.Count; index++) {
            tabs.SelectedIndex = index;
            for (var settle = 0; settle < 12; settle++) { host.UpdateLayout(); await Task.Delay(60); }
            var header = (tabs.Items[index] as TabItem)?.Header?.ToString() ?? index.ToString();
            var name = new string(header.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
            var size = new PixelSize(Math.Max(1, (int)host.Bounds.Width), Math.Max(1, (int)host.Bounds.Height));
            using var capture = new RenderTargetBitmap(size);
            capture.Render(host);
            capture.Save(Path.ChangeExtension(output, name + ".png"));
            Console.WriteLine($"    {header}");
        }

        foreach (var close in Windows().SelectMany(x => x.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.StandardButton>())
                     .Where(x => x.Text is "OK" or "Ok").ToArray()) {
            close.Command?.Execute(close.CommandParameter);
            break;
        }
        await Task.WhenAny(showing, Task.Delay(TimeSpan.FromSeconds(5)));
        Console.WriteLine($"PASS mod info shot: every tab of the information panel captured beside {output}");
    }

    private static Window[] Windows() =>
        (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
        ?.Windows.ToArray() ?? [];
}
