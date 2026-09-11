using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Polisher.Models;

namespace Polisher.Services;

public class OptimizationService
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    private const uint SPI_SETANIMATION = 0x0049;
    private const uint SPI_SETUIEFFECTS = 0x103F; // enables/disables the master UI effects switch
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    public Task SetAnimationsEnabledAsync(bool enabled) => Task.Run(() =>
    {
        SystemParametersInfo(SPI_SETUIEFFECTS, 0, enabled ? (IntPtr)1 : IntPtr.Zero,
            SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    });

    /// Toggles transparency effects (the setting under Settings > Personalization > Colors).
    public Task SetTransparencyEnabledAsync(bool enabled) => Task.Run(() =>
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: true)
            ?? Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        key?.SetValue("EnableTransparency", enabled ? 1 : 0, RegistryValueKind.DWord);
    });

    public Task SetAppsThemeAsync(bool dark) => Task.Run(() =>
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: true)
            ?? Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        key?.SetValue("AppsUseLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
        key?.SetValue("SystemUsesLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
    });

    public Task SetAccentColorAsync(byte r, byte g, byte b) => Task.Run(() =>
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent", writable: true)
            ?? Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
        uint colorValue = (uint)(0xFF << 24 | b << 16 | g << 8 | r);
        key?.SetValue("AccentColorMenu", colorValue, RegistryValueKind.DWord);
    });

    /// Switches the active Windows power plan by GUID via powercfg (built into Windows).
    public Task SetPowerPlanAsync(string schemeGuid) => Task.Run(() =>
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg", $"/setactive {schemeGuid}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
        }
        catch { /* powercfg missing/blocked — non-fatal */ }
    });

    // Well-known built-in Windows power scheme GUIDs.
    public const string PowerPlanBalanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public const string PowerPlanHighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string PowerPlanPowerSaver = "a1841308-3541-4fab-bc81-f71556f20b4a";

    /// Disables a startup entry by removing its Run-key value (does not delete the program).
    public Task DisableStartupItemAsync(string valueName) => Task.Run(() =>
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
        catch { }
    });

    public Task SetTaskbarSearchCompactAsync() => Task.Run(() =>
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Search", writable: true);
        key?.SetValue("SearchboxTaskbarMode", 1, RegistryValueKind.DWord); // 0=hidden,1=icon,2=box
    });

    /// Builds the profile's default item selection state against a supplied master item list.
    public void ApplyProfileSelection(ProfileType profile, IEnumerable<OptimizationItem> allItems)
    {
        foreach (var item in allItems)
        {
            item.IsSelected = profile switch
            {
                ProfileType.Balanced => item.Category is ItemCategory.Cleanup or ItemCategory.Optimization,
                ProfileType.Performance => item.Category is ItemCategory.Performance or ItemCategory.Cleanup or ItemCategory.Optimization,
                ProfileType.Aesthetic => item.Category is ItemCategory.Cleanup or ItemCategory.Optimization,
                ProfileType.LowEnd => true, // everything, including trimming visuals
                _ => item.IsSelected
            };
        }
    }
}
