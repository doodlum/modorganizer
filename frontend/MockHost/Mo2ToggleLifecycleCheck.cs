using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal static class Mo2ToggleLifecycleCheck
{
    internal static async Task Run(Window window)
    {
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 1, Height = 1, IsHitTestVisible = false };
        root.Children.Add(host);
        var toggle = Mo2ModRow.Activation("LifecycleToggle", "fixture", false, true, () => Task.FromResult(true));
        async Task<UnifiedIcon> Icon() {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (true) {
                var icon = toggle.GetVisualDescendants().OfType<UnifiedIcon>().FirstOrDefault();
                if (icon is not null) return icon;
                if (DateTime.UtcNow > until) throw new Exception("Toggle template did not load");
                await Task.Delay(25);
            }
        }
        try {
            host.Children.Add(toggle);
            var icon = await Icon();
            if (icon.Opacity != 0) throw new Exception("Unchecked icon initially visible");
            for (var i = 0; i < 4; i++) {
                host.Children.Remove(toggle);
                var previous = icon.Opacity;
                toggle.IsChecked = toggle.IsChecked != true;
                if (icon.Opacity != previous) throw new Exception("Detached icon still receives toggle changes");
                host.Children.Add(toggle);
                icon = await Icon();
                if (icon.Opacity != (toggle.IsChecked == true ? 1 : 0))
                    throw new Exception("Reattached icon did not synchronize state");
            }
            var template = toggle.Template;
            for (var i = 0; i < 4; i++) {
                var replaced = icon;
                var previous = replaced.Opacity;
                toggle.Template = new FuncControlTemplate<ToggleButton>((_, _) => new Border());
                toggle.ApplyTemplate();
                toggle.IsChecked = toggle.IsChecked != true;
                if (replaced.Opacity != previous) throw new Exception("Replaced template still receives toggle changes");
                toggle.Template = template;
                toggle.ApplyTemplate();
                icon = await Icon();
                if (ReferenceEquals(icon, replaced) || icon.Opacity != (toggle.IsChecked == true ? 1 : 0))
                    throw new Exception("Replacement toggle template did not synchronize state");
            }
            Console.WriteLine("PASS activation toggle lifecycle: detached/replaced icons stop observing; reattached/current icons synchronize state");
        } finally { root.Children.Remove(host); }
    }
}
