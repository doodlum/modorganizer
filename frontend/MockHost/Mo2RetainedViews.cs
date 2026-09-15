using Avalonia.Controls;

namespace Mo2.Frontend;

// Retain only explicitly eligible views, with a fixed limit including the active
// view. Eviction detaches subscriptions; page models remain owned by NMA.
internal sealed class Mo2RetainedViews<T>(Func<T, Control> create, Func<T, bool> canRetain,
    Action<Control> release, int capacity = 3) : Grid where T : class
{
    private readonly Dictionary<T, Control> _views = new(ReferenceEqualityComparer.Instance);
    private readonly List<T> _recent = [];
    internal int RetainedCount => _views.Count;

    internal Control Activate(T model)
    {
        if (capacity < 1) throw new InvalidOperationException("View cache capacity must be positive");
        foreach (var old in _recent.ToArray()) {
            if (ReferenceEquals(old, model)) continue;
            if (!canRetain(old)) Remove(old);
            else _views[old].IsVisible = false;
        }
        _recent.RemoveAll(item => ReferenceEquals(item, model));
        _recent.Add(model);
        while (_recent.Count > capacity) Remove(_recent[0]);
        if (!_views.TryGetValue(model, out var view)) {
            view = create(model);
            _views.Add(model, view);
            Children.Add(view);
        }
        view.IsVisible = true;
        return view;
    }

    private void Remove(T model)
    {
        if (_views.Remove(model, out var view)) {
            release(view);
            Children.Remove(view);
        }
        _recent.RemoveAll(item => ReferenceEquals(item, model));
    }

    internal void ClearRetained()
    {
        foreach (var model in _recent.ToArray()) Remove(model);
    }
}
