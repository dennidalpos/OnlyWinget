namespace OnlyWinget.Application.Security;

public interface ISecureDataProtectionService
{
    bool IsSupported { get; }
    byte[] Protect(byte[] userData, byte[]? optionalEntropy = null);
    byte[] Unprotect(byte[] encryptedData, byte[]? optionalEntropy = null);
    string ProtectString(string plainText, string? optionalEntropy = null);
    string UnprotectString(string encryptedText, string? optionalEntropy = null);
}
