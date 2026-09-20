namespace Mo2.Frontend;

internal static class Mo2CategoryMembershipCheck
{
    internal static void Run()
    {
        var multi = new Mo2LiveMod(default, "one", "one", 2, 0, Category: "Primary",
            CategoriesJson: "[\"Primary\",\"Secondary\",\"Weapons, armour\"]");
        var primary = multi with { Name = "two", CategoriesJson = "[\"Primary\"]" };
        var empty = multi with { CategoriesJson = "[]" };
        void Expect(bool value, string detail) { if (!value) throw new Exception(detail); }
        bool Match(Mo2LiveMod mod, bool all, params string[] choices) => Mo2ModsAdapter.MatchesCategories(mod, choices, all);
        Expect(Match(multi, true, "secondary"), "Secondary category unavailable");
        Expect(Match(multi, true, "Primary", "Secondary") && !Match(primary, true, "Primary", "Secondary"), "And membership failed");
        Expect(Match(primary, false, "Primary", "Secondary"), "Or membership failed");
        Expect(Match(multi, true, "Weapons, armour") && !Match(multi, true, "Weapons"), "Comma name was split");
        Expect(!Match(empty, true, "Primary") && Match(empty, true), "Explicit empty membership used display fallback");
        var legacy = multi with { Category = "Weapons, armour", CategoriesJson = null };
        Expect(Match(legacy, true, "Weapons, armour") && !Match(legacy, true, "Weapons"), "Legacy primary category split");
        Expect(multi == multi with { CategoriesJson = new string(multi.CategoriesJson!.ToCharArray()) }, "Unchanged native membership invalidates row identity");
        Mo2CategoryNode[] tree = [new(1, 1, "Parent", [new(2, 1, "Child", [new(3, 1, "Leaf", [])])]), new(4, 1, "Sibling", [])];
        var leaf = multi with { CategoriesJson = "[\"Leaf\"]" };
        Expect(Mo2ModsAdapter.MatchesCategories(leaf, ["Parent"], true, tree), "Parent did not include grandchild");
        Expect(Mo2ModsAdapter.MatchesCategories(leaf, ["Parent", "Child"], true, tree), "Ancestor And failed");
        Expect(!Mo2ModsAdapter.MatchesCategories(leaf, ["Sibling"], true, tree), "Sibling included unrelated descendant");
        bool Inverted(Mo2LiveMod mod, bool all, string[] include, string[] exclude) => Mo2ModsAdapter.MatchesCategories(mod, include, all, tree, exclude);
        var unrelated = leaf with { CategoriesJson = "[\"Sibling\"]" };
        Expect(!Inverted(leaf, true, [], ["Parent"]) && Inverted(unrelated, true, [], ["Parent"]), "Inverse ancestor did not exclude descendants");
        Expect(Inverted(leaf, true, ["Parent"], ["Sibling"]) && !Inverted(unrelated, true, ["Parent"], ["Sibling"]), "Mixed And failed");
        Expect(Inverted(leaf, false, ["Parent"], ["Parent"]) && Inverted(unrelated, false, ["Parent"], ["Parent"]), "Or incorrectly applied exclusions after union");
        Expect(!Inverted(leaf, true, ["Parent"], ["Parent"]), "Contradictory And accepted a row");
        Expect(Inverted(empty, true, [], ["Parent"]), "Unassigned mod should satisfy inverse category");
        Console.WriteLine("PASS category membership: secondary assignments, And/Or, comma names, empty native list, legacy fallback and unchanged record equality");
    }
}
