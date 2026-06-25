namespace OnlyWinget.Application.Storage;

public interface IWorkspaceStore
{
    Task<WorkspaceState> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(WorkspaceState state, CancellationToken cancellationToken);
}
