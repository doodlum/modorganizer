using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// Opt-in check for the view-options action: hiding and restoring columns, the
// last-column guard, reset to default, and that the choice survives a rebuild of
// the table source and a restart.
internal static class Mo2ColumnToggleCheck
{
    internal static async Task Run()
    {
        var host = new ContentControl();
        var window = new Window { Width = 1000, Height = 700, Content = host, ShowInTaskbar = false };
        window.Show();
        var panel = new Mo2ArchivesView();
        host.Content = panel;
        for (var attempt = 0; attempt < 40; attempt++) { await Task.Delay(60); window.UpdateLayout(); }
        try {
            // MO2 puts its own column chooser on the downloads header alone
            // (DownloadListView::onHeaderCustomContextMenu), which is where this
            // frontend puts that one. The chooser on these pages is the frontend's
            // own, for the columns its folder pages carry and MO2's tabs do not, and
            // it belongs in the page's own action row with the rest of them.
            //
            // Two earlier versions of this were both wrong about where to look. It
            // first wanted the button on the header line, and failed from the day the
            // tab toolbars went; it was then changed to require the page not to draw
            // one at all, which was true only because the page could not draw any of
            // its actions — a bug, not a decision. It is in the row now, with a press
            // on it, so neither mistake can be made again quietly.
            var action = panel.GetVisualDescendants().OfType<Button>().FirstOrDefault(x => x.Name == "ColumnToggleButton")
                ?? throw new Exception("Archives has no column chooser in its action row");
            if (panel.GetVisualDescendants().OfType<Panel>().FirstOrDefault(x => x.Name == "PanelActionRow") is not { } row ||
                !row.Children.Contains(action))
                throw new Exception("the column chooser is not in the page's own action row");
            if (!action.IsEffectivelyVisible || action.Bounds.Width <= 0) throw new Exception("the column chooser is not drawn");
            if (action.Flyout is null) throw new Exception("Column toggle has no menu");

            // The toggle is exercised directly: it owns the hidden set, and the panel
            // re-applies it to every freshly built source.
            var toggle = new Mo2ColumnToggle("column-check-fixture", () => { });
            var columns = new ColumnList<Mo2Archive> {
                new TextColumn<Mo2Archive, string>("Archive", x => x.Name),
                new TextColumn<Mo2Archive, string>("Mod", x => x.Mod),
                new TextColumn<Mo2Archive, string>("Loaded", x => x.Active ? "Yes" : "No"),
            };
            toggle.Apply(columns);
            if (columns.Count != 3) throw new Exception("A fresh table lost columns: " + columns.Count);
            if (toggle.Columns.Count != 3) throw new Exception("The toggle did not learn the column list");

            toggle.Toggle("Mod");
            var rebuilt = new ColumnList<Mo2Archive> {
                new TextColumn<Mo2Archive, string>("Archive", x => x.Name),
                new TextColumn<Mo2Archive, string>("Mod", x => x.Mod),
                new TextColumn<Mo2Archive, string>("Loaded", x => x.Active ? "Yes" : "No"),
            };
            toggle.Apply(rebuilt);
            if (rebuilt.Count != 2 || Enumerable.Any<IColumn<Mo2Archive>>(rebuilt, x => x.Header?.ToString() == "Mod"))
                throw new Exception("Hidden column came back when the source was rebuilt");

            // A reloaded toggle must remember the choice.
            var reloaded = new Mo2ColumnToggle("column-check-fixture", () => { });
            var afterRestart = new ColumnList<Mo2Archive> {
                new TextColumn<Mo2Archive, string>("Archive", x => x.Name),
                new TextColumn<Mo2Archive, string>("Mod", x => x.Mod),
            };
            reloaded.Apply(afterRestart);
            if (afterRestart.Count != 1) throw new Exception("The hidden column was not remembered across a reload");

            toggle.Toggle("Loaded");
            if (!toggle.CanHide("Archive") == false) { /* Archive is the only one left */ }
            if (toggle.CanHide("Archive")) throw new Exception("The last visible column can be hidden");
            toggle.Toggle("Archive");
            var guarded = new ColumnList<Mo2Archive> { new TextColumn<Mo2Archive, string>("Archive", x => x.Name) };
            toggle.Apply(guarded);
            if (guarded.Count != 1) throw new Exception("The table was left with no columns");

            toggle.Reset();
            var restored = new ColumnList<Mo2Archive> {
                new TextColumn<Mo2Archive, string>("Archive", x => x.Name),
                new TextColumn<Mo2Archive, string>("Mod", x => x.Mod),
                new TextColumn<Mo2Archive, string>("Loaded", x => x.Active ? "Yes" : "No"),
            };
            toggle.Apply(restored);
            if (restored.Count != 3) throw new Exception("Reset did not restore every column");
            if (toggle.Hidden.Count != 0) throw new Exception("Reset left hidden columns behind");

            Console.WriteLine("PASS column toggle: Archives draws its chooser in its own action row carrying its menu, " +
                "hiding survives a source rebuild and a reload, " +
                "the last visible column is protected, and reset restores all three columns");
        } finally {
            window.Close();
            foreach (var leftover in new[] { "columns-column-check-fixture.json" }) {
                var path = Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
                    "mo2-nexus-frontend", leftover);
                try { File.Delete(path); } catch (IOException) { }
            }
        }
    }
}
