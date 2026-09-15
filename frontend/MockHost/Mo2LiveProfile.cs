using System.Reactive.Linq;
using System.Text.Json;
using DynamicData;
using DynamicData.Kernel;
using NexusMods.Abstractions.Loadouts;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Pages;
using NexusMods.App.UI.Pages.LoadoutPage;
using NexusMods.MnemonicDB.Abstractions;

namespace Mo2.Frontend;

internal readonly record struct Mo2ProfileTarget(string Endpoint, string ProfilePath);
// The trailing fields are MO2's own remaining download columns (downloadlist.cpp):
// Filetime, Mod name, Version, Nexus ID and Source Game.
internal sealed record Mo2Download(string Name, string Path, long Bytes, bool Partial, bool Installed, bool Paused, bool Failed = false,
    string Filetime = "", string ModName = "", string Version = "", string ModId = "", string SourceGame = "")
{
    public bool CanControl(string operation) => operation == "delete" ? !Partial || Paused || Failed : Partial && (operation switch {
        "resume" => Paused || Failed,
        "pause" or "cancel" => !Paused && !Failed,
        _ => false
    });
}
internal sealed record Mo2ArchiveInstallResult(string ModName, string? ModDirectory);
// The columns are MO2's own, so this frontend can show the list MO2 shows rather
// than a subset chosen here. An MO2 without one of them sends "" for it.
internal sealed record Mo2LiveMod(EntityId Id, string Name, string DisplayName, int State, int Priority, string PriorityText = "", string Conflicts = "", string Flags = "", bool IsOverwrite = false, int NexusId = 0, bool IsSeparator = false, string Version = "", string Category = "", string NewestVersion = "",
    string Content = "", string Author = "", string Uploader = "", string SourceGame = "", string InstallTime = "", string Notes = "",
    // The colour MO2 draws the row in — a separator's own colour, and the colour of
    // an ordinary mod's Notes cell. Empty when the user has not given the mod one.
    string Color = "", string NotesColor = "")
{
    public bool CanManage => !IsOverwrite && (IsSeparator || (State & 4) == 0);
    // MO2 reports the newest version it knows about; an update is only claimed when
    // it actually differs from what is installed.
    public bool HasUpdate => NewestVersion.Length > 0 && Version.Length > 0 &&
        !NewestVersion.Equals(Version, StringComparison.OrdinalIgnoreCase);
}

