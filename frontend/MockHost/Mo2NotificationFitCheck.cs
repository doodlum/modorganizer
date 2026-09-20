using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using System.Reflection;

namespace Mo2.Frontend;

internal static class Mo2NotificationFitCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, string output)
    {
        using var turn = await Mo2CheckTurn.Take();
        Directory.CreateDirectory(output);
        var profile = new Mo2LiveProfile("notification-fit-fixture");
        typeof(Mo2LiveProfile).GetProperty(nameof(profile.ProfilePath))!.SetValue(profile, "Default");
        typeof(Mo2LiveProfile).GetProperty(nameof(profile.OriginalUiVisible))!.SetValue(profile, false);
        typeof(Mo2LiveProfile).GetField("_lastSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(profile, "fixture");
        var reports = Enumerable.Range(1, 8).Select(i => (
            Title: $"Warning {i}: a long diagnostic title that must wrap without hiding the expand control",
            Details: "A diagnostic extension reported a missing requirement. Review the affected mods and their dependencies in Health Check before launching the game.")).ToArray();
        using var notifications = new Mo2Notifications(shell, profile, () => Task.FromResult(reports));
        var button = notifications.Button;
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.VerticalAlignment = VerticalAlignment.Bottom;
        button.Margin = new Thickness(12);
        var window = new Window { Content = button, ShowInTaskbar = false, SystemDecorations = SystemDecorations.None };
        var flyout = (Flyout)button.Flyout!;
        try {
            window.Show();
            await Task.Delay(250);
            foreach (var size in new[] { new Size(1280, 750), new Size(640, 480), new Size(320, 240) }) {
                window.Width = size.Width; window.Height = size.Height;
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (Math.Abs(window.ClientSize.Width - size.Width) > 1 || Math.Abs(window.ClientSize.Height - size.Height) > 1) {
                    if (DateTime.UtcNow > deadline) throw new Exception($"Window resize did not settle at {size}; actual {window.ClientSize}");
                    await Task.Delay(50);
                }
                window.UpdateLayout();
                flyout.ShowAt(button);
                await Task.Delay(200);
                var content = (Control)flyout.Content!;
                var popup = content.GetVisualRoot() as Control ?? throw new Exception("Notification popup did not attach");
                popup.GetVisualDescendants().OfType<Expander>().First().IsExpanded = true;
                await Task.Delay(250); window.UpdateLayout();
                if (popup.Bounds.Width > window.ClientSize.Width + 1 || popup.Bounds.Height > window.ClientSize.Height + 1)
                    throw new Exception($"Notification popup {popup.Bounds.Size} exceeds window {window.ClientSize}");
                var scroll = content.GetVisualDescendants().OfType<ScrollViewer>().First();
                if (scroll.Viewport.Height <= 0 || scroll.Extent.Height <= scroll.Viewport.Height)
                    throw new Exception("Long notification list has no scrollable viewport");
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(popup.Bounds.Width), (int)Math.Ceiling(popup.Bounds.Height)));
                bitmap.Render(popup); bitmap.Save(Path.Combine(output, $"notifications-{size.Width:0}x{size.Height:0}.png"));
                Console.WriteLine($"PASS notification fit: window {window.ClientSize}, popup {popup.Bounds.Size}, expanded report and scrollable list");
                flyout.Hide();
            }
        } finally { flyout.Hide(); window.Close(); }
    }
}
