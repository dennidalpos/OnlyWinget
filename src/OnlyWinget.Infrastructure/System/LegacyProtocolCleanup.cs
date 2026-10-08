using System.Runtime.Versioning;
using Microsoft.Win32;

namespace OnlyWinget.Infrastructure.System;

[SupportedOSPlatform("windows")]
internal static class LegacyProtocolCleanup
{
    internal static bool RemoveOwnedRegistration(RegistryKey classesRoot, string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        using (var key = classesRoot.OpenSubKey("onlywinget"))
        {
            if (key?.GetValue(string.Empty) is not string label ||
                !string.Equals(label, "URL:OnlyWinget Protocol", StringComparison.OrdinalIgnoreCase) ||
                key.GetValue("URL Protocol") is not string) return false;

            using var command = key.OpenSubKey(@"shell\open\command");
            if (command?.GetValue(string.Empty) is not string value ||
                !string.Equals(value, $"\"{executablePath}\" \"%1\"", StringComparison.OrdinalIgnoreCase)) return false;
        }
        classesRoot.DeleteSubKeyTree("onlywinget", throwOnMissingSubKey: false);
        return true;
    }
}
