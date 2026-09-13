using System.Text.Json;

namespace Mo2.Frontend;

/// <summary>Deterministic transport check; does not open native/account dialogs.</summary>
internal static class Mo2DialogQueueCheck
{
    public static async Task Run()
    {
        foreach (var action in new[] { "showModDetails", "manageNexusAccount", "launch", "manageProfile" })
            foreach (var outcome in action == "manageProfile" ? new[] { "same-profile", "changed-profile", "failed-refresh", "missing-profile" } : new[] { "same-profile", "changed-profile", "failed-refresh" })
                await Check(action, outcome);
        Console.WriteLine("PASS: 13 controlled transport cases cover dialog/PLAY/profile-card clicks during refresh, duplicate suppression, stale/unavailable launch/dialog rejection and native card ownership; no native dialogs or game launched");
    }

    private static async Task Check(string action, string outcome)
    {
        var directory = Path.Combine(Path.GetTempPath(), "mo2-dialog-queue-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(directory, "requests"));
        Directory.CreateDirectory(Path.Combine(directory, "responses"));
        var session = Guid.NewGuid().ToString();
        await File.WriteAllTextAsync(Path.Combine(directory, "endpoint.json"), JsonSerializer.Serialize(new { protocol = 1, session }));
        var cardPath = Path.Combine(directory, "card-profile");
        var expectedPath = "test-profile";
        object Snapshot(string path) => new {
            instance = new { }, profile = new { name = path, path },
            mods = new[] { new { name = "Test mod", displayName = "Test mod", state = 1, priority = 0 } },
            plugins = Array.Empty<object>(),
            profiles = outcome == "missing-profile" ? Array.Empty<object>() : new object[] { new { name = "card-profile", path = cardPath } },
        };
        async Task<string> Take(string expected)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline) {
                var paths = Directory.GetFiles(Path.Combine(directory, "requests"), "*.json");
                if (paths.Length > 0) {
                    if (paths.Length != 1) throw new InvalidOperationException("Unexpected concurrent bridge requests");
                    using var request = JsonDocument.Parse(await File.ReadAllTextAsync(paths[0]));
                    if (request.RootElement.GetProperty("action").GetString() != expected || request.RootElement.GetProperty("session").GetString() != session)
                        throw new InvalidOperationException("Unexpected queued dialog action/session");
                    if (expected != "snapshot" && request.RootElement.GetProperty("profilePath").GetString() != expectedPath)
                        throw new InvalidOperationException("Dialog used a different target profile");
                    return paths[0];
                }
                await Task.Delay(10);
            }
            throw new TimeoutException("Expected controlled bridge request: " + expected);
        }
        async Task Reply(string request, object result, bool ok = true)
        {
            var id = Path.GetFileNameWithoutExtension(request);
            var response = Path.Combine(directory, "responses", id + ".json");
            await File.WriteAllTextAsync(response + ".tmp", JsonSerializer.Serialize(new { id, session, ok, result, error = "Controlled refresh failure" }));
            File.Delete(request);
            File.Move(response + ".tmp", response);
        }
        try {
            var profile = new Mo2LiveProfile(directory);
            var initial = profile.Refresh();
            await Reply(await Take("snapshot"), Snapshot("test-profile"));
            await initial;
            Task Open() => action switch {
                "showModDetails" => profile.ShowModDetails(profile.Mods.Single().Id),
                "manageNexusAccount" => profile.ManageNexusAccount(),
                "launch" => profile.Launch("Test executable"),
                _ => profile.ManageProfile(new(directory, directory), new("card-profile", cardPath, [], []), "copy"),
            };
            var refresh = profile.Refresh();
            var held = await Take("snapshot");
            var command = Open();
            if (command.IsCompleted || !(profile.ManagingMod || profile.Launching || profile.SelectingProfile) || profile.CanUseDownloads)
                throw new InvalidOperationException("Dialog click was dropped or not marked busy during refresh");
            var duplicate = Open();
            if (!duplicate.IsCompleted) throw new InvalidOperationException("Duplicate dialog click was queued");
            await duplicate;
            await Reply(held, Snapshot(outcome == "changed-profile" ? "different-profile" : "test-profile"), outcome != "failed-refresh");
            await refresh;
            if (action == "manageProfile") {
                expectedPath = outcome == "changed-profile" ? "different-profile" : "test-profile";
                await Reply(await Take("snapshot"), Snapshot(expectedPath));
                if (outcome != "missing-profile") {
                    var request = await Take(action);
                    using var data = JsonDocument.Parse(await File.ReadAllTextAsync(request));
                    if (data.RootElement.GetProperty("name").GetString() != "card-profile" || data.RootElement.GetProperty("operation").GetString() != "copy")
                        throw new InvalidOperationException("Profile-card action lost its explicit target/operation");
                    await Reply(request, Snapshot(expectedPath));
                }
            } else if (outcome == "same-profile") {
                await Reply(await Take(action), new { opened = true, tab = "nexusTab", completed = true, exitCode = 0 });
                await Reply(await Take("snapshot"), Snapshot("test-profile"));
            }
            await command.WaitAsync(TimeSpan.FromSeconds(5));
            if (action != "manageProfile" && outcome == "changed-profile" && !profile.Status.StartsWith("The MO2 profile changed."))
                throw new InvalidOperationException("Changed dialog target was not reported");
            if (action == "manageProfile" && outcome == "missing-profile" && !profile.Status.Contains("does not own"))
                throw new InvalidOperationException("Missing native profile was not rejected");
            var connected = action == "manageProfile" ? outcome != "missing-profile" : outcome != "failed-refresh";
            if (profile.ManagingMod || profile.Launching || profile.SelectingProfile || Directory.GetFiles(Path.Combine(directory, "requests"), "*.json").Length != 0 ||
                profile.IsConnected != connected)
                throw new InvalidOperationException("Dialog queue did not finish cleanly or sent a stale/duplicate action");
        } finally { Directory.Delete(directory, recursive: true); }
    }
}
