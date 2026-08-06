using System.Text.Json;
using Microsoft.Extensions.Logging;
using OnlyWinget.Application.Security;
using OnlyWinget.Application.Storage;

namespace OnlyWinget.Infrastructure.Security;

public sealed class DpapiSecretStore(
    string filePath,
    ISecureDataProtectionService protectionService,
    Action<string, Exception>? logger = null,
    ILogger<DpapiSecretStore>? storeLogger = null) : ISecureSecretStore
{
    private readonly SemaphoreSlim saveGate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        StorageConstants.ApplicationFolderName,
        "secure-secrets-v1.json");

    public async Task SaveSecretAsync(string key, string secretValue, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(secretValue);

        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var secretsMap = await LoadSecretsMapInternalAsync(cancellationToken).ConfigureAwait(false);
            var encryptedValue = protectionService.ProtectString(secretValue, optionalEntropy: key);
            secretsMap[key] = encryptedValue;

            await SaveSecretsMapInternalAsync(secretsMap, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            saveGate.Release();
        }
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var secretsMap = await LoadSecretsMapInternalAsync(cancellationToken).ConfigureAwait(false);
            if (!secretsMap.TryGetValue(key, out var encryptedValue))
            {
                return null;
            }

            try
            {
                return protectionService.UnprotectString(encryptedValue, optionalEntropy: key);
            }
            catch (Exception exception) when (exception is global::System.Security.Cryptography.CryptographicException or FormatException)
            {
                logger?.Invoke("DpapiSecretStore.GetSecretAsync", exception);
                storeLogger?.LogError(exception, "Failed to decrypt secret key '{Key}' from '{FilePath}'", key, filePath);
                return null;
            }
        }
        finally
        {
            saveGate.Release();
        }
    }

    public async Task<bool> DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var secretsMap = await LoadSecretsMapInternalAsync(cancellationToken).ConfigureAwait(false);
            if (!secretsMap.Remove(key))
            {
                return false;
            }

            await SaveSecretsMapInternalAsync(secretsMap, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            saveGate.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllSecretsAsync(CancellationToken cancellationToken = default)
    {
        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var secretsMap = await LoadSecretsMapInternalAsync(cancellationToken).ConfigureAwait(false);
            var decryptedSecrets = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (key, encryptedValue) in secretsMap)
            {
                try
                {
                    var decryptedValue = protectionService.UnprotectString(encryptedValue, optionalEntropy: key);
                    decryptedSecrets[key] = decryptedValue;
                }
                catch (Exception exception) when (exception is global::System.Security.Cryptography.CryptographicException or FormatException)
                {
                    logger?.Invoke("DpapiSecretStore.GetAllSecretsAsync", exception);
                    storeLogger?.LogError(exception, "Failed to decrypt secret key '{Key}' during bulk load", key);
                }
            }

            return decryptedSecrets;
        }
        finally
        {
            saveGate.Release();
        }
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "SecretsDocument DTO is defined statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "SecretsDocument DTO is defined statically.")]
    private async Task<Dictionary<string, string>> LoadSecretsMapInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var document = await JsonSerializer.DeserializeAsync<SecretsDocument>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return document is { SchemaVersion: 1, Secrets: not null }
                ? new Dictionary<string, string>(document.Secrets, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException exception)
        {
            logger?.Invoke("DpapiSecretStore.LoadSecretsMapInternalAsync", exception);
            storeLogger?.LogError(exception, "Failed to deserialize secrets store file at '{FilePath}'", filePath);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "SecretsDocument DTO is defined statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "SecretsDocument DTO is defined statically.")]
    private async Task SaveSecretsMapInternalAsync(Dictionary<string, string> secretsMap, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = filePath + ".tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                var document = new SecretsDocument(1, secretsMap);
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed record SecretsDocument(int SchemaVersion, Dictionary<string, string> Secrets);
}
