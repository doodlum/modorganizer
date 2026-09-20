using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;
using System.Reactive.Threading.Tasks;
using System.Text.Json;

namespace Mo2.Frontend;

// An external pointer-only driver observes native preview windows. No simulated
// Avalonia events and no save actions are used by this check.
internal static class Mo2SaveHoverCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window, string phasePath)
    {
        async Task Wait(Func<bool> ready, string failure) {
            var until = DateTime.UtcNow.AddSeconds(30);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception(failure);
                await Task.Delay(50);
            }
        }
        await Wait(() => live.Profile.IsConnected && live.Profile.CanPreviewSaves, "Updated native save-preview host did not connect");
        using var turn = await Mo2CheckTurn.Take();
        live.ShowProfile();
        var workspace = live.WorkspaceController.ActiveWorkspace;
        var panel = workspace.Panels.OrderBy(x => x.LogicalBounds.X).Last();
        panel.IsSelected = true;
        await Wait(() => ReferenceEquals(workspace.SelectedPanel, panel), "Could not select target panel");
        var originalTab = panel.SelectedTab;
        await live.ProfileMenu.SavesItem.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)).ToTask();
        await Wait(() => panel.SelectedTab.Contents.ViewModel is Mo2SavesPage, "Saves did not open in the selected panel");
        var saveTab = panel.SelectedTab;
        TreeDataGrid? table = null;
        await Wait(() => (table = window.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault(x =>
            x.IsEffectivelyVisible && x.Name == "SavesTable"))?.Rows?.Count >= 2, "Need two populated native saves");
        await Task.Delay(400);
        PixelPoint Row(int index) {
            var row = table!.GetVisualDescendants().OfType<TreeDataGridRow>().Single(x => x.RowIndex == index);
            return row.PointToScreen(new Point(70, row.Bounds.Height / 2));
        }
        async Task Phase(string name, PixelPoint? point, bool visible) {
            var record = new { stage = name, x = point?.X, y = point?.Y, visible,
                pid = Environment.ProcessId, window = window.TryGetPlatformHandle()!.Handle.ToInt64() };
            var temporary = phasePath + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(record));
            File.Move(temporary, phasePath, true);
            await Wait(() => {
                if (!File.Exists(phasePath + ".reply")) return false;
                using var reply = JsonDocument.Parse(File.ReadAllText(phasePath + ".reply"));
                if (reply.RootElement.GetProperty("stage").GetString() != name) return false;
                if (!reply.RootElement.GetProperty("ok").GetBoolean()) throw new Exception(reply.RootElement.GetProperty("error").GetString());
                return true;
            }, "Pointer driver did not complete " + name);
            if (table!.RowSelection!.SelectedItems.Count != 0) throw new Exception("Hover changed save selection");
            if (visible && !table.IsPointerOver) throw new Exception("Native preview was not driven by the frontend pointer");
        }
        Window? other = null;
        try {
            await Phase("first", Row(0), true);
            await Phase("second", Row(1), true);
            await Phase("leave", window.PointToScreen(new Point(700, 20)), false);
            await Phase("return", Row(0), true);
            panel.SelectTab(originalTab.Id);
            await Phase("hidden", null, false);
            panel.SelectTab(saveTab.Id);
            await Task.Delay(400);
            await Phase("reopen", Row(0), true);
            other = new Window { Title = "Save hover focus check", Width = 200, Height = 100, ShowInTaskbar = false };
            other.Show(); other.Activate();
            await Wait(() => !window.IsActive, "Test window did not deactivate the frontend");
            await Phase("deactivated", null, false);
            Console.WriteLine("PASS save hover UI: actual pointer entry, row change, leave, hidden tab, reopen and deactivation; native previews observed; selection unchanged");
        } finally { other?.Close(); panel.SelectTab(originalTab.Id); }
    }
}
