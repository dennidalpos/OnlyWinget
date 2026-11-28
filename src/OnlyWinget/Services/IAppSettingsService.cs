namespace OnlyWinget.Services;

internal interface IAppSettingsService
{
    event EventHandler? Changed;

    AppSettings Current { get; }

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);
}
