using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Text.Json;

namespace Mo2.Frontend;

internal static class Mo2KeyboardSearchCheck
{
    internal static async Task Run(Window window, string path)
    {
        using var turn = await Mo2CheckTurn.Take();
        async Task Wait(Func<bool> ready, string stage) {
            var until = DateTime.UtcNow.AddSeconds(25);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception("Keyboard search: " + stage);
                await Task.Delay(50);
            }
        }
        EventHandler<KeyEventArgs> keyTrace = (_, e) => Console.WriteLine($"KEYBOARD DIAGNOSTIC key={e.Key}; modifiers={e.KeyModifiers}; source={e.Source?.GetType().Name}");
        window.AddHandler(InputElement.KeyDownEvent, keyTrace, RoutingStrategies.Tunnel, true);
        try {
            foreach (var name in new[] { "ModsToolbarSearch", "PluginsToolbarSearch" }) {
                await Wait(() => window.GetVisualDescendants().OfType<Mo2ToolbarSearch>().Any(x => x.Name == name && x.IsEffectivelyVisible), name);
                var search = window.GetVisualDescendants().OfType<Mo2ToolbarSearch>().Single(x => x.Name == name && x.IsEffectivelyVisible);
                var page = search.GetVisualAncestors().OfType<Control>().First(x => x is Mo2ModsView or Mo2PluginsView);
                var table = page.GetVisualDescendants().OfType<TreeDataGrid>().Single();
                var box = search.GetVisualDescendants().OfType<TextBox>().Single();
                var button = search.GetVisualDescendants().OfType<Button>().First();
                await Wait(() => table.Rows?.Count > 0, "populated " + name);
                if (!string.IsNullOrEmpty(box.Text) || box.IsEffectivelyVisible)
                    throw new Exception("Keyboard test requires initially collapsed, empty search");
                var count = table.Rows!.Count;
                async Task Phase(string action, Func<bool> done) {
                    window.Activate();
                    await Wait(() => window.IsActive && button.Bounds.Width > 0 && button.Bounds.Height > 0, "active layout " + name);
                    window.UpdateLayout();
                    await Task.Delay(300);
                    var point = button.PointToScreen(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2));
                    var hit = window.InputHitTest(window.PointToClient(point)) as Visual;
                    if (hit != button && !(hit?.GetVisualAncestors().Contains(button) ?? false))
                        throw new Exception("Search button failed input hit-test: " + name);
                    if (action == "type" && !page.IsEffectivelyVisible)
                        throw new Exception("Search page is no longer visible: " + name);
                    var id = name + "-" + action;
                    var request = JsonSerializer.Serialize(new { Phase = id, Action = action, point.X, point.Y });
                    File.WriteAllText(path + ".tmp", request); File.Move(path + ".tmp", path, true);
                    await Wait(() => File.Exists(path + ".done") && File.ReadAllText(path + ".done") == id && done(), id);
                }
                await Phase("click", () => box.IsEffectivelyVisible && box.IsFocused);
                await Phase("type", () => box.IsFocused && box.Text == "mo2keyboardnomatch" && table.Rows!.Count == 0);
                await Phase("hide", () => !box.IsEffectivelyVisible && button.IsFocused && box.Text == "mo2keyboardnomatch");
                await Phase("reopen", () => box.IsEffectivelyVisible && box.IsFocused && box.Text == "mo2keyboardnomatch");
                await Phase("clear", () => !box.IsEffectivelyVisible && button.IsFocused && string.IsNullOrEmpty(box.Text) && table.Rows!.Count == count);
                Console.WriteLine("PASS desktop keyboard search: " + name + "; click/type filters rows, Ctrl+F retains query, Escape restores rows and focus");
            }
        } finally {
            window.RemoveHandler(InputElement.KeyDownEvent, keyTrace);
            File.Delete(path);
        }
    }
}
