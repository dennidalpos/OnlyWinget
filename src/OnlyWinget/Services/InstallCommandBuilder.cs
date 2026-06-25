// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class InstallCommandBuilder : IInstallCommandBuilder
{
    private readonly WingetCommandService _wingetService;

    public InstallCommandBuilder(WingetCommandService wingetService)
    {
        _wingetService = wingetService;
    }

    public IReadOnlyList<string> BuildInstallArguments(AppEntry app)
    {
        if (app.RequiresAdvancedArgumentsReview)
        {
            throw new InvalidOperationException("Advanced winget arguments must be reviewed before building install arguments.");
        }

        var args = new List<string>
        {
            "install",
            "--id",
            app.Id,
            "-e",
            "--source",
            AppEntry.NormalizeSource(app.Source)
        };

        AddOption(args, "--scope", app.Scope);
        AddOption(args, "--architecture", app.Architecture);
        AddOption(args, "--installer-type", app.InstallerType);
        AddOption(args, "--locale", app.Locale);
        if (app.SupportsInstallLocation)
        {
            AddOption(args, "--location", ExpandPathPlaceholders(app.InstallLocation));
        }

        if (app.SupportsLog)
        {
            var logPath = string.IsNullOrWhiteSpace(app.LogPath)
                ? _wingetService.CreateOperationLogPath("install", app.OperationKey)
                : ExpandPathPlaceholders(app.LogPath);
            AddOption(args, "--log", logPath);
        }

        switch (app.InstallMode)
        {
            case InstallModes.Interactive:
                args.Add("--interactive");
                break;
            case InstallModes.Silent:
                args.Add("--silent");
                break;
        }

        if (!string.IsNullOrWhiteSpace(app.OverrideArgs))
        {
            args.Add("--override");
            args.Add(ExpandAdvancedArguments(app.OverrideArgs));
        }
        else if (!string.IsNullOrWhiteSpace(app.AdditionalCustomArgs))
        {
            args.Add("--custom");
            args.Add(ExpandAdvancedArguments(app.AdditionalCustomArgs));
        }

        args.Add("--accept-package-agreements");
        args.Add("--accept-source-agreements");
        if (!string.Equals(app.InstallMode, InstallModes.Interactive, System.StringComparison.Ordinal))
        {
            args.Add("--disable-interactivity");
        }

        return args;
    }

    private static void AddOption(List<string> args, string option, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        args.Add(option);
        args.Add(value.Trim());
    }

    private static string ExpandAdvancedArguments(string value)
    {
        return Environment.ExpandEnvironmentVariables(value.Trim());
    }

    private static string ExpandPathPlaceholders(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Environment.ExpandEnvironmentVariables(value.Trim());
    }
}
