// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Collections.Generic;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class InstallCommandBuilder : IInstallCommandBuilder
{
    private readonly WingetService _wingetService;

    public InstallCommandBuilder(WingetService wingetService)
    {
        _wingetService = wingetService;
    }

    public IReadOnlyList<string> BuildInstallArguments(AppEntry app)
    {
        var args = new List<string>
        {
            "install",
            "--id",
            app.Id,
            "-e",
            "--source",
            string.IsNullOrWhiteSpace(app.Source) ? "winget" : app.Source
        };

        if (!string.IsNullOrWhiteSpace(app.Version))
        {
            args.Add("--version");
            args.Add(app.Version);
        }

        AddOption(args, "--scope", app.Scope);
        AddOption(args, "--architecture", app.Architecture);
        AddOption(args, "--installer-type", app.InstallerType);
        AddOption(args, "--locale", app.Locale);
        AddOption(args, "--location", app.InstallLocation);

        var logPath = string.IsNullOrWhiteSpace(app.LogPath)
            ? _wingetService.CreateOperationLogPath("install", app.Id)
            : app.LogPath.Trim();
        AddOption(args, "--log", logPath);

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
            args.Add(app.OverrideArgs.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(app.AdditionalCustomArgs))
        {
            args.Add("--custom");
            args.Add(app.AdditionalCustomArgs.Trim());
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
}
