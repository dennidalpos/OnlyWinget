using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using OnlyWinget.Application.Security;

namespace OnlyWinget.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiDataProtectionService : ISecureDataProtectionService
{
    [SupportedOSPlatformGuard("windows")]
    public bool IsSupported => OperatingSystem.IsWindows();

    public byte[] Protect(byte[] userData, byte[]? optionalEntropy = null)
    {
        ArgumentNullException.ThrowIfNull(userData);

        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("Windows DPAPI is only supported on Windows operating systems.");
        }

        return ProtectedData.Protect(userData, optionalEntropy, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] encryptedData, byte[]? optionalEntropy = null)
    {
        ArgumentNullException.ThrowIfNull(encryptedData);

        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("Windows DPAPI is only supported on Windows operating systems.");
        }

        return ProtectedData.Unprotect(encryptedData, optionalEntropy, DataProtectionScope.CurrentUser);
    }

    public string ProtectString(string plainText, string? optionalEntropy = null)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var entropyBytes = optionalEntropy is not null ? Encoding.UTF8.GetBytes(optionalEntropy) : null;
        var protectedBytes = Protect(plainBytes, entropyBytes);

        return Convert.ToBase64String(protectedBytes);
    }

    public string UnprotectString(string encryptedText, string? optionalEntropy = null)
    {
        ArgumentNullException.ThrowIfNull(encryptedText);

        var encryptedBytes = Convert.FromBase64String(encryptedText);
        var entropyBytes = optionalEntropy is not null ? Encoding.UTF8.GetBytes(optionalEntropy) : null;
        var plainBytes = Unprotect(encryptedBytes, entropyBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
