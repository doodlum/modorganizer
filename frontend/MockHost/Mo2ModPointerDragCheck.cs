using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Text.Json;

namespace Mo2.Frontend;

// Cooperates with the PID-scoped X11 driver in tools/check_mod_pointer_drag.py.
// Only the driver supplies input. Native state is read independently after each
// drop; the normal drag handlers, not this check, must perform the reorder.
internal static class Mo2ModPointerDragCheck
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
        if (!profile.ProfilePath.Replace('\\', '/').EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new Exception("Pointer drag requires the isolated FNV integration profile");
        await profile.Refresh();
        var target = profile.CurrentTarget;
        var moving = profile.Mods.Single(x => x.Name == "The Mod Configuration Menu");
        var destination = profile.Mods.Single(x => x.Name == "MCM Author Examples");
        if (!moving.CanManage || !destination.CanManage || (moving.State & 6) != 0 || (destination.State & 6) != 0 || destination.Priority != moving.Priority + 1)
            throw new Exception("Pointer drag requires the two disabled adjacent MCM mods");
        var original = profile.ModPriorityOrder;
        var swapped = original.ToArray();
        (swapped[moving.Priority], swapped[destination.Priority]) = (swapped[destination.Priority], swapped[moving.Priority]);
        var mods = profile.Mods.Select(x => (x.Name, x.State, x.Priority)).ToArray();
        var plugins = profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray();
        var client = new Mo2BridgeClient(target.Endpoint);
        async Task<string[]> HostOrder() => (await client.SendAsync("snapshot")).GetProperty("mods").EnumerateArray()
            .Where(x => !x.GetProperty("overwrite").GetBoolean() && x.GetProperty("priority").GetInt32() >= 0)
            .OrderBy(x => x.GetProperty("priority").GetInt32()).Select(x => x.GetProperty("name").GetString()!).ToArray();
        await Wait(() => window.GetVisualDescendants().OfType<Mo2ModsView>().Any(x => x.IsEffectivelyVisible), "Mods page rendered");
        var view = window.GetVisualDescendants().OfType<Mo2ModsView>().Single(x => x.IsEffectivelyVisible);
        var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
        await Wait(() => table.RowSelection is not null && table.Rows?.Count > 0, "Mods table source and selection attached");
        var dragStarts = 0; var drops = 0;
        table.RowDragStarted += (_, e) => { dragStarts++; Console.WriteLine("POINTER drag start: " + e.AllowedEffects); };
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
        async Task Phase(string id, bool after, string[] expected) {
            if (profile.CurrentTarget != target) throw new Exception("Profile changed before pointer drag");
            window.Activate();
            await Wait(() => window.IsActive, "owned window active before calculating drag points");
            table.RowSelection!.Clear();
            var rail = view.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Name == "ModsRailScrollBar");
            rail.Value = rail.Maximum;
            Border? Handle(string name) => table.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(x => x.Name == "ModDragHandle" && Equals(x.Tag, name) && x.IsEffectivelyVisible);
            await Wait(() => Handle(moving.Name) is not null && Handle(destination.Name) is not null, "drag handles visible");
            await Task.Delay(250);
            var grip = Handle(moving.Name)!;
            var row = Handle(destination.Name)!.GetVisualAncestors().OfType<TreeDataGridRow>().First();
            var start = grip.PointToScreen(new Point(grip.Bounds.Width / 2, grip.Bounds.Height / 2));
            var end = row.PointToScreen(new Point(start.X - row.PointToScreen(default).X, row.Bounds.Height * (after ? .8 : .2)));
            var hit = window.InputHitTest(window.PointToClient(start)) as Visual;
            Console.WriteLine("POINTER target: " + start + " -> " + end + ", hit=" + (hit as Control)?.GetType().Name);
            if (hit is null || !hit.GetSelfAndVisualAncestors().Contains(table))
                throw new Exception("Grip coordinates do not hit the Mods table: " + (hit as Control)?.Name);
            var priorStarts = dragStarts; var priorDrops = drops;
            var request = JsonSerializer.Serialize(new { Phase = id, Start = new { start.X, start.Y }, End = new { end.X, end.Y } });
            File.WriteAllText(phasePath + ".tmp", request); File.Move(phasePath + ".tmp", phasePath, true);
            await Wait(() => expected.SequenceEqual(profile.ModPriorityOrder), "physical drop did not produce " + id);
            if (dragStarts <= priorStarts || drops <= priorDrops) throw new Exception("Native order changed without table drag/drop events");
            if (!expected.SequenceEqual(await HostOrder())) throw new Exception("Independent native order disagrees with " + id);
            Console.WriteLine("PASS mod pointer drag: " + id + "; real grip input raised table drag/drop and native order matches");
        }
        try {
            await Phase("down", true, swapped);
            await Phase("up", false, original);
        } finally {
            window.RemoveHandler(InputElement.PointerPressedEvent, Press);
            table.RemoveHandler(InputElement.PointerMovedEvent, Move);
            File.Delete(phasePath);
            await profile.Refresh();
            if (profile.CurrentTarget == target && profile.ModPriorityOrder.SequenceEqual(swapped))
                await profile.MoveModsRelative([moving.Id], destination.Id, false, target, swapped);
            table.RowSelection?.Clear();
        }
        if (!original.SequenceEqual(await HostOrder()) || !mods.SequenceEqual(profile.Mods.Select(x => (x.Name, x.State, x.Priority))) ||
            !plugins.SequenceEqual(profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive))))
            throw new Exception("Pointer drag did not restore native order/activation");
        Console.WriteLine("PASS mod pointer restoration: original native mod order and all mod/plugin activation preserved");
    }
}
