using System.Security.Cryptography;
using System.Text;

namespace Mo2.Frontend;

// The Nexus login this application holds on the person's behalf.
//
// Signing in used to hand the credential straight to every MO2 instance and keep
// nothing: MO2 was the only thing that needed it. Wabbajack needs it too, and it
// is a separate program that would otherwise run its own sign-in — which is the
// one thing a person signing in here should never have to do twice.
//
// So the credential is kept, and kept carefully. On Windows it is sealed with the
// user's own data-protection key, so a copy of the file is worthless on another
// account or another machine. Elsewhere the file is readable only by its owner,
// which is what MO2 and Wabbajack both do with theirs. It is written only when a
// sign-in succeeds and deleted the moment the person signs out.
internal static class Mo2NexusCredential
{
    private static string Path => Mo2ConfigPaths.Combine("nexus-credential");

    internal static bool Held => File.Exists(Path);

    internal static void Save(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        try {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var bytes = Encoding.UTF8.GetBytes(key);
            if (OperatingSystem.IsWindows()) {
                File.WriteAllBytes(Path, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
                return;
            }
            var options = new FileStreamOptions {
                Mode = FileMode.Create, Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            };
            using var stream = new FileStream(Path, options);
            stream.Write(bytes);
        } catch (Exception) {
            // A credential that cannot be kept is not a failed sign-in: MO2 has it,
            // and the only thing lost is not having to paste it again for Wabbajack.
        }
    }

    internal static string? Read() => Own() ?? FromModOrganizer();

    private static string? Own()
    {
        try {
            if (!File.Exists(Path)) return null;
            var bytes = File.ReadAllBytes(Path);
            if (OperatingSystem.IsWindows())
                bytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Clean(Encoding.UTF8.GetString(bytes));
        } catch (Exception) { return null; }
    }

    // Where MO2 keeps the same credential: one entry in the Windows credential
    // store, which is what the bridge reads too.
    //
    // Read as a fallback so that signing in before this application kept a copy of
    // its own still counts — the sign-in happened, MO2 has the result, and asking
    // for it again to satisfy bookkeeping would be the exact thing this is meant to
    // avoid. Nothing is written here; MO2 owns that entry.
    private const string Mo2CredentialTarget = "ModOrganizer2_APIKEY";

    private static string? FromModOrganizer()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var handle = IntPtr.Zero;
        try {
            if (!CredRead(Mo2CredentialTarget, 1, 0, out handle)) return null;
            var credential = System.Runtime.InteropServices.Marshal.PtrToStructure<CredentialBlob>(handle);
            if (credential.BlobSize is 0 or > 8192 || credential.BlobSize % 2 != 0) return null;
            var bytes = new byte[credential.BlobSize];
            System.Runtime.InteropServices.Marshal.Copy(credential.Blob, bytes, 0, bytes.Length);
            return Clean(Encoding.Unicode.GetString(bytes));
        } catch (Exception) { return null; }
        finally { if (handle != IntPtr.Zero) CredFree(handle); }
    }

    // A Nexus key is printable ASCII. Anything else is not a key, whichever store
    // it came out of, and is not handed to another program as one.
    private static string? Clean(string value)
    {
        var key = value.Trim();
        if (key.Length is 0 or > 4096) return null;
        return key.Any(c => c < 33 || c > 126) ? null : key;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct CredentialBlob
    {
        public uint Flags, Type;
        public IntPtr TargetName, Comment;
        public long LastWritten;
        public uint BlobSize;
        public IntPtr Blob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes, TargetAlias, UserName;
    }

    [System.Runtime.InteropServices.DllImport("Advapi32.dll", EntryPoint = "CredReadW",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [System.Runtime.InteropServices.DllImport("Advapi32.dll")]
    private static extern void CredFree(IntPtr credential);

    internal static void Clear()
    {
        try { if (File.Exists(Path)) File.Delete(Path); } catch (IOException) { }
    }
}
