using Avalonia.Controls;
using Avalonia.Input;
using NexusMods.App.UI.Controls.Search;
using System.Reactive.Disposables;

namespace Mo2.Frontend;

internal static class Mo2SearchFocusCheck
{
    internal static async Task Run(Window window)
    {
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 600, Height = 150, RowDefinitions = new RowDefinitions("Auto,*") };
        var search = new SearchControl();
        var outside = new TextBox(); Grid.SetRow(outside, 1);
        host.Children.Add(search); host.Children.Add(outside);
        using var handlers = new CompositeDisposable();
        search.AttachKeyboardHandlers(host, handlers);
        root.Children.Add(host);
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception("Search keyboard focus did not settle");
                await Task.Delay(25);
            }
        }
        void Key(Key key, KeyModifiers modifiers = KeyModifiers.None) {
            if (window.FocusManager?.GetFocusedElement() is not InputElement focused)
                throw new Exception("Search left no focused keyboard target");
            var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };
            focused.RaiseEvent(args);
            if (!args.Handled) throw new Exception("Search shortcut did not reach its page handler");
        }
        try {
            var button = search.FindControl<Button>("SearchButton")!;
            var box = search.FindControl<TextBox>("SearchTextBox")!;
            await Wait(() => button.Focus());
            for (var i = 0; i < 4; i++) {
                Key(Avalonia.Input.Key.F, KeyModifiers.Control);
                await Wait(() => search.IsSearchVisible && box.IsFocused);
                box.Text = "fixture";
                Key(Avalonia.Input.Key.Escape);
                await Wait(() => !search.IsSearchVisible && button.IsFocused && search.SearchText == "");
                Key(Avalonia.Input.Key.F, KeyModifiers.Control);
                await Wait(() => search.IsSearchVisible && box.IsFocused);
                Key(Avalonia.Input.Key.F, KeyModifiers.Control);
                await Wait(() => !search.IsSearchVisible && button.IsFocused);
            }
            search.ToggleSearchPanelVisibility(); await Wait(() => box.IsFocused);
            outside.Focus(); await Wait(() => outside.IsFocused);
            search.ClearSearch();
            if (!outside.IsFocused) throw new Exception("Programmatic search clear stole focus from another control");
            Console.WriteLine("PASS search keyboard focus: repeated Ctrl+F/Escape/reopen and Ctrl+F close; background clear preserves outside focus");
        } finally { root.Children.Remove(host); }
    }
}
