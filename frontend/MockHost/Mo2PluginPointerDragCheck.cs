using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Text.Json;

namespace Mo2.Frontend;

// Cooperates with the PID-scoped X11 driver in tools/check_plugin_pointer_drag.py.
// Only the driver supplies input. Native state is read independently after each
// drop; the normal drag handlers, not this check, must perform the reorder.
internal static class Mo2PluginPointerDragCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window, string phasePath)
    {
        using var turn = await Mo2CheckTurn.Take();
        async Task Wait(Func<bool> ready, string stage) {
            var end = DateTime.UtcNow.AddSeconds(25);
            while (!ready()) {
                if (DateTime.UtcNow > end) throw new Exception("Pointer drag: " + stage);
                await Task.Delay(50);
            }
        }
        var profile = shell.Profile;
        await Wait(() => profile.CanChangeOriginalUi && profile.Mods.Count > 0, "native profile ready");
        if (!profile.ProfilePath.Replace('\\', '/').EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Clone Test"))
            throw new Exception("Pointer drag requires the isolated FNV integration profile");
        await profile.Refresh();
        var target = profile.CurrentTarget;
        var candidates = profile.Order.Plugins.Where(x => x.CanMove && x.DisplayName.StartsWith("MCM Example")).ToArray();
        if (candidates.Length < 3) throw new Exception("Pointer test needs three movable MCM example plugins");
        var moving = candidates[0];
        var destination = candidates[1];
        if (destination.SortIndex != moving.SortIndex + 1)
            throw new Exception("Pointer test needs adjacent example plugins");
        string[] Order() => profile.Order.Plugins.Select(x => x.DisplayName).ToArray();
        var original = Order();
        var swapped = original.ToArray();
        (swapped[moving.SortIndex], swapped[destination.SortIndex]) = (swapped[destination.SortIndex], swapped[moving.SortIndex]);
        var groupNames = candidates.Take(2).Select(x => x.DisplayName).ToArray();
        var groupTarget = candidates[2];
        var groupedList = original.Except(groupNames).ToList();
        groupedList.InsertRange(groupedList.IndexOf(groupTarget.DisplayName) + 1, groupNames);
        var grouped = groupedList.ToArray();
        var mods = profile.Mods.Select(x => (x.Name, x.State, x.Priority)).ToArray();
        var plugins = profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray();
        var client = new Mo2BridgeClient(target.Endpoint);
        async Task<string[]> HostOrder() => (await client.SendAsync("snapshot")).GetProperty("plugins").EnumerateArray()
            .OrderBy(x => x.GetProperty("priority").GetInt32()).Select(x => x.GetProperty("name").GetString()!).ToArray();
        await Wait(() => window.GetVisualDescendants().OfType<Mo2PluginsView>().Any(x => x.IsEffectivelyVisible), "Plugins page rendered");
        var view = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single(x => x.IsEffectivelyVisible);
        var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
        await Wait(() => table.RowSelection is not null && table.Rows?.Count > 0, "Plugins table source and selection attached");
        var dragStarts = 0; var drops = 0; var lastEffects = DragDropEffects.None; var draggedCount = 0;
        table.RowDragStarted += (_, e) => { dragStarts++; draggedCount = e.Models.Count(); lastEffects = e.AllowedEffects; Console.WriteLine("POINTER drag start: " + e.AllowedEffects); };
        table.AddHandler(TreeDataGrid.RowDropEvent, (_, e) => { drops++; Console.WriteLine("POINTER row drop: " + e.Position); },
            RoutingStrategies.Direct | RoutingStrategies.Bubble, handledEventsToo: true);
        void Press(object? sender, PointerPressedEventArgs e) => Console.WriteLine("POINTER press: " + e.Source?.GetType().Name +
            ", left=" + e.GetCurrentPoint(table).Properties.IsLeftButtonPressed + ", tablePoint=" + e.GetPosition(table) +
            ", ancestors=" + string.Join("/", (e.Source as Visual)?.GetVisualAncestors().OfType<Control>().Take(6).Select(x => x.GetType().Name + "#" + x.Name) ?? []));
        void Move(object? sender, PointerEventArgs e) {
            if (e.GetCurrentPoint(table).Properties.IsLeftButtonPressed)
                Console.WriteLine("POINTER held motion: " + e.GetPosition(table));
        }
        window.AddHandler(InputElement.PointerPressedEvent, Press, RoutingStrategies.Tunnel, true);
        table.AddHandler(InputElement.PointerMovedEvent, Move, RoutingStrategies.Tunnel, true);
        async Task Phase(string id, bool after, string[] expected, ScenarioPlugin? fixedSource = null, bool multi = false) {
            var source = fixedSource ?? moving;
            var dropTarget = multi ? groupTarget : destination;
            if (profile.CurrentTarget != target) throw new Exception("Profile changed before pointer drag");
            window.Activate();
            await Wait(() => window.IsActive, "owned window active before calculating drag points");
            table.RowSelection!.Clear();
            var rail = view.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Name == "PluginRailScrollBar");
            rail.Value = rail.Maximum;
            Border? Handle(string name) => table.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(x => x.Name == "PluginDragHandle" && Equals(x.Tag, name) && x.IsEffectivelyVisible);
            await Wait(() => Handle(source.DisplayName) is not null && Handle(dropTarget.DisplayName) is not null, "drag handles visible");
            await Task.Delay(250);
            if (multi) {
                for (var index = 0; index < groupNames.Length; index++) {
                    var selectedGrip = Handle(groupNames[index]) ?? throw new Exception("Selection grip is missing");
                    var point = selectedGrip.PointToScreen(new Point(selectedGrip.Bounds.Width / 2, selectedGrip.Bounds.Height / 2));
                    var selectId = id + "-select-" + index;
                    File.WriteAllText(phasePath + ".tmp", JsonSerializer.Serialize(new {
                        Phase = selectId, Action = "click", Control = index > 0,
                        Start = new { point.X, point.Y }, End = new { point.X, point.Y }
                    }));
                    File.Move(phasePath + ".tmp", phasePath, true);
                    var selectedCount = index + 1;
                    await Wait(() => view.ViewModel!.Adapter.SelectedModels.Count == selectedCount &&
                        groupNames.Take(selectedCount).All(name => view.ViewModel.Adapter.SelectedModels.Any(row =>
                            row.Key.Equals(profile.Order.Plugins.Single(plugin => plugin.DisplayName == name).Key))),
                        "physical multi-selection did not select the expected plugins");
                    await Wait(() => File.Exists(phasePath + ".done") && File.ReadAllText(phasePath + ".done") == selectId,
                        "selection input did not complete");
                }
            }
            var grip = Handle(source.DisplayName)!;
            var row = Handle(dropTarget.DisplayName)!.GetVisualAncestors().OfType<TreeDataGridRow>().First();
            var start = grip.PointToScreen(new Point(grip.Bounds.Width / 2, grip.Bounds.Height / 2));
            var end = row.PointToScreen(new Point(start.X - row.PointToScreen(default).X, row.Bounds.Height * (after ? .8 : .2)));
            var hit = window.InputHitTest(window.PointToClient(start)) as Visual;
            Console.WriteLine("POINTER target: " + start + " -> " + end + ", hit=" + (hit as Control)?.GetType().Name);
            if (hit is null || !hit.GetSelfAndVisualAncestors().Contains(table))
                throw new Exception("Grip coordinates do not hit the Plugins table: " + (hit as Control)?.Name);
            var priorStarts = dragStarts; var priorDrops = drops;
            var request = JsonSerializer.Serialize(new { Phase = id, Start = new { start.X, start.Y }, End = new { end.X, end.Y } });
            File.WriteAllText(phasePath + ".tmp", request); File.Move(phasePath + ".tmp", phasePath, true);
            if (fixedSource is not null) {
                await Wait(() => File.Exists(phasePath + ".done") && File.ReadAllText(phasePath + ".done") == id,
                    "driver did not finish the rejected gesture");
                await Task.Delay(500);
                if (dragStarts <= priorStarts || lastEffects != DragDropEffects.None)
                    throw new Exception("Fixed game master offered a reorder gesture instead of rejecting it");
                if (!expected.SequenceEqual(Order()) || !expected.SequenceEqual(await HostOrder()))
                    throw new Exception("Fixed game master gesture changed load order");
                Console.WriteLine("PASS plugin pointer restriction: fixed master rejects drag and native order stays unchanged");
                return;
            }
            await Wait(() => expected.SequenceEqual(Order()), "physical drop did not produce " + id);
            if (dragStarts <= priorStarts || drops <= priorDrops) throw new Exception("Native order changed without table drag/drop events");
            if (draggedCount != (multi ? 2 : 1)) throw new Exception("Grip drag changed the selected group");
            if (!expected.SequenceEqual(await HostOrder())) throw new Exception("Independent native order disagrees with " + id);
            Console.WriteLine("PASS plugin pointer drag: " + id + "; real grip input raised table drag/drop and native order matches");
        }
        try {
            await Phase("down", true, swapped);
            await Phase("up", false, original);
            await Phase("multi-down", true, grouped, multi: true);
            await Phase("multi-up", false, original, multi: true);
            await Phase("fixed", true, original, profile.Order.Plugins.First(x => !x.CanMove));
        } finally {
            window.RemoveHandler(InputElement.PointerPressedEvent, Press);
            table.RemoveHandler(InputElement.PointerMovedEvent, Move);
            File.Delete(phasePath); File.Delete(phasePath + ".done");
            await profile.Refresh();
            if (profile.CurrentTarget == target && (Order().SequenceEqual(swapped) || Order().SequenceEqual(grouped)))
                await profile.Order.ApplyOrder!(original.Select(name => profile.Order.Plugins.Single(x => x.DisplayName == name)).ToArray(), CancellationToken.None);
            table.RowSelection?.Clear();
        }
        if (!original.SequenceEqual(await HostOrder()) || !mods.SequenceEqual(profile.Mods.Select(x => (x.Name, x.State, x.Priority))) ||
            !plugins.SequenceEqual(profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive))))
            throw new Exception("Pointer drag did not restore native order/activation");
        Console.WriteLine("PASS plugin pointer restoration: original native plugin order and all mod/plugin activation preserved");
    }
}
