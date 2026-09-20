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
// details dialog — a modal, with a thirty minute timeout. A frontend-hosted MO2
// keeps its windows off screen, so that dialog was one the user could never see
// or answer while it held MO2's event loop and every later request behind it.
//
// Everything it showed is already in the snapshot the frontend keeps for each
// mod, so it is shown here instead.
internal static class Mo2ModInformationDialog
{
    private static readonly ButtonDefinitionId OpenFolder = ButtonDefinitionId.From("open-folder");

    public static async Task Show(IWindowManager windows, Mo2LiveProfile profile, EntityId id)
    {
        var found = profile.Mods.FirstOrDefault(mod => mod.Id == id);
        if (found is null) return;

        var rows = new List<(string Label, string Value)> {
            ("Name", found.DisplayName),
            ("Version", Or(found.Version, "not set")),
            ("Latest version", Or(found.NewestVersion, "not checked")),
            ("Category", Or(string.Join(", ", found.CategoryNames), Or(found.Category, "none"))),
            ("Priority", Or(found.PriorityText, found.Priority.ToString())),
            ("Author", Or(found.Author, "unknown")),
            ("Uploaded by", Or(found.Uploader, "unknown")),
            ("Installed", Or(found.InstallTime, "unknown")),
            ("Nexus ID", found.NexusId > 0 ? found.NexusId.ToString() : "not from Nexus"),
            ("Installed for", Or(found.SourceGame, profile.GameName)),
            ("Contents", Or(found.Content, "not reported")),
            ("Conflicts", Or(found.Conflicts, "none")),
            ("Flags", Or(found.Flags, "none")),
            ("Endorsed", Or(found.Endorsed, "not endorsed")),
            ("Notes", Or(found.Notes, "none")),
        };
        if (!found.Validated) rows.Add(("Warning", "MO2 could not read this mod cleanly"));

        var width = rows.Max(row => row.Label.Length);
        var text = string.Join('\n', rows.Select(row => row.Label.PadRight(width) + "   " + row.Value));

        var buttons = new List<DialogButtonDefinition> {
            new DialogButtonDefinition("Open folder", OpenFolder, ButtonAction.Reject),
            DialogStandardButtons.Ok,
        };

        var result = await windows.ShowDialog(DialogFactory.CreateStandardDialog(
            found.DisplayName, new StandardDialogParameters { Text = text }, buttons.ToArray(), DialogWindowSize.Medium),
            DialogWindowType.Modal);

        if (result.ButtonId == OpenFolder) await profile.OpenModFolder(found.Name);
    }

    private static string Or(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
