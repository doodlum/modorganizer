using System.Text.Json.Serialization;

namespace Mo2.Frontend;

internal sealed record Mo2CategoryNode(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("children")] Mo2CategoryNode[] Children)
{
    internal (int Type, int Id) Key => (Type, Id);
    internal IEnumerable<Mo2CategoryNode> Walk()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var descendant in child.Walk()) yield return descendant;
    }

    internal static HashSet<string> Names(string category, IEnumerable<Mo2CategoryNode> roots) =>
        roots.SelectMany(x => x.Walk()).Where(x => x.Type == 1 && x.Name.Equals(category, StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => x.Walk()).Where(x => x.Type == 1).Select(x => x.Name)
            .Append(category).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
