using Microsoft.Extensions.DependencyInjection;
using NexusMods.MnemonicDB;
using System.Reactive.Linq;
using DynamicData;
using NexusMods.Abstractions.Loadouts;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Pages;
using NexusMods.App.UI.Pages.LoadoutPage;
using NexusMods.MnemonicDB.Abstractions;

namespace Mo2.Frontend;

internal sealed record ScenarioMod(EntityId Id, string Name, string Plugin, bool Enabled, DateTimeOffset Installed);
internal sealed class ScenarioInstalledMods : ILoadoutDataProvider
{
    private readonly SourceCache<ScenarioMod, EntityId> _mods = new(x => x.Id);
    private readonly ScenarioPluginOrder _order;
    public ScenarioInstalledMods(ScenarioPluginOrder order)
    {
        _order = order;
        var plugins = new[] { "YUP - Base Game + All DLC.esm", "Desert Lighting.esp", "Mojave Encounters.esp", "Compatibility Patch.esp" };
        var names = new[] { "Yukichigai Unofficial Patch", "Desert Lighting", "Mojave Encounters", "Compatibility Patch" };
        for (var i = 0; i < names.Length; i++)
            _mods.AddOrUpdate(new ScenarioMod(EntityId.From((ulong)(100 + i)), names[i], plugins[i], true, DateTimeOffset.Now.AddMinutes(-10 - i)));
    }
    public void Install(ScenarioMod item)
    {
        if (_mods.Lookup(item.Id).HasValue) return;
        _mods.AddOrUpdate(item with { Installed = DateTimeOffset.Now, Enabled = true });
        _order.InstallPlugin(item.Plugin, item.Name);
    }
    public void CopyFrom(ScenarioInstalledMods source) => _mods.Edit(cache => { cache.Clear(); cache.AddOrUpdate(source.Mods); });
    public IReadOnlyCollection<ScenarioMod> Mods => _mods.Items.ToArray();
    public void Toggle(IEnumerable<LoadoutItemId> ids)
    {
        foreach (var id in ids)
        {
            var mod = _mods.Lookup(id.Value);
            if (mod.HasValue) {
                var updated = mod.Value with { Enabled = !mod.Value.Enabled };
                _mods.AddOrUpdate(updated);
                _order.SetPluginActive(updated.Plugin, updated.Enabled);
            }
        }
    }
    public void Remove(IEnumerable<LoadoutItemId> ids)
    {
        foreach (var id in ids.ToArray()) {
            var mod = _mods.Lookup(id.Value);
            if (mod.HasValue) _order.SetPluginActive(mod.Value.Plugin, false);
            _mods.RemoveKey(id.Value);
        }
    }
    public IObservable<int> CountLoadoutItems(LoadoutFilter filter) => System.Reactive.Linq.Observable.Defer(() => _mods.CountChanged.StartWith(_mods.Count).DistinctUntilChanged());
    public IObservable<IChangeSet<CompositeItemModel<EntityId>, EntityId>> ObserveLoadoutItems(LoadoutFilter filter)
        => _mods.Connect().Transform(mod => {
            var model = new CompositeItemModel<EntityId>(mod.Id);
            model.Add(SharedColumns.Name.NameComponentKey, new NameComponent(mod.Name));
            model.Add(SharedColumns.InstalledDate.ComponentKey, new DateComponent(mod.Installed));
            model.Add(LoadoutColumns.Collections.ComponentKey, new StringComponent("My Mods"));
            model.Add(LoadoutColumns.EnabledState.LoadoutItemIdsComponentKey, new LoadoutComponents.LoadoutItemIds(LoadoutItemId.From(mod.Id)));
            model.Add(LoadoutColumns.EnabledState.EnabledStateToggleComponentKey, new LoadoutComponents.EnabledStateToggle(new ValueComponent<bool?>(mod.Enabled)));
            model.Add(LoadoutColumns.EnabledState.UninstallItemComponentKey, new SharedComponents.UninstallItemAction(isEnabled: true));
            return model;
        });
}

// A private, empty in-memory database satisfies the original adapter's constructor.
// All displayed data still comes from the fixture provider above.
internal static class ScenarioDatabase
{
    public static Microsoft.Extensions.DependencyInjection.ServiceProvider Create()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddMnemonicDB();
        services.AddSingleton(NexusMods.MnemonicDB.Abstractions.DatomStoreSettings.InMemory);
        services.AddSingleton<NexusMods.MnemonicDB.Storage.Abstractions.IStoreBackend>(new NexusMods.MnemonicDB.Storage.RocksDbBackend.Backend());
        return services.BuildServiceProvider();
    }
}
