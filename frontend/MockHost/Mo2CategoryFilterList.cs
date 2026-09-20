using Avalonia;
using Avalonia.Controls;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal sealed class Mo2CategoryFilterList : ItemsControl
{
    protected override Type StyleKeyOverride => typeof(ItemsControl);
    internal HashSet<(int Type, int Id)> Selection { get; } = [];
    internal HashSet<(int Type, int Id)> Excluded { get; } = [];
    private readonly Dictionary<string, int> fallbackIds = new(StringComparer.OrdinalIgnoreCase);
    internal IEnumerable<string> IncludedNames => categoryRows.Where(x => Selection.Contains(x.Node.Key)).Select(x => x.Node.Name);
    internal IEnumerable<string> ExcludedNames => categoryRows.Where(x => Excluded.Contains(x.Node.Key)).Select(x => x.Node.Name);
    internal string Summary => string.Join(", ", IncludedNames.Concat(ExcludedNames.Select(x => "Not " + x)));
    private readonly HashSet<int> collapsedCategories = new();
    private readonly List<(CheckBox Box, Mo2CategoryNode Node, int[] Parents)> categoryRows = [];
    private string? categoryStructure = null;
    private void CategoryVisibility() {
        foreach (var row in categoryRows) row.Box.IsVisible = !row.Parents.Any(collapsedCategories.Contains);
    }

    internal void Reset()
    {
        Selection.Clear(); Excluded.Clear(); collapsedCategories.Clear(); fallbackIds.Clear(); categoryStructure = null;
    }

    internal void Refresh(IEnumerable<Mo2LiveMod> modsSource, Mo2CategoryNode[] tree, Action changed, IReadOnlyDictionary<(int Type, int Id), HashSet<string>>? matches = null, bool native = false, bool ready = true)
    {
        var mods = native ? [] : modsSource.Where(x => !x.IsSeparator).Select(x => x.CategoryNames).ToArray();
        var roots = tree;
        if (roots.Length == 0)
            roots = mods.SelectMany(x => x).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(name => {
                    if (!fallbackIds.TryGetValue(name, out var id)) fallbackIds[name] = id = -fallbackIds.Count - 1;
                    return new Mo2CategoryNode(id, 1, name, []);
                }).ToArray();
        var structure = System.Text.Json.JsonSerializer.Serialize(roots);
        if (structure != categoryStructure) {
            categoryStructure = structure;
            var available = roots.SelectMany(x => x.Walk()).Select(x => x.Key).ToHashSet();
            var removed = Selection.RemoveWhere(x => !available.Contains(x)) + Excluded.RemoveWhere(x => !available.Contains(x));
            if (removed > 0) changed();
            Items.Clear(); categoryRows.Clear();
            void Add(Mo2CategoryNode node, int[] parents) {
                var box = Mo2QtWidgets.Category(node.Name, 0, Excluded.Contains(node.Key) ? null : Selection.Contains(node.Key),
                    state => {
                        Selection.Remove(node.Key); Excluded.Remove(node.Key);
                        if (state == true) Selection.Add(node.Key);
                        else if (state is null) Excluded.Add(node.Key);
                        changed();
                    });
                box.Tag = node.Type == 1 ? node.Name : node;
                box.DataContext = node;
                box.Margin = new Thickness(parents.Length * 14, 0, 0, 0);
                if (node.Children.Length > 0) {
                    var label = new TextBlock { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
                    var expand = Mo2TableRow.IconButton("mdi-chevron-down", "Expand or collapse " + node.Name, () => { });
                    void SetExpanded() {
                        var collapsed = collapsedCategories.Contains(node.Id);
                        expand.Content = new UnifiedIcon { Value = new ProjektankerIcon(collapsed ? "mdi-chevron-right" : "mdi-chevron-down"), Size = Mo2Density.Glyph };
                        Avalonia.Automation.AutomationProperties.SetName(expand, (collapsed ? "Expand " : "Collapse ") + node.Name);
                        CategoryVisibility();
                    }
                    expand.AddHandler(Button.ClickEvent, (_, e) => {
                        if (!collapsedCategories.Add(node.Id)) collapsedCategories.Remove(node.Id);
                        SetExpanded(); e.Handled = true;
                    }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
                    expand.Width = 18; expand.Height = Mo2Density.Row;
                    var header = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*") };
                    Grid.SetColumn(label, 1); header.Children.Add(expand); header.Children.Add(label);
                    box.Content = header;
                    SetExpanded();
                }
                Items.Add(box); categoryRows.Add((box, node, parents));
                foreach (var child in node.Children) Add(child, [..parents, node.Id]);
            }
            foreach (var node in roots) Add(node, []);
            CategoryVisibility();
        }
        foreach (var row in categoryRows) {
            var names = native ? null : row.Node.Walk().Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var available = !native || (matches?.ContainsKey(row.Node.Key) == true);
            row.Box.IsEnabled = available && ready;
            var count = native ? (available ? matches![row.Node.Key].Count.ToString() : "…")
                : mods.Count(owned => owned.Any(names!.Contains)).ToString();
            var caption = $"{row.Node.Name} ({count})";
            if (row.Box.Content is Panel panel) {
                var text = panel.Children.OfType<TextBlock>().Single();
                if (text.Text != caption) text.Text = caption;
            } else if (row.Box.Content is TextBlock label && label.Text != caption) label.Text = caption;
        }
    }
}
