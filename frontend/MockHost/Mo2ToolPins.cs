using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using NexusMods.UI.Sdk.Icons;
using System.Text.Json;

namespace Mo2.Frontend;

internal static class Mo2ToolPins
{
    public static string Path => System.IO.Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mo2-nexus-frontend", "tool-pins.json");
    public static event Action? Changed;
    public static string[] Read() => File.Exists(Path) ? JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path)) ?? [] : [];
    public static void Save(IEnumerable<string> pins) {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path + ".tmp", JsonSerializer.Serialize(pins)); File.Move(Path + ".tmp",Path,true); Changed?.Invoke();
    }
}
internal static class Mo2ToolIcons
{
    private static readonly Dictionary<string,Bitmap> Images = new();
    public static Control Create(string encoded, double size) {
        if (encoded.Length > 0) {
            try {
                if (!Images.TryGetValue(encoded,out var image)) Images[encoded] = image = new Bitmap(new MemoryStream(Convert.FromBase64String(encoded)));
                return new Image { Source = image, Width = size, Height = size, Stretch = Stretch.Uniform };
            } catch (Exception error) when (error is FormatException or ArgumentException) { }
        }
        return new UnifiedIcon { Value = Mo2ToolsPage.ToolIcon, Width = size, Height = size };
    }
}
