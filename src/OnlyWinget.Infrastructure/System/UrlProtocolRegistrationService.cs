using System.Runtime.Versioning;
using Microsoft.Win32;
using OnlyWinget.Application.System;

namespace OnlyWinget.Infrastructure.System;

[SupportedOSPlatform("windows")]
public sealed class UrlProtocolRegistrationService : IUrlProtocolService
{
    private const string ProtocolScheme = "onlywinget";
    private const string RegistryKeyPath = @"Software\Classes\" + ProtocolScheme;

    bool IUrlProtocolService.IsRegistered() => IsRegistered();

    bool IUrlProtocolService.Register(string executablePath) => Register(executablePath);

    bool IUrlProtocolService.Unregister() => Unregister();

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
        catch (Exception exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"UrlProtocolRegistrationService.IsRegistered: {exception}");
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
        catch (Exception exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"UrlProtocolRegistrationService.Register: {exception}");
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
        catch (Exception exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"UrlProtocolRegistrationService.Unregister: {exception}");
            return false;
        }
    }
}
