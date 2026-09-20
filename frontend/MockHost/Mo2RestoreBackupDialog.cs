using NexusMods.App.UI.Dialog;
using NexusMods.App.UI.Dialog.Enums;
using NexusMods.App.UI.Windows;
using NexusMods.UI.Sdk.Dialog;
using NexusMods.UI.Sdk.Dialog.Enums;

namespace Mo2.Frontend;

// Choosing which backup of a list to go back to.
//
// MO2 has its own picker for this, and the frontend used to open it by clicking
// MO2's restore button. That cannot work in a frontend-hosted MO2: its windows
// are kept off screen, so the picker was a modal nobody could answer and it held
// MO2's event loop for good — every later request from the frontend queued behind
// it unanswered. The list comes from the bridge as files, and the choice is made
// here.
internal static class Mo2RestoreBackupDialog
{
    public static async Task Show(IWindowManager windows, Mo2LiveProfile profile, string list)
    {
        if (!profile.CanChangeOriginalUi) return;
        var target = profile.CurrentTarget;
        var what = list == "mods" ? "mod list" : "plugin order";

        IReadOnlyList<Mo2LiveProfile.Mo2OrderBackup> backups;
        try { backups = await profile.ListOrderBackups(list, target); }
        catch (Exception error) {
            await Tell(windows, $"Could not read {what} backups", error.Message);
            return;
        }

        if (backups.Count == 0) {
            await Tell(windows, "No backups", $"MO2 has no {what} backups for this profile yet. Create one first.");
            return;
        }

        // MO2 keeps ten, named by the moment they were taken. Newest first, because
        // going back one step is what this is for most of the time.
        var choices = backups
            .Select(backup => new DialogButtonDefinition(Describe(backup), ButtonDefinitionId.From(backup.Id), ButtonAction.Accept))
            .Prepend(DialogStandardButtons.Cancel)
            .ToArray();

        var dialog = DialogFactory.CreateStandardDialog($"Restore {what}", new StandardDialogParameters {
            Text = $"Replace this profile's {what} with one of MO2's backups. The current {what} is not backed up by this.",
        }, choices, DialogWindowSize.Medium);

        var result = await windows.ShowDialog(dialog, DialogWindowType.Modal);
        if (result.ButtonId == ButtonDefinitionId.Cancel) return;
        var chosen = backups.FirstOrDefault(backup => ButtonDefinitionId.From(backup.Id) == result.ButtonId);
        if (chosen is null) return;
        await profile.RestoreOrderBackup(list, chosen.Id, target);
    }

    private static string Describe(Mo2LiveProfile.Mo2OrderBackup backup) =>
        backup.Taken == DateTime.MinValue ? backup.Id : backup.Taken.ToString("d MMM yyyy, HH:mm");

    private static Task Tell(IWindowManager windows, string title, string text) =>
        windows.ShowDialog(DialogFactory.CreateStandardDialog(title, new StandardDialogParameters { Text = text },
            [DialogStandardButtons.Ok], DialogWindowSize.Small), DialogWindowType.Modal);
}
