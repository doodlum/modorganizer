using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2ToolPinOrderCheck
{
    internal static async Task Run(Grid host, Mo2LiveWorkspace shell, Mo2LiveProfile profile)
    {
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var expected = Environment.GetEnvironmentVariable("MO2_TOOL_PIN_TEST_CONFIG");
        if (string.IsNullOrEmpty(config) || config != expected || File.Exists(Mo2ToolPins.Path))
            throw new Exception("Pin ordering check requires an explicitly isolated, empty config directory");
        string Key(string id) => profile.Endpoint + "|tool:[\"" + id + "\"]";
        string[] before = ["other-host|tool:[\"foreign\"]", Key("first"), Key("missing"),
            "other-host|tool:[\"second-foreign\"]", Key("second")];
        Mo2ToolPins.Save(before);
        var view = new Mo2ToolsView(() => Task.FromResult(new[] {
            new Mo2Tool(["first"], "First extension", "", "", true),
            new Mo2Tool(["second"], "Second extension", "", "", true)
        })) { ViewModel = new Mo2ToolsPage(new FixtureWindows { ActiveWindow = shell }, profile) };
        async Task Wait(Func<bool> ready) {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!ready()) {
                if (DateTime.UtcNow > deadline) throw new Exception("Pin ordering UI did not settle");
                await Task.Delay(25);
            }
        }
        Grid Row(string name) => view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Text == name)
            .GetVisualAncestors().OfType<Grid>().First();
        Button Move(string name) => Row(name).Children.OfType<Button>().Single(x => x.Name == "MovePinnedToolEarlier");
        string[] Order() => view.GetVisualDescendants().OfType<TextBlock>()
            .Where(x => x.Text is "First extension" or "Second extension").Select(x => x.Text!).ToArray();
        try {
            host.Children.Add(view);
            await Wait(() => !view.IsReading && Order().Length == 2);
            if (Row("Secondary").Children.OfType<Button>().Any(x => x.Name == "MovePinnedToolEarlier"))
                throw new Exception("Native executable has a frontend-only reorder action");
            if (Move("First extension").IsEnabled || !Move("Second extension").IsEnabled)
                throw new Exception("Reorder availability includes foreign or missing tool pins");
            Move("Second extension").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => Order().SequenceEqual(new[] { "Second extension", "First extension" }));
            var after = before.ToArray(); (after[1], after[4]) = (after[4], after[1]);
            if (!Mo2ToolPins.Read().SequenceEqual(after) || Move("Second extension").IsEnabled)
                throw new Exception("Reorder failed to persist or changed unrelated pins");
            host.Children.Remove(view); host.Children.Add(view);
            await Wait(() => !view.IsReading && Order().SequenceEqual(new[] { "Second extension", "First extension" }));
            Move("First extension").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => Order().SequenceEqual(new[] { "First extension", "Second extension" }));
            if (!Mo2ToolPins.Read().SequenceEqual(before)) throw new Exception("Reverse reorder did not restore exact pin order");
            Console.WriteLine("PASS tool pin ordering: actual buttons reorder available extensions, persist across reopen and restore; foreign/missing pins untouched; native executables have no ineffective reorder action");
        } finally {
            host.Children.Remove(view);
            File.Delete(Mo2ToolPins.Path);
        }
    }
}
