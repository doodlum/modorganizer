namespace Mo2.Frontend;

// MO2's settings and notifications had no route from the frontend. They are the
// host's own dialogs, reached by triggering the real main-window action, so what
// can be verified without a person at the screen is that the action is wired and
// that it only reaches the entries it is allowed to. Opening the dialog itself is
// deliberately not exercised: it is modal and waits for whoever opened it.
internal static class Mo2OriginalActionCheck
{
    internal static async Task Run(string directory)
    {
        var client = new Mo2BridgeClient(directory);
        // The host refuses any editing action whose profile does not match the one it
        // has selected, so the request carries it exactly as the frontend's does.
        var profile = (await client.SendAsync("snapshot")).GetProperty("profile").GetProperty("path").GetString();
        async Task<string> Rejects(string? name)
        {
            try {
                await client.SendAsync("openOriginal", new() { ["name"] = name, ["profilePath"] = profile });
                return "";
            } catch (Mo2BridgeCommandException error) { return error.Message; }
        }

        var unknown = await Rejects("actionQuit");
        if (unknown.Length == 0) throw new Exception("MO2 accepted a window action that is not on the allowlist");
        if (unknown.Contains("Unsupported bridge action", StringComparison.Ordinal))
            throw new Exception("The host has no openOriginal action at all; its bridge module is older than this build");
        if (!unknown.Contains("Unsupported MO2 window action", StringComparison.Ordinal))
            throw new Exception("An allowlisted name was rejected for the wrong reason: " + unknown);

        // A name off the allowlist and a name that is not a string are both refused
        // by the host, not by the frontend, so a stale frontend cannot widen it.
        var missing = await Rejects(null);
        if (missing.Length == 0) throw new Exception("MO2 accepted a window action with no name");

        Console.WriteLine("PASS original window actions: the host exposes openOriginal, refuses a name off its allowlist " +
            $"(\"{unknown}\") and refuses no name at all. Opening MO2's settings dialog is not exercised here; it is modal " +
            "and waits for the person who opened it");
    }
}
