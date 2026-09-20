namespace Mo2.Frontend;

// "Not endorsed yet" is not shown, and nothing else is lost with it.
//
// The risk in dropping a flag is dropping the row's other flags with it, or
// leaving a row flagged with nothing but whitespace — which still draws the glyph,
// because the glyph only asks whether the text is empty.
internal static class Mo2ModFlagsCheck
{
    public static void Run()
    {
        void Same(string flags, string expected, string why)
        {
            var actual = Mo2ModFlags.Shown(flags);
            if (actual != expected)
                throw new Exception($"{why}: expected {Quote(expected)}, got {Quote(actual)}");
        }

        Same("", "", "no flags at all");
        Same("Not endorsed yet", "", "the only flag being the one that is dropped");
        Same("not endorsed yet", "", "MO2's wording in another casing");
        Same("Not endorsed yet\nBackup", "Backup", "the dropped flag first");
        Same("Backup\nNot endorsed yet", "Backup", "the dropped flag last");
        Same("Backup\nNot endorsed yet\nInvalid", "Backup\nInvalid", "the dropped flag between two others");
        Same("Backup\nInvalid", "Backup\nInvalid", "flags that are all kept");
        // A flag whose text merely mentions endorsement is a different flag.
        Same("Endorsed", "Endorsed", "a flag that is not the one being dropped");

        // What the row actually asks: an emptied flag list must draw no glyph.
        if (Mo2ModFlags.Shown("Not endorsed yet").Length != 0)
            throw new Exception("A row whose only flag was dropped would still draw a flag glyph");

        Console.WriteLine("PASS mod flags: \"Not endorsed yet\" is dropped, every other flag survives, and a row left with none shows none");
    }

    private static string Quote(string value) => "\"" + value.Replace("\n", "\\n") + "\"";
}
