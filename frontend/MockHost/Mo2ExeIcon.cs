using SkiaSharp;

namespace Mo2.Frontend;

// Game icons come out of the game's own executable. Steam's library art is
// portrait cover artwork, which cannot be squared off into the 48px tile NMA's
// game widgets use without letterboxing it onto a plate.
//
// The PE resource tree is walked here rather than through Windows' icon APIs so
// that this behaves the same under Linux, where MO2's games are still Windows
// executables on disk and nothing can call Shell32.
internal static class Mo2ExeIcon
{
    private const int RtIcon = 3;
    private const int RtGroupIcon = 14;

    internal static SKBitmap? Extract(string executable)
    {
        try {
            using var stream = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);
            if (!TryFindResources(reader, out var sections, out var resourceRva)) return null;
            var resourceOffset = ToOffset(sections, resourceRva);
            if (resourceOffset is null) return null;

            var groups = Entries(reader, resourceOffset.Value, resourceOffset.Value, RtGroupIcon);
            var icons = new Dictionary<int, long>();
            foreach (var icon in Entries(reader, resourceOffset.Value, resourceOffset.Value, RtIcon))
                icons[icon.Id] = icon.DataOffset;
            if (groups.Count == 0 || icons.Count == 0) return null;

            // The application icon is the group with the lowest id; Explorer shows
            // the same one.
            var group = groups.OrderBy(entry => entry.Id).First();
            var data = ReadData(reader, sections, group.DataOffset);
            if (data is null || data.Length < 6) return null;

            var count = BitConverter.ToUInt16(data, 4);
            (int Size, int Bits, int Id) best = (0, 0, 0);
            for (var index = 0; index < count; index++) {
                var entry = 6 + index * 14;
                if (entry + 14 > data.Length) break;
                int width = data[entry], height = data[entry + 1];
                if (width == 0) width = 256;
                if (height == 0) height = 256;
                if (width != height) continue;
                var bits = BitConverter.ToUInt16(data, entry + 6);
                var id = BitConverter.ToUInt16(data, entry + 12);
                // Largest square, breaking ties on colour depth.
                if (width > best.Size || (width == best.Size && bits > best.Bits)) best = (width, bits, id);
            }
            if (best.Size == 0 || !icons.TryGetValue(best.Id, out var chosen)) return null;

            var image = ReadData(reader, sections, chosen);
            return image is null ? null : Decode(image);
        } catch { return null; }
    }

    // A standalone .ico file, as Steam serves for its client icons. Same payloads
    // as the resources above, wrapped in a directory of their own.
    internal static SKBitmap? DecodeIcon(byte[] file)
    {
        try {
            if (file.Length < 6 || BitConverter.ToUInt16(file, 0) != 0 || BitConverter.ToUInt16(file, 2) != 1) return null;
            var count = BitConverter.ToUInt16(file, 4);
            (int Size, int Bits, int Offset, int Length) best = (0, 0, 0, 0);
            for (var index = 0; index < count; index++) {
                var entry = 6 + index * 16;
                if (entry + 16 > file.Length) break;
                int width = file[entry], height = file[entry + 1];
                if (width == 0) width = 256;
                if (height == 0) height = 256;
                if (width != height) continue;
                var bits = BitConverter.ToUInt16(file, entry + 6);
                var length = BitConverter.ToInt32(file, entry + 8);
                var offset = BitConverter.ToInt32(file, entry + 12);
                if (offset < 0 || length <= 0 || offset + length > file.Length) continue;
                if (width > best.Size || (width == best.Size && bits > best.Bits)) best = (width, bits, offset, length);
            }
            if (best.Size == 0) return null;
            return Decode(file.AsSpan(best.Offset, best.Length).ToArray());
        } catch { return null; }
    }

    private sealed record Resource(int Id, long DataOffset);

    private static bool TryFindResources(BinaryReader reader, out List<(uint Rva, uint Size, uint Pointer)> sections, out uint resourceRva)
    {
        sections = []; resourceRva = 0;
        var stream = reader.BaseStream;
        stream.Position = 0;
        if (reader.ReadUInt16() != 0x5A4D) return false;           // MZ
        stream.Position = 0x3C;
        stream.Position = reader.ReadUInt32();
        if (reader.ReadUInt32() != 0x00004550) return false;       // PE\0\0
        stream.Position += 2;                                       // Machine
        var sectionCount = reader.ReadUInt16();
        stream.Position += 12;                                      // timestamp, symbol table
        var optionalSize = reader.ReadUInt16();
        stream.Position += 2;                                       // characteristics
        var optionalStart = stream.Position;
        var magic = reader.ReadUInt16();                             // PE32 (0x10B) or PE32+ (0x20B)
        // Data directories follow the fixed part of the optional header.
        stream.Position = optionalStart + (magic == 0x20B ? 112 : 96);
        stream.Position += 2 * 8;                                   // export and import directories
        resourceRva = reader.ReadUInt32();
        if (resourceRva == 0) return false;

        stream.Position = optionalStart + optionalSize;
        for (var index = 0; index < sectionCount; index++) {
            stream.Position += 8;                                   // name
            stream.Position += 4;                                   // virtual size
            var rva = reader.ReadUInt32();
            var rawSize = reader.ReadUInt32();
            var pointer = reader.ReadUInt32();
            stream.Position += 16;                                  // relocations onward
            sections.Add((rva, rawSize, pointer));
        }
        return true;
    }

    private static long? ToOffset(List<(uint Rva, uint Size, uint Pointer)> sections, uint rva)
    {
        foreach (var section in sections)
            if (rva >= section.Rva && rva < section.Rva + Math.Max(section.Size, 1))
                return section.Pointer + (rva - section.Rva);
        return null;
    }

    // Resources are a three-level tree: type, then name or id, then language.
    private static List<Resource> Entries(BinaryReader reader, long root, long directory, int type)
    {
        var found = new List<Resource>();
        foreach (var (id, offset, isDirectory) in ReadDirectory(reader, directory)) {
            if (id != type || !isDirectory) continue;
            foreach (var (resourceId, nameOffset, nameIsDirectory) in ReadDirectory(reader, root + offset)) {
                if (!nameIsDirectory) { found.Add(new Resource(resourceId, root + nameOffset)); continue; }
                foreach (var (_, languageOffset, languageIsDirectory) in ReadDirectory(reader, root + nameOffset)) {
                    if (languageIsDirectory) continue;
                    found.Add(new Resource(resourceId, root + languageOffset));
                    break;
                }
            }
        }
        return found;
    }

    private static List<(int Id, uint Offset, bool IsDirectory)> ReadDirectory(BinaryReader reader, long position)
    {
        var entries = new List<(int, uint, bool)>();
        reader.BaseStream.Position = position + 12;
        var named = reader.ReadUInt16();
        var ids = reader.ReadUInt16();
        for (var index = 0; index < named + ids; index++) {
            var name = reader.ReadUInt32();
            var offset = reader.ReadUInt32();
            // Named entries set the high bit; icon resources are always id-based.
            if ((name & 0x80000000) == 0)
                entries.Add(((int)name, offset & 0x7FFFFFFF, (offset & 0x80000000) != 0));
        }
        return entries;
    }

    private static byte[]? ReadData(BinaryReader reader, List<(uint Rva, uint Size, uint Pointer)> sections, long entry)
    {
        reader.BaseStream.Position = entry;
        var rva = reader.ReadUInt32();
        var size = reader.ReadUInt32();
        if (size == 0 || size > 8 * 1024 * 1024) return null;
        var offset = ToOffset(sections, rva);
        if (offset is null) return null;
        reader.BaseStream.Position = offset.Value;
        return reader.ReadBytes((int)size);
    }

    private static SKBitmap? Decode(byte[] image)
    {
        // 256px icons are normally stored as PNG; the smaller ones as a DIB.
        if (image.Length > 8 && image[0] == 0x89 && image[1] == (byte)'P' && image[2] == (byte)'N' && image[3] == (byte)'G')
            return SKBitmap.Decode(image);
        if (image.Length < 40) return null;

        var headerSize = BitConverter.ToInt32(image, 0);
        var width = BitConverter.ToInt32(image, 4);
        var height = BitConverter.ToInt32(image, 8) / 2;           // XOR and AND masks are stacked
        var bits = BitConverter.ToUInt16(image, 14);
        if (width <= 0 || height <= 0 || width > 1024 || height > 1024) return null;
        if (headerSize < 40 || headerSize > image.Length) return null;

        var declared = BitConverter.ToInt32(image, 32);
        var paletteEntries = bits <= 8 ? (declared > 0 ? declared : 1 << bits) : 0;
        var paletteStart = headerSize;
        var pixels = paletteStart + paletteEntries * 4;
        var rowSize = (width * bits + 31) / 32 * 4;
        var maskRow = (width + 31) / 32 * 4;
        var maskStart = pixels + rowSize * height;

        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        for (var y = 0; y < height; y++) {
            var row = pixels + (height - 1 - y) * rowSize;         // DIB rows run bottom-up
            var mask = maskStart + (height - 1 - y) * maskRow;
            for (var x = 0; x < width; x++) {
                byte b, g, r, a = 255;
                if (bits == 32) {
                    var at = row + x * 4;
                    if (at + 3 >= image.Length) return null;
                    b = image[at]; g = image[at + 1]; r = image[at + 2]; a = image[at + 3];
                } else if (bits == 24) {
                    var at = row + x * 3;
                    if (at + 2 >= image.Length) return null;
                    b = image[at]; g = image[at + 1]; r = image[at + 2];
                } else {
                    var index = bits switch {
                        8 => row + x < image.Length ? image[row + x] : -1,
                        4 => row + x / 2 < image.Length ? (x % 2 == 0 ? image[row + x / 2] >> 4 : image[row + x / 2] & 0x0F) : -1,
                        1 => row + x / 8 < image.Length ? (image[row + x / 8] >> (7 - x % 8)) & 1 : -1,
                        _ => -1,
                    };
                    if (index < 0) return null;
                    var at = paletteStart + index * 4;
                    if (at + 2 >= image.Length) return null;
                    b = image[at]; g = image[at + 1]; r = image[at + 2];
                }
                // Formats without their own alpha are cut out by the AND mask.
                if (bits != 32 && mask + x / 8 < image.Length && ((image[mask + x / 8] >> (7 - x % 8)) & 1) == 1) a = 0;
                bitmap.SetPixel(x, y, new SKColor(r, g, b, a));
            }
        }
        // A 32bpp icon whose alpha channel is entirely zero predates per-pixel
        // alpha and is still masked the old way.
        if (bits == 32 && AllTransparent(bitmap)) ApplyMask(bitmap, image, maskStart, maskRow);
        return bitmap;
    }

    private static bool AllTransparent(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Alpha != 0) return false;
        return true;
    }

    private static void ApplyMask(SKBitmap bitmap, byte[] image, int maskStart, int maskRow)
    {
        for (var y = 0; y < bitmap.Height; y++) {
            var mask = maskStart + (bitmap.Height - 1 - y) * maskRow;
            for (var x = 0; x < bitmap.Width; x++) {
                var clear = mask + x / 8 < image.Length && ((image[mask + x / 8] >> (7 - x % 8)) & 1) == 1;
                var pixel = bitmap.GetPixel(x, y);
                bitmap.SetPixel(x, y, new SKColor(pixel.Red, pixel.Green, pixel.Blue, clear ? (byte)0 : (byte)255));
            }
        }
    }
}
