namespace OnlyWinget.Application.App;

public sealed class ApplicationStartupOrchestrator(OnlyWingetApplication application)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        await application.LoadWorkspaceAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        await application.RefreshCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (!application.State.Capabilities.CanUseWinget)
        {
            return;
        }

        var update = await application.UpdateSourcesAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!update.Succeeded)
        {
            await application.RefreshSourcesAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        await application.RefreshWorkspacePackageMetadataAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
