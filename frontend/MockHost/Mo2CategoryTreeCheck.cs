using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace Mo2.Frontend;

internal static class Mo2CategoryTreeCheck
{
    internal static async Task Run()
    {
        var list = new Mo2CategoryFilterList();
        var window = new Window { Width = 260, Height = 420, ShowInTaskbar = false,
            Content = new Border { Padding = new Thickness(16), Child = list } };
        Mo2CategoryNode[] tree = [new(1, 1, "Parent", [new(2, 1, "Child", [new(3, 1, "Leaf", [])])]), new(4, 1, "Sibling", [])];
        var leaf = new Mo2LiveMod(default, "leaf", "leaf", 2, 0, CategoriesJson: "[\"Leaf\"]");
        var sibling = leaf with { Name = "sibling", CategoriesJson = "[\"Sibling\"]" };
        var changed = 0;
        void Refresh(params Mo2LiveMod[] mods) => list.Refresh(mods, tree, () => changed++);
        void Expect(bool value, string message) { if (!value) throw new Exception(message); }
        CheckBox Row(string name) => list.Items.OfType<CheckBox>().Single(x => (string)x.Tag! == name);
        string Caption(CheckBox box) => box.Content is Panel panel ? panel.Children.OfType<TextBlock>().Single().Text! : ((TextBlock)box.Content!).Text!;
        async Task Toggle(CheckBox box) {
            ((Panel)box.Content!).Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(40); window.UpdateLayout();
        }
        try {
            Refresh(leaf, sibling); window.Show(); await Task.Delay(150); window.UpdateLayout();
            var parent = Row("Parent"); var child = Row("Child"); var last = Row("Leaf");
            Expect(Caption(parent) == "Parent (1)" && Caption(child) == "Child (1)", "Descendant counts missing");
            Expect(child.Margin.Left > parent.Margin.Left && last.Margin.Left > child.Margin.Left, "Nesting is not indented");
            var click = typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            click.Invoke(parent, null);
            Expect(parent.IsChecked == true && list.Selection.Contains((1, 1)), "First click did not include");
            click.Invoke(parent, null);
            Expect(parent.IsChecked is null && list.Excluded.Contains((1, 1)) && !list.Selection.Contains((1, 1)), "Second click did not invert");
            Refresh(leaf, sibling);
            Expect(parent.IsChecked is null && list.Excluded.Contains((1, 1)), "Refresh lost inverse state");
            click.Invoke(parent, null);
            Expect(parent.IsChecked == false && list.Excluded.Count == 0, "Third click did not clear");
            parent.Focus();
            foreach (var expected in new bool?[] { true, null, false }) {
                parent.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
                parent.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
                Expect(parent.IsChecked == expected, "Space did not follow include/invert/clear cycle");
            }
            var down = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space, KeyModifiers = KeyModifiers.Shift };
            var up = new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space, KeyModifiers = KeyModifiers.Shift };
            parent.RaiseEvent(down); parent.RaiseEvent(up);
            Expect(down.Handled && up.Handled && parent.IsChecked is null && list.Excluded.Contains((1, 1)), "Shift+Space did not reverse exactly once");
            var pointer = new Pointer(0, PointerType.Mouse, true);
            var position = parent.TranslatePoint(new Point(6, parent.Bounds.Height / 2), window) ?? default;
            parent.RaiseEvent(new PointerPressedEventArgs(parent, pointer, window, position, 0,
                new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed), KeyModifiers.None));
            parent.RaiseEvent(new PointerReleasedEventArgs(parent, pointer, window, position, 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.RightButtonReleased), KeyModifiers.None, MouseButton.Right));
            Expect(parent.IsChecked == true && list.Excluded.Count == 0, "Right click did not reverse inverted to included");
            parent.IsChecked = true;
            Expect(list.Selection.SetEquals([(1, 1)]), "Parent checkbox lost category selection");
            var before = changed;
            await Toggle(child); await Toggle(parent); await Toggle(parent);
            Expect(child.IsVisible && !last.IsVisible && parent.IsChecked == true && changed == before,
                "Expansion changed selection or forgot nested collapsed state");
            await Toggle(child); Expect(last.IsVisible, "Grandchild did not reappear");
            Refresh(leaf, leaf with { Name = "second" }, sibling);
            Expect(ReferenceEquals(parent, Row("Parent")) && parent.IsChecked == true && Caption(parent) == "Parent (2)",
                "Count refresh recreated the selected row or kept stale count");
            await Toggle(parent); Refresh(leaf, sibling);
            Expect(!last.IsVisible && !child.IsVisible, "Native refresh forgot collapsed state");
            await Toggle(parent);
            await Task.Delay(40); window.UpdateLayout();
            foreach (var box in list.Items.OfType<CheckBox>())
                Expect(box.Bounds.Width > 0 && box.Bounds.Right <= list.Bounds.Width + 1, $"Category row {box.Tag} bounds {box.Bounds} escapes list {list.Bounds}");
            Directory.CreateDirectory("frontend/artifacts");
            using (var image = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height))) {
                image.Render(window); image.Save("frontend/artifacts/category-tree-controlled.png");
            }
            parent.IsChecked = null;
            tree = [tree[1]];
            Refresh(sibling);
            Expect(list.Selection.Count == 0 && list.Excluded.Count == 0 && !list.Items.OfType<CheckBox>().Any(x => (string)x.Tag! == "Parent"), "Removed category retained an invisible filter");
            Row("Sibling").IsChecked = null;
            list.Reset(); Refresh(sibling);
            Expect(list.Selection.Count == 0 && list.Excluded.Count == 0 && list.Items.OfType<CheckBox>().All(x => x.IsChecked == false), "Profile reset retained selection");
            Row("Sibling").IsChecked = true;
            tree = [new(4, 1, "Renamed", [])]; Refresh(sibling);
            Expect(Row("Renamed").IsChecked == true && list.Selection.SetEquals([(1, 4)]), "Renaming a category lost its ID selection");
            tree = [new(10, 0, "Same label", []), new(10, 2, "Same label", [])];
            var nativeMatches = new Dictionary<(int Type, int Id), HashSet<string>> {
                [(0, 10)] = new() { "leaf" }, [(2, 10)] = new() { "sibling" },
            };
            list.Refresh([leaf, sibling], tree, () => changed++, nativeMatches, native: true);
            var duplicateRows = list.Items.OfType<CheckBox>().ToArray();
            duplicateRows[0].IsChecked = true; duplicateRows[1].IsChecked = null;
            Expect(list.Selection.SetEquals([(0, 10)]) && list.Excluded.SetEquals([(2, 10)]), "Same-label or same-ID criteria collided across types");
            list.Refresh([leaf, sibling], tree, () => changed++, native: true);
            Expect(duplicateRows.All(x => !x.IsEnabled) && Caption(duplicateRows[0]).EndsWith("(…)"), "Unloaded native filters offered guessed matches");
            list.Refresh([leaf, sibling], tree, () => changed++, nativeMatches, native: true);
            Expect(duplicateRows.All(x => x.IsEnabled) && Caption(duplicateRows[0]).EndsWith("(1)"), "Native filter counts did not arrive");
            list.Refresh([leaf, sibling], tree, () => changed++, nativeMatches, native: true, ready: false);
            Expect(duplicateRows.All(x => !x.IsEnabled) && Caption(duplicateRows[0]).EndsWith("(1)") &&
                duplicateRows[0].IsChecked == true && duplicateRows[1].IsChecked is null &&
                ReferenceEquals(duplicateRows[0], list.Items[0]), "Refreshing native matches lost count, selection or row identity");
            var updatedMatches = new Dictionary<(int Type, int Id), HashSet<string>>(nativeMatches) { [(0, 10)] = [] };
            list.Refresh([leaf, sibling], tree, () => changed++, updatedMatches, native: true);
            Expect(duplicateRows.All(x => x.IsEnabled) && Caption(duplicateRows[0]).EndsWith("(0)") && duplicateRows[0].IsChecked == true,
                "Refreshed native count did not replace the old count and retain selection");

        } finally { window.Close(); }
        Console.WriteLine("PASS category tree UI: nested rendering, descendant counts, independent expansion, retained selection/count updates, narrow fit and profile reset");
    }
}
