using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// A centred narrow thumb that expands in place, shared by both installed lists.
internal sealed class Mo2ListScrollBar : ScrollBar
{
    public Mo2ListScrollBar()
    {
        Orientation = Orientation.Vertical;
        SmallChange = 40;
        AllowAutoHide = false;
        Template = new FuncControlTemplate<ScrollBar>((bar, scope) => {
            var line = new Border { Width = 2, CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#777780"), HorizontalAlignment = HorizontalAlignment.Center };
            var thumb = new Thumb { MinHeight = 20,
                Template = new FuncControlTemplate<Thumb>((_, _) => line) };
            var track = new Track { Name = "PART_Track", Orientation = Orientation.Vertical,
                IsDirectionReversed = true, Thumb = thumb };
            var decrease = new RepeatButton { Template = new FuncControlTemplate<RepeatButton>((_, _) => new Border { Background = Brushes.Transparent }) };
            var increase = new RepeatButton { Template = new FuncControlTemplate<RepeatButton>((_, _) => new Border { Background = Brushes.Transparent }) };
            decrease.Click += (_, _) => bar.Value = Math.Max(bar.Minimum, bar.Value - bar.LargeChange);
            increase.Click += (_, _) => bar.Value = Math.Min(bar.Maximum, bar.Value + bar.LargeChange);
            track.DecreaseButton = decrease; track.IncreaseButton = increase;
            scope.Register("PART_Track", track);
            track.Bind(Track.MinimumProperty, new Binding(nameof(Minimum)) { Source = bar });
            track.Bind(Track.MaximumProperty, new Binding(nameof(Maximum)) { Source = bar });
            track.Bind(Track.ViewportSizeProperty, new Binding(nameof(ViewportSize)) { Source = bar });
            track.Bind(Track.ValueProperty, new Binding(nameof(Value)) { Source = bar, Mode = BindingMode.TwoWay });
            var root = new Grid { Background = Brushes.Transparent };
            root.Children.Add(new Border { Width = 2, Background = Brush.Parse("#393940"), HorizontalAlignment = HorizontalAlignment.Center });
            root.Children.Add(track);
            root.PointerEntered += (_, _) => line.Width = 8;
            root.PointerExited += (_, _) => line.Width = 2;
            return root;
        });
    }

    public static void Connect(ScrollBar bar, TreeDataGrid table)
    {
        ScrollViewer? viewer = null;
        var syncing = false;
        void Sync() {
            if (viewer is null) return;
            syncing = true;
            bar.Maximum = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
            bar.ViewportSize = viewer.Viewport.Height;
            bar.LargeChange = viewer.Viewport.Height;
            bar.Value = viewer.Offset.Y;
            bar.IsEnabled = bar.Maximum > 0;
            syncing = false;
        }
        void Scrolled(object? sender, ScrollChangedEventArgs args) => Sync();
        table.LayoutUpdated += (_, _) => {
            var next = viewer?.GetVisualRoot() is not null ? viewer : table.GetVisualDescendants().OfType<ScrollViewer>()
                .FirstOrDefault(x => x.GetVisualDescendants().Any(c => c.GetType().Name == "TreeDataGridRowsPresenter"));
            if (next != viewer) {
                if (viewer is not null) viewer.ScrollChanged -= Scrolled;
                viewer = next;
                if (viewer is not null) { viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden; viewer.ScrollChanged += Scrolled; }
            }
            Sync();
        };
        bar.PropertyChanged += (_, args) => {
            if (!syncing && args.Property == ValueProperty && viewer is not null) viewer.Offset = new Vector(viewer.Offset.X, bar.Value);
        };
    }
}
