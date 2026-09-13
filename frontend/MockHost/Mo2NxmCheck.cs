namespace Mo2.Frontend;

internal static class Mo2NxmCheck
{
    public static void Run()
    {
        const string url = "nxm://newvegas/mods/42507/files/1?key=synthetic%2Bvalue%2F&expires=123&user_id=456";
        var link = Mo2NexusLink.Parse(url);
        if (link.NxmUri != url || link.ModId != 42507 || link.FileId != 1 || link.ToString().Contains("synthetic"))
            throw new Exception("Signed NXM parameters were changed or disclosed");
        foreach (var value in new[] { "nxm://user@newvegas/mods/1/files/1", "nxm://newvegas:123/mods/1/files/1", "nxm://newvegas/mods/1/files/1#x", "nxm://newvegas/mods/0/files/1" }) {
            try { Mo2NexusLink.Parse(value); throw new Exception("Invalid NXM was accepted"); }
            catch (ArgumentException) { }
        }
        Mo2CatalogEntry Entry(string path,string game) => new(new(path,path + "/bridge"),new(path,game,"Default",path + "/mods",path + "/downloads",[],[]),null);
        var fnv = Entry("/fnv","New Vegas"); var second = Entry("/fnv-second","New Vegas"); var skyrim = Entry("/skyrim","Skyrim Special Edition");
        if (Mo2NxmRouter.Resolve("newvegas",[fnv,skyrim],new Dictionary<string,string>()) != fnv.Registration) throw new Exception("Wrong game route");
        if (Mo2NxmRouter.Resolve("newvegas",[fnv,second,skyrim],new Dictionary<string,string> { ["newvegas"] = second.Registration.Directory }) != second.Registration) throw new Exception("Preference ignored");
        foreach (var routes in new[] { new Dictionary<string,string>(), new Dictionary<string,string> { ["newvegas"] = "/missing" } }) {
            try { Mo2NxmRouter.Resolve("newvegas",[fnv,second],routes); throw new Exception("Ambiguous or stale route was accepted"); }
            catch (InvalidOperationException) { }
        }
        Console.WriteLine("PASS: signed NXM preservation, safe diagnostics, malformed links, game routing, explicit instance preference, ambiguous/stale target rejection");
    }
}
