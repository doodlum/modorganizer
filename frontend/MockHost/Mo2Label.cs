using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Mo2.Frontend;

// The Label chip from the reference sheets, which the shared library has no
// control for: a small rounded tag in a brand colour, in a strong, weak or
// outlined fill. Built from theme brushes so it follows the app's palette.
internal sealed class Mo2Label : Border
{
    internal enum Brands { Neutral, Info, Success, Warning, Danger, Premium }
    internal enum Fills { Strong, Weak, Outline }

    private static IBrush Brush(string key) => (IBrush)Application.Current!.FindResource(key)!;

    private static (string Fill, string Text) Palette(Brands brand) => brand switch {
        Brands.Info => ("InfoTranslucentSubduedBrush", "InfoStrongBrush"),
        Brands.Success => ("SuccessTranslucentSubduedBrush", "SuccessStrongBrush"),
        Brands.Warning => ("WarningTranslucentSubduedBrush", "WarningStrongBrush"),
        Brands.Danger => ("DangerTranslucentSubduedBrush", "DangerStrongBrush"),
        Brands.Premium => ("PremiumTranslucentSubduedBrush", "PremiumStrongBrush"),
        _ => ("NeutralTranslucentSubduedBrush", "NeutralStrongBrush"),
    };

    // Not every palette exists in every theme; fall back to neutral rather than
    // throwing a resource lookup at runtime.
    private static IBrush Resolve(string key, string fallback) =>
        Application.Current!.TryFindResource(key, out var found) && found is IBrush brush ? brush : Brush(fallback);

    internal Mo2Label(string text, Brands brand = Brands.Neutral, Fills fill = Fills.Weak)
    {
        var (fillKey, textKey) = Palette(brand);
        var accent = Resolve(textKey, "NeutralStrongBrush");
        var plate = Resolve(fillKey, "NeutralTranslucentSubduedBrush");
        Height = 20;
        Padding = new Thickness(6, 0);
        CornerRadius = new CornerRadius(10);
        VerticalAlignment = VerticalAlignment.Center;
        Background = fill == Fills.Outline ? Brushes.Transparent : plate;
        BorderBrush = fill == Fills.Outline ? accent : Brushes.Transparent;
        BorderThickness = new Thickness(fill == Fills.Outline ? 1 : 0);
        Child = new TextBlock {
            Text = text, Foreground = fill == Fills.Strong ? Brush("NeutralStrongBrush") : accent,
            VerticalAlignment = VerticalAlignment.Center,
            Theme = (ControlTheme)Application.Current!.FindResource("BodySMMediumTheme")!,
        };
        if (fill == Fills.Strong) Background = accent;
    }
}
