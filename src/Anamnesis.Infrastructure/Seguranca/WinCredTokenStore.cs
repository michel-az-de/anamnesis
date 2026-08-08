using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Anamnesis.Application.Contracts;
using Microsoft.Win32.SafeHandles;

namespace Anamnesis.Infrastructure.Seguranca;

public class WinCredTokenStore : IAgendaTokenStore
{
    public Task SalvarAsync(string chave, string jsonToken, CancellationToken ct = default)
    {
        var cred = new CREDENTIAL
        {
            Type = CRED_TYPE.GENERIC,
            TargetName = Marshal.StringToCoTaskMemUni(chave),
            CredentialBlob = Marshal.StringToCoTaskMemUni(jsonToken),
            CredentialBlobSize = (uint)(Encoding.Unicode.GetByteCount(jsonToken)),
            Persist = (uint)CRED_PERSIST.LOCAL_MACHINE,
            UserName = Marshal.StringToCoTaskMemUni(Environment.UserName),
        };

        try
        {
            if (!CredWrite(ref cred, 0))
            {
                throw new InvalidOperationException($"Falha ao salvar credencial WinCred: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(cred.TargetName);
            Marshal.FreeCoTaskMem(cred.CredentialBlob);
            Marshal.FreeCoTaskMem(cred.UserName);
        }

        return Task.CompletedTask;
    }

    public Task<string?> RecuperarAsync(string chave, CancellationToken ct = default)
    {
        if (!CredRead(chave, CRED_TYPE.GENERIC, 0, out var credPtr))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            var jsonToken = Marshal.PtrToStringUni(cred.CredentialBlob, (int)(cred.CredentialBlobSize / 2));
            return Task.FromResult<string?>(jsonToken);
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    public Task RemoverAsync(string chave, CancellationToken ct = default)
    {
        CredDelete(chave, CRED_TYPE.GENERIC, 0);
        return Task.CompletedTask;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, CRED_TYPE type, int reservedFlag, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, CRED_TYPE type, int reservedFlag);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public CRED_TYPE Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint DateTimeLow;
        public uint DateTimeHigh;
    }

    private enum CRED_TYPE : uint
    {
        GENERIC = 1,
    }

    private enum CRED_PERSIST : uint
    {
        SESSION = 1,
        LOCAL_MACHINE = 2,
        ENTERPRISE = 3,
    }
}
