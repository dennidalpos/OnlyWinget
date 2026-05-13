// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OnlyWinget.Services;

internal static class WingetProgressParser
{
    private static readonly Regex PercentPattern = new(@"(?<!\d)(\d{1,3})%", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DownloadSizePattern = new(
        @"(?<current>\d+(?:[\.,]\d+)?)\s*(?<currentUnit>B|KB|MB|GB)\s*/\s*(?<total>\d+(?:[\.,]\d+)?)\s*(?<totalUnit>B|KB|MB|GB)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryParse(string line, out WingetProgressInfo progress)
    {
        progress = new WingetProgressInfo();
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = Normalize(line);
        if (TryParsePercentage(normalized, out var percentage))
        {
            progress = new WingetProgressInfo
            {
                Percentage = percentage,
                PhaseText = normalized.Trim()
            };
            return true;
        }

        if (TryParseDownloadSize(normalized, out percentage))
        {
            progress = new WingetProgressInfo
            {
                Percentage = percentage,
                PhaseText = normalized.Trim()
            };
            return true;
        }

        if (IsIndeterminatePhase(normalized))
        {
            progress = new WingetProgressInfo
            {
                IsIndeterminate = true,
                PhaseText = normalized.Trim()
            };
            return true;
        }

        return false;
    }

    private static bool TryParsePercentage(string line, out int percentage)
    {
        percentage = 0;
        var match = PercentPattern.Match(line);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        percentage = ClampPercentage(parsed);
        return true;
    }

    private static bool TryParseDownloadSize(string line, out int percentage)
    {
        percentage = 0;
        var match = DownloadSizePattern.Match(line);
        if (!match.Success)
        {
            return false;
        }

        if (!TryParseSize(match.Groups["current"].Value, match.Groups["currentUnit"].Value, out var currentBytes) ||
            !TryParseSize(match.Groups["total"].Value, match.Groups["totalUnit"].Value, out var totalBytes) ||
            totalBytes <= 0)
        {
            return false;
        }

        percentage = ClampPercentage((int)Math.Round(currentBytes * 100.0 / totalBytes));
        return true;
    }

    private static bool TryParseSize(string value, string unit, out double bytes)
    {
        bytes = 0;
        var normalizedValue = value.Replace(',', '.');
        if (!double.TryParse(normalizedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        var multiplier = unit.ToUpperInvariant() switch
        {
            "B" => 1d,
            "KB" => 1024d,
            "MB" => 1024d * 1024d,
            "GB" => 1024d * 1024d * 1024d,
            _ => 0d
        };

        if (multiplier <= 0)
        {
            return false;
        }

        bytes = parsed * multiplier;
        return true;
    }

    private static bool IsIndeterminatePhase(string line)
    {
        return ContainsAny(line,
            "downloading",
            "download in corso",
            "verifying installer hash",
            "verifica dell'hash",
            "starting package install",
            "avvio installazione pacchetto",
            "installing",
            "installazione in corso",
            "upgrading",
            "aggiornamento in corso",
            "uninstalling",
            "disinstallazione in corso");
    }

    private static string Normalize(string output)
    {
        var noAnsi = Regex.Replace(output, @"\x1B\[[0-9;?]*[ -/]*[@-~]", string.Empty);
        return noAnsi.Replace('\b', ' ');
    }

    private static int ClampPercentage(int percentage) => Math.Max(0, Math.Min(100, percentage));

    private static bool ContainsAny(string text, params string[] values)
    {
        return Array.Exists(values, value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
