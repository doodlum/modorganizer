using System.Text.RegularExpressions;
using Avalonia.Controls;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.Filters;
using NexusMods.App.UI.Controls.Search;
using NexusMods.Abstractions.Games;
using NexusMods.MnemonicDB.Abstractions;

namespace Mo2.Frontend;

// Plain text stays a phrase, as in NMA. Only supported, complete qualifiers
// are removed from that phrase; unknown syntax still searches literal names.
internal sealed class Mo2SearchQuery
{
    private static readonly Regex Qualifier = new("(?<!\\S)(?<key>is|has|category|version|mod):(?:\"(?<value>[^\"]+)\"|(?<value>[^\\s\"]+))(?=\\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly List<(string Key, string Value)> _terms = [];
    internal string Text { get; }
    internal bool Structured => _terms.Count > 0;
    internal Mo2SearchQuery(string text, bool plugins)
    {
        Text = Regex.Replace(Qualifier.Replace(text, match => {
            var key = match.Groups["key"].Value.ToLowerInvariant();
            var value = match.Groups["value"].Value;
            var normalized = value.ToLowerInvariant();
            var supported = key switch {
                "is" => normalized is "enabled" or "disabled" || normalized == (plugins ? "locked" : "separator"),
                "has" => normalized == (plugins ? "warnings" : "conflicts"),
                "mod" => plugins,
                "category" or "version" => !plugins,
                _ => false
            };
            if (!supported) return match.Value;
            _terms.Add((key, value));
            return "";
        }), @"\s+", " ").Trim();
    }
    private static bool Contains(string text, string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);
    internal bool Matches(Mo2LiveMod mod) => Contains(mod.DisplayName, Text) && _terms.All(term => term.Key switch {
        "is" => term.Value.ToLowerInvariant() switch {
            "enabled" => !mod.IsSeparator && (mod.State & 2) != 0,
            "disabled" => !mod.IsSeparator && (mod.State & (2 | 4)) == 0,
            "separator" => mod.IsSeparator,
            _ => false
        },
        "has" => !string.IsNullOrWhiteSpace(mod.Conflicts),
        "category" => Contains(mod.Category, term.Value),
        "version" => Contains(mod.Version, term.Value),
        _ => false
    });
    internal bool Matches(ScenarioPlugin plugin) => Contains(plugin.DisplayName, Text) && _terms.All(term => term.Key switch {
        "is" => term.Value.ToLowerInvariant() switch {
            "enabled" => plugin.IsActive,
            "disabled" => !plugin.IsActive,
            "locked" => plugin.IsLocked,
            _ => false
        },
        "has" => plugin.HasWarning,
        "mod" => Contains(plugin.ModName, term.Value),
        _ => false
    });

    private sealed record RowFilter(Mo2LiveProfile Profile, Mo2SearchQuery Query, bool Plugins) : Filter
    {
        public override bool MatchesRow<TKey>(CompositeItemModel<TKey> itemModel) => Plugins
            ? itemModel.Key is ISortItemKey key && Profile.Order.FindPlugin(key) is { } plugin && Query.Matches(plugin)
            : itemModel.Key is EntityId id && Profile.FindMod(id) is { } mod && Query.Matches(mod);
    }

    internal static IDisposable Attach(SearchControl search, Mo2LiveProfile profile, bool plugins)
    {
        search.FilterFactory = text => {
            var query = new Mo2SearchQuery(text, plugins);
            return query.Structured ? new RowFilter(profile, query, plugins) : new Filter.TextFilter(text, CaseSensitive: false);
        };
        var box = search.FindControl<TextBox>("SearchTextBox")!;
        ToolTip.SetTip(box, plugins
            ? "Search names. Combine with is:enabled, is:disabled, is:locked, has:warnings or mod:\"Mod name\"."
            : "Search names. Combine with is:enabled, is:disabled, is:separator, has:conflicts, category:\"Category name\" or version:1.0.");
        var revision = profile.ContentRevision;
        void Refresh() {
            if (revision == profile.ContentRevision) return;
            revision = profile.ContentRevision;
            if (new Mo2SearchQuery(search.SearchText, plugins).Structured) search.RefreshSearchFilter();
        }
        profile.Changed += Refresh;
        search.RefreshSearchFilter();
        return System.Reactive.Disposables.Disposable.Create(() => {
            profile.Changed -= Refresh;
            search.FilterFactory = null;
        });
    }
}
