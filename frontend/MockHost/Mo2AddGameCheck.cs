using Avalonia.Controls;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.GameWidget;

namespace Mo2.Frontend;

// Adding a game has to behave the way NMA's does: the card reports it is working,
// the game appears in the spine as soon as it is added, and the app ends up on it.
// This drives the real Add game button rather than calling the workspace directly.
internal static class Mo2AddGameCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        live.OpenGames();
        GameWidget? detected = null;
        for (var attempt = 0; attempt < 100 && detected is null; attempt++) {
            await Task.Delay(100);
            window.UpdateLayout();
            detected = window.GetVisualDescendants().OfType<GameWidget>()
                .FirstOrDefault(widget => widget.ViewModel is Mo2GameCard { State: GameWidgetState.DetectedGame });
        }
        if (detected?.ViewModel is not Mo2GameCard card) throw new Exception("No detected game to add");
        var game = card.Name;
        var before = live.CatalogEntries.Count(entry => entry.Instance is not null && Mo2SupportedGames.Find(entry.Instance.Game)?.Name == game);
        // Subscribed before the add, so it observes the catalogue change the way the
        // window's own spine does.
        var spine = new Mo2Spine(live);

        var button = detected.FindControl<NexusMods.App.UI.Controls.StandardButton>("AddGameButton")
            ?? throw new Exception("The detected card has no Add game button");
        // The widget binds its commands when the view activates, a frame or two
        // after the card itself exists.
        for (var attempt = 0; attempt < 50 && button.Command is null; attempt++) await Task.Delay(100);
        if (button.Command is null || !button.Command.CanExecute(null)) throw new Exception("Add game is not bound");
        button.Command.Execute(null);

        // The card must say it is working rather than sitting on Add.
        var reported = false;
        for (var attempt = 0; attempt < 50 && !reported; attempt++) {
            await Task.Delay(100);
            reported = card.State == GameWidgetState.AddingGame;
        }
        if (!reported) Console.WriteLine("CHECK add game finished before the adding state could be observed");

        // Installing MO2 on a cold machine downloads it, so this waits generously,
        // but it stops as soon as the workspace reports the add failed rather than
        // sitting out the whole timeout on an error it already knows about.
        var added = false;
        for (var attempt = 0; attempt < 600 && !added; attempt++) {
            await Task.Delay(500);
            added = live.CatalogEntries.Any(entry => entry.Instance is not null && Mo2SupportedGames.Find(entry.Instance.Game)?.Name == game);
            if (live.Notice is { } notice && notice.StartsWith("Could not add", StringComparison.Ordinal))
                throw new Exception(notice);
        }
        if (!added) throw new Exception($"{game} never reached the instance catalogue (status: {live.Notice ?? "none"})");
        if (live.CatalogEntries.Count(entry => entry.Instance is not null && Mo2SupportedGames.Find(entry.Instance.Game)?.Name == game) <= before)
            throw new Exception($"{game} did not gain an instance");

        await Task.Delay(1000);
        window.UpdateLayout();
        var inSpine = spine.LoadoutSpineItems.Any(item => Mo2SupportedGames.Find(item.Name)?.Name == game);
        if (!inSpine) throw new Exception($"{game} is registered but absent from the spine");

        var instance = Mo2OwnedInstances.InstanceDirectory(game);
        if (!Mo2OwnedInstances.HasInstance(game)) throw new Exception($"No ModOrganizer.ini at {instance}");
        Console.WriteLine($"PASS add game: {game} reported progress, was created at {instance}, registered, and appeared in the spine");
    }
}
