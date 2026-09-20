using System.Text.Json;
using NexusMods.App.UI.Settings;

namespace Mo2.Frontend;

// Persist NMA's own alert preferences, without persisting fixture/game settings.
internal sealed class Mo2AlertPreferences(string path)
{
    public static string DefaultPath => Mo2ConfigPaths.Combine("alerts.json");

    public AlertSettings Read()
    {
        try {
            if (File.Exists(path) && JsonSerializer.Deserialize<AlertSettings>(File.ReadAllText(path)) is { AlertStatus: not null } settings)
                return settings;
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) {
            Console.Error.WriteLine("Could not read alert preferences; using defaults.");
        }
        return new AlertSettings();
    }

    public void Save(AlertSettings settings)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
            File.Move(temporary, path, overwrite: true);
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Could not save alert preferences; this session's choice is retained.");
        } finally {
            try { File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }
}
