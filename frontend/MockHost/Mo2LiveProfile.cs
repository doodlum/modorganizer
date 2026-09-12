using System.Reactive.Linq;
using System.Text.Json;
using DynamicData;
using NexusMods.Abstractions.Loadouts;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Pages;
using NexusMods.App.UI.Pages.LoadoutPage;
using NexusMods.MnemonicDB.Abstractions;

namespace Mo2.Frontend;

internal sealed record Mo2Download(string Name, string Path, long Bytes, bool Partial, bool Installed, bool Paused);
internal sealed record Mo2LiveMod(EntityId Id, string Name, string DisplayName, int State, int Priority);

// Only a view of the running host. No activation/order/profile files are written here.
internal sealed class Mo2LiveProfile : IInstalledModsSource
{
    private Mo2BridgeClient _client;
    private readonly SemaphoreSlim _commands = new(1);
    private readonly SourceCache<Mo2LiveMod, EntityId> _mods = new(x => x.Id);
    private readonly Dictionary<string, EntityId> _ids = new(StringComparer.OrdinalIgnoreCase);
    public R3.BindableReactiveProperty<string> CollectionName { get; } = new("Connecting to MO2…");
    public IReadOnlyCollection<Mo2LiveMod> Mods => _mods.Items.ToArray();
    public ScenarioPluginOrder Order { get; } = new(false);
    public IReadOnlyList<Mo2Download> Downloads { get; private set; } = [];
    public string Endpoint { get; private set; }
    public bool SelectingProfile { get; private set; }
    public string NexusGame { get; private set; } = "";
    public bool Installing { get; private set; }
    public bool Launching { get; private set; }
    public IReadOnlyList<string> Executables { get; private set; } = [];
    public string ProfilePath { get; private set; } = "";
    public string Status { get; private set; } = "Connecting to MO2…";
    public event Action? Changed;
    public Mo2LiveProfile(string endpoint)
    {
        Endpoint = endpoint;
        _client = new(endpoint);
        Order.ApplyOrder = Reorder;
    }
    private string? _lastSnapshot;
    private void Apply(JsonElement snapshot)
    {
        var raw = snapshot.GetRawText();
        if (raw == _lastSnapshot) return;
        _lastSnapshot = raw;
        Executables = snapshot.TryGetProperty("executables", out var executables) ? executables.EnumerateArray().Select(x => x.GetString()!).ToArray() : [];
        NexusGame = snapshot.TryGetProperty("nexusGame", out var game) ? game.GetString() ?? "" : "";
        Downloads = snapshot.TryGetProperty("downloads", out var downloads) ? downloads.EnumerateArray()
            .Where(x => !x.GetProperty("hidden").GetBoolean())
            .Select(x => new Mo2Download(x.GetProperty("name").GetString()!, x.GetProperty("path").GetString()!, x.GetProperty("bytes").GetInt64(), x.GetProperty("partial").GetBoolean(), x.GetProperty("installed").GetBoolean(), x.GetProperty("paused").GetBoolean())).ToArray() : [];
        var profile = snapshot.GetProperty("profile");
        ProfilePath = profile.GetProperty("path").GetString()!;
        CollectionName.Value = profile.GetProperty("name").GetString()!;
        var mods = snapshot.GetProperty("mods").EnumerateArray().Select(mod => {
            var name = mod.GetProperty("name").GetString()!;
            if (!_ids.TryGetValue(name, out var id)) _ids[name] = id = EntityId.From((ulong)_ids.Count + 100);
            return new Mo2LiveMod(id, name, mod.GetProperty("displayName").GetString()!, mod.GetProperty("state").GetInt32(), mod.GetProperty("priority").GetInt32());
        }).ToArray();
        _mods.Edit(cache => { cache.Clear(); cache.AddOrUpdate(mods); });
        Order.Replace(snapshot.GetProperty("plugins").EnumerateArray().Select(plugin => new ScenarioPlugin(
            plugin.GetProperty("name").GetString()!, plugin.GetProperty("origin").GetString()!, plugin.GetProperty("priority").GetInt32(),
            plugin.GetProperty("masters").EnumerateArray().Select(x => x.GetString()!).ToArray()) {
                IsActive = plugin.GetProperty("state").GetInt32() == 2,
            }));
        Status = $"{mods.Length} mods · {Order.Plugins.Count} plugins · Connected to MO2";
        Changed?.Invoke();
    }
    public async Task Refresh()
    {
        if (!await _commands.WaitAsync(0)) return;
        try { Apply(await _client.SendAsync("snapshot")); }
        catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    private void Report(Exception error) { _lastSnapshot = null; Status = error.Message; Changed?.Invoke(); }
    public void Toggle(IEnumerable<LoadoutItemId> ids)
    {
        var selected = ids.Select(id => _mods.Lookup(id.Value)).Where(x => x.HasValue).Select(x => x.Value).ToArray();
        _ = ToggleAsync(selected, ProfilePath);
    }
    private async Task ToggleAsync(Mo2LiveMod[] selected, string profile)
    {
        await _commands.WaitAsync();
        try {
            foreach (var mod in selected) {
                if ((mod.State & 4) != 0) continue; // MO2 essential content cannot be disabled.
                Apply(await _client.SendAsync("setModActive", new() { ["profilePath"] = profile, ["name"] = mod.Name, ["enabled"] = (mod.State & 2) == 0 }));
            }
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    private async Task Reorder(ScenarioPlugin[] desired, CancellationToken token)
    {
        var profile = ProfilePath;
        await _commands.WaitAsync(token);
        try {
            for (var index = 0; index < desired.Length; index++) {
                var current = Order.Plugins.FirstOrDefault(x => x.DisplayName == desired[index].DisplayName);
                if (current?.SortIndex == index) continue;
                Apply(await _client.SendAsync("setPluginPriority", new() { ["profilePath"] = profile, ["name"] = desired[index].DisplayName, ["priority"] = index }, token));
            }
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    public async Task SetPluginsActive(IEnumerable<string> names, bool enabled)
    {
        var profile = ProfilePath;
        var selected = names.ToArray();
        await _commands.WaitAsync();
        try {
            foreach (var name in selected)
                Apply(await _client.SendAsync("setPluginActive", new() { ["profilePath"] = profile, ["name"] = name, ["enabled"] = enabled }));
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    public async Task MoveMod(EntityId id, int delta)
    {
        var profile = ProfilePath;
        await _commands.WaitAsync();
        try {
            if (profile != ProfilePath) throw new InvalidOperationException("Active profile changed; select the mod again");
            var found = _mods.Lookup(id);
            if (!found.HasValue) return;
            var mod = found.Value;
            if ((mod.State & 4) != 0 || mod.Priority < 0) return;
            var priority = Math.Clamp(mod.Priority + delta, 0, _mods.Count - 1);
            Apply(await _client.SendAsync("setModPriority", new() { ["profilePath"] = profile, ["name"] = mod.Name, ["priority"] = priority }));
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    public async Task<bool> SelectProfile(Mo2Registration registration, Mo2ProfileSnapshot selected)
    {
        await _commands.WaitAsync();
        try {
            SelectingProfile = true;
            Status = "Connecting to " + selected.Name; Changed?.Invoke();
            if (!File.Exists(Path.Combine(registration.Endpoint, "endpoint.json"))) throw new InvalidOperationException("Start MO2 with the frontend bridge enabled for this instance");
            var client = new Mo2BridgeClient(registration.Endpoint);
            var snapshot = await client.SendAsync("snapshot");
            if (!snapshot.GetProperty("profiles").EnumerateArray().Any(x =>
                Mo2InstanceCatalog.LocalPath(x.GetProperty("path").GetString()!) == Path.GetFullPath(selected.Directory)))
                throw new InvalidOperationException("The MO2 host does not own the selected profile");
            var current = snapshot.GetProperty("profile");
            if (Mo2InstanceCatalog.LocalPath(current.GetProperty("path").GetString()!) != Path.GetFullPath(selected.Directory))
                snapshot = await client.SendAsync("selectProfile", new() { ["profilePath"] = current.GetProperty("path").GetString(), ["name"] = selected.Name }, timeout: TimeSpan.FromMinutes(2));
            if (Mo2InstanceCatalog.LocalPath(snapshot.GetProperty("profile").GetProperty("path").GetString()!) != Path.GetFullPath(selected.Directory))
                throw new InvalidOperationException("MO2 did not finish selecting the requested profile");
            _client = client; Endpoint = registration.Endpoint;
            _lastSnapshot = null; Apply(snapshot);
            return true;
        } catch (Exception error) { Report(error); return false; }
        finally { SelectingProfile = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task Launch(string executable)
    {
        var profile = ProfilePath;
        if (!await _commands.WaitAsync(0)) return;
        try {
            Launching = true; Status = "Launching " + executable + " through MO2; close the application to return"; Changed?.Invoke();
            var result = await _client.SendAsync("launch", new() { ["profilePath"] = profile, ["name"] = executable }, timeout: TimeSpan.FromHours(12));
            _lastSnapshot = null; Apply(await _client.SendAsync("snapshot"));
            Status = result.GetProperty("completed").GetBoolean() ? executable + " exited (code " + result.GetProperty("exitCode").GetInt32() + ")" : "MO2 stopped waiting for the application; check the host";
        } catch (Exception error) { Report(error); }
        finally { Launching = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task ManageProfiles()
    {
        var profile = ProfilePath;
        await _commands.WaitAsync();
        try {
            SelectingProfile = true; Status = "Manage profiles in MO2"; Changed?.Invoke();
            var snapshot = await _client.SendAsync("manageProfiles", new() { ["profilePath"] = profile }, timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null; Apply(snapshot);
        } catch (Exception error) { Report(error); }
        finally { SelectingProfile = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task InstallArchive(string path)
    {
        var profile = ProfilePath;
        await _commands.WaitAsync();
        try {
            Installing = true; Status = "Complete installation in MO2"; Changed?.Invoke();
            var hostPath = path.StartsWith('/') ? "Z:" + path : path;
            var result = await _client.SendAsync("installArchive", new() { ["profilePath"] = profile, ["path"] = hostPath }, timeout: TimeSpan.FromMinutes(30));
            _lastSnapshot = null;
            Apply(await _client.SendAsync("snapshot"));
            Status = result.GetProperty("installed").GetBoolean() ? "Installed " + result.GetProperty("modName").GetString() : "MO2 did not install the archive (cancelled or failed)";
        } catch (Exception error) { Report(error); }
        finally { Installing = false; Changed?.Invoke(); _commands.Release(); }
    }
    public async Task DownloadNexus(string link)
    {
        var profile = ProfilePath;
        var game = NexusGame;
        await _commands.WaitAsync();
        try {
            var file = Mo2NexusLink.Parse(link);
            if (!string.Equals(file.Game, game, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose a Nexus file for the current game");
            await _client.SendAsync("startNexusDownload", new() { ["profilePath"] = profile, ["game"] = game, ["modId"] = file.ModId, ["fileId"] = file.FileId });
            Status = "Download requested through MO2"; Changed?.Invoke();
        } catch (Exception error) { Report(error); }
        finally { _commands.Release(); }
    }
    public void Remove(IEnumerable<LoadoutItemId> ids) => Report(new NotSupportedException("Use MO2 to uninstall mods while installer integration is being connected."));
    public void Rename(string name) => Report(new NotSupportedException("Use MO2 to rename profiles while profile management is being connected."));
    public IObservable<int> CountLoadoutItems(LoadoutFilter filter) => Observable.Defer(() => _mods.CountChanged.StartWith(_mods.Count).DistinctUntilChanged());
    public IObservable<IChangeSet<CompositeItemModel<EntityId>, EntityId>> ObserveLoadoutItems(LoadoutFilter filter)
        => _mods.Connect().Transform(mod => {
            var model = new CompositeItemModel<EntityId>(mod.Id);
            model.Add(Mo2ModsAdapter.PriorityKey, new ValueComponent<int>(mod.Priority));
            model.Add(SharedColumns.Name.NameComponentKey, new NameComponent(mod.DisplayName));
            if ((mod.State & 4) == 0) {
                model.Add(LoadoutColumns.EnabledState.LoadoutItemIdsComponentKey, new LoadoutComponents.LoadoutItemIds(LoadoutItemId.From(mod.Id)));
                model.Add(LoadoutColumns.EnabledState.EnabledStateToggleComponentKey, new LoadoutComponents.EnabledStateToggle(new ValueComponent<bool?>((mod.State & 2) != 0)));
            }
            model.Add(LoadoutColumns.EnabledState.UninstallItemComponentKey, new SharedComponents.UninstallItemAction(isEnabled: false));
            return model;
        });
}
