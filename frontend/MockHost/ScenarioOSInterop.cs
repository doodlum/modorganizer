using NexusMods.App.UI.Notifications;
using NexusMods.Paths;
using NexusMods.Sdk;

namespace Mo2.Frontend;

// External operations are observable test actions in the frontend-only host.
internal sealed class ScenarioOSInterop : IOSInterop
{
    private static void Show(string operation) => new WindowNotificationService().ShowToast(operation);
    public AbsolutePath GetRunningExecutablePath(out string rawPath) { rawPath = Environment.ProcessPath!; return FileSystem.Shared.FromUnsanitizedFullPath(rawPath); }
    public void OpenUri(Uri uri) => Show($"Open link: {uri}");
    public void OpenFile(AbsolutePath path) => Show($"Open file: {path}");
    public void OpenDirectory(AbsolutePath path) => Show($"Open folder: {path}");
    public void OpenFileInDirectory(AbsolutePath path) => Show($"Show file: {path}");
    public ValueTask<FileSystemMount[]> GetFileSystemMounts(CancellationToken cancellationToken = default) => ValueTask.FromResult(Array.Empty<FileSystemMount>());
    public ValueTask<FileSystemMount?> GetFileSystemMount(AbsolutePath path, CancellationToken cancellationToken = default) => ValueTask.FromResult<FileSystemMount?>(null);
    public ValueTask RegisterUriSchemeHandler(string scheme, bool setAsDefaultHandler = true, CancellationToken cancellationToken = default) { Show($"Register protocol: {scheme}"); return ValueTask.CompletedTask; }
}
