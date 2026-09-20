using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Overlays;
using ReactiveUI;

namespace Mo2.Frontend;

// NMA shows one overlay at a time over the whole window: a translucent scrim
// across the spine, sidebar and workspace with the overlay's own view centred in
// it. See MainWindow.axaml's OverlayBorder/OverlayViewHost, which this mirrors —
// the MO2 shell builds the same "72,232,*" by "Auto,*,Auto" grid, so the layer
// drops in at the same cell, span and ZIndex.
internal sealed class Mo2OverlayHost
{
    internal OverlayController Controller { get; } = new();

    internal Mo2OverlayHost(Grid grid)
    {
        var viewHost = new ViewModelViewHost {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = false,
        };
        // MO2 runs in short/narrow Steam Deck panels where NMA's desktop-sized
        // overlays would not fit; scrolling keeps their controls reachable.
        var scroll = new ScrollViewer {
            Content = viewHost,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        var scrim = new Border {
            Name = "OverlayBorder",
            Background = (IBrush)Application.Current!.FindResource("BrandTranslucentDark800Brush")!,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ZIndex = 1,
            IsVisible = false,
            Child = scroll,
        };
        Grid.SetColumn(scrim, 0); Grid.SetColumnSpan(scrim, 3);
        Grid.SetRow(scrim, 0); Grid.SetRowSpan(scrim, 2);
        grid.Children.Add(scrim);

        // The controller raises this by hand after each dequeue, so the binding
        // follows both the opening and the closing of every overlay.
        Controller.WhenAnyValue(controller => controller.CurrentOverlay).Subscribe(overlay => {
            viewHost.ViewModel = overlay;
            scrim.IsVisible = overlay is not null;
        });
    }
}
