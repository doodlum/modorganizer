using NexusMods.App.UI.Dialog;
using NexusMods.App.UI.Dialog.Enums;
using NexusMods.App.UI.Windows;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.UI.Sdk.Dialog;
using NexusMods.UI.Sdk.Dialog.Enums;

namespace Mo2.Frontend;

// A mod's information, shown by the frontend.
//
// This used to send showModDetails to the bridge, which opens MO2's own mod
// information window. A frontend-hosted MO2 keeps its windows off screen, so that
// dialog was one the user could never see or answer while it held MO2's event loop
// and every later request behind it.
//
// What stood in its place was a list of the fields the snapshot happened to carry.
// MO2's panel is nine tabs over the mod's own folder, and most of what it shows —
// the readmes, the ini files, which plugins the game can see, what the mod fights
// with, its categories, the note kept against it — was not reachable at all. Those
// tabs are rebuilt here, in this application's own controls, and shown in its own
// dialog: see Mo2ModInfoPage for what each one reads and Mo2ModInfoView for how it
// is drawn.
internal static class Mo2ModInformationDialog
{
    private static readonly ButtonDefinitionId OpenFolder = ButtonDefinitionId.From("open-folder");

    public static async Task Show(IWindowManager windows, Mo2LiveProfile profile, EntityId id)
    {
        var found = profile.Mods.FirstOrDefault(mod => mod.Id == id);
        if (found is null) return;

        var page = new Mo2ModInfoPage(profile, found);
        var buttons = new[] {
            new DialogButtonDefinition("Open folder", OpenFolder, ButtonAction.Reject),
            DialogStandardButtons.Ok,
        };

        // MO2 titles the window with the mod, and gives the panel the room nine tabs
        // need rather than the room a sentence needs.
        var result = await windows.ShowDialog(
            DialogFactory.CreateDialog(found.DisplayName, buttons, page, DialogWindowSize.Large),
            DialogWindowType.Modal);

        if (result.ButtonId == OpenFolder) await profile.OpenModFolder(found.Name);
    }
}
