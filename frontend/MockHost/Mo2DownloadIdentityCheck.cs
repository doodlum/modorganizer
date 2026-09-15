using System.Reflection;
using NexusMods.Abstractions.Downloads;

namespace Mo2.Frontend;

internal static class Mo2DownloadIdentityCheck
{
    internal static void Run()
    {
        var profile = new Mo2LiveProfile("");
        var connected = typeof(Mo2LiveProfile).GetField("_lastSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var downloads = typeof(Mo2LiveProfile).GetProperty(nameof(Mo2LiveProfile.Downloads))!;
        var path = typeof(Mo2LiveProfile).GetProperty(nameof(Mo2LiveProfile.ProfilePath))!;
        connected.SetValue(profile, "fixture"); path.SetValue(profile, "/profile-a");
        var provider = new Mo2DownloadProvider();
        var identities = (Dictionary<string, DownloadId>)typeof(Mo2DownloadProvider)
            .GetField("_ids", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(provider)!;
        var retained = new Mo2Download("retained.zip", "/retained.zip", 1, false, false, false);
        downloads.SetValue(profile, new[] { retained }); provider.Refresh(profile);
        var retainedId = identities[retained.Path];
        for (var i = 0; i < 200; i++) {
            var temporary = new Mo2Download($"{i}.zip", $"/{i}.zip", 1, false, false, false);
            downloads.SetValue(profile, new[] { retained, temporary }); provider.Refresh(profile);
            if (identities.Count != 2 || identities[retained.Path] != retainedId)
                throw new Exception("Download turnover retained removed IDs or changed a surviving row identity");
        }
        path.SetValue(profile, "/profile-b");
        downloads.SetValue(profile, new[] { retained }); provider.Refresh(profile);
        if (identities.Count != 1 || identities[retained.Path] == retainedId)
            throw new Exception("Profile change retained prior row identities");
        connected.SetValue(profile, null); provider.Refresh(profile);
        if (identities.Count != 0) throw new Exception("Disconnected provider retained obsolete identities");
        Console.WriteLine("PASS download identities: bounded during 200 file replacements, stable surviving rows, profile isolation and disconnect cleanup");
    }
}
