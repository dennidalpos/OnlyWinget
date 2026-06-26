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
                ["source", "update", "--accept-source-agreements"],
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
        var rawOutput = JoinOutput(result);
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
        if (!TryGetAny(row, out var name, "Name", "Nome") || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        TryGetAny(row, out var argument, "Argument", "Argomento");
        TryGetAny(row, out var explicitValue, "Explicit", "Contenuti espliciti");

        return new WingetSource(
            name.Trim(),
            argument.Trim(),
            IsTrue(explicitValue),
            string.IsNullOrWhiteSpace(argument) ? WingetSourceStatus.Unknown : WingetSourceStatus.Available);
    }

    private static bool TryGetAny(IReadOnlyDictionary<string, string> row, out string value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool IsTrue(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("si", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("sì", StringComparison.OrdinalIgnoreCase);

    private static string JoinOutput(WingetCommandResult result) =>
        string.Join(Environment.NewLine, result.StandardOutput, result.StandardError).Trim();
}
