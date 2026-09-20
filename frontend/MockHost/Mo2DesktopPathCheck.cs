namespace Mo2.Frontend;

// A path handed to the file manager has to be one the file manager can find.
//
// Windows gives a packaged application its own view of the per-user data
// directory: this application writes to AppData/Local/x and the bytes land in
// AppData/Local/Packages/<package>/LocalCache/Local/x. Everything that compares
// folders — this application's idea of an instance against MO2's — collapses both
// spellings to the first, because the two name the same directory from inside.
//
// Explorer is not inside. Given the collapsed spelling it finds nothing and opens
// on its own home instead of the folder, which is what Open in Explorer and Reveal
// in Explorer did. So a path on its way out is spelled the other way, and this is
// what holds that apart from the comparing.
internal static class Mo2DesktopPathCheck
{
    public static void Run()
    {
        // The two spellings of one directory, as Windows gives them.
        const string virtualPath = @"C:\Users\someone\AppData\Local\mo2-nexus-frontend\instances\a-game\mods";
        const string realPath = @"C:\Users\someone\AppData\Local\Packages\Some.App_abc\LocalCache\Local\mo2-nexus-frontend\instances\a-game\mods";

        // Comparing still collapses both to the one spelling, or a host disowns its
        // own profile — which is what that behaviour was written for.
        var left = Mo2InstanceCatalog.LocalPath(virtualPath);
        var right = Mo2InstanceCatalog.LocalPath(realPath);
        if (!left.Equals(right, StringComparison.OrdinalIgnoreCase))
            throw new Exception($"The two spellings of one directory no longer compare equal:\n    {left}\n    {right}");
        Console.WriteLine($"  comparing: both spellings collapse to {left}");

        // Going out is the other way about, and is answered against this machine
        // rather than against a made-up path: the real store is found by looking for
        // the application's own directory inside a package's cache, because a
        // redirected process is not told that it is one.
        var own = Mo2ConfigPaths.DataCombine();
        var outward = Mo2InstanceCatalog.DesktopPath(own);
        var redirected = !outward.Equals(Path.GetFullPath(Mo2InstanceCatalog.LocalPath(own)), StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"  this application's data: {own}");
        Console.WriteLine($"  handed to the desktop as: {outward}");
        Console.WriteLine($"  {(redirected ? "redirected, so the real location is used" : "not redirected, so the path is handed on as it is")}");

        // Whatever it resolved to has to be somewhere that exists, or the file
        // manager opens on its own home — which is the whole failure this addresses.
        if (!Directory.Exists(outward))
            throw new Exception($"The desktop would be sent to {outward}, which is not there");

        if (redirected && outward.Replace('\\', '/').IndexOf("/LocalCache/Local/", StringComparison.OrdinalIgnoreCase) < 0)
            throw new Exception($"A redirected path resolved to something outside a package cache: {outward}");

        // A path with nothing to do with the data directory is never rewritten.
        const string elsewhere = @"C:\Program Files (x86)\Steam\steamapps\common\Fallout New Vegas";
        if (!Mo2InstanceCatalog.DesktopPath(elsewhere).Equals(Path.GetFullPath(elsewhere), StringComparison.OrdinalIgnoreCase))
            throw new Exception("A path outside the data directory was rewritten");
        Console.WriteLine("  a path elsewhere on the machine is left alone");

        Console.WriteLine("PASS desktop path: folders are compared under one spelling and handed to the desktop under the one it can find");
    }
}
