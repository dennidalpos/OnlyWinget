using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetSourceService(
    IWingetCommandRunner commandRunner,
    WingetTableParser tableParser,
    WingetErrorClassifier errorClassifier) : IWingetSourceService
{
    public async Task<WingetOperationOutcome<WingetSource>> ListSourcesAsync(CancellationToken cancellationToken)
    {
        var result = await commandRunner.RunAsync("winget", ["source", "list"], cancellationToken)
            .ConfigureAwait(false);
        return CreateOutcome(result, parseSources: true);
    }

    public async Task<WingetOperationOutcome<WingetSource>> UpdateSourcesAsync(CancellationToken cancellationToken)
    {
        var result = await commandRunner.RunAsync(
                "winget",
                ["source", "update"],
                cancellationToken)
            .ConfigureAwait(false);
        return CreateOutcome(result, parseSources: false);
    }

    public async Task<WingetOperationOutcome<WingetSource>> AddSourceAsync(
        string name,
        string argument,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument);

        var result = await commandRunner.RunAsync(
                "winget",
                ["source", "add", "--name", name.Trim(), "--arg", argument.Trim(), "--accept-source-agreements"],
                cancellationToken)
            .ConfigureAwait(false);
        return CreateOutcome(result, parseSources: false);
    }

    public async Task<WingetOperationOutcome<WingetSource>> RemoveSourceAsync(
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var result = await commandRunner.RunAsync(
                "winget",
                ["source", "remove", "--name", name.Trim()],
                cancellationToken)
            .ConfigureAwait(false);
        return CreateOutcome(result, parseSources: false);
    }

    public async Task<WingetOperationOutcome<WingetSource>> ResetSourcesAsync(CancellationToken cancellationToken)
    {
        var result = await commandRunner.RunAsync(
                "winget",
                ["source", "reset", "--force"],
                cancellationToken)
            .ConfigureAwait(false);
        return CreateOutcome(result, parseSources: false);
    }

    private WingetOperationOutcome<WingetSource> CreateOutcome(WingetCommandResult result, bool parseSources)
    {
        var rawOutput = WingetOutputHelpers.JoinOutput(result);
        if (!result.Succeeded)
        {
            return WingetOperationOutcome<WingetSource>.Failure(
                errorClassifier.Classify(result) ?? new ClassifiedWingetError(WingetErrorKind.SourceUnavailable, "winget source failed."),
                rawOutput);
        }

        var rows = parseSources
            ? tableParser.Parse(result.StandardOutput)
                .Select(ToSource)
                .Where(source => source is not null)
                .Cast<WingetSource>()
                .ToArray()
            : [];

        return WingetOperationOutcome<WingetSource>.Success(rows, rawOutput);
    }

    private static WingetSource? ToSource(IReadOnlyDictionary<string, string> row)
    {
        if (!WingetOutputHelpers.TryGet(row, "Name", out var name))
        {
            return null;
        }

        WingetOutputHelpers.TryGet(row, "Argument", out var argument);
        WingetOutputHelpers.TryGet(row, "Explicit", out var explicitValue);

        return new WingetSource(
            name.Trim(),
            argument.Trim(),
            IsTrue(explicitValue),
            string.IsNullOrWhiteSpace(argument) ? WingetSourceStatus.Unknown : WingetSourceStatus.Available);
    }

    private static bool IsTrue(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("si", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("sì", StringComparison.OrdinalIgnoreCase);
}
