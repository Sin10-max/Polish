using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Polisher.Models;

namespace Polisher.Services;

/// <summary>
/// Reads real information from the machine. No random registry edits —
/// everything here is a read, so it's safe to call anytime (including
/// before the user grants elevation for the write side in CleanupService
/// / OptimizationService).
/// </summary>
public class SystemInfoService
{
    private PerformanceCounter? _cpuCounter;

    public SystemInfoService()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // first call always returns 0, warm it up
        }
        catch
        {
            _cpuCounter = null; // counters can be unavailable in locked-down environments
        }
    }

    public float GetCpuUsagePercent()
    {
        try { return _cpuCounter?.NextValue() ?? 0f; }
        catch { return 0f; }
    }

    public (double usedGb, double totalGb, double usedPercent) GetPrimaryDiskUsage()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            double totalGb = drive.TotalSize / 1024d / 1024 / 1024;
            double freeGb = drive.TotalFreeSpace / 1024d / 1024 / 1024;
            double usedGb = totalGb - freeGb;
            double pct = totalGb == 0 ? 0 : (usedGb / totalGb) * 100;
            return (usedGb, totalGb, pct);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    public double GetTotalRamGb()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                var bytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                return Math.Round(bytes / 1024d / 1024 / 1024, 1);
            }
        }
        catch { }
        return 0;
    }

    /// Enumerates programs launching at boot from the two most common Run keys.
    public List<(string Name, string Command, string Impact)> GetStartupItems()
    {
        var results = new List<(string, string, string)>();
        void ReadKey(RegistryKey root, string path)
        {
            try
            {
                using var key = root.OpenSubKey(path);
                if (key == null) return;
                foreach (var name in key.GetValueNames())
                {
                    var cmd = key.GetValue(name)?.ToString() ?? string.Empty;
                    // Simple heuristic impact score based on known heavy publishers/keywords.
                    string impact = "Low";
                    var lower = (name + cmd).ToLowerInvariant();
                    if (lower.Contains("update") || lower.Contains("helper") || lower.Contains("sync"))
                        impact = "Medium";
                    if (lower.Contains("adobe") || lower.Contains("teams") || lower.Contains("discord") || lower.Contains("spotify"))
                        impact = "High";
                    results.Add((name, cmd, impact));
                }
            }
            catch { }
        }

        ReadKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run");
        ReadKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
        return results;
    }

    public string RateStartupImpact(List<(string Name, string Command, string Impact)> items)
    {
        int high = items.Count(i => i.Impact == "High");
        if (items.Count == 0) return "Excellent";
        if (high >= 3) return "Poor";
        if (high >= 1 || items.Count >= 8) return "Moderate";
        return "Good";
    }

    public long GetFolderSizeBytes(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return 0;
            long size = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { size += new FileInfo(file).Length; } catch { }
            }
            return size;
        }
        catch { return 0; }
    }

    public static string FormatBytes(long bytes)
    {
        double gb = bytes / 1024d / 1024 / 1024;
        if (gb >= 1) return $"{gb:0.0} GB";
        double mb = bytes / 1024d / 1024;
        return $"{mb:0} MB";
    }
}
