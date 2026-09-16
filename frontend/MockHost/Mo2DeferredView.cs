using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Mo2.Frontend;

// Construct each expensive panel body below input/render priority. This lets
// the new workspace appear and process input between the two list layouts.
//
// The priority here, and building the two panels one after another instead of
// together, were both measured against time-to-first-rows and neither moved it:
// input, loaded and normal all landed within noise of background, and queueing the
// bodies changed nothing. What is left between the profile's data arriving and its
// rows appearing is Avalonia laying out and rendering the two tables, which is work
// rather than waiting, so it does not respond to being scheduled differently.
internal sealed class Mo2DeferredView<T> : ReactiveUserControl<T> where T : class
{
    private long _version;
    private T? _loadedModel;
    private Control? _body;
    // The body this view is holding, which is what the reuse rules are about: it is
    // handed from a detached view to a fresh one for the same page model, and never
    // taken from one that is still on screen. Content is the surface this view draws
    // — the body and the loading overlay together — so reading the body off that was
    // reading the wrong thing.
    internal Control? LoadedBody => _body;
    // And which model that body was built for. A view whose model has just been
    // replaced is still holding the previous body until its own turn on the
    // dispatcher comes round, so "is there a body" and "is there a body for this
    // model" are different questions, and only the second one means loaded.
    internal object? LoadedModel => _loadedModel;
    private readonly ContentControl _content = new() {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };
    // Page models own the lifetime of their reusable bodies. Transfer only from
    // detached hosts; concurrent hosts always get independent controls.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<T, Mo2DeferredView<T>> Bodies = new();

    private Mo2DeferredView(string title, Func<T, Control> create, bool reuseBody)
    {
        var loading = new TextBlock { Name = "PanelPreparationMessage", Text = "Loading " + title + "…", Opacity = .65, IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var surface = new Grid();
        surface.Children.Add(_content); surface.Children.Add(loading); Content = surface;
        this.WhenActivated(disposables => {
            this.WhenAnyValue(view => view.ViewModel).Subscribe(model => {
                var version = ++_version;
                if (model is null) { _content.Content = null; loading.IsVisible = false; return; }
                if (ReferenceEquals(model, _loadedModel) && _body is not null) { _content.Content = _body; loading.IsVisible = false; return; }
                _content.Content = null; loading.IsVisible = true;
                Dispatcher.UIThread.Post(() => {
                    if (version != _version || !ReferenceEquals(model, ViewModel)) return;
                    using var timing = Mo2UiLatencyProbe.Measure("Create and attach " + title + " panel body", always: true);
                    Control? body = null;
                    var reuse = reuseBody && Environment.GetEnvironmentVariable("MO2_REUSE_PAGE_BODIES") != "0";
                    if (reuse && Bodies.TryGetValue(model, out var previous) &&
                        !ReferenceEquals(previous, this) && previous.GetVisualRoot() is null &&
                        ReferenceEquals(previous._loadedModel, model) && previous._body is { } existing) {
                        previous._content.Content = null;
                        previous._body = null;
                        previous._loadedModel = null;
                        body = existing;
                        Mo2UiLatencyProbe.Count("Page bodies reused");
                    }
                    var created = body is null;
                    using (Mo2StartupCheck.Phase("build:" + title)) body ??= create(model);
                    if (version != _version) return;
                    _loadedModel = model; _body = body;
                    if (created && (reuseBody || title == "panel") && Environment.GetEnvironmentVariable("MO2_PREPARE_TEMPLATES") != "0") {
                        var preparing = Mo2StartupCheck.Phase("templates:" + title);
                        Mo2TemplatePreparation.Attach(body, () => _content.Content = body,
                            () => version == _version && IsEffectivelyVisible && ReferenceEquals(model, ViewModel),
                            success => {
                                preparing.Dispose();
                                var run = Mo2TemplatePreparation.LastRun;
                                Mo2StartupCheck.Note($"templates:{title} {run.Applied} controls over {run.Turns} turns, " +
                                    $"{run.Working.TotalMilliseconds:F0}ms of work");
                                if (version == _version) loading.IsVisible = false;
                                if (success) Mo2UiLatencyProbe.Census(title, body);
                            });
                    }
                    else {
                        _content.Content = body; loading.IsVisible = false;
                        Mo2UiLatencyProbe.Census(title, body);
                    }
                    if (reuse) { Bodies.Remove(model); Bodies.Add(model, this); }
                }, DispatcherPriority.Background);
            }).DisposeWith(disposables);
            Disposable.Create(() => _version++).DisposeWith(disposables);
        });
    }

    internal static Mo2DeferredView<T> For(T model, string title, Func<T, Control> create, bool reuseBody = false)
    {
        return new Mo2DeferredView<T>(title, create, reuseBody) { ViewModel = model };
    }
}
