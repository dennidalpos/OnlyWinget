// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

internal static class WingetTableParser
{
    public static IReadOnlyList<SearchResult> ParseSearchResults(string output)
    {
        var rows = ParseWingetTable(
            output,
            new TableColumn("Name", "Nome", "Name"),
            new TableColumn("Id", "ID", "Id"),
            new TableColumn("Version", "Versione", "Version"),
            new TableColumn("Source", "Origine", "Source"));

        if (rows.Count == 0)
        {
            rows = ParseWingetTable(
                output,
                new TableColumn("Name", "Nome", "Name"),
                new TableColumn("Id", "ID", "Id"),
                new TableColumn("Version", "Versione", "Version"));
        }

        return rows
            .Select(row => new SearchResult
            {
                Name = row.GetValueOrDefault("Name", string.Empty),
                Id = row.GetValueOrDefault("Id", string.Empty),
                Version = FirstToken(row.GetValueOrDefault("Version", string.Empty)),
                Source = NormalizeSource(row.GetValueOrDefault("Source", string.Empty))
            })
            .Where(row => IsValidWingetId(row.Id))
            .ToList();
    }

    public static IReadOnlyList<UpdateEntry> ParseUpgradeEntries(string output)
    {
        var rows = ParseWingetTable(
            output,
            IsUpgradeSummaryLine,
            new TableColumn("Name", "Nome", "Name"),
            new TableColumn("Id", "ID", "Id"),
            new TableColumn("Version", "Versione", "Version"),
            new TableColumn("Available", "Disponibile", "Available"),
            new TableColumn("Source", "Origine", "Source"));

        if (rows.Count == 0)
        {
            rows = ParseWingetTable(
                output,
                IsUpgradeSummaryLine,
                new TableColumn("Name", "Nome", "Name"),
                new TableColumn("Id", "ID", "Id"),
                new TableColumn("Version", "Versione", "Version"),
                new TableColumn("Available", "Disponibile", "Available"));
        }

        return rows
            .Select(row => new UpdateEntry
            {
                Name = row.GetValueOrDefault("Name", string.Empty),
                Id = row.GetValueOrDefault("Id", string.Empty),
                Version = FirstToken(row.GetValueOrDefault("Version", string.Empty)),
                Available = FirstToken(row.GetValueOrDefault("Available", string.Empty)),
                Source = NormalizeSource(row.GetValueOrDefault("Source", string.Empty)),
                IsSelected = true
            })
            .Where(row => IsValidWingetId(row.Id))
            .ToList();
    }

    private static IReadOnlyList<Dictionary<string, string>> ParseWingetTable(string output, params TableColumn[] columns)
    {
        return ParseWingetTable(output, shouldSkipLine: null, columns);
    }

    private static IReadOnlyList<Dictionary<string, string>> ParseWingetTable(string output, Func<string, bool>? shouldSkipLine, params TableColumn[] columns)
    {
        var lines = NormalizeWingetOutput(output)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(line => line.TrimEnd())
            .Where(line => !IsProgressLine(line))
            .ToArray();
        var rowStartIndex = -1;
        Dictionary<string, int>? headerIndexes = null;

        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryGetHeaderIndexes(lines[index], columns, out var currentHeaderIndexes))
            {
                continue;
            }

            headerIndexes = currentHeaderIndexes;
            rowStartIndex = index + 1;
            break;
        }

        if (rowStartIndex < 0 || headerIndexes == null)
        {
            return new List<Dictionary<string, string>>();
        }

        var orderedColumns = headerIndexes.OrderBy(pair => pair.Value).ToList();
        var rows = new List<Dictionary<string, string>>();

        for (var lineIndex = rowStartIndex; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line) || line.Trim().All(c => c == '-' || c == ' '))
            {
                continue;
            }

            if (shouldSkipLine?.Invoke(line) == true)
            {
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var columnIndex = 0; columnIndex < orderedColumns.Count; columnIndex++)
            {
                var start = orderedColumns[columnIndex].Value;
                var end = columnIndex + 1 < orderedColumns.Count
                    ? orderedColumns[columnIndex + 1].Value
                    : line.Length;

                if (start >= line.Length)
                {
                    row[orderedColumns[columnIndex].Key] = string.Empty;
                    continue;
                }

                var length = Math.Max(0, Math.Min(end, line.Length) - start);
                row[orderedColumns[columnIndex].Key] = line.Substring(start, length).Trim();
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string NormalizeWingetOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return string.Empty;
        }

        var noAnsi = Regex.Replace(output, @"\x1B\[[0-9;?]*[ -/]*[@-~]", string.Empty);
        return noAnsi.Replace('\b', ' ');
    }

    private static bool IsProgressLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed.All(c => c is '-' or '/' or '\\' or '|' or '.' or '█' or '▒' or '■'))
        {
            return true;
        }

        return Regex.IsMatch(trimmed, @"^\d{1,3}%$");
    }

    private static bool IsUpgradeSummaryLine(string line)
    {
        var trimmed = line.Trim();
        return Regex.IsMatch(trimmed, @"^\d+\s+(upgrades?\s+available|aggiornamenti\s+disponibili)\.?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool TryGetHeaderIndexes(string line, IReadOnlyList<TableColumn> columns, out Dictionary<string, int> indexes)
    {
        indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in columns)
        {
            var match = column.Headers
                .Select(header => Regex.Match(
                    line,
                    $@"(?<!\S){Regex.Escape(header)}(?=\s+|\s*$)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                .Where(candidate => candidate.Success)
                .OrderBy(candidate => candidate.Index)
                .FirstOrDefault();

            if (match == null)
            {
                indexes.Clear();
                return false;
            }

            indexes[column.Key] = match.Index;
        }

        return indexes.Values.Distinct().Count() == indexes.Count;
    }

    private static bool IsValidWingetId(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && !id.Any(char.IsWhiteSpace);
    }

    private static string FirstToken(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }

    private static string NormalizeSource(string value)
    {
        var firstToken = FirstToken(value);
        return IsNoUpdateMarker(firstToken)
            ? AppEntry.DefaultSource
            : AppEntry.NormalizeSource(firstToken);
    }

    private static bool IsNoUpdateMarker(string value)
    {
        var normalized = value.Trim();
        return normalized.Equals("No", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Nessun", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Nessuno", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record TableColumn(string Key, params string[] Headers);
}
