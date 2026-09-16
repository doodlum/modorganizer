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
    // Two colours, not one, and on opposite sides of the luminance line MO2 picks its
    // foreground by. A single colour cannot tell a bar that carries MO2's colour from
    // one painted the same constant every time, and cannot show the name's ink follows
    // the colour at all: these two must come back with *different* ink.
    private const string Chosen = "#B91C1C";
    private const string Pale = "#E8D44D";
    // Readable is a measurement, not a restatement. Comparing the drawn ink against
    // the same Mo2Density.Ink the view calls is a tautology — a rule that picked an
    // unreadable foreground would satisfy it — so the contrast is computed here,
    // independently, and held to WCAG AA for body text.
    private const double MinimumContrast = 4.5;

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

        var inks = new List<Color>();
        var described = new List<string>();
        try {
            foreach (var choice in new[] { Chosen, Pale }) {
                await profile.SetModColor(separator.Name, choice, target);
                await Until(() => profile.Mods.FirstOrDefault(x => x.Name == separator.Name)?.Color is { Length: > 0 } held
                                  && Color.TryParse(held, out var shown) && Color.TryParse(choice, out var asked) && shown == asked,
                    $"MO2 did not report {choice} for " + separator.Name);
                var reported = profile.Mods.First(x => x.Name == separator.Name).Color;
                Color.TryParse(reported, out var parsed);

                // And the bar the list actually draws, which is the point of reading it.
                Border? bar = null;
                await Until(() => (bar = Bar(window, separator.Name)) is { } drawn
                                  && (drawn.Background as ISolidColorBrush)?.Color == parsed,
                    $"The separator's bar was never painted {reported}");
                var title = bar!.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(x => x.Name == "SeparatorTitle");
                if ((title?.Foreground as ISolidColorBrush)?.Color is not { } ink)
                    { faults.Add($"the separator's name has no colour of its own over {reported}"); continue; }
                inks.Add(ink);
                if (ink != ((ISolidColorBrush)Mo2Density.Ink(parsed)).Color)
                    faults.Add($"the name over {reported} is not the ink the shared rule picks");
                var contrast = Contrast(ink, parsed);
                if (contrast < MinimumContrast)
                    faults.Add($"the name over {reported} is drawn at {contrast:F1}:1, under the {MinimumContrast}:1 it must read at");
                else described.Add($"{reported} carries its name at {contrast:F1}:1");
            }
            // The two colours sit either side of the luminance line, so an ink that
            // never moved is one that does not follow the colour at all.
            if (inks.Count == 2 && inks[0] == inks[1])
                faults.Add($"both colours drew their name in the same {inks[0]}, so the ink does not follow the colour");
        } finally {
            await profile.SetModColor(separator.Name, original.Length > 0 ? original : null, target);
            await Task.Delay(300);
        }

        var restored = profile.Mods.FirstOrDefault(x => x.Name == separator.Name)?.Color ?? "";
        if (restored != original) faults.Add($"the separator was left {(restored.Length > 0 ? restored : "uncoloured")}, not as it was found");

        if (faults.Count > 0) Console.WriteLine("FAIL separator colour: " + string.Join("; ", faults));
        else Console.WriteLine($"PASS separator colour: MO2 coloured {separator.Name} through its own action and the bridge read it " +
            $"back both times — {string.Join(", ", described)}, each name in ink that changed with the colour — " +
            "and it was put back as it was found");

        // WCAG relative luminance and contrast, worked out here rather than taken from
        // the rule under test.
        static double Contrast(Color a, Color b)
        {
            static double Channel(byte value)
            {
                var part = value / 255.0;
                return part <= 0.03928 ? part / 12.92 : Math.Pow((part + 0.055) / 1.055, 2.4);
            }
            static double Luminance(Color c) => .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
            var (first, second) = (Luminance(a), Luminance(b));
            return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
        }

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