// Only a view of the running host. No activation/order/profile files are written here.
internal sealed class Mo2LiveProfile : IInstalledModsSource
{
    private Mo2BridgeClient? _client;
    private Mo2BridgeClient Client => _client ?? throw new InvalidOperationException("Select an MO2 profile before editing");
    private readonly SemaphoreSlim _commands = new(1);
    private readonly SourceCache<Mo2LiveMod, EntityId> _mods = new(x => x.Id);
    private readonly Dictionary<string, EntityId> _ids = new(StringComparer.OrdinalIgnoreCase);
    public R3.BindableReactiveProperty<string> CollectionName { get; } = new("Connecting to MO2…");
    public IReadOnlyCollection<Mo2LiveMod> Mods => _mods.Items.ToArray();
    public Mo2LiveMod? FindMod(EntityId id) => _mods.Lookup(id).ValueOrDefault();
    public ScenarioPluginOrder Order { get; } = new(false);
    public IReadOnlyList<Mo2Download> Downloads { get; private set; } = [];
    // The downloads MO2 has been told to hide, which its own Hidden files box shows.
    public IReadOnlyList<Mo2Download> HiddenDownloads { get; private set; } = [];
    public string Endpoint { get; private set; }
    public bool SelectingProfile { get; private set; }
    public bool? OriginalUiVisible { get; private set; }
    public bool CanChangeOriginalUi => IsConnected && OriginalUiVisible.HasValue && ProfilePath.Length > 0 && !Installing && !SelectingProfile && !Launching && !ManagingMod;
    public string? LogsDirectory { get; private set; }
    public string? DownloadsDirectory { get; private set; }
    internal (Mo2ExternalArchiveCopy Copy, Mo2ArchiveInstallResult Installed, Mo2ProfileTarget Target)? VerifiedExternalImport { get; set; }
    public string NexusGame { get; private set; } = "";
    public string GameName => NexusGame switch { "newvegas" => "Fallout: New Vegas", "skyrimspecialedition" => "Skyrim Special Edition", _ => "MO2 profile" };
    public bool Installing { get; private set; }
    public bool Launching { get; private set; }
    public bool ManagingMod { get; private set; }
    public bool CanSortPlugins { get; private set; }
    public string SortPluginsUnavailableReason { get; private set; } = "Connect to MO2 to sort plugins.";
    public IReadOnlyDictionary<string,string> ExecutableIcons { get; private set; } = new Dictionary<string,string>();
    public IReadOnlyList<Mo2Tool> Tools { get; private set; } = [];
    public string SelectedExecutable { get; private set; } = "";
    public IReadOnlyList<string> Executables { get; private set; } = [];
    public string ProfilePath { get; private set; } = "";
    public string Status { get; private set; } = "Connecting to MO2…";
    public event Action? Changed;
    public event Action<string,string,string>? CollectionRenamed;
    public event Action<string,string>? CollectionRemoved;
    public Mo2LiveProfile(string endpoint)
    {
        Endpoint = endpoint;
        if (endpoint.Length > 0) _client = new(endpoint);
        else {
            CollectionName.Value = "MO2 profiles";
            Status = "Select a profile in My Loadouts to connect to its MO2 instance";
        }
        Order.ApplyOrder = Reorder;
    }
    private string? _lastSnapshot;
    private string? _highlightSnapshot;
    private string? _contentSnapshot;
    public long ContentRevision { get; private set; }
    public bool IsConnected => _lastSnapshot is not null;
    public Mo2ProfileTarget CurrentTarget => new(Endpoint, ProfilePath);
    private bool CanStartHostAction => IsConnected && ProfilePath.Length > 0 && !Installing && !SelectingProfile && !Launching && !ManagingMod;
    public bool CanUseDownloads => CanStartHostAction;
    private bool CheckDownloadTarget(Mo2ProfileTarget target)
    {
        if (target != CurrentTarget) Status = "The MO2 profile changed. Choose the archive or download action again.";
        else if (!IsConnected || ProfilePath.Length == 0) Status = "Select a connected MO2 profile before using downloads.";
        else return true;
        Changed?.Invoke();
        return false;
    }
    private void Apply(JsonElement snapshot)
    {
        using var timing = Mo2UiLatencyProbe.Measure("Apply native snapshot");
        var raw = snapshot.GetRawText();
        if (raw == _lastSnapshot) return;
        _lastSnapshot = raw;
        CanSortPlugins = snapshot.TryGetProperty("canSortPlugins", out var canSort) && canSort.ValueKind == JsonValueKind.True;
        SortPluginsUnavailableReason = snapshot.TryGetProperty("sortPluginsUnavailableReason", out var sortReason)
            ? sortReason.GetString() ?? "Plugin sorting is unavailable in MO2." : "Plugin sorting is unavailable in MO2.";
        ExecutableIcons = snapshot.TryGetProperty("executableIcons", out var icons) ? icons.EnumerateObject().ToDictionary(x => x.Name,x => x.Value.GetString() ?? "") : new Dictionary<string,string>();
        SelectedExecutable = snapshot.TryGetProperty("selectedExecutable", out var selectedExecutable) ? selectedExecutable.GetString() ?? "" : "";
        Executables = snapshot.TryGetProperty("executables", out var executables) ? executables.EnumerateArray().Select(x => x.GetString()!).ToArray() : [];
        LogsDirectory = snapshot.GetProperty("instance").TryGetProperty("logsPath", out var logs) && logs.GetString() is { } logsPath ? Mo2InstanceCatalog.LocalPath(logsPath) : null;
        DownloadsDirectory = snapshot.GetProperty("instance").TryGetProperty("downloadsPath", out var downloadsPath) && downloadsPath.GetString() is { } downloadFolder ? Mo2InstanceCatalog.LocalPath(downloadFolder) : null;
        OriginalUiVisible = snapshot.GetProperty("instance").TryGetProperty("uiVisible", out var visible) && visible.ValueKind is JsonValueKind.True or JsonValueKind.False ? visible.GetBoolean() : null;
        NexusGame = snapshot.TryGetProperty("nexusGame", out var game) ? game.GetString() ?? "" : "";
        // MO2 keeps a download it has been told to hide and offers a box beside the
        // list to show them again, so the hidden ones are read and set aside rather
        // than dropped on the way in.
        var allDownloads = snapshot.TryGetProperty("downloads", out var downloads) ? downloads.EnumerateArray()
            .Select(x => (Hidden: x.GetProperty("hidden").GetBoolean(), Download: new Mo2Download(x.GetProperty("name").GetString()!, x.GetProperty("path").GetString()!, x.GetProperty("bytes").GetInt64(), x.GetProperty("partial").GetBoolean(), x.GetProperty("installed").GetBoolean(), x.GetProperty("paused").GetBoolean(), x.TryGetProperty("failed", out var failed) && failed.ValueKind == JsonValueKind.True,
                x.TryGetProperty("filetime", out var filetime) ? filetime.GetString() ?? "" : "",
                x.TryGetProperty("modName", out var downloadMod) ? downloadMod.GetString() ?? "" : "",
                x.TryGetProperty("version", out var downloadVersion) ? downloadVersion.GetString() ?? "" : "",
                x.TryGetProperty("modId", out var downloadModId) ? downloadModId.GetString() ?? "" : "",
                x.TryGetProperty("sourceGame", out var downloadGame) ? downloadGame.GetString() ?? "" : ""))).ToArray() : [];
        Downloads = allDownloads.Where(x => !x.Hidden).Select(x => x.Download).ToArray();
        HiddenDownloads = allDownloads.Where(x => x.Hidden).Select(x => x.Download).ToArray();
        var profile = snapshot.GetProperty("profile");
        if (ProfilePath != profile.GetProperty("path").GetString()) _selectedModNames = [];
        ProfilePath = profile.GetProperty("path").GetString()!;
        CollectionName.Value = profile.GetProperty("name").GetString()!;
        // An MO2 that does not carry one of these columns leaves it out rather than
        // sending it empty, so every one of them is optional here.
        static string Text(JsonElement owner, string name) =>
            owner.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
        var mods = snapshot.GetProperty("mods").EnumerateArray().Select(mod => {
            var name = mod.GetProperty("name").GetString()!;
            if (!_ids.TryGetValue(name, out var id)) _ids[name] = id = EntityId.From((ulong)_ids.Count + 100);
            return new Mo2LiveMod(id, name, mod.GetProperty("displayName").GetString()!, mod.GetProperty("state").GetInt32(), mod.GetProperty("priority").GetInt32(),
                mod.TryGetProperty("priorityText", out var priorityText) ? priorityText.GetString() ?? "" : mod.GetProperty("priority").GetInt32().ToString(),
                mod.TryGetProperty("conflicts", out var conflicts) ? conflicts.GetString() ?? "" : "",
                mod.TryGetProperty("flags", out var flags) ? flags.GetString() ?? "" : "",
                mod.TryGetProperty("overwrite", out var overwrite) && overwrite.GetBoolean(),
                mod.TryGetProperty("nexusId", out var nexusId) ? nexusId.GetInt32() : 0,
                mod.TryGetProperty("separator", out var separator) && separator.GetBoolean(),
                mod.TryGetProperty("version", out var version) ? version.GetString() ?? "" : "",
                mod.TryGetProperty("category", out var category) ? category.GetString() ?? "" : "",
                mod.TryGetProperty("newestVersion", out var newest) ? newest.GetString() ?? "" : "",
                Text(mod, "content"), Text(mod, "author"), Text(mod, "uploader"),
                Text(mod, "sourceGame"), Text(mod, "installTime"), Text(mod, "notes"),
                Text(mod, "color"), Text(mod, "notesColor"));
        }).ToArray();
        _mods.Edit(cache => {
            var ids = mods.Select(x => x.Id).ToHashSet();
            cache.RemoveKeys(cache.Keys.Where(x => !ids.Contains(x)).ToArray());
            foreach (var mod in mods) {
                var old = cache.Lookup(mod.Id);
                if (!old.HasValue || old.Value != mod) cache.AddOrUpdate(mod);
            }
        });
        Order.Replace(snapshot.GetProperty("plugins").EnumerateArray().Select(plugin => new ScenarioPlugin(
            plugin.GetProperty("name").GetString()!, plugin.GetProperty("origin").GetString()!, plugin.GetProperty("priority").GetInt32(),
            plugin.GetProperty("masters").EnumerateArray().Select(x => x.GetString()!).ToArray()) {
                IsActive = plugin.GetProperty("state").GetInt32() == 2,
                HasWarning = plugin.TryGetProperty("hasWarning", out var warning) && warning.GetBoolean(),
                IsLocked = plugin.TryGetProperty("locked", out var locked) && locked.GetBoolean(),
                CanToggle = !plugin.TryGetProperty("canToggle", out var toggle) || toggle.GetBoolean(),
                CanMove = !plugin.TryGetProperty("canMove", out var move) || move.GetBoolean(),
                Diagnostics = plugin.TryGetProperty("diagnostics", out var diagnostics) ? diagnostics.GetString() ?? "" : "",
                ModIndex = plugin.TryGetProperty("modIndex", out var modIndex) ? modIndex.GetString() ?? "" : "",
                PluginFlags = Text(plugin, "pluginFlags"), PriorityText = Text(plugin, "priorityText"),
                FormVersion = Text(plugin, "formVersion"), HeaderVersion = Text(plugin, "headerVersion"),
                Author = Text(plugin, "author"), Description = Text(plugin, "description"),
            }));
        var contentSnapshot = Endpoint + ProfilePath + snapshot.GetProperty("mods").GetRawText() + snapshot.GetProperty("plugins").GetRawText();
        if (contentSnapshot != _contentSnapshot) { _contentSnapshot = contentSnapshot; ContentRevision++; }
        var highlightSnapshot = Endpoint + ProfilePath + snapshot.GetProperty("mods").GetRawText();
        if (highlightSnapshot != _highlightSnapshot) { _highlightSnapshot = highlightSnapshot; _ = RefreshHighlights(); }
        Status = $"{mods.Count(x => !x.IsOverwrite && !x.IsSeparator)} mods · {Order.Plugins.Count} plugins · Connected to MO2";
        Changed?.Invoke();
    }
    // Counted so a check can tell "the timer asked MO2 for nothing" from "the timer
    // asked and MO2 had nothing to say", which look identical from the outside.
    internal int SnapshotReads { get; private set; }

    public async Task Refresh()
    {
        if (_client is null || !await _commands.WaitAsync(0)) return;
        _sinceFullRefresh = 0;
        SnapshotReads++;
        try { Apply(await Client.SendAsync("snapshot")); }
        catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }

    // The periodic refresh exists to notice changes made in MO2 itself; everything
    // the frontend does already applies the snapshot the action returns. A snapshot
    // costs MO2 tens of milliseconds on its own UI thread, so asking for one every
    // couple of seconds regardless taxed the host for nothing most of the time.
    // Changes made in MO2 land in the profile, the mods folder or the downloads
    // folder, so their timestamps decide whether to ask at all.
    private const int RefreshAnywayEvery = 10;
    private int _sinceFullRefresh;
    private (DateTime Profile, DateTime Mods, DateTime Downloads) _watched;

