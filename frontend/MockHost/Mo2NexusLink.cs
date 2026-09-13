namespace Mo2.Frontend;

internal sealed record Mo2NexusLink(string Game, int ModId, int FileId)
{
    public string? NxmUri { get; init; }
    public override string ToString() => $"{Game} mod {ModId}, file {FileId}";
    public static Mo2NexusLink Parse(string value)
    {
        value = value.Trim();
        if (value.Length > 8192 || value.Any(char.IsControl) || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || !uri.IsDefaultPort || uri.Fragment.Length > 0) throw new ArgumentException("Paste a Nexus file link");
        var path = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (uri.Scheme == "nxm" && path.Length == 4 && path[0] == "mods" && path[2] == "files" &&
            int.TryParse(path[1], out var nxmMod) && nxmMod > 0 && int.TryParse(path[3], out var nxmFile) && nxmFile > 0)
            return new(uri.Host, nxmMod, nxmFile) { NxmUri = value };
        if (uri.Scheme == "https" && uri.Host is "www.nexusmods.com" or "nexusmods.com" && path.Length == 3 && path[1] == "mods" &&
            int.TryParse(path[2], out var mod) && mod > 0) {
            var fileId = uri.Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2)).FirstOrDefault(x => x.Length == 2 && x[0] == "file_id");
            if (fileId is not null && int.TryParse(fileId[1], out var file) && file > 0) return new(path[0], mod, file);
        }
        throw new ArgumentException("Use a Nexus file link containing file_id, or an nxm:// file link");
    }
}
