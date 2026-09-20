using System.Diagnostics;
using System.Text.Json;

namespace Mo2.Frontend;

// The Wabbajack the frontend installs modlists with.
//
// Wabbajack is an application with a window of its own, and that window is never
// wanted here — a modlist install has to look like this application installing it.
// So what is used is the command line Wabbajack publishes beside that window in
// the same release, wabbajack-cli. It takes a modlist and an output folder and
// says what it is doing on stdout, which is all the frontend needs to show the
// work as its own.
//
// It is downloaded and kept here rather than found on the machine, for the same
// reason MO2 is: an installed Wabbajack belongs to the person who installed it,
// with their settings and their logins, and reaching into it would mean changing
// something the frontend does not own. The published build is self-contained, so
// nothing has to be installed for it to run — including on ARM64, where it runs
// under the same x64 emulation MO2 does.
internal static class Mo2WabbajackTool
{
    internal static string ToolRoot => Mo2ConfigPaths.DataCombine("wabbajack");
    // Where the release puts it: the release archive is the application folder,
    // and the command line sits in cli beneath it.
    internal static string Executable => Path.Combine(ToolRoot, "cli", "wabbajack-cli.exe");
    private static string VersionFile => Path.Combine(ToolRoot, ".installed-version");

    internal static bool Installed => File.Exists(Executable);
    internal static string? InstalledVersion =>
        File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : null;

    internal sealed record Release(string Version, string Url, long Size);

    // The newest published Wabbajack, and the full release archive from it. The tag
    // also carries the launcher as a bare Wabbajack.exe, which is the window this
    // deliberately does not use; the archive is the one holding the command line.
    internal static async Task<Release> LatestRelease(HttpClient client, CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.github.com/repos/wabbajack-tools/wabbajack/releases/latest");
        request.Headers.UserAgent.ParseAdd("mo2-nexus-frontend");
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var version = document.RootElement.GetProperty("tag_name").GetString()
            ?? throw new InvalidOperationException("The Wabbajack release has no tag");
        foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray()) {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            // Wabbajack.zip is the launcher on its own; the application archive is
            // named for the version.
            if (name.StartsWith("Wabbajack", StringComparison.OrdinalIgnoreCase)) continue;
            return new Release(version, asset.GetProperty("browser_download_url").GetString()!,
                asset.GetProperty("size").GetInt64());
        }
        throw new InvalidOperationException("The Wabbajack release has no application archive");
    }

    // Downloads and extracts Wabbajack if it is not already here. Extraction goes to
    // a staging directory and is moved into place once it is complete, so an install
    // that is interrupted never leaves a half-extracted Wabbajack that looks whole.
    internal static async Task Install(Action<string?> report, CancellationToken token = default)
    {
        if (Installed) return;

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(40) };
        report("Looking up the latest Wabbajack release…");
        var release = await LatestRelease(client, token);

        Directory.CreateDirectory(Mo2ConfigPaths.DataCombine());
        var archive = Path.Combine(Mo2ConfigPaths.DataCombine(), $"Wabbajack-{release.Version}.zip");
        if (!File.Exists(archive) || new FileInfo(archive).Length != release.Size) {
            report($"Downloading Wabbajack {release.Version}…");
            using var response = await client.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            var partial = archive + ".part";
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var destination = File.Create(partial))
                await Copy(source, destination, release.Size, report, token);
            File.Move(partial, archive, overwrite: true);
        }

        var staging = ToolRoot + ".staging";
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        report($"Extracting Wabbajack {release.Version}…");
        Mo2ArchiveFiles.Extract(archive, staging, token);

        var root = staging;
        if (!File.Exists(Path.Combine(root, "cli", "wabbajack-cli.exe")) &&
            Directory.GetDirectories(root) is [var only] && Directory.GetFiles(root).Length == 0)
            root = only;
        if (!File.Exists(Path.Combine(root, "cli", "wabbajack-cli.exe")))
            throw new InvalidOperationException("The Wabbajack archive contained no wabbajack-cli.exe");

        if (Directory.Exists(ToolRoot)) Directory.Delete(ToolRoot, recursive: true);
        Directory.Move(root, ToolRoot);
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        await File.WriteAllTextAsync(VersionFile, release.Version, token);
        report($"Wabbajack {release.Version} installed.");
    }

    private static async Task Copy(Stream source, Stream destination, long size, Action<string?> report, CancellationToken token)
    {
        var buffer = new byte[1 << 20];
        long done = 0;
        var announced = -1;
        while (true) {
            var read = await source.ReadAsync(buffer, token);
            if (read == 0) break;
            await destination.WriteAsync(buffer.AsMemory(0, read), token);
            done += read;
            if (size <= 0) continue;
            var percent = (int)(done * 100 / size);
            if (percent == announced) continue;
            announced = percent;
            report($"Downloading Wabbajack… {percent}%");
        }
    }

    // Wabbajack asks the machine for its own Nexus login and keeps it in a store of
    // its own. Nothing here writes into that store: the login this application
    // already holds is handed to the command line for the run instead, through the
    // environment variable Wabbajack reads before it asks for anything. So a person
    // who has signed in here is never asked to sign in again, and no credential of
    // theirs is copied anywhere it would outlive the install.
    internal const string ApiKeyVariable = "NEXUS_API_KEY";

    // Wabbajack reads its Nexus login from one of two places, and which one depends
    // on who is asking. Its Nexus API checks NEXUS_API_KEY; its downloader asks the
    // token store directly, which falls back to NEXUS_OAUTH_INFO and otherwise
    // throws before a single file is fetched — "No login data for nexus-oauth-info",
    // which is what the first real install run died of after resolving every archive.
    //
    // So both are set. The second is the store's own record with the OAuth half left
    // empty, which is exactly how Wabbajack represents a login that is an API key:
    // it sees no token to refresh, falls through to the key, and validates it as an
    // API key — which is also what tells it the account is premium.
    internal const string TokenVariable = "NEXUS_OAUTH_INFO";

    internal static string TokenFor(string apiKey) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?> {
            ["oauth"] = null,
            ["api_key"] = apiKey,
        });

    internal static ProcessStartInfo Command(string? apiKey, params string[] arguments)
    {
        if (!Installed) throw new InvalidOperationException("Wabbajack is not installed yet");
        var start = new ProcessStartInfo(Executable) {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = ToolRoot,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (string.IsNullOrWhiteSpace(apiKey)) return start;
        start.Environment[ApiKeyVariable] = apiKey;
        start.Environment[TokenVariable] = TokenFor(apiKey);
        return start;
    }
}
