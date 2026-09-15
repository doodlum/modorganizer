using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Layout;

namespace Mo2.Frontend;

// The view-options action the reference puts beside a table's Actions column: a
// gear that lists the table's columns as toggles, plus Reset to default. Panels
// rebuild their source on every render, so the hidden set lives here and is
// re-applied to each fresh column list.
internal sealed class Mo2ColumnToggle
{
    private static string PathFor(string panel) => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
        "mo2-nexus-frontend", "columns-" + panel + ".json");

    private readonly string _panel;
    private readonly Action _rerender;
    private readonly HashSet<string> _hidden = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _known = [];
    private readonly MenuFlyout _flyout = new() { Placement = PlacementMode.BottomEdgeAlignedRight };

    internal Button Action { get; }
    internal IReadOnlyCollection<string> Hidden => _hidden;
    internal IReadOnlyList<string> Columns => _known;

    internal Mo2ColumnToggle(string panel, Action rerender)
    {
        _panel = panel; _rerender = rerender;
        try {
            if (JsonSerializer.Deserialize<string[]>(File.ReadAllText(PathFor(panel))) is { } saved)
                foreach (var name in saved) _hidden.Add(name);
        } catch (IOException) { } catch (JsonException) { }
        Action = Mo2ModRow.IconButton("mdi-tune-variant", "Choose columns", () => { });
        Action.Name = "ColumnToggleButton";
        Action.Flyout = _flyout;
        _flyout.Opening += (_, _) => Build();
    }

    // The last visible column cannot be hidden: a table with no columns shows nothing
    // and offers no way back except the reset action.
    internal bool CanHide(string column) => _hidden.Contains(column) || _known.Count(x => !_hidden.Contains(x)) > 1;

    internal void Toggle(string column)
    {
        if (!_hidden.Remove(column)) {
            if (!CanHide(column)) return;
            _hidden.Add(column);
        }
        Save();
        _rerender();
    }

    internal void Reset()
    {
        if (_hidden.Count == 0) return;
        _hidden.Clear(); Save(); _rerender();
    }

    private void Save()
    {
        var path = PathFor(_panel);
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(_hidden.ToArray()));
            File.Move(path + ".tmp", path, true);
        } catch (IOException) { ToolTip.SetTip(Action, "Columns changed; preference could not be saved."); }
    }

    // Called with each freshly built column list: learns the full set once, then
    // drops whatever is hidden.
    internal void Apply<T>(IList<IColumn<T>> columns)
    {
        foreach (var column in columns) {
            if (Name(column) is { Length: > 0 } name && !_known.Contains(name)) _known.Add(name);
        }
        for (var index = columns.Count - 1; index >= 0; index--) {
            if (Name(columns[index]) is { Length: > 0 } name && _hidden.Contains(name)) columns.RemoveAt(index);
        }
    }

    private static string Name(IColumn column) => column.Header?.ToString() ?? "";

    private void Build()
    {
        _flyout.Items.Clear();
        foreach (var column in _known) {
            var item = new MenuItem { Header = column, StaysOpenOnClick = true,
                Icon = new CheckBox { IsChecked = !_hidden.Contains(column), IsHitTestVisible = false,
                    VerticalAlignment = VerticalAlignment.Center } };
            item.IsEnabled = CanHide(column);
            var name = column;
            item.Click += (_, _) => { Toggle(name); Build(); };
            _flyout.Items.Add(item);
        }
        _flyout.Items.Add(new MenuItem { Header = "-" });
        var reset = new MenuItem { Header = "Reset to default", IsEnabled = _hidden.Count > 0 };
        reset.Click += (_, _) => { Reset(); Build(); };
        _flyout.Items.Add(reset);
    }
}
