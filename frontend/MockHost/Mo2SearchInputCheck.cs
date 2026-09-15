using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Search;
using System.Reactive.Linq;

namespace Mo2.Frontend;

internal static class Mo2SearchInputCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        var layout = Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT");
        if (layout is null || !Path.GetFullPath(layout).StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
            throw new Exception("Search input verification requires a temporary layout");
        await shell.ProfileMenu.LeftMenuItemLoadout.NavigateCommand.Execute(
            NexusMods.App.UI.Controls.Navigation.NavigationInformation.From(NexusMods.App.UI.WorkspaceSystem.NavigationInput.Default));
        var deadline = DateTime.UtcNow.AddSeconds(20);
        Mo2ModsView? view;
        while ((view = window.GetVisualDescendants().OfType<Mo2ModsView>().FirstOrDefault(x => x.IsEffectivelyVisible)) is null) {
            if (DateTime.UtcNow > deadline) throw new Exception("Mods search view did not attach");
            await Task.Delay(50);
        }
        var search = view.GetVisualDescendants().OfType<SearchControl>().First();
        var box = search.FindControl<TextBox>("SearchTextBox")!;
        var oldText = box.Text;
        var wasVisible = search.IsSearchVisible;
        var keys = 0; var textEvents = 0; var changes = 0; var maximumLength = 0;
        var handledKeys = 0; var unmodifiedKeys = 0; var symbols = 0;
        var modifiers = KeyModifiers.None;
        void Key(object? sender, KeyEventArgs args) => keys++;
        void AfterKey(object? sender, KeyEventArgs args) {
            modifiers |= args.KeyModifiers;
            if (args.Handled) handledKeys++;
            if (args.KeyModifiers == KeyModifiers.None) unmodifiedKeys++;
            if (!string.IsNullOrEmpty(args.KeySymbol)) symbols++;
        }
        void Text(object? sender, TextInputEventArgs args) => textEvents++;
        void Changed(object? sender, TextChangedEventArgs args) { changes++; maximumLength = Math.Max(maximumLength, box.Text?.Length ?? 0); }
        window.AddHandler(InputElement.KeyDownEvent, Key, RoutingStrategies.Tunnel, handledEventsToo: true);
        window.AddHandler(InputElement.KeyDownEvent, AfterKey, RoutingStrategies.Bubble, handledEventsToo: true);
        window.AddHandler(InputElement.TextInputEvent, Text, RoutingStrategies.Tunnel, handledEventsToo: true);
        box.TextChanged += Changed;
        try {
            box.Text = "";
            if (!search.IsSearchVisible) search.ToggleSearchPanelVisibility();
            window.Activate(); box.Focus();
            await Task.Delay(300);
            Console.WriteLine($"READY search input: active={window.IsActive}, focused={box.IsFocused}, enabled={box.IsEffectivelyEnabled}, readonly={box.IsReadOnly}");
            deadline = DateTime.UtcNow.AddSeconds(30);
            while (box.Text != "mo2inputcheck" && DateTime.UtcNow < deadline) await Task.Delay(50);
            var matched = box.Text == "mo2inputcheck";
            Console.WriteLine($"{(matched ? "PASS" : "FAIL")} native search input: keys={keys}, handledKeys={handledKeys}, unmodifiedKeys={unmodifiedKeys}, modifiers={modifiers}, symbols={symbols}, textEvents={textEvents}, changes={changes}, maximumLength={maximumLength}, focused={box.IsFocused}, active={window.IsActive}");
        } finally {
            window.RemoveHandler(InputElement.KeyDownEvent, Key);
            window.RemoveHandler(InputElement.KeyDownEvent, AfterKey);
            window.RemoveHandler(InputElement.TextInputEvent, Text);
            box.TextChanged -= Changed;
            box.Text = oldText;
            if (search.IsSearchVisible != wasVisible) search.ToggleSearchPanelVisibility();
        }
    }
}
