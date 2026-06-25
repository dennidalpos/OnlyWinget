using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class CommandAvailability(IWingetCommandRunner commandRunner) : ICommandAvailability
{
    public async Task<bool> IsWingetAvailableAsync(CancellationToken cancellationToken)
    {
        var result = await commandRunner.RunAsync("winget", ["--version"], cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }
}
