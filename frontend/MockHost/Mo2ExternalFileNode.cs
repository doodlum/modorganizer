using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class Mo2ExternalFileNode(string path, Mo2ExternalFile? file = null) : ReactiveObject
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path);
    public Mo2ExternalFile? File { get; } = file;
    public bool IsFolder => File is null;
    public string Kind => File?.Kind ?? "Folder";
    public List<Mo2ExternalFileNode> Children { get; } = [];

    // NMA's External Changes view reports a size and a file count for folders as
    // well as files, so folders aggregate their descendants. Links have no size.
    public bool IsLink => File?.Kind == "External link";
    public long TotalBytes => IsFolder
        ? Children.Sum(child => child.TotalBytes)
        : IsLink ? 0 : File!.Bytes;
    public int FileCount => IsFolder ? Children.Sum(child => child.FileCount) : 1;

    public string SizeText => IsLink ? "—" : Mo2FolderPage.SizeText(TotalBytes);
    public string FileCountText => IsFolder ? FileCount.ToString() : "";


    private bool _expanded;
    public bool IsExpanded { get => _expanded; set => this.RaiseAndSetIfChanged(ref _expanded, value); }

    public static Mo2ExternalFileNode[] Build(IEnumerable<Mo2ExternalFile> files, string query,
        IReadOnlyDictionary<string, bool>? expansion = null)
    {
        var root = new Mo2ExternalFileNode("");
        var folders = new Dictionary<string, Mo2ExternalFileNode>(StringComparer.Ordinal) { [""] = root };
        foreach (var file in files.Where(file => file.Path.Contains(query, StringComparison.OrdinalIgnoreCase))) {
            var parts = Mo2ExternalFiles.PhysicalParts(file.Path);
            var parent = root;
            for (var index = 0; index < parts.Length - 1; index++) {
                var path = string.Join('/', parts.Take(index + 1));
                if (!folders.TryGetValue(path, out var folder)) {
                    folder = new(path) { IsExpanded = query.Length > 0 ||
                        (expansion?.TryGetValue(path, out var expanded) == true ? expanded : index == 0) };
                    folders.Add(path, folder); parent.Children.Add(folder);
                }
                parent = folder;
            }
            parent.Children.Add(new(file.Path, file));
        }
        foreach (var folder in folders.Values) folder.Children.Sort((left, right) => {
            var kind = right.IsFolder.CompareTo(left.IsFolder);
            return kind != 0 ? kind : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        });
        return root.Children.ToArray();
    }

    public IEnumerable<Mo2ExternalFileNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
        foreach (var node in child.DescendantsAndSelf()) yield return node;
    }
}
