using System.Diagnostics;

namespace Mo2.Frontend;

// Wabbajack is downloaded, kept where this application keeps its own things, and
// runs. Running it is the part that matters: the published build is win-x64 and
// self-contained, and this machine is ARM64, so what is being checked is that it
// comes up under emulation without a .NET of its own installed.
internal static class Mo2WabbajackToolCheck
{
    public static async Task Run()
    {
        await Mo2WabbajackTool.Install(line => { if (line is { Length: > 0 }) Console.WriteLine("  " + line); });
        if (!Mo2WabbajackTool.Installed) throw new Exception("Wabbajack reported installed but its command line is not there");
        Console.WriteLine($"  {Mo2WabbajackTool.Executable} ({Mo2WabbajackTool.InstalledVersion})");

        var start = Mo2WabbajackTool.Command(apiKey: null, "--help");
        using var process = Process.Start(start) ?? throw new Exception("Wabbajack's command line did not start");
        var output = await process.StandardOutput.ReadToEndAsync();
        var errors = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var text = output + errors;
        // The two verbs this application relies on, proven present in the build that
        // was actually downloaded rather than assumed from the source.
        foreach (var verb in new[] { "install", "list-modlists" })
            if (!text.Contains(verb, StringComparison.Ordinal))
                throw new Exception($"Wabbajack's command line does not offer {verb}: {text[..Math.Min(400, text.Length)]}");

        // The login is handed over for the run and never written into Wabbajack's
        // own store, so the run has to carry it in its environment.
        var carried = Mo2WabbajackTool.Command("example-key", "--help");
        if (!carried.Environment.TryGetValue(Mo2WabbajackTool.ApiKeyVariable, out var value) || value != "example-key")
            throw new Exception("The Nexus login is not handed to Wabbajack's command line");

        Console.WriteLine($"PASS wabbajack tool: {Mo2WabbajackTool.InstalledVersion} downloaded, runs here, offers install and list-modlists");
    }
}
