using System.Security.Cryptography;
using System.Text;
using AegisVault.Core.Services;

namespace AegisVault.Platform;

/// <summary>
/// Windows DPAPI key protector (CurrentUser scope). The protected blob is
/// bound to the current Windows account and cannot be moved to another user.
/// </summary>
public sealed class DpapiKeyProtector : IKeyProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AegisVault|device-key|v1");

    public string Id => "dpapi";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public byte[] Protect(ReadOnlySpan<byte> data)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI is only available on Windows.");
        }

        var plain = data.ToArray();
        try
        {
            return ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public byte[] Unprotect(ReadOnlySpan<byte> data)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI is only available on Windows.");
        }

        return ProtectedData.Unprotect(data.ToArray(), Entropy, DataProtectionScope.CurrentUser);
    }
}
