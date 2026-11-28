namespace OnlyWinget.Application.Storage;

public interface ISourcePreferenceStore
{
    Task<SourcePreferences> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(SourcePreferences preferences, CancellationToken cancellationToken);
}
