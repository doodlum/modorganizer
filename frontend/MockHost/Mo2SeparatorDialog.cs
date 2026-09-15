using NexusMods.App.UI.Dialog;
using NexusMods.App.UI.Dialog.Enums;
using NexusMods.App.UI.Windows;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.UI.Sdk.Dialog;
using NexusMods.UI.Sdk.Dialog.Enums;

namespace Mo2.Frontend;

internal static class Mo2SeparatorDialog
{
    public static async Task<bool> ConfirmRemoval(IWindowManager windows, IReadOnlyList<Mo2LiveMod> mods)
    {
        if (mods.Count == 0) return false;
        var separatorsOnly = mods.All(mod => mod.IsSeparator);
        var title = mods.Count == 1 ? $"Delete {mods[0].DisplayName}?" : $"Delete {mods.Count} items?";
        var dialog = DialogFactory.CreateStandardDialog(title, new StandardDialogParameters {
            Text = separatorsOnly
                ? mods.Count == 1 ? "Delete this separator from this MO2 instance? Its mods stay installed and keep their enabled states." : "Delete these separators from this MO2 instance? Their mods stay installed and keep their enabled states."
                : "Delete these installed mods from this MO2 instance and all its profiles? Downloaded archives are kept.\n\n" + string.Join("\n", mods.Select(mod => mod.DisplayName))
        }, [DialogStandardButtons.Cancel, new DialogButtonDefinition("Delete", ButtonDefinitionId.Accept, ButtonAction.Accept, ButtonStyling.Primary)], DialogWindowSize.Small);
        return (await windows.ShowDialog(dialog, DialogWindowType.Modal)).ButtonId == ButtonDefinitionId.Accept;
    }

    public static async Task Rename(IWindowManager windows, Mo2LiveProfile profile, string name)
    {
        if (!profile.CanChangeOriginalUi) return;
        var target = profile.CurrentTarget;
        var separator = profile.Mods.FirstOrDefault(mod => mod.Name == name && mod.IsSeparator);
        if (separator is null) return;
        var dialog = DialogFactory.CreateStandardDialog("Rename separator", new StandardDialogParameters {
            Text = "Choose a name for this separator.", InputLabel = "Separator name", InputText = separator.DisplayName
        }, [DialogStandardButtons.Cancel, new DialogButtonDefinition("Rename", ButtonDefinitionId.Accept, ButtonAction.Accept, ButtonStyling.Primary)], DialogWindowSize.Small);
        var result = await windows.ShowDialog(dialog, DialogWindowType.Modal);
        if (result.ButtonId == ButtonDefinitionId.Accept && !string.IsNullOrWhiteSpace(result.InputText) && result.InputText.Trim() != separator.DisplayName)
            await profile.RenameCollection(name, result.InputText.Trim(), target);
    }

    // MO2's Rename on an ordinary mod. The same prompt as a separator's, because in
    // MO2 it is the same editor on the same list.
    public static async Task RenameMod(IWindowManager windows, Mo2LiveProfile profile, string name)
    {
        if (!profile.CanChangeOriginalUi) return;
        var target = profile.CurrentTarget;
        var mod = profile.Mods.FirstOrDefault(x => x.Name == name && !x.IsSeparator);
        if (mod is null) return;
        var dialog = DialogFactory.CreateStandardDialog("Rename mod", new StandardDialogParameters {
            Text = "MO2 renames the mod's folder with it.", InputLabel = "Mod name", InputText = mod.DisplayName
        }, [DialogStandardButtons.Cancel, new DialogButtonDefinition("Rename", ButtonDefinitionId.Accept, ButtonAction.Accept, ButtonStyling.Primary)], DialogWindowSize.Small);
        var result = await windows.ShowDialog(dialog, DialogWindowType.Modal);
        if (result.ButtonId == ButtonDefinitionId.Accept && !string.IsNullOrWhiteSpace(result.InputText) && result.InputText.Trim() != mod.DisplayName)
            await profile.RenameMod(name, result.InputText.Trim(), target);
    }

    public static async Task Create(IWindowManager windows, Mo2LiveProfile profile, EntityId? above)
    {
        if (!profile.CanChangeOriginalUi) return;
        var target = profile.CurrentTarget;
        var dialog = DialogFactory.CreateStandardDialog("Add separator", new StandardDialogParameters {
            Text = "Organise your mod list with a named separator.", InputLabel = "Separator name", InputWatermark = "e.g. Visual improvements"
        }, [DialogStandardButtons.Cancel, new DialogButtonDefinition("Create", ButtonDefinitionId.Accept, ButtonAction.Accept, ButtonStyling.Primary)], DialogWindowSize.Small);
        var result = await windows.ShowDialog(dialog, DialogWindowType.Modal);
        if (result.ButtonId == ButtonDefinitionId.Accept && !string.IsNullOrWhiteSpace(result.InputText) && target == profile.CurrentTarget)
            await profile.CreateSeparator(above, result.InputText.Trim());
    }
}
