using System.Runtime.Versioning;
using System.Security.Cryptography;
using OnlyWinget.Infrastructure.Security;

namespace OnlyWinget.Tests;

[SupportedOSPlatform("windows")]
public sealed class DpapiSecurityTests
{
    [Fact]
    public void DpapiDataProtectionService_IsSupportedOnWindows()
    {
        var service = new DpapiDataProtectionService();
        Assert.Equal(OperatingSystem.IsWindows(), service.IsSupported);
    }

    [Fact]
    public void DpapiDataProtectionService_ProtectsAndUnprotectsBytesSuccessfully()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = new DpapiDataProtectionService();
        var rawBytes = "SecretPayload123!"u8.ToArray();

        var protectedBytes = service.Protect(rawBytes);
        Assert.NotNull(protectedBytes);
        Assert.NotEqual(rawBytes, protectedBytes);

        var unprotectedBytes = service.Unprotect(protectedBytes);
        Assert.Equal(rawBytes, unprotectedBytes);
    }

    [Fact]
    public void DpapiDataProtectionService_ProtectsAndUnprotectsStringSuccessfully()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = new DpapiDataProtectionService();
        const string originalText = "ghp_PersonalAccessTokenSecretKey999";

        var encryptedText = service.ProtectString(originalText);
        Assert.False(string.IsNullOrWhiteSpace(encryptedText));
        Assert.NotEqual(originalText, encryptedText);

        var decryptedText = service.UnprotectString(encryptedText);
        Assert.Equal(originalText, decryptedText);
    }

    [Fact]
    public void DpapiDataProtectionService_ProtectsAndUnprotectsWithEntropy()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = new DpapiDataProtectionService();
        const string originalText = "SuperSensitiveToken";
        const string entropy = "user-specific-entropy-key";

        var encryptedText = service.ProtectString(originalText, entropy);
        Assert.NotEqual(originalText, encryptedText);

        var decryptedText = service.UnprotectString(encryptedText, entropy);
        Assert.Equal(originalText, decryptedText);

        // Fail to decrypt if wrong entropy is passed
        Assert.Throws<CryptographicException>(() => service.UnprotectString(encryptedText, "wrong-entropy"));
    }

    [Fact]
    public void DpapiDataProtectionService_ThrowsOnTamperedEncryptedString()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = new DpapiDataProtectionService();
        const string originalText = "DataToEncrypt";
        var encryptedText = service.ProtectString(originalText);

        var tamperedText = encryptedText[..^4] + "AAAA";

        Assert.ThrowsAny<Exception>(() => service.UnprotectString(tamperedText));
    }

    [Fact]
    public async Task DpapiSecretStore_SavesGetsDeletesSecretsSuccessfully()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"onlywinget-secrets-{Guid.NewGuid():N}.json");
        try
        {
            var protectionService = new DpapiDataProtectionService();
            var store = new DpapiSecretStore(tempFilePath, protectionService);

            const string key = "github_token";
            const string secretValue = "ghp_abcdef1234567890";

            await store.SaveSecretAsync(key, secretValue);
            var loadedValue = await store.GetSecretAsync(key);
            Assert.Equal(secretValue, loadedValue);

            var fileContent = await File.ReadAllTextAsync(tempFilePath);
            Assert.DoesNotContain(secretValue, fileContent); // Ensure secret is encrypted at rest

            var deleted = await store.DeleteSecretAsync(key);
            Assert.True(deleted);

            var deletedValue = await store.GetSecretAsync(key);
            Assert.Null(deletedValue);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task DpapiSecretStore_GetAllSecretsAsync_UnprotectsAllStoredSecrets()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"onlywinget-secrets-{Guid.NewGuid():N}.json");
        try
        {
            var protectionService = new DpapiDataProtectionService();
            var store = new DpapiSecretStore(tempFilePath, protectionService);

            await store.SaveSecretAsync("key1", "val1");
            await store.SaveSecretAsync("key2", "val2");

            var all = await store.GetAllSecretsAsync();
            Assert.Equal(2, all.Count);
            Assert.Equal("val1", all["key1"]);
            Assert.Equal("val2", all["key2"]);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task DpapiSecretStore_HandlesCorruptedStoreGracefully()
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"onlywinget-secrets-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFilePath, "{ invalid_json: ");

            var protectionService = new DpapiDataProtectionService();
            var store = new DpapiSecretStore(tempFilePath, protectionService);

            var secret = await store.GetSecretAsync("any_key");
            Assert.Null(secret);

            var all = await store.GetAllSecretsAsync();
            Assert.Empty(all);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}
