using Avalonia.Controls;
using NexusMods.App.UI.WorkspaceSystem;
using System.Reactive.Threading.Tasks;

namespace Mo2.Frontend;

// Selecting a game in the spine has to connect to that game's MO2 and leave the
// app in that game's own workspace, not Home. This drives the spine item itself,
// so it covers the command the sidebar actually carries.
internal static class Mo2SelectGameCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        var spine = new Mo2Spine(live);
        for (var attempt = 0; attempt < 120 && spine.LoadoutSpineItems.Count == 0; attempt++) {
            await Task.Delay(250);
            live.RefreshCatalog();
        }
        if (spine.LoadoutSpineItems.Count == 0) throw new Exception("No game in the spine to select");
        var item = spine.LoadoutSpineItems[0];
        var game = item.Name;

        if (live.WorkspaceController.ActiveWorkspace.Context is not HomeContext)
            throw new Exception("The app did not start on Home");

        await item.Click.Execute().ToTask();

        // Starting MO2 and waiting for its bridge is slow, and slower again under
        // emulation, so this waits in minutes rather than seconds and says where it
        // has got to rather than reporting nothing until it gives up.
        for (var attempt = 0; attempt < 400 && !live.Profile.IsConnected; attempt++) {
            await Task.Delay(500);
            if (attempt % 20 == 19) Console.WriteLine($"  waiting {(attempt + 1) / 2}s: connected={live.Profile.IsConnected} status={live.Profile.Status}");
        }
        if (!live.Profile.IsConnected) throw new Exception($"Selecting {game} did not connect to its MO2 host: {live.Profile.Status}");

        for (var attempt = 0; attempt < 120 && live.WorkspaceController.ActiveWorkspace.Context is HomeContext; attempt++)
            await Task.Delay(250);
        if (live.WorkspaceController.ActiveWorkspace.Context is not Mo2WorkspaceContext)
            throw new Exception($"Selecting {game} left the app in {live.WorkspaceController.ActiveWorkspace.Context?.GetType().Name}, not its own workspace");

        // Opening a game's profiles has to land inside that game rather than on a
        // Home page beside it, and has to leave the game selected in the spine.
        if (!await live.SelectGame(game, openProfiles: true))
            throw new Exception($"Opening {game} profiles did not select the game");
        static bool ShowsProfiles(Mo2LiveWorkspace live) => live.WorkspaceController.ActiveWorkspace.Panels
            .Any(panel => panel.SelectedTab.Contents.ViewModel is Mo2LoadoutsPage { GameScoped: true });
        for (var attempt = 0; attempt < 60 && !ShowsProfiles(live); attempt++) await Task.Delay(250);
        if (live.WorkspaceController.ActiveWorkspace.Context is not Mo2WorkspaceContext)
            throw new Exception("Opening profiles left the game's workspace");
        if (!ShowsProfiles(live)) {
            var showing = string.Join(", ", live.WorkspaceController.ActiveWorkspace.Panels
                .Select(panel => panel.SelectedTab.Contents.ViewModel?.GetType().Name ?? "none"));
            throw new Exception($"Opening profiles did not show the game's profiles page; panels show {showing}");
        }

        window.UpdateLayout();
        Console.WriteLine($"  game={game} profilePath={live.Profile.ProfilePath} mods={live.Profile.Mods.Count} executables={live.Profile.Executables.Count}");
        Console.WriteLine($"PASS select game: {game} connects to its own MO2 host and opens its own workspace, separate from Home");
    }
}
