using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Everything MO2 puts on a tab, drawn whole, in the layout MO2 itself uses.
//
// The frontend's own default is one page beside another; MO2's is the mod list on
// the left and its tabs on the right, which is narrower than any page was measured
// at. A widget that does not fit there is not missing — it is drawn, reported
// present, and cut in half — so this walks the preset's own tabs and fails on a
// control that is clipped by the panel holding it or squeezed below the size it
// asked for.
internal static class Mo2PresetFitCheck
{
    // A control may end up a pixel or two under what it asked for through rounding.
    private const double Slack = 2;
    // The least a list's name column can be and still read as a name rather than as
    // the icon in front of one.
    private const double NameColumn = 120;

    internal static async Task Run(Mo2LiveWorkspace live, Window window, string? directory)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();
        using var turn = await Mo2CheckTurn.Take();

        var workspace = live.WorkspaceController.ActiveWorkspace;
        var button = window.GetVisualDescendants().OfType<StandardButton>().Single(x => x.Name == "Mo2LayoutPresetButton");
        var menu = (MenuFlyout)button.Flyout!;
        var actions = menu.Items.OfType<MenuItem>().ToArray();
        var faults = new List<string>();
        var described = new List<string>();

        actions[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        try {
            await Until(() => workspace.Panels.Count == 2 && workspace.Panels.Any(x => x.Tabs.Count == 5),
                "The MO2 panel layout did not settle");
            var right = workspace.Panels.OrderBy(x => x.LogicalBounds.X).Last();
            var left = workspace.Panels.OrderBy(x => x.LogicalBounds.X).First();

            foreach (var panel in new[] { left, right }) {
                foreach (var tab in panel.Tabs.ToArray()) {
                    panel.SelectTab(tab.Id);
                    var title = (tab.Contents.ViewModel as IPageViewModelInterface)?.TabTitle ?? "page";
                    PanelView? view = null;
                    await Until(() => (view = window.GetVisualDescendants().OfType<PanelView>()
                        .FirstOrDefault(x => x.IsEffectivelyVisible && ReferenceEquals(x.ViewModel, panel))) is not null,
                        $"{title} never drew its panel");
                    // A panel reflows into its new page, so it is measured once it has
                    // stopped moving rather than as soon as the page is there.
                    var settled = default(Rect);
                    for (var attempt = 0; attempt < 30; attempt++) {
                        await Task.Delay(100);
                        window.UpdateLayout();
                        var now = view!.Bounds;
                        if (now == settled && now.Height > 0) break;
                        settled = now;
                    }

                    if (directory is not null) {
                        Directory.CreateDirectory(directory);
                        using var shot = new RenderTargetBitmap(new PixelSize(
                            Math.Max(1, (int)window.ClientSize.Width), Math.Max(1, (int)window.ClientSize.Height)));
                        shot.Render(window);
                        shot.Save(Path.Combine(directory, "preset-" + title.Replace(" ", "-").ToLowerInvariant() + ".png"));
                    }

                    // Every widget the page draws: MO2's buttons, boxes, fields and the
                    // captions beside them. A control cut off by the panel's edge, or
                    // given less room than it asked for, is one the user cannot read.
                    var bounds = new Rect(view!.Bounds.Size);
                    var clipped = new List<string>();
                    // The page's own widgets, not the panel's tab strip: the strip
                    // scrolls its tabs and squeezes its own close buttons by design,
                    // which is chrome every panel shares rather than anything a page
                    // draws.
                    foreach (var control in view.GetVisualDescendants().OfType<Control>()
                                 .Where(x => x.IsEffectivelyVisible && x is Button or CheckBox or RadioButton or ComboBox or TextBox)
                                 .Where(x => x.GetVisualAncestors().OfType<Control>().All(a => a.Name is not ("TabHeaderBorder" or "TabHeaderScrollViewer")))) {
                        var at = control.TranslatePoint(default, view);
                        if (at is null || control.Bounds.Width <= 0) continue;
                        var box = new Rect(at.Value, control.Bounds.Size);
                        if (box.Right > bounds.Right + Slack || box.Left < bounds.Left - Slack)
                            clipped.Add($"{Describe(control)} runs to {box.Right:F0} of {bounds.Right:F0}");
                        // What a control asked for includes the margin around it,
                        // which its bounds do not: comparing the two directly reported
                        // every widget with a margin as squeezed by exactly its margin.
                        else if (control.DesiredSize.Width - control.Margin.Left - control.Margin.Right - control.Bounds.Width > Slack)
                            clipped.Add($"{Describe(control)} is {control.Bounds.Width:F0}px where it asked for " +
                                $"{control.DesiredSize.Width - control.Margin.Left - control.Margin.Right:F0}" +
                                $" (in {string.Join(" < ", control.GetVisualAncestors().OfType<Control>().Take(3).Select(a => $"{a.GetType().Name}#{a.Name} {a.Bounds.Width:F0}"))})");
                    }
                    // And the column every list is read by. A table of fixed columns
                    // in a half-width panel spends its width on the ones beside the
                    // name and leaves the name itself at its icon: Data drew five
                    // columns of dates and sizes against a name column of nothing.
                    // Only a table that has its rows: one still being given its source
                    // has the columns it was born with, which is a placeholder column
                    // 30px wide rather than anything the page drew.
                    foreach (var table in view.GetVisualDescendants().OfType<TreeDataGrid>()
                                 .Where(x => x.Bounds.Width > 0 && x.Rows?.Count > 0)) {
                        var first = table.GetVisualDescendants().OfType<TreeDataGridColumnHeader>()
                            .OrderBy(x => x.TranslatePoint(default, table)?.X ?? 0).FirstOrDefault();
                        if (first is not null && first.Bounds.Width < NameColumn)
                            clipped.Add($"its name column is {first.Bounds.Width:F0}px of the table's {table.Bounds.Width:F0}");
                    }
                    if (clipped.Count > 0) faults.Add($"{title}: {string.Join("; ", clipped.Take(4))}");
                    else described.Add(title);
                }
            }
        } finally {
            actions[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await Task.Delay(800);
        }

        if (faults.Count > 0) Console.WriteLine("FAIL MO2 preset fit: " + string.Join(" | ", faults));
        else Console.WriteLine("PASS MO2 preset fit: in MO2's own panel layout, every widget on " +
            string.Join(", ", described) + " is drawn whole inside its panel");

        static string Describe(Control control) =>
            (control.Name ?? control.GetType().Name) +
            (control is ContentControl { Content: string text } && text.Length > 0 ? $" (\"{text}\")" : "");

        async Task Until(Func<bool> ready, string failure)
        {
            for (var attempt = 0; attempt < 200; attempt++) {
                if (ready()) return;
                await Task.Delay(100);
                window.UpdateLayout();
            }
            throw new Exception(failure);
        }
    }
}
