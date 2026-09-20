using Avalonia;
using Avalonia.Controls;

namespace Mo2.Frontend;

// Only attached, virtualized rows build their controls. Visible cell content must
// be ready for layout: dispatching it at Background priority left entire tables
// blank while the render queue was busy.
internal sealed class Mo2DeferredRow : Decorator
{
    private readonly Func<Control> _create;

    // A fixed line height also keeps measurement stable before attachment.
    internal Mo2DeferredRow(Func<Control> create) { _create = create; Height = Mo2Density.Row; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Child ??= _create();
    }
}
