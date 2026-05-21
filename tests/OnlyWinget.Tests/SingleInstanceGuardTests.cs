// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.IO;
using System.Threading.Tasks;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public async Task TryAcquire_ReturnsFalse_WhenAnotherOwnerHoldsTheLockFile()
    {
        var lockFilePath = CreateLockFilePath();
        using var first = new SingleInstanceGuard(lockFilePath);

        Assert.True(first.TryAcquire());

        var secondAcquired = await Task.Run(() =>
        {
            using var second = new SingleInstanceGuard(lockFilePath);
            return second.TryAcquire();
        });

        Assert.False(secondAcquired);
    }

    [Fact]
    public async Task Dispose_ReleasesTheLockFile()
    {
        var lockFilePath = CreateLockFilePath();
        using (var first = new SingleInstanceGuard(lockFilePath))
        {
            Assert.True(first.TryAcquire());
        }

        var secondAcquired = await Task.Run(() =>
        {
            using var second = new SingleInstanceGuard(lockFilePath);
            return second.TryAcquire();
        });

        Assert.True(secondAcquired);
    }

    private static string CreateLockFilePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, SingleInstanceGuard.DefaultLockFileName);
    }
}
