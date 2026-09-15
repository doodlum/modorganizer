namespace Mo2.Frontend;

internal static class Mo2NotificationStateCheck
{
    public static void Run()
    {
        var state = new Mo2NotificationState();
        var fnv = new Mo2ProfileTarget("fnv", "Default");
        var skyrim = new Mo2ProfileTarget("skyrim", "Default");
        var warning = new Mo2NotificationState.Warning("Missing masters", "Enable the required master.");
        void Require(bool condition) { if (!condition) throw new InvalidOperationException("Notification state check failed"); }
        Require(state.Update(fnv, [warning, warning]) == 1 && state.Unread == 1 && state.Warnings.Length == 1);
        state.MarkRead(); Require(state.Unread == 0 && state.Warnings.Length == 1);
        Require(state.Update(fnv, [warning]) == 0 && state.Unread == 0);
        state.Select(skyrim); Require(state.Warnings.Length == 0);
        Require(state.Update(skyrim, [warning]) == 1 && state.Unread == 1);
        state.Select(fnv); Require(state.Unread == 0 && state.Warnings.Length == 1);
        state.Update(fnv, []); Require(state.Warnings.Length == 0);
        Require(state.Update(fnv, [warning]) == 1 && state.Unread == 1);
        state.MarkRead();
        Require(state.Update(fnv, [warning with { Details = "A different master is missing." }]) == 1 && state.Unread == 1);
        Console.WriteLine("PASS notifications: deduplication, unread/read state, persistent active warnings, instance isolation, resolution and recurrence, changed diagnostics");
    }
}
