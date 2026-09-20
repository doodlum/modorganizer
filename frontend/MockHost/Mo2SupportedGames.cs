using System.Reactive;
using Avalonia.Media.Imaging;
using NexusMods.Abstractions.GameLocators;
using NexusMods.Abstractions.Games;
using NexusMods.App.UI.Controls.MiniGameWidget.Standard;
using NexusMods.UI.Sdk;
using ReactiveUI;

namespace Mo2.Frontend;

// More than one Steam app id per game because the editions MO2 treats as one game
// ship separately: Fallout 3 and its Game of the Year edition, Oblivion and its
// Deluxe reissue. The first id that resolves wins.
// IconUrl covers the games Steam cannot supply an icon for. Tale of Two Wastelands
// is the case in point: it is an overhaul that runs on New Vegas, so it has no
// Steam app of its own and would otherwise borrow New Vegas's icon.
internal sealed record Mo2SupportedGame(string Name, string? NexusDomain, string[] SteamAppIds, string? Executable, string? IconUrl = null)
{
    // Icons are keyed by game, not by Nexus domain: TTW and New Vegas share a
    // domain but are separate rows with separate artwork.
    internal string IconKey => new string(Name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray())
        .Trim('-').Replace("--", "-").Replace("--", "-");

    // What MO2's own game plugin calls this game, which is what goes in
    // ModOrganizer.ini and what the bridge reports back. Most match the name shown
    // here; the two that do not are spelled out at their entry.
    internal string Mo2GameName => Mo2Game ?? Name;
    internal string? Mo2Game { get; init; }
}

// NMA lists the games it supports but has not found below the ones it has. MO2
// has no equivalent catalogue to read: the bridge reports only the single managed
// game of a connected instance, so nothing can enumerate the rest, and onboarding
// needs this list precisely when no instance is connected yet. These are MO2's own
// bundled game plugins, named as MO2 names them.
internal static class Mo2SupportedGames
{
    internal static readonly Mo2SupportedGame[] All = [
        new("Morrowind", "morrowind", ["22320"], "Morrowind.exe"),
        new("Oblivion", "oblivion", ["22330", "900883"], "Oblivion.exe"),
        new("Fallout 3", "fallout3", ["22370", "22300"], "Fallout3.exe"),
        new("Fallout: New Vegas", "newvegas", ["22380"], "FalloutNV.exe") { Mo2Game = "New Vegas" },
        new("Fallout 4", "fallout4", ["377160"], "Fallout4.exe"),
        new("Fallout 4 VR", "fallout4vr", ["611660"], "Fallout4VR.exe"),
        new("Skyrim", "skyrim", ["72850"], "TESV.exe"),
        new("Skyrim Special Edition", "skyrimspecialedition", ["489830"], "SkyrimSE.exe"),
        new("Skyrim VR", "skyrimvr", ["611670"], "SkyrimVR.exe"),
        new("Starfield", "starfield", ["1716740"], "Starfield.exe"),
        new("Enderal", "enderal", ["933480"], "TESV.exe"),
        new("Enderal Special Edition", "enderalspecialedition", ["976620"], "SkyrimSE.exe"),
        // No Steam app of its own: TTW is an overhaul installed by hand onto a copy
        // of New Vegas. Detecting it from New Vegas's app id would claim it is
        // installed whenever New Vegas is, and list the same game twice.
        new("Tale of Two Wastelands", "newvegas", [], "FalloutNV.exe",
            "https://cdn2.steamgriddb.com/icon/218936f8ceddcffa9b096d796546a7ca.png") { Mo2Game = "TTW" },
    ];

    internal static Mo2SupportedGame? Find(string game)
    {
        if (string.IsNullOrWhiteSpace(game)) return null;
        return All.FirstOrDefault(entry => string.Equals(entry.Name, game, StringComparison.OrdinalIgnoreCase))
            // MO2 reports New Vegas under more than one spelling, so match the tail too.
            ?? All.FirstOrDefault(entry => entry.Name.EndsWith(game, StringComparison.OrdinalIgnoreCase)
                || game.EndsWith(entry.Name, StringComparison.OrdinalIgnoreCase));
    }

    // Games neither registered with MO2 nor installed on this machine, in MO2's
    // own order. A game that is sitting on disk has been found, whether or not an
    // MO2 instance manages it yet, so it does not belong in a "not found" list.
    internal static Mo2SupportedGame[] Missing(IEnumerable<string> accountedFor)
    {
        var known = accountedFor.Select(Find).Where(entry => entry is not null)
            .Select(entry => entry!.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return All.Where(entry => !known.Contains(entry.Name)).ToArray();
    }

    // Supported games this machine actually has, found through Steam. A game with
    // no Steam app of its own is never detected this way, which is what keeps TTW
    // from being reported as installed because New Vegas is.
    internal static Mo2SupportedGame[] Installed() =>
        All.Where(entry => ExecutablePath(entry.Name) is not null).ToArray();

    // The installed game executable, when Steam has it. Games MO2 supports but the
    // machine does not have simply have no executable to take an icon from.
    internal static string? ExecutablePath(string game)
    {
        var entry = Find(game);
        if (entry?.Executable is null) return null;
        foreach (var appId in entry.SteamAppIds) {
            var directory = Mo2SteamPaths.InstallDirectory(appId);
            if (directory is null) continue;
            var path = Path.Combine(directory, entry.Executable);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    // Steam's cached artwork for a game, whichever edition is present.
    internal static string? LibraryArt(string game)
    {
        var entry = Find(game);
        if (entry is null) return null;
        foreach (var appId in entry.SteamAppIds)
            if (Mo2SteamPaths.LibraryArt(appId) is { } art) return art;
        return null;
    }
}

// Supplies the native MiniGameWidget directly, the way Mo2GameCard supplies the
// native GameWidget. Game stays null: the widget only reads it to decide which
// store row to show, and MO2 does not tell us where an unregistered game lives.
internal sealed class Mo2MiniGameCard : AViewModel<IMiniGameWidgetViewModel>, IMiniGameWidgetViewModel
{
    private static readonly Uri SupportedGamesUri = new("https://github.com/ModOrganizer2/modorganizer/wiki");
    public IGame? Game { get; set; }
    public GameInstallation[]? GameInstallations { get; set; }
    public string Name { get; set; }
    public bool IsFound { get; set; }
    public Bitmap Image { get; }
    public ReactiveCommand<Unit, Unit> GiveFeedbackCommand { get; }
    public Mo2MiniGameCard(Mo2SupportedGame game, Action<Uri> open)
    {
        Name = game.Name;
        Image = Mo2GameArt.SquareIcon(game.Name);
        GiveFeedbackCommand = ReactiveCommand.Create(() => open(SupportedGamesUri));
    }
}
