using Avalonia.Controls;
using Avalonia.LogicalTree;
using System.Reflection;

namespace Mo2.Frontend;

internal static class Mo2NotificationLifecycleCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell)
    {
        var profile = new Mo2LiveProfile("notification-fixture");
        typeof(Mo2LiveProfile).GetProperty(nameof(profile.ProfilePath))!.SetValue(profile, "Default");
        typeof(Mo2LiveProfile).GetProperty(nameof(profile.OriginalUiVisible))!.SetValue(profile, false);
        void Connection(bool connected) {
            typeof(Mo2LiveProfile).GetField("_lastSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(profile, connected ? "fixture" : null);
            ((Action?)typeof(Mo2LiveProfile).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(profile))?.Invoke();
        }
        var reads = new List<TaskCompletionSource<(string Title, string Details)[]>>();
        Connection(true);
        using var notifications = new Mo2Notifications(shell, profile, () => {
            var pending = new TaskCompletionSource<(string Title, string Details)[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            reads.Add(pending); return pending.Task;
        });
        string Status() => ToolTip.GetTip(notifications.Button)?.ToString() ?? "";
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(10);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception("Notification lifecycle did not settle");
                await Task.Delay(25);
            }
        }
        await Wait(() => reads.Count == 1);
        Connection(false);
        if (!Status().Contains("disconnected")) throw new Exception("Disconnect did not mark notifications unavailable");
        Connection(true);
        reads[0].SetResult([("Old session warning", "Old details")]);
        await Wait(() => reads.Count == 2);
        if (!Status().Contains("Checking MO2 warnings")) throw new Exception("Old session result replaced reconnect status");
        reads[1].SetResult([]);
        await Wait(() => Status().Contains("No MO2 warnings"));
        Connection(false);
        if (Status().Contains("No MO2 warnings") || !Status().Contains("cannot be checked"))
            throw new Exception("Disconnected host retains a current healthy status");
        Connection(true);
        await Wait(() => reads.Count == 3);
        reads[2].SetResult([("Current warning", "Current details")]);
        await Wait(() => Status().Contains("1 MO2 warning"));
        var content = (Control)((Flyout)notifications.Button.Flyout!).Content!;
        Expander Card() => content.GetLogicalDescendants().OfType<Expander>().Single();
        var card = Card(); card.IsExpanded = true;
        var refresh = notifications.Refresh();
        await Wait(() => reads.Count == 4);
        reads[3].SetResult([("Current warning", "Current details")]);
        await refresh;
        if (!ReferenceEquals(card, Card()) || !Card().IsExpanded)
            throw new Exception("Unchanged warning refresh rebuilt or collapsed the report being read");
        Connection(false);
        if (!Status().Contains("last known warnings") || !Status().Contains("1 unread"))
            throw new Exception("Disconnected host did not retain its cached warning as last known");
        if (!ReferenceEquals(card, Card()) || !Card().IsExpanded)
            throw new Exception("Connection status update collapsed a cached warning");
        var markRead = content.GetLogicalDescendants().OfType<Button>().Single(x => Equals(x.Content, "Mark all read"));
        markRead.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        if (!Status().Contains("0 unread") || !ReferenceEquals(card, Card()) || !Card().IsExpanded)
            throw new Exception("Marking warnings read collapsed the report or did not update its badge");
        Connection(true);
        await Wait(() => reads.Count == 5);
        reads[4].SetResult([("Current warning", "Changed details")]);
        await Wait(() => !ReferenceEquals(card, Card()));
        if (!Status().Contains("1 unread")) throw new Exception("Changed diagnostic did not become unread");
        Console.WriteLine("PASS notification reading: unchanged refresh, disconnect and mark-read preserve expanded report controls; unread status updates");
        Console.WriteLine("PASS notification connection lifecycle: disconnect marks unknown health, reconnect refreshes immediately, old session responses are ignored, cached unread warnings remain labeled last known");
    }
}
