using Avalonia.Controls;

namespace Mo2.Frontend;

internal static class Mo2OrderHistoryMenu
{
    public static void Add(MenuFlyout menu, Func<Mo2LiveProfile?> profile, string list)
    {
        Mo2ProfileTarget? target = null;
        var label = list == "mods" ? "mod list" : "plugin order";
        var backup = new MenuItem { Header = "Back up " + label + "…" };
        var restore = new MenuItem { Header = "Restore " + label + "…" };
        menu.Opening += (_,_) => {
            var current = profile(); target = current?.CurrentTarget;
            backup.IsEnabled = restore.IsEnabled = current?.CanChangeOriginalUi == true;
        };
        async Task Run(string operation) {
            if (profile() is { } current && target is { } captured)
                await current.OrderBackup(list, operation, captured);
        }
        backup.Click += async (_,_) => await Run("backup");
        restore.Click += async (_,_) => await Run("restore");
        menu.Items.Add(backup); menu.Items.Add(restore);
    }
}
