using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
namespace Mo2.Frontend;

internal static class Mo2MenuAvailabilityCheck
{
    private sealed record Row(string Name);
    internal static async Task Run()
    {
        var source = new FlatTreeDataGridSource<Row>(new[] { new Row("first"), new Row("second") });
        source.Columns.Add(new TextColumn<Row, string>("Name", x => x.Name));
        var table = new TreeDataGrid { Source = source };
        var pending = new Queue<TaskCompletionSource<Mo2MenuEntry[]?>>();
        TaskCompletionSource<Mo2MenuEntry[]?> Next() {
            var result = new TaskCompletionSource<Mo2MenuEntry[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Enqueue(result); return result;
        }
        var old = Next(); var current = Next();
        Mo2RowMenu.Attach<Row>(table, _ => [Mo2EntryMenu.Action("Preview", () => Task.CompletedTask),
            Mo2EntryMenu.Action("Execute", () => Task.CompletedTask),
            Mo2EntryMenu.Action("Local guard", () => Task.CompletedTask, false)], availability: _ => pending.Dequeue().Task);
        var window = new Window { Content = table, Width = 400, Height = 300, ShowInTaskbar = false };
        window.Show();
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (!ready()) { if (DateTime.UtcNow > until) throw new Exception("Menu availability did not settle"); await Task.Delay(20); }
        }
        try {
            source.RowSelection!.Select(new IndexPath(0));
            var menu = table.ContextMenu!;
            menu.Open(table);
            await Wait(() => pending.Count == 1);
            if (menu.Items.OfType<MenuItem>().Any(x => x.IsEnabled)) throw new Exception("Actions remained enabled during native lookup");
            menu.Close();
            source.RowSelection.Select(new IndexPath(1));
            menu.Open(table);
            await Wait(() => pending.Count == 0);
            current.SetResult([new("Preview", false, false, []), new("Execute", true, false, []), new("Local guard", true, false, [])]);
            await Wait(() => menu.Items.OfType<MenuItem>().Single(x => x.Header as string == "Execute").IsEnabled);
            old.SetResult([new("Preview", true, false, []), new("Execute", false, false, [])]);
            await Task.Delay(100);
            var entries = menu.Items.OfType<MenuItem>().ToDictionary(x => (string)x.Header!, x => x.IsEnabled);
            if (entries["Preview"] || !entries["Execute"] || entries["Local guard"])
                throw new Exception("Late availability changed a reopened menu, or bypassed a local guard");
            menu.Close();
            Console.WriteLine("PASS menu availability: pending actions disabled; current native flags applied; stale reply discarded; local guards retained");
        } finally { old.TrySetResult([]); current.TrySetResult([]); window.Close(); source.Dispose(); }
    }
}
