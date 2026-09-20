using Avalonia.Controls;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.GameWidget;
using NexusMods.App.UI.Controls.MiniGameWidget.Standard;

namespace Mo2.Frontend;

// Reports what My Games actually renders, against the geometry NMA's own markup
// declares for it. The widgets are upstream views driven by frontend view models,
// so anything off here is what the models feed them, not the markup.
internal static class Mo2GameWidgetCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        live.OpenGames();
        // The widget is realised before its view model binds, so measuring on the
        // first frame it exists reports an empty card and passes on nothing.
        static bool Ready(Window window) => window.GetVisualDescendants().OfType<GameWidget>()
            .Any(widget => widget.FindControl<Image>("GameImage") is { Source: not null, Bounds.Width: > 0 });
        for (var attempt = 0; attempt < 100 && !Ready(window); attempt++) {
            await Task.Delay(100);
            window.UpdateLayout();
        }
        window.UpdateLayout();
        if (!Ready(window)) throw new Exception("No game tile ever drew: the card is realised but its image never bound");

        var faults = new List<string>();
        foreach (var widget in window.GetVisualDescendants().OfType<GameWidget>().Where(x => x.Bounds.Height > 0)) {
            var card = widget.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == "GameWidgetBorder");
            var image = widget.FindControl<Image>("GameImage");
            var section = widget.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == "ImageSectionBorder");
            var store = widget.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == "StoreBackground");
            var version = widget.FindControl<TextBlock>("VersionTextBlock");
            var add = widget.FindControl<StandardButton>("AddGameButton");
            Console.WriteLine($"  card={card?.Bounds.Size} image={image?.Bounds.Size} source={(image?.Source as Avalonia.Media.Imaging.Bitmap)?.PixelSize} " +
                $"store={store?.Bounds.Size} add={add?.Bounds.Size} version=\"{version?.Text}\" versionBounds={version?.Bounds.Size}");

            // NMA fixes the card at 140 and decodes its tile to 140x210.
            if (card is not null && Math.Abs(card.Bounds.Width - 140) > 1) faults.Add($"card is {card.Bounds.Width} wide, not 140");
            if (image is null || image.Source is null || image.Bounds.Width <= 0) faults.Add("the card has no tile image");
            else {
                if (Math.Abs(image.Bounds.Width - 140) > 1) faults.Add($"tile is {image.Bounds.Width} wide, not 140");
                if (Math.Abs(image.Bounds.Height - 210) > 2) faults.Add($"tile is {image.Bounds.Height} tall, not 210");
            }
            if (store is not null && (Math.Abs(store.Bounds.Width - 36) > 1 || Math.Abs(store.Bounds.Height - 36) > 1))
                faults.Add($"store badge is {store.Bounds.Size}, not 36x36");
            if (section is not null && Math.Abs(section.Margin.Bottom - 12) > .5) faults.Add($"image section margin is {section.Margin.Bottom}, not 12");
            if (add is not null && add.IsVisible && add.Bounds.Width > 0 && Math.Abs(add.Bounds.Width - 140) > 1)
                faults.Add($"Add game button is {add.Bounds.Width} wide, not the full 140");
            if (version is not null && string.IsNullOrWhiteSpace(version.Text)) faults.Add("the card shows no version line");
        }

        foreach (var mini in window.GetVisualDescendants().OfType<MiniGameWidget>().Where(x => x.Bounds.Height > 0).Take(3)) {
            var border = mini.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == "MiniGameWidgetBorder");
            var image = mini.FindControl<Image>("GameImage");
            Console.WriteLine($"  mini card={border?.Bounds.Size} image={image?.Bounds.Size} source={(image?.Source as Avalonia.Media.Imaging.Bitmap)?.PixelSize}");
            if (border is not null && Math.Abs(border.Bounds.Width - 194) > 1) faults.Add($"mini card is {border.Bounds.Width} wide, not 194");
            if (image is null || image.Source is null || image.Bounds.Width <= 0) faults.Add("a mini card has no icon");
            else if (Math.Abs(image.Bounds.Width - 48) > 1 || Math.Abs(image.Bounds.Height - 48) > 1)
                faults.Add($"mini icon is {image.Bounds.Size}, not 48x48");
        }

        if (faults.Count > 0) throw new Exception("My Games geometry: " + string.Join("; ", faults.Distinct()));
        Console.WriteLine("PASS my games geometry matches NMA: 140 wide cards with 140x210 tiles, 36x36 store badges, 194 wide mini cards with 48x48 icons");
    }
}