    internal (DateTime Profile, DateTime Mods, DateTime Downloads) WatchedStamps()
    {
        DateTime Stamp(string? path)
        {
            if (string.IsNullOrEmpty(path)) return default;
            try { return Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path) : default; }
            catch (IOException) { return default; }
            catch (UnauthorizedAccessException) { return default; }
        }
        var profile = ProfilePath.Length == 0 ? null : Mo2InstanceCatalog.LocalPath(ProfilePath);
        var mods = profile is null ? null : Path.GetFullPath(Path.Combine(profile, "..", "..", "mods"));
        return (Stamp(profile), Stamp(mods), Stamp(DownloadsDirectory));
    }

    public async Task RefreshIfChanged()
    {
        if (_client is null) return;
        var stamps = WatchedStamps();
        // A full snapshot still runs regularly, so nothing that changed somewhere
        // these timestamps do not cover can stay stale indefinitely.
        if (stamps == _watched && ++_sinceFullRefresh < RefreshAnywayEvery) return;
        _watched = stamps;
        await Refresh();
    }
    public async Task<(string Title, string Details)[]> ReadHealth()
    {
        var target = CurrentTarget;
        var path = target.ProfilePath;
        if (path.Length == 0) throw new InvalidOperationException("Select an MO2 profile to check its health.");
        await _commands.WaitAsync();
        try {
            if (CurrentTarget != target) throw new InvalidOperationException("Profile changed; checking the selected profile again.");
            var result = await Client.SendAsync("healthCheck", new() { ["profilePath"] = path });
            return result.GetProperty("problems").EnumerateArray().Select(x =>
                (x.GetProperty("title").GetString()!, x.GetProperty("details").GetString()!)).ToArray();
        } finally { _commands.Release(); }
    }
    public async Task<Mo2Tool[]> ReadTools()
    {
        var target = CurrentTarget;
        await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) throw new InvalidOperationException("Connect to the selected MO2 profile to view tools.");
            var result = await Client.SendAsync("listTools", new() { ["profilePath"] = target.ProfilePath });
            var tools = result.GetProperty("tools").EnumerateArray().Select(x => new Mo2Tool(
                x.GetProperty("id").EnumerateArray().Select(y => y.GetString()!).ToArray(),
                x.GetProperty("name").GetString()!, x.GetProperty("group").GetString()!,
                x.GetProperty("description").GetString()!, x.GetProperty("enabled").GetBoolean(), x.TryGetProperty("icon", out var icon) ? icon.GetString() ?? "" : "")).ToArray();
            Tools = tools; Changed?.Invoke(); return tools;
        } finally { _commands.Release(); }
    }
    public async Task RunTool(Mo2Tool tool, Mo2ProfileTarget target, bool manageExecutables = false)
    {
        if (!CanStartHostAction) return;
        ManagingMod = true; Status = "Opening " + tool.Name; Changed?.Invoke();
        await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) throw new InvalidOperationException("The profile changed. Refresh Tools before opening a tool.");
            await Client.SendAsync(manageExecutables ? "manageExecutables" : "runTool", new() { ["profilePath"] = target.ProfilePath, ["tool"] = tool.Id }, timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task<Mo2OverwriteFile[]> ReadOverwrite(Mo2ProfileTarget target)
    {
        await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) throw new InvalidOperationException("Connect to the selected MO2 profile to view Overwrite.");
            var result = await Client.SendAsync("readOverwrite", new() { ["profilePath"] = target.ProfilePath });
            return result.GetProperty("files").EnumerateArray().Select(x => new Mo2OverwriteFile(x.GetProperty("path").GetString()!, x.GetProperty("bytes").GetInt64())).ToArray();
        } finally { _commands.Release(); }
    }
    public async Task<string?> SaveAction(Mo2Save save, string operation, Mo2ProfileTarget target)
    {
        if (!CanStartHostAction || target != CurrentTarget) return "Connect to the selected profile and close any current MO2 dialog before using a save action.";
        ManagingMod = true; Changed?.Invoke(); await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) return "The profile changed. Select the save again.";
            Status = "Opening MO2 save action…"; Changed?.Invoke();
            await Client.SendAsync("saveAction", new() { ["profilePath"] = target.ProfilePath, ["file"] = save.File, ["operation"] = operation }, timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
            return null;
        } catch (Exception error) { Report(error); return error.Message; }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task<Mo2Save[]> ReadSaves(Mo2ProfileTarget target)
    {
        await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) throw new InvalidOperationException("Connect to the selected MO2 profile to browse saves.");
            var result = await Client.SendAsync("readSaves", new() { ["profilePath"] = target.ProfilePath });
            return result.GetProperty("saves").EnumerateArray().Select(x => new Mo2Save(x.GetProperty("name").GetString()!, x.GetProperty("file").GetString()!)).ToArray();
        } finally { _commands.Release(); }
    }
    public async Task<Mo2DataEntry[]> ReadDataDirectory(Mo2ProfileTarget target, string directory)
    {
        await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) throw new InvalidOperationException("Connect to the selected MO2 profile to browse Data.");
            var result = await Client.SendAsync("readDataDirectory", new() { ["profilePath"] = target.ProfilePath, ["directory"] = directory });
            return result.GetProperty("entries").EnumerateArray().Select(x => new Mo2DataEntry(x.GetProperty("name").GetString()!, x.GetProperty("directory").GetBoolean(), x.GetProperty("origins").EnumerateArray().Select(o => o.GetString()!).ToArray(), x.GetProperty("archive").GetString()!,
                x.TryGetProperty("size", out var size) ? size.GetString() ?? "" : "",
                x.TryGetProperty("modified", out var modified) ? modified.GetString() ?? "" : "")).ToArray();
        } finally { _commands.Release(); }
    }
    public async Task<Mo2Archive[]> ReadArchives(Mo2ProfileTarget target)
    {
        await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) throw new InvalidOperationException("Connect to the selected MO2 profile to browse archives.");
            var result = await Client.SendAsync("readArchives",new() { ["profilePath"] = target.ProfilePath });
            return result.GetProperty("archives").EnumerateArray().Select(x => new Mo2Archive(x.GetProperty("name").GetString()!,x.GetProperty("mod").GetString()!,x.GetProperty("active").GetBoolean(),x.GetProperty("canToggle").GetBoolean())).ToArray();
        } finally { _commands.Release(); }
    }
    // MO2's own tick beside an archive, which decides whether MO2 manages it. MO2
    // writes the profile's archive list whenever that list changes, so ticking the
    // item in MO2 is the whole operation.
    public async Task<Mo2Archive[]?> SetArchiveManaged(Mo2Archive archive, bool enabled, Mo2ProfileTarget target)
    {
        if (!CanStartHostAction || target != CurrentTarget) return null;
        ManagingMod = true; Changed?.Invoke(); await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) return null;
            Status = (enabled ? "Letting MO2 manage " : "Leaving ") + archive.Name + (enabled ? "" : " to the game"); Changed?.Invoke();
            var result = await Client.SendAsync("setArchiveManaged", new() {
                ["profilePath"] = target.ProfilePath, ["name"] = archive.Name, ["mod"] = archive.Mod, ["enabled"] = enabled });
            return result.GetProperty("archives").EnumerateArray().Select(x => new Mo2Archive(
                x.GetProperty("name").GetString()!, x.GetProperty("mod").GetString()!,
                x.GetProperty("active").GetBoolean(), x.GetProperty("canToggle").GetBoolean())).ToArray();
        } catch (Exception error) { Report(error); return null; }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task SortPlugins(Mo2ProfileTarget target)
    {
        if (!CanStartHostAction || !CanSortPlugins || target != CurrentTarget) return;
        await _commands.WaitAsync();
        try {
            if (!CanStartHostAction || !CanSortPlugins || target != CurrentTarget) return;
            ManagingMod = true; Status = "Sorting plugins in MO2…"; Changed?.Invoke();
            await Client.SendAsync("sortPlugins", new() { ["profilePath"] = target.ProfilePath }, timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task OrderBackup(string list, string operation, Mo2ProfileTarget target)
    {
        if (!CanStartHostAction || target != CurrentTarget) return;
        ManagingMod = true; Changed?.Invoke(); await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) return;
            Status = "Opening MO2 " + (list == "mods" ? "mod-list " : "plugin-order ") + operation + "…"; Changed?.Invoke();
            await Client.SendAsync("orderBackup", new() { ["profilePath"] = target.ProfilePath, ["list"] = list, ["operation"] = operation }, timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    // MO2's own "Select Color..." and "Reset Color", which colour the row a mod is
    // drawn in — the whole row for a separator, the Notes cell for a mod. Passing no
    // colour takes MO2's reset.
    public async Task SetModColor(string name, string? color, Mo2ProfileTarget target)
    {
        if (!CanStartHostAction || target != CurrentTarget) return;
        ManagingMod = true; Changed?.Invoke(); await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) return;
            Status = (color is null ? "Resetting the colour of " : "Colouring ") + name + " in MO2"; Changed?.Invoke();
            await Client.SendAsync("setModColor", new() { ["profilePath"] = target.ProfilePath, ["name"] = name, ["color"] = color });
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task SetPluginLocked(string name, bool locked, Mo2ProfileTarget target)
    {
        if (!CanStartHostAction || target != CurrentTarget) return;
        ManagingMod = true; Changed?.Invoke(); await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) return;
            await Client.SendAsync("setPluginLocked", new() { ["profilePath"] = target.ProfilePath, ["name"] = name, ["locked"] = locked });
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task ExtractArchive(Mo2Archive archive, Mo2ProfileTarget target)
    {
        if (!CanStartHostAction || target != CurrentTarget) return;
        ManagingMod = true; Changed?.Invoke(); await _commands.WaitAsync();
        try {
            if (target != CurrentTarget) return;
            Status = "Choose an extraction folder in MO2"; Changed?.Invoke();
            await Client.SendAsync("extractArchive", new() { ["profilePath"] = target.ProfilePath, ["name"] = archive.Name, ["mod"] = archive.Mod }, timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task<string?> PreviewDataFile(string directory, Mo2DataEntry entry, Mo2ProfileTarget target, string operation = "preview")
    {
        if (!CanStartHostAction || target != CurrentTarget || entry.Directory) return "Connect to the selected profile and select a file after closing any current MO2 dialog.";
        ManagingMod = true; Changed?.Invoke(); await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) return "The profile changed. Select the file again.";
            Status = (operation switch { "preview" => "Previewing ", "hide" => "Hiding ", "reveal" => "Revealing ", _ => "Unhiding " }) + entry.Name + " in MO2"; Changed?.Invoke();
            var result = await Client.SendAsync("dataFileAction", new() { ["operation"] = operation == "reveal" && OperatingSystem.IsLinux() ? "revealPath" : operation, ["profilePath"] = target.ProfilePath, ["directory"] = directory, ["name"] = entry.Name, ["origins"] = entry.Origins }, timeout: TimeSpan.FromMinutes(30));
            if (operation == "reveal" && OperatingSystem.IsLinux()) {
                var nativePath = result.GetProperty("path").GetString()!;
                if (!nativePath.Replace('\\', '/').StartsWith("Z:/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("This file is inside a Wine drive that cannot be opened by the Linux file manager.");
                var path = Mo2InstanceCatalog.LocalPath(nativePath);
                if (!File.Exists(path)) throw new FileNotFoundException("The source file moved; refresh Data before opening its location.");
                var start = new System.Diagnostics.ProcessStartInfo("xdg-open") { UseShellExecute = false };
                start.ArgumentList.Add(Path.GetDirectoryName(path)!);
                await Mo2DesktopLauncher.StartAsync(start);
            }
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
            return null;
        } catch (Exception error) { Report(error); return error.Message; }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task BrowseArchive(string name,Mo2ProfileTarget target)
    {
        if (!CanStartHostAction || target != CurrentTarget) return;
        ManagingMod = true; Changed?.Invoke(); await _commands.WaitAsync();
        try {
            if (target != CurrentTarget) return;
            Status = "Browsing " + name + " in MO2"; Changed?.Invoke();
            await Client.SendAsync("previewArchive",new() { ["profilePath"] = target.ProfilePath, ["name"] = name },timeout:TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task OverwriteAction(string operation, Mo2ProfileTarget target)
    {
        if (!CanStartHostAction) return;
        ManagingMod = true; Status = "Opening MO2 Overwrite action…"; Changed?.Invoke();
        await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) throw new InvalidOperationException("The profile changed. Reopen Overwrite before continuing.");
            await Client.SendAsync("overwriteAction", new() { ["profilePath"] = target.ProfilePath, ["operation"] = operation }, timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    private void Report(Exception error)
    {
        // A correlated native rejection proves the host responded. Preserve the
        // last state until polling updates it; failures to read state still disconnect.
        if (error is not Mo2BridgeCommandException { Action: not "snapshot" }) _lastSnapshot = null;
        Status = error.Message; Changed?.Invoke();
    }
    public void Toggle(IEnumerable<LoadoutItemId> ids)
    {
        var selected = ids.Select(id => _mods.Lookup(id.Value)).Where(x => x.HasValue).Select(x => x.Value).ToArray();
        _ = ToggleAsync(selected, CurrentTarget);
    }
    public Task ToggleMod(EntityId id) => FindMod(id) is { } mod ? ToggleAsync([mod], CurrentTarget) : Task.CompletedTask;
    private async Task ToggleAsync(Mo2LiveMod[] selected, Mo2ProfileTarget target)
    {
        await _commands.WaitAsync();
        try {
            foreach (var mod in selected) {
                if (target != CurrentTarget || !CanStartHostAction) return;
                if ((mod.State & 4) != 0) continue; // MO2 essential content cannot be disabled.
                Apply(await Client.SendAsync("setModActive", new() { ["profilePath"] = target.ProfilePath, ["name"] = mod.Name, ["enabled"] = (mod.State & 2) == 0 }));
            }
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    private async Task Reorder(ScenarioPlugin[] desired, CancellationToken token)
    {
        var target = CurrentTarget;
        var profile = target.ProfilePath;
        await _commands.WaitAsync(token);
        try {
            if (target != CurrentTarget || !CanStartHostAction) throw new InvalidOperationException("Active profile changed; select the plugins again");
            var baseline = desired.OrderBy(x => x.SortIndex).Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray();
            Apply(await Client.SendAsync("snapshot", cancellationToken: token));
            if (target != CurrentTarget || !baseline.SequenceEqual(Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive))))
                throw new InvalidOperationException("MO2 plugin state changed; select the plugins again before reordering");
            for (var index = 0; index < desired.Length; index++) {
                var current = Order.Plugins.FirstOrDefault(x => x.DisplayName == desired[index].DisplayName);
                if (current?.SortIndex == index) continue;
                Apply(await Client.SendAsync("setPluginPriority", new() { ["profilePath"] = profile, ["name"] = desired[index].DisplayName, ["priority"] = index }, token));
            }
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    public async Task SetPluginsActive(IEnumerable<string> names, bool enabled)
    {
        var target = CurrentTarget;
        var selected = names.ToArray();
        await _commands.WaitAsync();
        try {
            foreach (var name in selected) {
                if (target != CurrentTarget || !CanStartHostAction) return;
                if (!Order.Plugins.Any(plugin => plugin.DisplayName == name && plugin.CanToggle && plugin.IsActive != enabled)) continue;
                Apply(await Client.SendAsync("setPluginActive", new() { ["profilePath"] = target.ProfilePath, ["name"] = name, ["enabled"] = enabled }));
            }
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    public async Task MoveMod(EntityId id, int delta, bool absolute = false)
    {
        var target = CurrentTarget;
        var profile = target.ProfilePath;
        Status = "Updating mod priority…"; Changed?.Invoke();
        await _commands.WaitAsync();
        try {
            if (target != CurrentTarget || !CanStartHostAction) throw new InvalidOperationException("Active profile changed; select the mod again");
            var found = _mods.Lookup(id);
            if (!found.HasValue) return;
            var mod = found.Value;
            if (!mod.CanManage || mod.Priority < 0) return;
            var priority = Math.Clamp(absolute ? delta : mod.Priority + delta, 0, _mods.Items.Where(x => !x.IsOverwrite && x.Priority >= 0).Max(x => x.Priority));
            Apply(await Client.SendAsync("setModPriority", new() { ["profilePath"] = profile, ["name"] = mod.Name, ["priority"] = priority }));
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    public string[] ModPriorityOrder => Mods.Where(x => !x.IsOverwrite && x.Priority >= 0).OrderBy(x => x.Priority).Select(x => x.Name).ToArray();
    public async Task MoveModsRelative(EntityId[] ids, EntityId targetId, bool after, Mo2ProfileTarget requestedTarget, string[] expectedOrder)
    {
        await _commands.WaitAsync();
        try {
            if (requestedTarget != CurrentTarget || !CanStartHostAction) return;
            Apply(await Client.SendAsync("snapshot"));
            if (!ModPriorityOrder.SequenceEqual(expectedOrder)) throw new InvalidOperationException("Mod priorities changed while dragging; select the mods again");
            var selected = ids.Select(FindMod).ToArray();
            var target = FindMod(targetId);
            if (selected.Any(x => x is null || !x.CanManage) || target is null || target.IsOverwrite) return;
            var moves = Mo2ModOrder.Plan(expectedOrder, selected.Select(x => x!.Name), target.Name, after);
            ManagingMod = true; Status = "Updating mod priorities…"; Changed?.Invoke();
            var expected = expectedOrder.ToList();
            foreach (var move in moves) {
                if (requestedTarget != CurrentTarget) throw new InvalidOperationException("Active profile changed; remaining moves cancelled");
                expected.Remove(move.Name); expected.Insert(move.Priority, move.Name);
                Apply(await Client.SendAsync("setModPriority", new() { ["profilePath"] = requestedTarget.ProfilePath, ["name"] = move.Name, ["priority"] = move.Priority }));
                if (!ModPriorityOrder.SequenceEqual(expected)) throw new InvalidOperationException("MO2 adjusted the requested order; remaining moves cancelled");
            }
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task<bool> SelectProfile(Mo2Registration registration, Mo2ProfileSnapshot selected)
    {
        await _commands.WaitAsync();
        try {
            SelectingProfile = true;
            Status = "Connecting to " + selected.Name; Changed?.Invoke();
            var snapshot = await Mo2HostStartup.Connect(registration, message => { if (Status != message) { Status = message; Changed?.Invoke(); } });
            var client = new Mo2BridgeClient(registration.Endpoint);
            if (!snapshot.GetProperty("profiles").EnumerateArray().Any(x =>
                Mo2InstanceCatalog.LocalPath(x.GetProperty("path").GetString()!) == Path.GetFullPath(selected.Directory)))
                throw new InvalidOperationException("The MO2 host does not own the selected profile");
            var current = snapshot.GetProperty("profile");
            if (Mo2InstanceCatalog.LocalPath(current.GetProperty("path").GetString()!) != Path.GetFullPath(selected.Directory))
                snapshot = await client.SendAsync("selectProfile", new() { ["profilePath"] = current.GetProperty("path").GetString(), ["name"] = selected.Name }, timeout: TimeSpan.FromMinutes(2));
            if (Mo2InstanceCatalog.LocalPath(snapshot.GetProperty("profile").GetProperty("path").GetString()!) != Path.GetFullPath(selected.Directory))
                throw new InvalidOperationException("MO2 did not finish selecting the requested profile");
            if (Endpoint != registration.Endpoint) Tools = [];
            _client = client; Endpoint = registration.Endpoint;
            _lastSnapshot = null; Apply(snapshot);
            try { Mo2NxmRouter.Remember(NexusGame,registration); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) {
                Status = "Connected, but the NXM game route could not be saved"; Changed?.Invoke();
            }
            return true;
        } catch (Exception error) { Report(error); return false; }
        finally { SelectingProfile = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task Launch(string executable)
    {
        if (!CanStartHostAction) return;
        var target = CurrentTarget;
        var profile = target.ProfilePath;
        Launching = true; Changed?.Invoke();
        await _commands.WaitAsync();
        try {
            if (!IsConnected) return;
            if (target != CurrentTarget) { Status = "The MO2 profile changed. Launch again."; return; }
            Status = "Launching " + executable + " through MO2; close the application to return"; Changed?.Invoke();
            var result = await Client.SendAsync("launch", new() { ["profilePath"] = profile, ["name"] = executable }, timeout: TimeSpan.FromHours(12));
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
            Status = result.GetProperty("completed").GetBoolean() ? executable + " exited (code " + result.GetProperty("exitCode").GetInt32() + ")" : "MO2 stopped waiting for the application; check the host";
        } catch (Exception error) { Report(error); }
        finally { Launching = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task ManageProfile(Mo2Registration registration, Mo2ProfileSnapshot target, string operation)
    {
        if (Installing || SelectingProfile || Launching || ManagingMod) return;
        SelectingProfile = true; Changed?.Invoke();
        await _commands.WaitAsync();
        try {
            Status = "Manage " + target.Name + " in MO2"; Changed?.Invoke();
            var snapshot = await Mo2HostStartup.Connect(registration, message => { Status = message; Changed?.Invoke(); });
            if (!snapshot.GetProperty("profiles").EnumerateArray().Any(x =>
                Mo2InstanceCatalog.LocalPath(x.GetProperty("path").GetString()!) == Path.GetFullPath(target.Directory)))
                throw new InvalidOperationException("The MO2 host does not own this profile");
            var client = new Mo2BridgeClient(registration.Endpoint);
            snapshot = await client.SendAsync("manageProfile", new() {
                ["profilePath"] = snapshot.GetProperty("profile").GetProperty("path").GetString(),
                ["name"] = target.Name, ["operation"] = operation
            }, timeout: TimeSpan.FromMinutes(30));
            // Acting on a card does not select that profile; MO2 remains authoritative.
            if (Endpoint == registration.Endpoint) { _lastSnapshot = null; Apply(snapshot); }
            else Status = "Profile action finished in MO2";
        } catch (Exception error) { Report(error); }
        finally { SelectingProfile = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task ManageNexusAccount()
    {
        if (!CanStartHostAction) return;
        var target = CurrentTarget;
        var profile = target.ProfilePath;
        ManagingMod = true; Changed?.Invoke();
        await _commands.WaitAsync();
        try {
            if (!IsConnected) return;
            if (target != CurrentTarget) { Status = "The MO2 profile changed. Open the dialog again."; return; }
            Status = "Manage the Nexus account in MO2"; Changed?.Invoke();
            var result = await Client.SendAsync("manageNexusAccount", new() { ["profilePath"] = profile }, timeout: TimeSpan.FromMinutes(30));
            if (!result.GetProperty("opened").GetBoolean() || result.GetProperty("tab").GetString() != "nexusTab") throw new InvalidOperationException("MO2 did not open Nexus settings");
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task SetOriginalUiVisible(bool visible)
    {
        if (!CanChangeOriginalUi) return;
        var target = CurrentTarget;
        await _commands.WaitAsync();
        try {
            if (target != CurrentTarget || !CanChangeOriginalUi) return;
            ManagingMod = true; Changed?.Invoke();
            Apply(await Client.SendAsync("setUiVisible", new() { ["profilePath"] = ProfilePath, ["visible"] = visible }));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task ManageProfiles()
    {
        var profile = ProfilePath;
        await _commands.WaitAsync();
        try {
            SelectingProfile = true; Status = "Manage profiles in MO2"; Changed?.Invoke();
            var snapshot = await Client.SendAsync("manageProfiles", new() { ["profilePath"] = profile }, timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(snapshot);
        } catch (Exception error) { Report(error); }
        finally { SelectingProfile = false; Changed?.Invoke(); _commands.Release(); }
    }
    // MO2's own main-window entries that the frontend has no page of its own for.
    // The host owns the dialog, its validation and everything it writes; this only
    // asks for it to be opened and applies whatever state MO2 reports afterwards.
    // The frontend previously had no route to MO2's settings at all.
    public async Task OpenOriginal(string name, string label)
    {
        var target = CurrentTarget;
        await _commands.WaitAsync();
        try {
            ManagingMod = true; Status = label + " is open in MO2"; Changed?.Invoke();
            // Carries the profile like every other host action, so MO2 refuses it if
            // the selected profile moved underneath the frontend.
            // MO2's dialogs are modal to its own window and stay open for as long as
            // the user needs, so this waits the same way the profile manager does.
            await Client.SendAsync("openOriginal", new() { ["name"] = name, ["profilePath"] = target.ProfilePath },
                timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }

    internal async Task RunExternalFileAction(Mo2ProfileTarget target, Func<Task> action)
    {
        await _commands.WaitAsync();
        try {
            if (!CheckDownloadTarget(target)) throw new InvalidOperationException("The selected MO2 profile is unavailable or busy.");
            Apply(await Client.SendAsync("snapshot"));
            if (target != CurrentTarget) throw new InvalidOperationException("The MO2 profile changed. Review the action again.");
            ManagingMod = true; Changed?.Invoke();
            await action();
        } finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }

    public async Task<Mo2ArchiveInstallResult?> InstallArchive(string path, Mo2ProfileTarget? requestedTarget = null)
    {
        var target = requestedTarget ?? CurrentTarget;
        var profile = target.ProfilePath;
        await _commands.WaitAsync();
        try {
            if (!CheckDownloadTarget(target)) return null;
            Installing = true; Status = "Complete installation in MO2"; Changed?.Invoke();
            var hostPath = path.StartsWith('/') ? "Z:" + path : path;
            var result = await Client.SendAsync("installArchive", new() { ["profilePath"] = profile, ["path"] = hostPath }, timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null;
            Apply(await Client.SendAsync("snapshot"));
            Status = result.GetProperty("installed").GetBoolean() ? "Installed " + result.GetProperty("modName").GetString() : "MO2 did not install the archive (cancelled or failed)";
            if (!result.GetProperty("installed").GetBoolean()) return null;
            var directory = result.TryGetProperty("modPath", out var modPath) ? modPath.GetString()?.Replace('\\', '/') : null;
            // Wine resolves this inside the installing host, so C: and custom
            // drives use that instance's actual prefix, never a guessed prefix.
            var unixDirectory = result.TryGetProperty("modUnixPath", out var unixPath) && unixPath.ValueKind == JsonValueKind.String
                ? unixPath.GetString() : null;
            var localDirectory = !string.IsNullOrEmpty(directory) &&
                (OperatingSystem.IsWindows() || directory.StartsWith("Z:/", StringComparison.OrdinalIgnoreCase))
                    ? Mo2InstanceCatalog.LocalPath(directory) : null;
            if (OperatingSystem.IsLinux() && unixDirectory is { Length: > 0 } &&
                unixDirectory.StartsWith('/') && !unixDirectory.Contains('\0'))
                localDirectory = Path.GetFullPath(unixDirectory);
            return new(result.GetProperty("modName").GetString()!, localDirectory);
        } catch (Exception error) { Report(error); return null; }
        finally { Installing = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task ControlDownload(string path, string operation, Mo2ProfileTarget? requestedTarget = null)
    {
        var target = requestedTarget ?? CurrentTarget;
        var profile = target.ProfilePath;
        await _commands.WaitAsync();
        try {
            if (!CheckDownloadTarget(target)) return;
            if (Downloads.FirstOrDefault(file => file.Path == path)?.CanControl(operation) != true) {
                Status = "Download state changed; refresh before choosing another action.";
                Changed?.Invoke(); return;
            }
            if (operation == "delete") { ManagingMod = true; Status = "Confirm download deletion in MO2"; Changed?.Invoke(); }
            await Client.SendAsync("controlDownload", new() { ["profilePath"] = profile, ["path"] = path, ["operation"] = operation },
                timeout: operation == "delete" ? TimeSpan.FromMinutes(30) : null);
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
            Status = operation == "delete" ? "Download deletion dialog closed" : operation + " requested through MO2"; Changed?.Invoke();
        } catch (Exception error) { Report(error); }
        finally { if (operation == "delete") { ManagingMod = false; Changed?.Invoke(); } _commands.Release(); }
    }
    public async Task DownloadNexus(string link, Mo2ProfileTarget? requestedTarget = null)
    {
        var target = requestedTarget ?? CurrentTarget;
        var profile = target.ProfilePath;
        var game = NexusGame;
        await _commands.WaitAsync();
        try {
            if (!CheckDownloadTarget(target)) return;
            var file = Mo2NexusLink.Parse(link);
            if (!string.Equals(file.Game, game, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose a Nexus file for the current game");
            if (file.NxmUri is { } nxm)
                await Client.SendAsync("startNxmDownload", new() { ["profilePath"] = profile, ["url"] = nxm });
            else
                await Client.SendAsync("startNexusDownload", new() { ["profilePath"] = profile, ["game"] = game, ["modId"] = file.ModId, ["fileId"] = file.FileId });
            Status = "Download requested through MO2"; Changed?.Invoke();
        } catch (ArgumentException error) {
            // Link validation happens locally and says nothing about host health.
            Status = error.Message; Changed?.Invoke();
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    // How a folder is opened on the desktop the frontend is running on. Set by the
    // workspace, which owns the interop; MO2's own Open in Explorer would open the
    // folder inside the Windows prefix instead.
    public Action<string>? OpenLocalFolder { get; set; }

    // MO2's "Open in Explorer" for a mod. MO2 is asked where the mod is rather than
    // asked to open it, because the two are on different sides of the prefix.
    public async Task OpenModFolder(string name)
    {
        await _commands.WaitAsync();
        try {
            if (!IsConnected) return;
            var result = await Client.SendAsync("modPath", new() { ["profilePath"] = CurrentTarget.ProfilePath, ["name"] = name });
            var path = result.TryGetProperty("path", out var value) ? Mo2InstanceCatalog.LocalPath(value.GetString() ?? "") : "";
            if (path.Length == 0 || !Directory.Exists(path)) { Status = "MO2 no longer has a folder for " + name; Changed?.Invoke(); return; }
            OpenLocalFolder?.Invoke(path);
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }

    // MO2's Rename, through the list model's own editor so MO2 renames the folder
    // and tells every profile about it.
    public async Task RenameMod(string name, string newName, Mo2ProfileTarget target)
    {
        if (!CanStartHostAction || target != CurrentTarget) return;
        ManagingMod = true; Changed?.Invoke(); await _commands.WaitAsync();
        try {
            if (!IsConnected || target != CurrentTarget) return;
            Status = "Renaming " + name + " in MO2"; Changed?.Invoke();
            await Client.SendAsync("renameMod", new() { ["profilePath"] = target.ProfilePath, ["name"] = name, ["newName"] = newName });
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }

    // MO2's Query Metadata, which asks Nexus for what its downloads are missing and
    // writes the answers into the .meta files beside them. MO2's own button is
    // pressed, so its offline-mode guard and login prompt are the ones that apply.
    public async Task QueryDownloadMetadata()
    {
        await _commands.WaitAsync();
        try {
            if (!IsConnected) return;
            Status = "Asking MO2 to query download metadata"; Changed?.Invoke();
            await Client.SendAsync("queryDownloadMetadata", new() { ["profilePath"] = CurrentTarget.ProfilePath },
                timeout: TimeSpan.FromMinutes(5));
            Status = "MO2 is querying download metadata"; Changed?.Invoke();
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    private int _selectionVersion;
    private string[] _selectedModNames = [];
    public IReadOnlySet<string> LinkedPlugins { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    // The other direction. Selecting mods has always marked the plugins they
    // install; selecting plugins marked nothing, so the two tables answered the
    // same question — what goes with what — only when you asked it from one side.
    public IReadOnlySet<string> LinkedMods { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> WinningMods { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> LosingMods { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private object? _dataHighlightOwner;
    private Mo2ProfileTarget? _dataHighlightTarget;
    private string? _dataHighlightMod;
    public bool IsDataSourceHighlighted(string name) => IsConnected && _dataHighlightTarget == CurrentTarget &&
        string.Equals(_dataHighlightMod, name, StringComparison.OrdinalIgnoreCase);
    public void HighlightDataSource(object owner, Mo2ProfileTarget target, string? mod)
    {
        if (mod is null) {
            if (!ReferenceEquals(_dataHighlightOwner, owner)) return;
            _dataHighlightOwner = null; _dataHighlightTarget = null; _dataHighlightMod = null;
        } else {
            if (!IsConnected || target != CurrentTarget) return;
            _dataHighlightOwner = owner; _dataHighlightTarget = target; _dataHighlightMod = mod;
        }
        HighlightsChanged?.Invoke();
    }
    public event Action? HighlightsChanged;
    // The mods that install the given plugins, marked in the Mods table for as long
    // as those plugins are selected. Local: which mod a plugin came from is already
    // known here, so unlike the mod selection this needs nothing from MO2.
    public void HighlightPlugins(IEnumerable<string> pluginNames)
    {
        var names = pluginNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var owners = Order.Plugins.Where(plugin => names.Contains(plugin.DisplayName))
            .Select(plugin => plugin.ModName).Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (owners.SetEquals(LinkedMods)) return;
        LinkedMods = owners;
        HighlightsChanged?.Invoke();
    }

    public void HighlightMods(IEnumerable<EntityId> ids)
    {
        var selected = ids.ToHashSet();
        _selectedModNames = Mods.Where(x => selected.Contains(x.Id)).Select(x => x.Name).ToArray();
        _ = RefreshHighlights();
    }
    private async Task RefreshHighlights()
    {
        var version = ++_selectionVersion;
        var target = CurrentTarget;
        var names = _selectedModNames;
        LinkedPlugins = Order.Plugins.Where(p => names.Contains(p.ModName,StringComparer.OrdinalIgnoreCase)).Select(p => p.DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        WinningMods = new HashSet<string>(); LosingMods = new HashSet<string>(); HighlightsChanged?.Invoke();
        if (names.Length == 0 || !CanStartHostAction) return;
        await Task.Delay(120); // Coalesce rapid pointer/keyboard selection changes.
        if (version != _selectionVersion) return;
        await _commands.WaitAsync();
        try {
            if (version != _selectionVersion || target != CurrentTarget || !CanStartHostAction) return;
            var result = await Client.SendAsync("selectionLinks",new() { ["profilePath"] = target.ProfilePath, ["names"] = names });
            if (version != _selectionVersion || target != CurrentTarget) return;
            HashSet<string> Read(string key) => result.GetProperty(key).EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            LinkedPlugins = Read("plugins"); WinningMods = Read("winningMods"); LosingMods = Read("losingMods");
            HighlightsChanged?.Invoke();
        } catch (Exception error) { if (version == _selectionVersion && target == CurrentTarget) Report(error); }
        finally { _commands.Release(); }
    }
    public async Task MoveIntoCollection(IEnumerable<EntityId> ids, string key)
    {
        var target = CurrentTarget;
        var names = ids.Select(FindMod).Where(x => x is { CanManage: true, IsSeparator: false }).OrderBy(x => x!.Priority).Select(x => x!.Name).ToArray();
        await _commands.WaitAsync();
        try {
            if (target != CurrentTarget || !CanStartHostAction) return;
            Apply(await Client.SendAsync("snapshot"));
            foreach (var name in names) {
                if (target != CurrentTarget) return;
                var groups = Mo2Collections.Build(Mods);
                var index = groups.ToList().FindIndex(x => x.Key == key);
                if (index < 0) throw new InvalidOperationException("Collection no longer exists");
                if (groups[index].Mods.Any(x => x.Name == name)) continue;
                var mod = Mods.FirstOrDefault(x => x.Name == name);
                if (mod is null || !mod.CanManage || mod.IsSeparator) continue;
                var next = index + 1 < groups.Count ? Mods.First(x => x.Name == groups[index + 1].Key) : null;
                var priority = next is null ? Mods.Where(x => !x.IsOverwrite).Max(x => x.Priority) : next.Priority - (mod.Priority < next.Priority ? 1 : 0);
                Apply(await Client.SendAsync("setModPriority", new() { ["profilePath"] = target.ProfilePath, ["name"] = name, ["priority"] = priority }));
            }
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    public async Task SetCollectionEnabled(string key, bool enabled)
    {
        var target = CurrentTarget;
        await _commands.WaitAsync();
        try {
            if (target != CurrentTarget || !CanStartHostAction) return;
            Apply(await Client.SendAsync("snapshot"));
            if (target != CurrentTarget) return;
            var collection = Mo2Collections.Build(Mods).FirstOrDefault(x => x.Key == key);
            if (collection is null) return;
            foreach (var mod in collection.Mods.Where(x => (x.State & 4) == 0 && ((x.State & 2) != 0) != enabled)) {
                if (target != CurrentTarget) return;
                Apply(await Client.SendAsync("setModActive", new() { ["profilePath"] = target.ProfilePath, ["name"] = mod.Name, ["enabled"] = enabled }));
            }
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    public async Task<bool> RenameCollection(string key, string title, Mo2ProfileTarget target)
    {
        if (!CanStartHostAction || target != CurrentTarget || key.Length == 0) return false;
        await _commands.WaitAsync();
        try {
            if (!CanStartHostAction || target != CurrentTarget) return false;
            ManagingMod = true; Changed?.Invoke();
            var result = await Client.SendAsync("renameSeparator", new() { ["profilePath"] = target.ProfilePath, ["name"] = key, ["collectionName"] = title });
            if (!result.GetProperty("renamed").GetBoolean()) return false;
            CollectionRenamed?.Invoke(target.Endpoint, key, result.GetProperty("name").GetString()!);
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
            return true;
        } catch (Exception error) { Report(error); return false; }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task<bool> RemoveCollection(string key, Mo2ProfileTarget requestedTarget)
    {
        if (!CanStartHostAction || requestedTarget != CurrentTarget) return false;
        await _commands.WaitAsync();
        try {
            if (requestedTarget != CurrentTarget || !Mods.Any(x => x.Name == key && x.IsSeparator)) return false;
            ManagingMod = true; Changed?.Invoke();
            var result = await Client.SendAsync("removeSeparator", new() { ["profilePath"] = requestedTarget.ProfilePath, ["name"] = key }, timeout: TimeSpan.FromSeconds(30));
            if (result.GetProperty("removed").GetBoolean()) CollectionRemoved?.Invoke(requestedTarget.Endpoint, key);
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
            return result.GetProperty("removed").GetBoolean();
        } catch (Exception error) { Report(error); return false; }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task CreateSeparator(EntityId? above, string? collectionName = null)
    {
        if (!CanStartHostAction) return;
        var target = CurrentTarget;
        var name = above is { } id ? Mods.FirstOrDefault(x => x.Id == id)?.Name : null;
        ManagingMod = true; Changed?.Invoke(); await _commands.WaitAsync();
        try {
            if (target != CurrentTarget) return;
            await Client.SendAsync("createSeparator",new() { ["profilePath"] = target.ProfilePath, ["name"] = name, ["collectionName"] = collectionName },timeout:TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task ShowModDetails(EntityId id)
    {
        if (!CanStartHostAction) return;
        var target = CurrentTarget;
        var profile = target.ProfilePath;
        var mod = _mods.Lookup(id);
        if (!mod.HasValue) return;
        ManagingMod = true; Changed?.Invoke();
        await _commands.WaitAsync();
        try {
            if (!IsConnected) return;
            if (target != CurrentTarget) { Status = "The MO2 profile changed. Open the dialog again."; return; }
            Status = "Opening mod details in MO2"; Changed?.Invoke();
            await Client.SendAsync("showModDetails", new() { ["profilePath"] = profile, ["name"] = mod.Value.Name }, timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    internal Func<IReadOnlyList<Mo2LiveMod>, Task<bool>>? ConfirmRemoval { get; set; }
    private bool _confirmingRemoval;
    public void Remove(IEnumerable<LoadoutItemId> ids)
    {
        var selected = ids.Select(id => _mods.Lookup(id.Value)).Where(x => x.HasValue && x.Value.CanManage).Select(x => x.Value.Name).ToArray();
        _ = RemoveAsync(selected, CurrentTarget);
    }
    private async Task RemoveAsync(string[] names, Mo2ProfileTarget target)
    {
        if (_confirmingRemoval || names.Length == 0 || !CanStartHostAction) return;
        var confirmed = false;
        if (ConfirmRemoval is { } confirm) {
            _confirmingRemoval = true;
            try { confirmed = await confirm(Mods.Where(mod => names.Contains(mod.Name)).ToArray()); }
            finally { _confirmingRemoval = false; }
            if (!confirmed) return;
        }
        if (target != CurrentTarget || !CanStartHostAction) return;
        await _commands.WaitAsync();
        try {
            if (target != CurrentTarget || !CanStartHostAction) return;
            ManagingMod = true; Status = confirmed ? "Removing mods through MO2" : "Confirm mod removal in MO2"; Changed?.Invoke();
            foreach (var name in names) {
                var result = await Client.SendAsync("removeMod", new() { ["profilePath"] = target.ProfilePath, ["name"] = name, ["confirmed"] = confirmed }, timeout: TimeSpan.FromMinutes(30));
                _lastSnapshot = null; Apply(await Client.SendAsync("snapshot"));
                if (!result.GetProperty("removed").GetBoolean()) {
                    Status = "MO2 kept " + name; break;
                }
                Status = "Uninstalled " + name + " through MO2";
            }
        } catch (Exception error) { Report(error); }
        finally { ManagingMod = false; Changed?.Invoke(); _commands.Release(); }
    }
    public IObservable<int> CountLoadoutItems(LoadoutFilter filter) => _mods.Connect().Filter(x => !x.IsOverwrite && !x.IsSeparator).QueryWhenChanged(x => x.Count).DistinctUntilChanged();
    // Selecting a different profile updates the shared snapshot before its old
    // workspace detaches. Buffer raw changes so that departing adapters do not
    // construct the next game's models. A surviving adapter receives the batch
    // on resume (including same-profile refreshes and failed selection).
    private IObservable<IChangeSet<Mo2LiveMod, EntityId>> ObservePresentationMods()
        => Observable.Defer(() => _mods.Connect().BatchIf(
            Observable.FromEvent(handler => Changed += handler, handler => Changed -= handler)
                .Select(_ => SelectingProfile).DistinctUntilChanged(),
            initialPauseState: SelectingProfile,
            scheduler: System.Reactive.Concurrency.CurrentThreadScheduler.Instance));
    public IObservable<IChangeSet<CompositeItemModel<EntityId>, EntityId>> ObserveLoadoutItems(LoadoutFilter filter)
        => ObservePresentationMods().Filter(x => !x.IsOverwrite).Transform(CreateModModel);
    public IObservable<IChangeSet<CompositeItemModel<EntityId>, EntityId>> ObserveFilteredMods(
        IObservable<Func<Mo2LiveMod,bool>> filter, Mo2ProfileTarget target)
        => Observable.Create<IChangeSet<Mo2LiveMod, EntityId>>(observer => {
            // A retained page owns a presentation of one profile. Ignore other
            // games entirely, then reconcile the latest snapshot on return.
            var cache = new SourceCache<Mo2LiveMod, EntityId>(mod => mod.Id);
            var subscription = cache.Connect().Subscribe(observer);
            long revision = -1;
            void Sync() {
                if (SelectingProfile || CurrentTarget != target || revision == ContentRevision) return;
                revision = ContentRevision;
                var current = Mods.ToDictionary(mod => mod.Id);
                cache.Edit(update => {
                    update.RemoveKeys(cache.Keys.Where(id => !current.ContainsKey(id)).ToArray());
                    foreach (var mod in current.Values) {
                        var existing = cache.Lookup(mod.Id);
                        if (!existing.HasValue || existing.Value != mod) update.AddOrUpdate(mod);
                    }
                });
            }
            Changed += Sync;
            Sync();
            return System.Reactive.Disposables.Disposable.Create(() => {
                Changed -= Sync; subscription.Dispose(); cache.Dispose();
            });
        }).Filter(x => !x.IsOverwrite).Filter(filter).Transform(CreateModModel);
    internal IObservable<IChangeSet<NexusMods.Abstractions.Games.IReactiveSortItem, NexusMods.Abstractions.Games.ISortItemKey>> ObservePresentationPlugins(Mo2ProfileTarget target)
        => Observable.Create<IChangeSet<NexusMods.Abstractions.Games.IReactiveSortItem, NexusMods.Abstractions.Games.ISortItemKey>>(observer => {
            var cache = new SourceCache<NexusMods.Abstractions.Games.IReactiveSortItem, NexusMods.Abstractions.Games.ISortItemKey>(plugin => plugin.Key);
            var subscription = cache.Connect().Subscribe(observer);
            long revision = -1;
            void Sync() {
                if (SelectingProfile || CurrentTarget != target || revision == ContentRevision) return;
                revision = ContentRevision;
                var current = Order.Plugins.ToDictionary(plugin => plugin.Key);
                cache.Edit(update => {
                    update.RemoveKeys(cache.Keys.Where(key => !current.ContainsKey(key)).ToArray());
                    update.AddOrUpdate(current.Values);
                });
            }
            Changed += Sync;
            Sync();
            return System.Reactive.Disposables.Disposable.Create(() => {
                Changed -= Sync; subscription.Dispose(); cache.Dispose();
            });
        });
    private static CompositeItemModel<EntityId> CreateModModel(Mo2LiveMod mod) {
            Mo2UiLatencyProbe.Count("Mod models created");
            var model = new CompositeItemModel<EntityId>(mod.Id);
            model.Add(Mo2ModsAdapter.StateKey, new ValueComponent<int>(mod.State));
            model.Add(Mo2ModsAdapter.PriorityKey, new ValueComponent<int>(mod.Priority));
            model.Add(Mo2ModsAdapter.PriorityTextKey, new ValueComponent<string>(mod.PriorityText));
            model.Add(Mo2ModsAdapter.ConflictsKey, new ValueComponent<string>(mod.Conflicts));
            model.Add(Mo2ModsAdapter.FlagsKey, new ValueComponent<string>(mod.Flags));
            model.Add(SharedColumns.Name.NameComponentKey, new NameComponent(mod.DisplayName));
            model.Add(LoadoutColumns.EnabledState.LoadoutItemIdsComponentKey, new LoadoutComponents.LoadoutItemIds(LoadoutItemId.From(mod.Id)));
            model.Add(LoadoutColumns.EnabledState.ViewModFilesComponentKey, new SharedComponents.ViewModFilesAction(isEnabled: true));
            if ((mod.State & 4) == 0) {
                model.Add(LoadoutColumns.EnabledState.EnabledStateToggleComponentKey, new LoadoutComponents.EnabledStateToggle(new ValueComponent<bool?>((mod.State & 2) != 0)));
            }
            model.Add(LoadoutColumns.EnabledState.UninstallItemComponentKey, new SharedComponents.UninstallItemAction(isEnabled: mod.CanManage));
            return model;
        }
}
