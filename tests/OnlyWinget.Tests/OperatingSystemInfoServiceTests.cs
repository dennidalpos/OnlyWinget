// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class OperatingSystemInfoServiceTests
{
    [Fact]
    public void Detect_FormatsWindows11BadgeContext()
    {
        var service = new OperatingSystemInfoService(
            versionProvider: () => new Version(10, 0, 22631, 0),
            osArchitectureProvider: () => Architecture.X64,
            processArchitectureProvider: () => Architecture.X64,
            cultureProvider: () => CultureInfo.GetCultureInfo("it-IT"));

        var info = service.Detect();

        Assert.Equal("Windows 11", info.ProductName);
        Assert.Equal("22631", info.Build);
        Assert.Equal("x64", info.NormalizedArchitecture);
        Assert.Equal("x64", info.ProcessArchitecture);
        Assert.Equal("it-IT", info.UiCultureName);
        Assert.Equal("Windows 11 x64 it-IT", info.DisplayText);
    }

    [Fact]
    public void Detect_FormatsWindows10AndNormalizesArm64()
    {
        var service = new OperatingSystemInfoService(
            versionProvider: () => new Version(10, 0, 19045, 0),
            osArchitectureProvider: () => Architecture.Arm64,
            processArchitectureProvider: () => Architecture.X64,
            cultureProvider: () => CultureInfo.GetCultureInfo("en-US"));

        var info = service.Detect();

        Assert.Equal("Windows 10", info.ProductName);
        Assert.Equal("arm64", info.NormalizedArchitecture);
        Assert.Equal("x64", info.ProcessArchitecture);
        Assert.Equal("Windows 10 arm64 en-US", info.DisplayText);
    }
}
