using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// A separator's colour, end to end: MO2's own "Select Color..." sets it, the
// bridge reads it back off the row MO2 draws, and the bar on the mod list is
// painted in it.
//
// The frontend drew every separator in the same grey, because the colour never
// left MO2: nothing in the snapshot carried it. A check that only compared what
// the frontend painted against what the frontend held would have passed on that,
// so this one goes through MO2 and puts the colour back when it is done.
internal static class Mo2SeparatorColorCheck
{
    private const string Chosen = "#B91C1C";

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        var profile = live.Profile;
        for (var attempt = 0; attempt < 300 && !(profile.IsConnected && profile.CanChangeOriginalUi); attempt++)
            await Task.Delay(100);
        if (!profile.CanChangeOriginalUi) throw new Exception("MO2 is not ready to be changed");
        if (profile.Mods.FirstOrDefault(x => x.IsSeparator) is not { } separator)
            { Console.WriteLine("SKIP separator colour: this profile has no separator"); return; }

        var target = profile.CurrentTarget;
        var original = separator.Color;
        var faults = new List<string>();
        using var turn = await Mo2CheckTurn.Take();
        live.ShowProfile();
        await Navigate(live.ProfileMenu.LeftMenuItemLoadout);
        await Task.Delay(400);

        try {
            await profile.SetModColor(separator.Name, Chosen, target);
            await Until(() => profile.Mods.FirstOrDefault(x => x.Name == separator.Name)?.Color.Length > 0,
                "MO2 did not report a colour for " + separator.Name);
            var reported = profile.Mods.First(x => x.Name == separator.Name).Color;
            if (!Color.TryParse(reported, out var parsed) || !Color.TryParse(Chosen, out var wanted) || parsed != wanted)
                faults.Add($"MO2 reports {reported}, not the {Chosen} it was given");

            // And the bar the list actually draws, which is the point of reading it.
            Border? bar = null;
            await Until(() => (bar = Bar(window, separator.Name)) is not null, "The separator's bar was never drawn");
            if ((bar!.Background as ISolidColorBrush)?.Color != parsed)
                faults.Add($"the bar is painted {(bar.Background as ISolidColorBrush)?.Color.ToString() ?? "nothing"}, not {reported}");
            var title = bar.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(x => x.Name == "SeparatorTitle");
            if ((title?.Foreground as ISolidColorBrush)?.Color != ((ISolidColorBrush)Mo2Density.Ink(parsed)).Color)
                faults.Add("the separator's name is not drawn in the ink that reads against its colour");
        } finally {
            await profile.SetModColor(separator.Name, original.Length > 0 ? original : null, target);
            await Task.Delay(300);
        }

        var restored = profile.Mods.FirstOrDefault(x => x.Name == separator.Name)?.Color ?? "";
        if (restored != original) faults.Add($"the separator was left {(restored.Length > 0 ? restored : "uncoloured")}, not as it was found");

        if (faults.Count > 0) Console.WriteLine("FAIL separator colour: " + string.Join("; ", faults));
        else Console.WriteLine($"PASS separator colour: MO2 coloured {separator.Name} {Chosen} through its own action, " +
            "the bridge read it back, the bar was painted in it with readable ink, and it was put back as it was found");

        static Border? Bar(Window window, string name) =>
            window.GetVisualDescendants().OfType<TextBlock>()
                .FirstOrDefault(x => x.Name == "SeparatorTitle" && (string?)x.Tag == name)
                ?.GetVisualAncestors().OfType<Border>().FirstOrDefault(x => x.Name == "ModSeparatorBar");

        static async Task Until(Func<bool> ready, string failure)
        {
            for (var attempt = 0; attempt < 200; attempt++) {
                if (ready()) return;
                await Task.Delay(100);
            }
            throw new Exception(failure);
        }

        static async Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel? item)
        {
            if (item is null) return;
            await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
                item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
            await Task.Delay(300);
        }
    }
}
