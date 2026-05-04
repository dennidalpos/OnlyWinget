// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class OperatingSystemInfoService
{
    private readonly Func<Version> _versionProvider;
    private readonly Func<Architecture> _osArchitectureProvider;
    private readonly Func<Architecture> _processArchitectureProvider;
    private readonly Func<CultureInfo> _cultureProvider;

    public OperatingSystemInfoService(
        Func<Version>? versionProvider = null,
        Func<Architecture>? osArchitectureProvider = null,
        Func<Architecture>? processArchitectureProvider = null,
        Func<CultureInfo>? cultureProvider = null)
    {
        _versionProvider = versionProvider ?? (() => Environment.OSVersion.Version);
        _osArchitectureProvider = osArchitectureProvider ?? (() => RuntimeInformation.OSArchitecture);
        _processArchitectureProvider = processArchitectureProvider ?? (() => RuntimeInformation.ProcessArchitecture);
        _cultureProvider = cultureProvider ?? (() => CultureInfo.CurrentUICulture);
    }

    public OperatingSystemInfo Detect()
    {
        var version = _versionProvider();
        var productName = GetWindowsProductName(version);
        var osArchitecture = NormalizeArchitecture(_osArchitectureProvider());
        var processArchitecture = NormalizeArchitecture(_processArchitectureProvider());
        var culture = _cultureProvider();

        return new OperatingSystemInfo
        {
            ProductName = productName,
            Version = version.ToString(),
            Build = version.Build > 0 ? version.Build.ToString(CultureInfo.InvariantCulture) : string.Empty,
            NormalizedArchitecture = osArchitecture,
            ProcessArchitecture = processArchitecture,
            UiCultureName = culture.Name
        };
    }

    private static string GetWindowsProductName(Version version)
    {
        if (version.Major >= 10 && version.Build >= 22000)
        {
            return "Windows 11";
        }

        if (version.Major >= 10)
        {
            return "Windows 10";
        }

        return "Windows";
    }

    private static string NormalizeArchitecture(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => architecture.ToString().ToLowerInvariant()
        };
    }
}
