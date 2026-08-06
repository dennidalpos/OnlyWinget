using System.Runtime.Versioning;
using Microsoft.Win32;

namespace OnlyWinget.Infrastructure.System;

[SupportedOSPlatform("windows")]
public static class UrlProtocolRegistrationService
{
    private const string ProtocolScheme = "onlywinget";
    private const string RegistryKeyPath = @"Software\Classes\" + ProtocolScheme;

    public static bool IsRegistered()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key is not null && key.GetValue("URL Protocol") is not null;
        }
        catch
        {
            return false;
        }
    }

    public static bool Register(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
            if (key is null)
            {
                return false;
            }

            key.SetValue(string.Empty, "URL:OnlyWinget Protocol");
            key.SetValue("URL Protocol", string.Empty);

            using var commandKey = key.CreateSubKey(@"shell\open\command");
            if (commandKey is null)
            {
                return false;
            }

            commandKey.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool Unregister()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(RegistryKeyPath, throwOnMissingSubKey: false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
