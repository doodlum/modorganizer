namespace Mo2.Frontend;

// One home for the frontend's own preference files.
//
// This host previously spelled the same location two ways: XDG_CONFIG_HOME with
// SpecialFolder.ApplicationData as the fallback, and XDG_CONFIG_HOME with
// ~/.config. On Linux those agree, because .NET already resolves
// ApplicationData to XDG_CONFIG_HOME or ~/.config, so the difference never
// showed. On Windows they diverge — %APPDATA% against a literal ~/.config —
// which would split instances.json, layouts, pins and column preferences across
// two roots. Resolve it in one place instead.
//
// The Linux result is unchanged: XDG_CONFIG_HOME when set, otherwise ~/.config.
internal static class Mo2ConfigPaths
{
    internal static string Root =>
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
        ?? (OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"));

    // Frontend state lives under a single named directory in that root.
    internal static string Combine(params string[] parts) =>
        Path.Combine([Root, "mo2-nexus-frontend", .. parts]);

    // Preferences are small and belong with configuration; MO2 itself and the
    // instances the frontend owns are neither, so they live in the data location
    // for the platform rather than beside a handful of JSON files.
    internal static string DataRoot =>
        Environment.GetEnvironmentVariable("MO2_FRONTEND_DATA")
        ?? (OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetEnvironmentVariable("XDG_DATA_HOME")
              ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"));

    internal static string DataCombine(params string[] parts) =>
        Path.Combine([DataRoot, "mo2-nexus-frontend", .. parts]);
}
