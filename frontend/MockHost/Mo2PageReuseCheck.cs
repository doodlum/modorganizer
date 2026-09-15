using Avalonia.Controls;

namespace Mo2.Frontend;

internal static class Mo2PageReuseCheck
{
    internal static async Task Run(Window window)
    {
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 1, Height = 1, IsHitTestVisible = false };
        root.Children.Add(host);
        var builds = 0;
        TextBox Create(object _) { builds++; return new TextBox { Text = "initial" }; }
        Mo2DeferredView<object> View(object model) => Mo2DeferredView<object>.For(model, "fixture", Create, reuseBody: true);
        async Task<TextBox> Body(Mo2DeferredView<object> view) {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (view.Content is not TextBox) {
                if (DateTime.UtcNow > until) throw new Exception("Deferred page did not load");
                await Task.Delay(25);
            }
            return (TextBox)view.Content;
        }
        try {
            var model = new object();
            var first = View(model); host.Children.Add(first);
            var firstBody = await Body(first); firstBody.Text = "retained";
            host.Children.Remove(first);
            var second = View(model); host.Children.Add(second);
            var secondBody = await Body(second);
            if (!ReferenceEquals(firstBody, secondBody) || builds != 1 || secondBody.Text != "retained" || first.Content is not null)
                throw new Exception("Detached body or its state was not transferred exclusively");

            var concurrent = View(model); host.Children.Add(concurrent);
            var concurrentBody = await Body(concurrent);
            if (ReferenceEquals(secondBody, concurrentBody) || builds != 2 || second.Content != secondBody)
                throw new Exception("Concurrent host stole an attached body");
            host.Children.Remove(concurrent);
            var replacement = View(model); host.Children.Add(replacement);
            if (!ReferenceEquals(await Body(replacement), concurrentBody) || builds != 2 || second.Content != secondBody)
                throw new Exception("Reopening a second host disturbed the first host");
            host.Children.Remove(replacement);
            host.Children.Add(first);
            if (!ReferenceEquals(await Body(first), concurrentBody) || builds != 2)
                throw new Exception("Previously emptied owner could not reclaim a detached body");

            var pending = View(new object()); host.Children.Add(pending); host.Children.Remove(pending);
            await Task.Delay(100);
            if (builds != 2) throw new Exception("Detached pending page constructed a body");
            host.Children.Add(pending); await Body(pending);
            if (builds != 3) throw new Exception("New model reused another model's body");
            var oldBody = pending.Content;
            pending.ViewModel = new object(); pending.ViewModel = new object();
            var replacedBody = await Body(pending);
            if (builds != 4 || ReferenceEquals(replacedBody, oldBody))
                throw new Exception("Rapid model replacement constructed stale work or kept the old body");
            Console.WriteLine("PASS page reuse lifecycle: state handoff, concurrent hosts, reopened owners, cancelled construction and model replacement");
        } finally { root.Children.Remove(host); }
    }
}
