using System;
using System.Collections.Generic;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.Winget;

internal static class WingetOutputHelpers
{
    public static string JoinOutput(WingetCommandResult result) =>
        string.Join(Environment.NewLine, result.StandardOutput, result.StandardError).Trim();

    public static bool TryGet(IReadOnlyDictionary<string, string> row, string key, out string value)
    {
        if (row.TryGetValue(key, out var rawValue) && !string.IsNullOrWhiteSpace(rawValue))
        {
            value = rawValue.Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }
}
