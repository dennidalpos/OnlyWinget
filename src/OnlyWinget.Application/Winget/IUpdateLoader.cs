namespace OnlyWinget.Application.Winget;

public interface IUpdateLoader
{
    Task<IReadOnlyList<PackageUpdate>> LoadUpdatesAsync(CancellationToken cancellationToken);
}
