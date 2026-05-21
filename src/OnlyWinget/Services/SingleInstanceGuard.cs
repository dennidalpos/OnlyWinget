// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.IO;

namespace OnlyWinget.Services;

public sealed class SingleInstanceGuard : IDisposable
{
    public const string DefaultLockFileName = "OnlyWinget.instance.lock";

    private readonly string _lockFilePath;
    private FileStream? _lockFile;
    private bool _disposed;

    public SingleInstanceGuard(string? lockFilePath = null)
    {
        _lockFilePath = string.IsNullOrWhiteSpace(lockFilePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OnlyWinget",
                DefaultLockFileName)
            : lockFilePath;

        if (string.IsNullOrWhiteSpace(_lockFilePath))
        {
            throw new ArgumentException("Lock file path cannot be empty.", nameof(lockFilePath));
        }
    }

    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_lockFile != null)
        {
            return true;
        }

        try
        {
            var directory = Path.GetDirectoryName(_lockFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _lockFile = new FileStream(
                _lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lockFile?.Dispose();
        _lockFile = null;

        try
        {
            if (File.Exists(_lockFilePath))
            {
                File.Delete(_lockFilePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        _disposed = true;
    }
}
