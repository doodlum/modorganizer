using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Mo2.Frontend;

// A modlist's own picture, for the browser.
//
// Fetched once and kept on disk, because the browser draws two hundred rows and
// each picture lives on whichever server its author chose. Nothing blocks on it:
// a row draws immediately with the game's icon and swaps in the author's artwork
// if and when it arrives, so a slow or missing server costs nothing but a plainer
// row.
internal static class Mo2ModlistArt
{
    private static string CacheRoot => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CACHE_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache"),
        "mo2-nexus-frontend", "modlist-art");

    private static readonly Dictionary<string, Bitmap?> Loaded = new();
    private static readonly SemaphoreSlim Limit = new(4);

    private static string PathFor(Mo2Modlist list) => Path.Combine(CacheRoot, list.InstanceKey + ".image");

    internal static async Task Load(Mo2Modlist list, Action<Bitmap?> ready)
    {
        if (list.ImageUrl is not { Length: > 0 } url) return;
        lock (Loaded)
            if (Loaded.TryGetValue(list.InstanceKey, out var cached)) { ready(cached); return; }

        Bitmap? bitmap = null;
        try {
            var path = PathFor(list);
            if (!File.Exists(path)) {
                await Limit.WaitAsync();
                try {
                    if (!File.Exists(path)) {
                        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("mo2-nexus-frontend");
                        var bytes = await client.GetByteArrayAsync(url);
                        Directory.CreateDirectory(CacheRoot);
                        await File.WriteAllBytesAsync(path + ".part", bytes);
                        File.Move(path + ".part", path, overwrite: true);
                    }
                } finally { Limit.Release(); }
            }
            // Decoded to the size a row draws it at rather than full size: these are
            // gallery banners, and two hundred of them at full resolution is a great
            // deal of memory for a list of names.
            await using var file = File.OpenRead(path);
            bitmap = Bitmap.DecodeToWidth(file, 144);
        } catch (Exception) { bitmap = null; }

        lock (Loaded) Loaded[list.InstanceKey] = bitmap;
        if (bitmap is null) return;
        await Dispatcher.UIThread.InvokeAsync(() => ready(bitmap));
    }
}
