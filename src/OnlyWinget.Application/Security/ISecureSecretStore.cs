namespace OnlyWinget.Application.Security;

public interface ISecureSecretStore
{
    Task SaveSecretAsync(string key, string secretValue, CancellationToken cancellationToken = default);
    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> DeleteSecretAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetAllSecretsAsync(CancellationToken cancellationToken = default);
}
