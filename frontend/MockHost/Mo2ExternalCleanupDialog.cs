using NexusMods.App.UI.Dialog;
using NexusMods.App.UI.Dialog.Enums;
using NexusMods.App.UI.Windows;
using NexusMods.UI.Sdk.Dialog;
using NexusMods.UI.Sdk.Dialog.Enums;

namespace Mo2.Frontend;

internal static class Mo2ExternalCleanupDialog
{
    internal static async Task<bool> Confirm(IWindowManager windows, string title, string text, string action)
    {
        var dialog = DialogFactory.CreateStandardDialog(title, new StandardDialogParameters { Text = text },
            [DialogStandardButtons.Cancel, new DialogButtonDefinition(action, ButtonDefinitionId.Accept, ButtonAction.Accept, ButtonStyling.Primary)], DialogWindowSize.Small);
        return (await windows.ShowDialog(dialog, DialogWindowType.Modal)).ButtonId == ButtonDefinitionId.Accept;
    }
}
