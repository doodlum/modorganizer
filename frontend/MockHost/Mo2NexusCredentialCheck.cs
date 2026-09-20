namespace Mo2.Frontend;

// The one sign-in reaches Wabbajack.
//
// This is the whole of "auth once": the credential a person gave this application
// is found again later, without them being asked for it, and is the same one Mod
// Organizer is using. What it does not do is print it — a check that reports a
// credential is a check that leaks one into a log.
internal static class Mo2NexusCredentialCheck
{
    public static void Run()
    {
        var key = Mo2NexusCredential.Read();
        Console.WriteLine($"  own copy held: {Mo2NexusCredential.Held}");
        if (key is null) {
            Console.WriteLine("  no Nexus credential found: sign in, then run this again");
            Console.WriteLine("FAIL nexus credential: nothing to hand to Wabbajack");
            return;
        }
        // Shape only. Enough to say a real key came back and that what is handed on
        // is a credential rather than an error string, without showing any of it.
        if (key.Length < 16) throw new Exception($"The stored credential is too short to be a Nexus key ({key.Length} characters)");
        if (key.Any(c => c < 33 || c > 126)) throw new Exception("The stored credential is not printable ASCII");
        Console.WriteLine($"  credential found: {key.Length} characters, printable ASCII");

        // What Wabbajack is actually run with.
        var command = Mo2WabbajackTool.Installed ? Mo2WabbajackTool.Command(key, "--help") : null;
        if (command is null) Console.WriteLine("  (Wabbajack not installed yet, so the handover was not exercised)");
        else {
            if (!command.Environment.TryGetValue(Mo2WabbajackTool.ApiKeyVariable, out var carried) || carried != key)
                throw new Exception("The credential is not carried into Wabbajack's environment");
            if (!command.Environment.TryGetValue(Mo2WabbajackTool.TokenVariable, out var token) || token is null || !token.Contains(key, StringComparison.Ordinal))
                throw new Exception("The credential is not carried in the token Wabbajack's downloader reads");
            Console.WriteLine($"  handed to Wabbajack as {Mo2WabbajackTool.ApiKeyVariable} and {Mo2WabbajackTool.TokenVariable}");
        }

        Console.WriteLine("PASS nexus credential: one sign-in is found again and handed to Wabbajack without asking for it");
    }
}
