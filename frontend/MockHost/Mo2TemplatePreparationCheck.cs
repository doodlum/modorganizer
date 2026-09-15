using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2TemplatePreparationCheck
{
    internal static async Task Run(Window owner)
    {
        var host = new Grid();
        var window = new Window { Width = 320, Height = 160, Content = host };
        window.Show(owner);
        try {
            async Task Check(bool cancel)
            {
                var content = new StackPanel();
                var root = new UserControl { Content = content };
                var builds = 0; var inputTurns = 0; var current = true;
                for (var i = 0; i < 3; i++) {
                    content.Children.Add(new TemplatedControl {
                        Template = new FuncControlTemplate((_, _) => {
                            if (inputTurns < builds) throw new Exception("Template preparation blocked queued input between builds");
                            builds++;
                            Dispatcher.UIThread.Post(() => {
                                inputTurns++;
                                if (cancel && builds == 1) { current = false; host.Children.Remove(root); }
                            }, DispatcherPriority.Input);
                            return new Border { Height = 20, Width = 40 };
                        })
                    });
                }
                var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Mo2TemplatePreparation.Attach(root, () => host.Children.Add(root), () => current, success => done.SetResult(success));
                var success = await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
                if (success == cancel || !root.IsVisible || builds != (cancel ? 1 : 3))
                    throw new Exception("Template preparation completion/cancellation did not restore visibility and stop work");
                if (cancel) {
                    current = true; cancel = false;
                    var resumed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    Mo2TemplatePreparation.Attach(root, () => host.Children.Add(root), () => current, result => resumed.SetResult(result));
                    if (!await resumed.Task.WaitAsync(TimeSpan.FromSeconds(5)) || builds != 3)
                        throw new Exception("Resumed preparation rebuilt existing templates or lost remaining work");
                }
                await Task.Delay(100);
                if (builds != 3 || inputTurns != 3 || !root.IsVisible)
                    throw new Exception("Showing prepared templates rebuilt them or lost queued input");
                host.Children.Remove(root);
            }
            await Check(false); await Check(true);
            var first = new object(); var second = new object();
            UserControl? firstBody = null; UserControl? secondBody = null;
            Mo2DeferredView<object>? deferred = null;
            var sawLoading = false;
            deferred = Mo2DeferredView<object>.For(first, "test", model => {
                var body = new UserControl { Content = new TemplatedControl {
                    Template = new FuncControlTemplate((_, _) => {
                        if (ReferenceEquals(model, first))
                            Dispatcher.UIThread.Post(() => deferred!.ViewModel = second, DispatcherPriority.Input);
                        else
                            sawLoading = deferred!.GetVisualDescendants().OfType<TextBlock>()
                                .Single(text => text.Name == "PanelPreparationMessage").IsEffectivelyVisible;
                        return new Border { Height = 20, Width = 40 };
                    })
                } };
                if (ReferenceEquals(model, first)) firstBody = body; else secondBody = body;
                return body;
            }, reuseBody: true);
            host.Children.Add(deferred);
            var until = DateTime.UtcNow.AddSeconds(5);
            while (secondBody?.IsEffectivelyVisible != true) {
                if (DateTime.UtcNow >= until) throw new Exception("Replacement deferred body did not become visible");
                await Task.Delay(25);
            }
            if (!sawLoading || firstBody?.GetVisualRoot() is not null ||
                deferred.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PanelPreparationMessage").IsVisible)
                throw new Exception("Canceled preparation hid the new loading message, restored stale content, or left the message visible");
            host.Children.Remove(deferred);
            var retainedHost = new Grid(); host.Children.Add(retainedHost);
            UserControl? retainedBody = null;
            var retainedBuilds = 0; var bodyCreations = 0;
            var retainedView = Mo2DeferredView<object>.For(new object(), "retained test", _ => {
                bodyCreations++;
                var content = new StackPanel();
                for (var i = 0; i < 3; i++) content.Children.Add(new TemplatedControl {
                    Template = new FuncControlTemplate((_, _) => {
                        retainedBuilds++;
                        if (retainedBuilds == 1)
                            Dispatcher.UIThread.Post(() => retainedHost.IsVisible = false, DispatcherPriority.Input);
                        return new Border { Width = 40, Height = 20 };
                    })
                });
                return retainedBody = new UserControl { Content = content };
            }, reuseBody: true);
            retainedHost.Children.Add(retainedView);
            until = DateTime.UtcNow.AddSeconds(5);
            while (retainedHost.IsVisible || retainedBody?.IsVisible != true) {
                if (DateTime.UtcNow >= until) throw new Exception("Hidden retained view did not cancel preparation");
                await Task.Delay(25);
            }
            await Task.Delay(100);
            if (retainedBuilds != 1 || retainedBody.GetVisualRoot() is null)
                throw new Exception("Preparation continued while retained view was hidden, or detached its body");
            retainedHost.IsVisible = true;
            until = DateTime.UtcNow.AddSeconds(5);
            while (retainedBuilds != 3 || !retainedBody.IsEffectivelyVisible) {
                if (DateTime.UtcNow >= until) throw new Exception("Retained view did not recover after interrupted preparation");
                await Task.Delay(25);
            }
            if (bodyCreations != 1 || retainedView.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => text.Name == "PanelPreparationMessage").IsVisible)
                throw new Exception("Returning to retained view recreated its body or left the loading message visible");
            host.Children.Remove(retainedHost);
            Console.WriteLine("PASS template preparation: input between builds, reveal without rebuild, cancellation/detach stops work, visibility restored, reattach completes remaining templates");
            Console.WriteLine("PASS deferred loading: message visible during preparation, replacement model survives stale completion, old body stays detached, message clears when ready");
            Console.WriteLine("PASS retained preparation: hiding without detach cancels remaining work, returning keeps body identity, finishes templates and clears loading");
        } finally { window.Close(); }
    }
}
