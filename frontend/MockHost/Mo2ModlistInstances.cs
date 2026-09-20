using System.Text.Json;

namespace Mo2.Frontend;

// What a modlist instance remembers about the list it was installed from.
internal sealed record Mo2ModlistInstall(
    string Title, string Repository, string MachineUrl, string Game,
    string Version, string? ImageUrl, DateTime Installed)
{
    internal string NamespacedName => $"{Repository}/{MachineUrl}";
}

// A modlist install is an MO2 instance like any other — Wabbajack lays down
// ModOrganizer.ini and portable.txt itself, which is exactly the shape this
// application already creates for a base game. What it is not is interchangeable
// with one: a person can hold Viva New Vegas and their own New Vegas setup at the
// same time, and those are two separate things to be in, not one game with two
// sets of mods.
//
// So the instance carries a note of the list it came from. Everything that wants
// to tell the two apart reads that note rather than guessing from the directory
// name, and an instance without one is a plain base-game instance as before.
internal static class Mo2ModlistInstances
{
    private const string MarkerName = "nexus-modlist.json";

    // Beside the base-game instances rather than somewhere of its own: they are the
    // same kind of thing, and the catalogue already looks after that directory.
    internal static string DirectoryFor(Mo2Modlist list) =>
        Path.Combine(Mo2OwnedInstances.InstancesRoot, list.InstanceKey);

    internal static string MarkerPath(string instance) => Path.Combine(instance, MarkerName);

    // The list's own picture, kept beside its note so the sidebar can draw the
    // modlist rather than the game it is built on, and so it still can when the
    // gallery is out of reach.
    internal static string ArtworkPath(string instance) => Path.Combine(instance, "nexus-modlist-art.image");

    internal static Mo2ModlistInstall? Describe(string instance)
    {
        try {
            var path = MarkerPath(instance);
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<Mo2ModlistInstall>(File.ReadAllText(path));
        } catch (Exception) { return null; }
    }

    internal static void Mark(string instance, Mo2Modlist list)
    {
        var note = new Mo2ModlistInstall(list.Title, list.Repository, list.MachineUrl,
            list.Game ?? list.WabbajackGame, list.Version, list.ImageUrl, DateTime.UtcNow);
        Directory.CreateDirectory(instance);
        File.WriteAllText(MarkerPath(instance), JsonSerializer.Serialize(note, new JsonSerializerOptions { WriteIndented = true }));
    }

    // Whether this list already has an instance here, so the gallery can say so
    // rather than offering to install it again over the top.
    internal static bool Installed(Mo2Modlist list) =>
        File.Exists(Path.Combine(DirectoryFor(list), "ModOrganizer.ini"));

    // What the sidebar calls this instance, and what it draws for it. A base-game
    // instance is its game, as it has always been; a modlist is its own name, so two
    // instances of one game do not collapse into a single button.
    internal static string LabelFor(string instance, string game) => Describe(instance)?.Title ?? game;
}
