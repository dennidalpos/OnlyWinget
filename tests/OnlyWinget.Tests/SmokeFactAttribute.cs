// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class SmokeFactAttribute : FactAttribute
{
    public const string EnvironmentVariableName = "ONLYWINGET_RUN_WINGET_SMOKE";

    public SmokeFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariableName), "1", StringComparison.Ordinal))
        {
            Skip = $"Live winget smoke test not_run. Set {EnvironmentVariableName}=1 or use scripts/check.ps1 -RunWingetSmoke to execute it.";
        }
    }
}
