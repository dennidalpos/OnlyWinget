using Microsoft.Extensions.Logging;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetUpdateLoader(
    IWingetCommandRunner commandRunner,
    WingetTableParser tableParser,
    WingetErrorClassifier errorClassifier,
    UpdateScanOptions? scanOptions = null,
    Microsoft.Extensions.Logging.ILogger<WingetUpdateLoader>? logger = null) : IUpdateLoader
{
    private const int NoInstalledPackagesFound = unchecked((int)0x8A150014);
    private static readonly string[] RequiredColumns = ["Id", "Version", "Available"];

    public async Task<WingetOperationOutcome<PackageUpdate>> LoadUpdatesAsync(string source, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var arguments = new List<string>
        {
            "upgrade", "--source", source, "--accept-source-agreements", "--disable-interactivity"
        };
        if (scanOptions?.IncludeUnknownVersions == true)
        {
            arguments.Add("--include-unknown");
        }

        if (scanOptions?.IncludePinned == true)
        {
            arguments.Add("--include-pinned");
        }

        var result = await commandRunner.RunAsync("winget", arguments, cancellationToken).ConfigureAwait(false);

        var rawOutput = WingetOutputHelpers.JoinOutput(result);
        LogSkippedPackages(source, rawOutput);
        if (!result.Succeeded)
        {
            if (result.ExitCode == NoInstalledPackagesFound)
            {
                return WingetOperationOutcome<PackageUpdate>.Success([], rawOutput);
            }

            return WingetOperationOutcome<PackageUpdate>.Failure(
                errorClassifier.Classify(result) ?? new ClassifiedWingetError(WingetErrorKind.Unknown, "winget upgrade failed."),
                rawOutput);
        }

        if (HasNoUpdates(rawOutput))
        {
            return WingetOperationOutcome<PackageUpdate>.Success([], rawOutput);
        }

        var table = tableParser.ParseTable(result.StandardOutput);
        var missingColumns = RequiredColumns
            .Where(column => !table.Columns.Contains(column, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (missingColumns.Length > 0)
        {
            return InvalidOutput($"WinGet upgrade output is missing a table with the required columns: {string.Join(", ", missingColumns)}.", rawOutput);
        }

        var rows = table.Rows
            .Select(row => ToUpdate(row, source))
            .OfType<PackageUpdate>()
            .ToArray();
        if (rows.Length == 0 && table.Rows.Count > 0)
        {
            return InvalidOutput("The WinGet upgrade table did not contain readable package rows.", rawOutput);
        }

        if (table.Rows.Any(IsIncompletePackageRow))
        {
            return InvalidOutput("The WinGet upgrade table contains a package row with no available version.", rawOutput);
        }

        return WingetOperationOutcome<PackageUpdate>.Success(rows, rawOutput);
    }

    private void LogSkippedPackages(string source, string rawOutput)
    {
        foreach (var line in rawOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (char.IsAsciiDigit(line[0]) &&
                (line.Contains("--include-unknown", StringComparison.Ordinal) ||
                 line.Contains("--include-pinned", StringComparison.Ordinal)))
            {
                logger?.LogInformation("winget upgrade ({Source}) skipped packages: {Summary}", source, line);
            }
        }
    }

    private static WingetOperationOutcome<PackageUpdate> InvalidOutput(string message, string rawOutput) =>
        WingetOperationOutcome<PackageUpdate>.Failure(new ClassifiedWingetError(WingetErrorKind.InvalidOutput, message), rawOutput);

    private static bool HasNoUpdates(string output) => output
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .All(IsNoUpdateLine);

    private static bool IsNoUpdateLine(string line)
    {
        var isEmptyResult = line.TrimEnd('.').ToUpperInvariant() is
            "-" or "\\" or "|" or "/" or
            "NO PACKAGES NEED AN UPDATE" or "NO AVAILABLE UPGRADE FOUND" or "NO APPLICABLE UPDATE FOUND" or
            "NO INSTALLED PACKAGE FOUND MATCHING INPUT CRITERIA" or
            "NON È STATO TROVATO ALCUN PACCHETTO INSTALLATO CORRISPONDENTE AI CRITERI DI INPUT" or
            "NON SONO STATI TROVATI AGGIORNAMENTI DISPONIBILI" or "NON È STATO TROVATO ALCUN AGGIORNAMENTO APPLICABILE";
        var isSkippedPackageSummary = char.IsAsciiDigit(line[0]) &&
            (line.Contains("--include-unknown", StringComparison.Ordinal) ||
             line.Contains("--include-pinned", StringComparison.Ordinal));
        return isEmptyResult || isSkippedPackageSummary;
    }

    private static bool IsIncompletePackageRow(IReadOnlyDictionary<string, string> row) =>
        WingetTableParser.IsValidRow(row) &&
        WingetOutputHelpers.TryGet(row, "Id", out var id) && !id.Any(char.IsWhiteSpace) &&
        WingetOutputHelpers.TryGet(row, "Version", out var version) && !version.Any(char.IsWhiteSpace) &&
        !WingetOutputHelpers.TryGet(row, "Available", out _);

    private static PackageUpdate? ToUpdate(IReadOnlyDictionary<string, string> row, string requestedSource)
    {
        if (!WingetTableParser.IsValidRow(row) ||
            !WingetOutputHelpers.TryGet(row, "Id", out var id) ||
            !WingetOutputHelpers.TryGet(row, "Version", out var version) ||
            !WingetOutputHelpers.TryGet(row, "Available", out var available) ||
            id.Any(char.IsWhiteSpace))
        {
            return null;
        }

        WingetOutputHelpers.TryGet(row, "Name", out var name);
        WingetOutputHelpers.TryGet(row, "Source", out var source);

        return new PackageUpdate(
            new PackageIdentity(id, string.IsNullOrWhiteSpace(source) ? requestedSource : source),
            string.IsNullOrWhiteSpace(name) ? id : name,
            version,
            available);
    }
}
