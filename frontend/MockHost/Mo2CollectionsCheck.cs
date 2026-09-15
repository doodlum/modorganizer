using NexusMods.MnemonicDB.Abstractions;
namespace Mo2.Frontend;
internal static class Mo2CollectionsCheck
{
    public static void Run()
    {
        Mo2LiveMod Mod(int id, string name, int priority, bool separator = false, bool overwrite = false) => new(EntityId.From((ulong)id), name, name.Replace("_separator", ""), 1, priority, IsSeparator: separator, IsOverwrite: overwrite);
        var mods = new[] { Mod(5,"B",4), Mod(2,"First_separator",1,true), Mod(6,"Empty_separator",5,true), Mod(1,"Game",0), Mod(4,"Second_separator",3,true), Mod(3,"A",2), Mod(7,"Overwrite",6,overwrite:true) };
        var groups = Mo2Collections.Build(mods);
        if (!groups.Select(x => x.Title).SequenceEqual(new[] { "Ungrouped", "First", "Second", "Empty" }) || !groups.Select(x => x.Mods.Count).SequenceEqual(new[] { 1,1,1,0 })) throw new Exception("Separator boundaries or priority ordering incorrect");
        if (!groups.SelectMany(x => x.Mods).Select(x => x.Name).SequenceEqual(new[] { "Game", "A", "B" })) throw new Exception("Wrong membership or overwrite included");
        groups = Mo2Collections.Build(mods.Where(x => !x.IsSeparator));
        if (groups.Count != 1 || groups[0].Mods.Count != 3) throw new Exception("Ungrouped mods missing");
        groups = Mo2Collections.Build(mods.Select(x => x.Name == "A" ? x with { Priority = 7 } : x));
        if (groups[1].Mods.Count != 0 || groups[3].Mods.Single().Name != "A") throw new Exception("Priority movement did not change collection membership");
        if (Mo2Collections.Build([]).Single().Mods.Count != 0) throw new Exception("Empty profile placeholder missing");
        Console.WriteLine("PASS collection boundaries, sorted membership, empty groups, ungrouped placeholder, priority movement, overwrite exclusion");
    }
}
