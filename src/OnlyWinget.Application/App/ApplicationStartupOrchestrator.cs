namespace OnlyWinget.Application.App;

public sealed class ApplicationStartupOrchestrator(OnlyWingetApplication application)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        var loaded = await application.LoadWorkspaceAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!loaded.Succeeded)
        {
            return;
        }

        await application.RefreshCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (!application.State.Capabilities.CanUseWinget)
        {
            return;
        }

        await application.RefreshSourcesAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        await application.UpdateSourcesAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        await application.RefreshWorkspacePackageMetadataAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
