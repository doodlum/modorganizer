using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2RetainedViewsCheck
{
    internal static Task Run(Window window)
    {
        var root = (Grid)window.Content!;
        var allowed = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var released = new List<Control>();
        var detached = new List<Control>();
        var cache = new Mo2RetainedViews<object>(_ => {
            var view = new Border();
            view.DetachedFromVisualTree += (_, _) => detached.Add(view);
            return view;
        }, allowed.Contains, released.Add) { Width = 1, Height = 1, IsHitTestVisible = false };
        root.Children.Add(cache);
        try {
            var a = new object(); var b = new object(); var c = new object(); var d = new object();
            allowed.UnionWith([a, b, c, d]);
            var first = cache.Activate(a); var second = cache.Activate(b);
            if (first.IsVisible || !second.IsVisible || first.GetVisualRoot() != window)
                throw new Exception("Eligible inactive view was detached or remained visible");
            if (!ReferenceEquals(first, cache.Activate(a))) throw new Exception("Retained view was rebuilt");
            cache.Activate(c); cache.Activate(d);
            if (cache.RetainedCount != 3 || second.GetVisualRoot() is not null ||
                released.Count != 1 || !ReferenceEquals(released[0], second) || !detached.Contains(second))
                throw new Exception("Least-recently-used view was not released and detached");
            allowed.Remove(a); cache.Activate(d);
            if (first.GetVisualRoot() is not null || cache.RetainedCount != 2 || !released.Contains(first))
                throw new Exception("Newly ineligible hidden view survived navigation");
            var rebuilt = cache.Activate(b);
            if (ReferenceEquals(second, rebuilt)) throw new Exception("Evicted view was silently reused");
            cache.ClearRetained();
            if (cache.RetainedCount != 0 || cache.Children.Count != 0 || released.Count != 5 || detached.Count != 5)
                throw new Exception("Clearing cache did not release every view exactly once");
            cache.ClearRetained();
            if (released.Count != 5) throw new Exception("Cache released a view twice");
            Console.WriteLine("PASS workspace cache: retained identity, single visibility, LRU bound, ineligible eviction, detach/rebuild and clear exactly once");
            var keyboard = new Mo2RetainedViews<object>(_ => new TextBox(), _ => true, _ => { });
            root.Children.Add(keyboard);
            try {
                var firstBox = (TextBox)keyboard.Activate(a);
                if (!firstBox.Focus()) throw new Exception("Cannot focus cached workspace fixture");
                keyboard.Activate(b);
                if (ReferenceEquals(window.FocusManager?.GetFocusedElement(), firstBox))
                    throw new Exception("Keyboard focus remains inside hidden workspace");
                Console.WriteLine("PASS hiding a retained workspace removes focus from its hidden text box");
            } finally { keyboard.ClearRetained(); root.Children.Remove(keyboard); }

        } finally { cache.ClearRetained(); root.Children.Remove(cache); }
        return Task.CompletedTask;
    }
    internal static async Task Live(Mo2LiveWorkspace shell, Window window)
    {
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(20);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception("Workspace eviction navigation did not settle");
                await Task.Delay(25);
            }
        }
        var workspace = shell.WorkspaceController.ActiveWorkspace;
        var original = window.GetVisualDescendants().OfType<NexusMods.App.UI.WorkspaceSystem.WorkspaceView>()
            .Single(view => ReferenceEquals(view.ViewModel, workspace));
        await shell.ProfileMenu.ToolsItem.NavigateCommand.Execute(
            NexusMods.App.UI.Controls.Navigation.NavigationInformation.From(NexusMods.App.UI.WorkspaceSystem.NavigationInput.Default));
        await Wait(() => window.GetVisualDescendants().OfType<Mo2ToolsView>().Any(view => view.IsEffectivelyVisible && !view.IsReading));
        shell.ShowHome();
        await Wait(() => original.GetVisualRoot() is null);
        if (original.ViewModel is not null) throw new Exception("Evicted utility workspace kept its model");
        shell.ShowProfile();
        await Wait(() => window.GetVisualDescendants().OfType<Mo2ToolsView>().Any(view => view.IsEffectivelyVisible && !view.IsReading));
        var reopened = window.GetVisualDescendants().OfType<NexusMods.App.UI.WorkspaceSystem.WorkspaceView>()
            .Single(view => ReferenceEquals(view.ViewModel, workspace));
        if (ReferenceEquals(original, reopened)) throw new Exception("Utility workspace bypassed eviction");
        Console.WriteLine("PASS native workspace eviction: Tools workspace detaches on Home, releases its model and reopens with fresh controls");
    }

}
