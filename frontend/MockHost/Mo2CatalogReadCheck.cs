using System.Diagnostics;

namespace Mo2.Frontend;

internal static class Mo2CatalogReadCheck
{
    public static async Task Run()
    {
        if (!OperatingSystem.IsLinux()) throw new NotSupportedException("This slow-filesystem check uses a Linux FIFO.");
        var root = Path.Combine(Path.GetTempPath(), "mo2-catalog-check-" + Guid.NewGuid().ToString("N"));
        var profile = Path.Combine(root, "profiles", "Default");
        Directory.CreateDirectory(profile);
        await File.WriteAllTextAsync(Path.Combine(root,"ModOrganizer.ini"), "[General]\ngameName=Catalog fixture\nselected_profile=Default\n");
        var fifo = Path.Combine(profile,"modlist.txt");
        try {
            var start = new ProcessStartInfo("mkfifo") { UseShellExecute = false };
            start.ArgumentList.Add(fifo);
            using (var process = Process.Start(start)!) {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0) throw new Exception("Could not create slow-read fixture");
            }
            var catalog = new Mo2InstanceCatalog(Path.Combine(root,"plugins","data","frontend-bridge"));
            var clock = Stopwatch.StartNew();
            var pending = catalog.ReadAsync();
            var returned = clock.Elapsed;
            await Task.Delay(200);
            var blockedOnFile = !pending.IsCompleted;
            // Release the real filesystem read only after confirming the caller
            // could continue while its profile file was unavailable.
            var writer = Task.Run(() => File.WriteAllText(fifo, "+Slow fixture mod\n"));
            var entries = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            await writer.WaitAsync(TimeSpan.FromSeconds(10));
            var fixture = entries.Single(x => x.Registration.Directory == root);
            if (!blockedOnFile || returned > TimeSpan.FromSeconds(1) || fixture.Instance?.Profiles.Single().ModEntries.Single().Name != "Slow fixture mod")
                throw new Exception("Catalog blocked the caller or lost the completed profile read");
            if (!catalog.Registrations.Any(x => x.Directory == root)) throw new Exception("Registration snapshot omitted the instance");
            Console.WriteLine($"PASS: catalog returned in {returned.TotalMilliseconds:F1} ms while a profile FIFO blocked; completed rows preserved after release");
        } finally { Directory.Delete(root,true); }
    }
}
