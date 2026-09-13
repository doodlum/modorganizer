using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Controls.MarkdownRenderer;
using ReactiveUI;

namespace Mo2.Frontend;

// Native diagnostic pages require this interface, but MO2 supplies plain text.
// Render it directly so filenames, punctuation and URLs are preserved verbatim.
internal sealed class Mo2DiagnosticText : MarkdownRendererViewModel;

internal sealed class Mo2DiagnosticTextView : ReactiveUserControl<Mo2DiagnosticText>
{
    public SelectableTextBlock Text { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public Mo2DiagnosticTextView()
    {
        Content = new ScrollViewer { Content = Text };
        this.WhenActivated(d => this.OneWayBind(ViewModel, vm => vm.Contents, view => view.Text.Text).DisposeWith(d));
    }
}
