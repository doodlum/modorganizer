using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Mo2.Frontend;

// Let the native table realize lightweight rows before their controls are
// attached. A row removed by scrolling/filtering cancels its queued work.
internal sealed class Mo2DeferredRow : Decorator
{
    private readonly Func<Control> _create;
    private long _version;

    // One MO2 list line tall while the row is still a placeholder, so the table
    // scrolls and measures at the height the built row will take.
    internal Mo2DeferredRow(Func<Control> create) { _create = create; Height = Mo2Density.Row; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Child is not null) return;
        var version = ++_version;
        Dispatcher.UIThread.Post(() => {
            if (_version != version) return;
            var child = _create();
            if (_version == version) Child = child;
        }, DispatcherPriority.Background);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ++_version;
        base.OnDetachedFromVisualTree(e);
    }
}
