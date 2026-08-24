using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using OnlyWinget.Application.System;

namespace OnlyWinget.Infrastructure.System;

public sealed class PcMetricsService : IPcMetricsService
{
    private long lastIdleTime;
    private long lastTotalTime;
    private (string status, DateTime timestamp) cachedNetworkStatus = ("Unknown", DateTime.MinValue);

    public PcMetrics GetCurrentMetrics()
    {
        var cpuUsage = GetCpuUsage();
        var (ramPercent, ramText) = GetRamInfo();
        var (diskPercent, diskText) = GetDiskInfo();
        var uptimeText = GetUptimeText();
        var osVersionText = GetOsVersionText();
        var networkStatusText = GetNetworkStatusText();

        return new PcMetrics(
            cpuUsage,
            ramPercent,
            ramText,
            diskPercent,
            diskText,
            uptimeText,
            osVersionText,
            networkStatusText);
    }

    private double GetCpuUsage()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
            {
                var idleTime = FileTimeToLong(idleFt);
                var totalTime = FileTimeToLong(kernelFt) + FileTimeToLong(userFt);

                var idleDelta = idleTime - lastIdleTime;
                var totalDelta = totalTime - lastTotalTime;

                lastIdleTime = idleTime;
                lastTotalTime = totalTime;

                if (totalDelta > 0 && lastTotalTime > 0)
                {
                    var cpu = 100.0 * (1.0 - ((double)idleDelta / totalDelta));
                    return Math.Clamp(Math.Round(cpu, 1), 0.0, 100.0);
                }
            }
        }
        catch (Exception exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"PcMetricsService.GetCpuUsage: {exception}");
        }
        return 0.0;
    }

    private static (double percent, string text) GetRamInfo()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    var totalGb = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    var availGb = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                    var usedGb = totalGb - availGb;
                    var loadPct = memStatus.dwMemoryLoad;
                    return (loadPct, $"{usedGb:F1} GB / {totalGb:F1} GB ({loadPct}%)");
                }
            }
        }
        catch (Exception exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"PcMetricsService.GetRamInfo: {exception}");
        }
        return (0, "N/A");
    }

    private static (double percent, string text) GetDiskInfo()
    {
        try
        {
            var systemDrivePath = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(systemDrivePath);
            if (drive.IsReady)
            {
                var totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                var usedGb = totalGb - freeGb;
                var pct = totalGb > 0 ? (usedGb / totalGb) * 100.0 : 0.0;
                return (Math.Round(pct, 1), $"{freeGb:F1} GB free / {totalGb:F1} GB");
            }
        }
        catch (Exception exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"PcMetricsService.GetDiskInfo: {exception}");
        }
        return (0, "N/A");
    }

    private static string GetUptimeText()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return uptime.Days > 0
            ? $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
            : $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
    }

    private static string GetOsVersionText()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (key is not null)
                {
                    var productName = key.GetValue("ProductName")?.ToString();
                    var displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? key.GetValue("ReleaseId")?.ToString();
                    if (Environment.OSVersion.Version.Build >= 22000 && productName is not null && productName.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
                    {
                        productName = "Windows 11" + productName["Windows 10".Length..];
                    }

                    if (!string.IsNullOrWhiteSpace(productName))
                    {
                        return !string.IsNullOrWhiteSpace(displayVersion)
                            ? $"{productName} {displayVersion}"
                            : productName;
                    }
                }
            }
            catch
            {
                // Fallback to OSDescription
            }
        }

        return RuntimeInformation.OSDescription;
    }

    private string GetNetworkStatusText()
    {
        try
        {
            if ((DateTime.UtcNow - cachedNetworkStatus.timestamp).TotalSeconds < 5)
            {
                return cachedNetworkStatus.status;
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                cachedNetworkStatus = ("Disconnected", DateTime.UtcNow);
                return "Disconnected";
            }

            var activeInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            string result;
            if (activeInterfaces.Any(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
            {
                result = "Connected (Wi-Fi)";
            }
            else if (activeInterfaces.Any(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
            {
                result = "Connected (Ethernet)";
            }
            else
            {
                result = activeInterfaces.Count > 0 ? "Connected" : "Disconnected";
            }

            cachedNetworkStatus = (result, DateTime.UtcNow);
            return result;
        }
        catch (Exception exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"PcMetricsService.GetNetworkStatusText: {exception}");
            return "Unknown";
        }
    }

    private static long FileTimeToLong(global::System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        return ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(
        out global::System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime,
        out global::System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
        out global::System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);
}
