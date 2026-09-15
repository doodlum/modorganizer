namespace Mo2.Frontend;

internal static class Mo2SearchQueryCheck
{
    internal static void Run()
    {
        var checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        var mod = new Mo2LiveMod(default, "MCM", "The Mod Configuration Menu", 2, 1,
            Conflicts: "Overwrites files", Version: "1.5", Category: "User Interface");
        bool Mod(string text, Mo2LiveMod? entry = null) => new Mo2SearchQuery(text, false).Matches(entry ?? mod);
        Check(Mod("configuration menu is:enabled category:\"User Interface\" has:conflicts"), "Combined mod qualifiers");
        Check(!Mod("is:disabled"), "Enabled mod excluded from disabled query");
        Check(Mod("is:disabled", mod with { State = 1 }), "Disabled mod included");
        Check(!Mod("is:disabled", mod with { State = 4 }), "Unmanaged game files are not disabled mods");
        Check(!Mod("is:disabled", mod with { State = 1, IsSeparator = true }), "Separators are not disabled mods");
        Check(Mod("is:separator", mod with { IsSeparator = true }), "Separator query");
        Check(!Mod("is:enabled is:disabled"), "Qualifiers are conjunctive");
        Check(Mod("IS:ENABLED category:\"user interface\" version:1.5"), "Case-insensitive values and keys");
        Check(!Mod("has:conflicts", mod with { Conflicts = "" }), "No-conflict mod excluded");
        var unknown = new Mo2SearchQuery("is:favorite", false);
        Check(!unknown.Structured && unknown.Text == "is:favorite", "Unknown qualifier stays literal");
        var partial = new Mo2SearchQuery("category:\"User Interface", false);
        Check(!partial.Structured, "Incomplete quote stays literal while typing");
        Check(!new Mo2SearchQuery("Acategory:User", false).Structured, "Only whole tokens are parsed");
        var plugin = new ScenarioPlugin("MCM.esp", "The Mod Configuration Menu", 1) { HasWarning = true, IsLocked = true };
        bool Plugin(string text) => new Mo2SearchQuery(text, true).Matches(plugin);
        Check(Plugin("mcm is:enabled is:locked has:warnings mod:\"Configuration Menu\""), "Combined plugin qualifiers");
        Check(!Plugin("is:disabled"), "Active plugin excluded");
        plugin.IsActive = false;
        Check(Plugin("is:disabled") && !Plugin("is:enabled"), "Predicate uses current activation");
        Check(!new Mo2SearchQuery("has:conflicts", true).Structured, "Unsupported page qualifier stays literal");
        Check(new Mo2SearchQuery("Configuration Menu", false).Text == "Configuration Menu", "Plain name phrase preserved");
        Console.WriteLine($"PASS: {checks} search query checks");
    }
}
