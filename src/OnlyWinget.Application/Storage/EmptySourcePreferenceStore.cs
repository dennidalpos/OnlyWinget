namespace OnlyWinget.Application.Storage;

public sealed class EmptySourcePreferenceStore : ISourcePreferenceStore
{
    public Task<SourcePreferences> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SourcePreferences([], DefaultSourcesConfigured: true));

    public Task SaveAsync(SourcePreferences preferences, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
