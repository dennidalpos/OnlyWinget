// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.IO;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class LogRetentionTests
{
    [Fact]
    public void CleanupOldLogs_DeletesLogFilesOlderThanRetentionPeriod()
    {
        var dir = CreateTempDirectory();
        try
        {
            var now = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc);
            var env = new WingetRuntimeEnvironment(dir, () => now);

            // Old log (35 days ago — should be deleted)
            var oldLog = Path.Combine(dir, "old.log");
            File.WriteAllText(oldLog, "old");
            File.SetLastWriteTimeUtc(oldLog, now.AddDays(-35));

            // Recent log (10 days ago — should be kept)
            var recentLog = Path.Combine(dir, "recent.log");
            File.WriteAllText(recentLog, "recent");
            File.SetLastWriteTimeUtc(recentLog, now.AddDays(-10));

            env.CleanupOldLogs(30);

            Assert.False(File.Exists(oldLog), "Old log should have been deleted.");
            Assert.True(File.Exists(recentLog), "Recent log should have been kept.");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CleanupOldLogs_DoesNotDeleteNonLogFiles()
    {
        var dir = CreateTempDirectory();
        try
        {
            var now = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc);
            var env = new WingetRuntimeEnvironment(dir, () => now);

            // A .tmp file older than 30 days — must NOT be deleted (only .log files are targeted)
            var tmpFile = Path.Combine(dir, "runtime.tmp");
            File.WriteAllText(tmpFile, "temp");
            File.SetLastWriteTimeUtc(tmpFile, now.AddDays(-60));

            env.CleanupOldLogs(30);

            Assert.True(File.Exists(tmpFile), "Non-log files must not be deleted.");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CleanupOldLogs_DoesNotDeleteDirectoryItself()
    {
        var dir = CreateTempDirectory();
        try
        {
            var now = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc);
            var env = new WingetRuntimeEnvironment(dir, () => now);

            // Write a very old log to ensure cleanup runs
            var oldLog = Path.Combine(dir, "install.log");
            File.WriteAllText(oldLog, "x");
            File.SetLastWriteTimeUtc(oldLog, now.AddDays(-90));

            env.CleanupOldLogs(30);

            Assert.True(Directory.Exists(dir), "The runtime directory must not be deleted by cleanup.");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void CleanupOldLogs_IsNoOp_WhenDirectoryDoesNotExist()
    {
        var dir = Path.Combine(Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N"), "nonexistent");
        var env = new WingetRuntimeEnvironment(dir, () => DateTime.UtcNow);

        // Must not throw even if directory does not exist.
        var ex = Record.Exception(() => env.CleanupOldLogs(30));
        Assert.Null(ex);
    }

    [Fact]
    public void CleanupLocalTemp_DelegatesToCleanupOldLogs_WithDefaultRetention()
    {
        var dir = CreateTempDirectory();
        try
        {
            var now = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc);
            var env = new WingetRuntimeEnvironment(dir, () => now);

            var oldLog = Path.Combine(dir, "install-old.log");
            File.WriteAllText(oldLog, "x");
            File.SetLastWriteTimeUtc(oldLog, now.AddDays(-(WingetRuntimeEnvironment.DefaultLogRetentionDays + 1)));

            var freshLog = Path.Combine(dir, "install-fresh.log");
            File.WriteAllText(freshLog, "y");
            File.SetLastWriteTimeUtc(freshLog, now.AddDays(-1));

            env.CleanupLocalTemp();

            Assert.False(File.Exists(oldLog), "Log older than retention period must be deleted via CleanupLocalTemp.");
            Assert.True(File.Exists(freshLog), "Fresh log must survive CleanupLocalTemp.");
            Assert.True(Directory.Exists(dir), "Runtime directory must survive CleanupLocalTemp.");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
