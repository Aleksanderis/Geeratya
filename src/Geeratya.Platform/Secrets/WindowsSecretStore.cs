using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Geeratya.Platform.Secrets;

/// <summary>Windows Credential Manager through advapi32 (generic credentials, persisted for the user).</summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsSecretStore : ISecretStore
{
    private const string TargetPrefix = "geeratya:";
    private const uint GenericCredential = 1;
    private const uint PersistLocalMachine = 2;

    public bool IsAvailable => true;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!CredRead(TargetPrefix + key, GenericCredential, 0, out var buffer))
        {
            // Only "no such credential" means "no token saved"; any other error is a broken or locked vault.
            CredentialManagerErrors.ThrowUnlessNotFound(Marshal.GetLastPInvokeError(), "read");

            return Task.FromResult<string?>(null);
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(buffer);
            var bytes = new byte[credential.CredentialBlobSize];

            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);

            return Task.FromResult<string?>(Encoding.Unicode.GetString(bytes));
        }
        finally
        {
            CredFree(buffer);
        }
    }

    public Task<bool> SetAsync(string key, string secret, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = Encoding.Unicode.GetBytes(secret);
        var blob = Marshal.AllocHGlobal(bytes.Length);
        var target = Marshal.StringToHGlobalUni(TargetPrefix + key);

        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);

            var credential = new NativeCredential
            {
                Type = GenericCredential,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = PersistLocalMachine,
            };

            if (!CredWrite(ref credential, 0))
            {
                CredentialManagerErrors.Throw(Marshal.GetLastPInvokeError(), "store");
            }

            return Task.FromResult(true);
        }
        finally
        {
            Array.Clear(bytes);
            Marshal.FreeHGlobal(blob);
            Marshal.FreeHGlobal(target);
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!CredDelete(TargetPrefix + key, GenericCredential, 0))
        {
            CredentialManagerErrors.ThrowUnlessNotFound(Marshal.GetLastPInvokeError(), "remove");
        }

        return Task.CompletedTask;
    }

    [LibraryImport(
        "advapi32.dll",
        EntryPoint = "CredReadW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [LibraryImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredWrite(ref NativeCredential credential, uint flags);

    [LibraryImport(
        "advapi32.dll",
        EntryPoint = "CredDeleteW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredDelete(string target, uint type, uint flags);

    [LibraryImport("advapi32.dll")]
    private static partial void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public uint LastWrittenLow;
        public uint LastWrittenHigh;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
