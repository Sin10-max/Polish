using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Polisher.Models;
using Polisher.Services;

namespace Polisher;

public partial class MainWindow : Window
{
    private readonly SystemInfoService _sysInfo = new();
    private readonly CleanupService _cleanup = new();
    private readonly OptimizationService _optimize = new();

    public ObservableCollection<OptimizationItem> StartupItems { get; } = new();
    public ObservableCollection<OptimizationItem> VisualsItems { get; } = new();
    public ObservableCollection<OptimizationItem> CleanupItems { get; } = new();
    public ObservableCollection<OptimizationItem> ImprovementItems { get; } = new();

    private readonly DispatcherTimer _monitorTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();

        StartupList.ItemsSource = StartupItems;
        VisualsList.ItemsSource = VisualsItems;
        CleanupList.ItemsSource = CleanupItems;
        ImprovementsList.ItemsSource = ImprovementItems;

        BuildVisualsItems();
        _monitorTimer.Tick += (_, _) => UpdateLiveMonitor();
        _monitorTimer.Start();

        Loaded += async (_, _) => await RunFullScanAsync();
    }

    // ===================== Live monitor =====================

    private void UpdateLiveMonitor()
    {
        var cpu = _sysInfo.GetCpuUsagePercent();
        CpuText.Text = $"{cpu:0}%";

        var ramTotal = _sysInfo.GetTotalRamGb();
        RamText.Text = ramTotal > 0 ? $"{ramTotal:0.0} GB" : "N/A";

        var (_, _, diskPct) = _sysInfo.GetPrimaryDiskUsage();
        DiskText.Text = $"{diskPct:0}%";

        NetText.Text = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()
            ? "Online" : "Offline";
    }

    // ===================== Scan =====================

    private async Task RunFullScanAsync()
    {
        await Task.WhenAll(RescanStartupAsync(), RescanCleanupAsync());
        RecomputeRatingsAndImprovements();
    }

    private Task RescanStartupAsync() => Task.Run(() =>
    {
        var items = _sysInfo.GetStartupItems();
        Dispatcher.Invoke(() =>
        {
            StartupItems.Clear();
            foreach (var (name, cmd, impact) in items)
            {
                StartupItems.Add(new OptimizationItem
                {
                    Id = "startup-" + name,
                    Category = ItemCategory.Performance,
                    Title = name,
                    Description = $"Launches at sign-in · {impact} impact",
                    Detail = impact,
                    Action = () => _optimize.DisableStartupItemAsync(name)
                });
            }
            NoStartupText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        });
    });

    private Task RescanCleanupAsync() => Task.Run(() =>
    {
        long tempBytes = _sysInfo.GetFolderSizeBytes(_cleanup.TempPath) + _sysInfo.GetFolderSizeBytes(_cleanup.WindowsTempPath);
        long cacheBytes = _sysInfo.GetFolderSizeBytes(_cleanup.ExplorerCachePath);
        long updateBytes = _sysInfo.GetFolderSizeBytes(_cleanup.UpdateCachePath);
        long dumpBytes = _sysInfo.GetFolderSizeBytes(_cleanup.CrashDumpPath);

        Dispatcher.Invoke(() =>
        {
            CleanupItems.Clear();

            var temp = new OptimizationItem
            {
                Id = "cleanup-temp", Category = ItemCategory.Cleanup,
                Title = "Temporary Files", Description = "Safe-to-delete temp files from apps and Windows.",
                Detail = SystemInfoService.FormatBytes(tempBytes)
            };
            temp.Action = async () =>
            {
                var freed = await _cleanup.CleanTempFilesAsync();
                temp.ResultSummary = $"Freed {SystemInfoService.FormatBytes(freed)}.";
            };
            CleanupItems.Add(temp);

            var cache = new OptimizationItem
            {
                Id = "cleanup-cache", Category = ItemCategory.Cleanup,
                Title = "App & Explorer Caches", Description = "Thumbnail and shell caches Windows will rebuild automatically.",
                Detail = SystemInfoService.FormatBytes(cacheBytes)
            };
            cache.Action = async () =>
            {
                var freed = await _cleanup.CleanCachesAsync();
                cache.ResultSummary = $"Freed {SystemInfoService.FormatBytes(freed)}.";
            };
            CleanupItems.Add(cache);

            var updates = new OptimizationItem
            {
                Id = "cleanup-updates", Category = ItemCategory.Cleanup,
                Title = "Old Windows Update Files", Description = "Downloaded update packages no longer needed.",
                Detail = SystemInfoService.FormatBytes(updateBytes)
            };
            updates.Action = async () =>
            {
                var freed = await _cleanup.CleanOldUpdatesAsync();
                updates.ResultSummary = $"Freed {SystemInfoService.FormatBytes(freed)}.";
            };
            CleanupItems.Add(updates);

            var dumps = new OptimizationItem
            {
                Id = "cleanup-dumps", Category = ItemCategory.Cleanup,
                Title = "Crash Dumps", Description = "Diagnostic dump files from past app crashes.",
                Detail = SystemInfoService.FormatBytes(dumpBytes)
            };
            dumps.Action = async () =>
            {
                var freed = await _cleanup.CleanCrashDumpsAsync();
                dumps.ResultSummary = $"Freed {SystemInfoService.FormatBytes(freed)}.";
            };
            CleanupItems.Add(dumps);

            var bin = new OptimizationItem
            {
                Id = "cleanup-bin", Category = ItemCategory.Cleanup,
                Title = "Recycle Bin", Description = "Empties the Recycle Bin permanently.",
                Detail = ""
            };
            bin.Action = async () =>
            {
                await _cleanup.EmptyRecycleBinAsync();
                bin.ResultSummary = "Recycle Bin emptied.";
            };
            CleanupItems.Add(bin);

            var large = new OptimizationItem
            {
                Id = "cleanup-large", Category = ItemCategory.Cleanup,
                Title = "Large Unused Files (review only)",
                Description = "Finds files over 500 MB in your profile — nothing is deleted automatically.",
                Detail = ""
            };
            large.Action = async () =>
            {
                var found = await _cleanup.FindLargeFilesAsync();
                large.ResultSummary = found.Count == 0
                    ? "No large files over 500 MB found."
                    : $"Found {found.Count} large file(s), largest: {System.IO.Path.GetFileName(found[0].Path)} ({SystemInfoService.FormatBytes(found[0].Size)}). Review them in File Explorer.";
            };
            CleanupItems.Add(large);
        });
    });

    private void BuildVisualsItems()
    {
        VisualsItems.Add(new OptimizationItem
        {
            Id = "visual-transparency", Category = ItemCategory.Visuals,
            Title = "Keep Transparency Effects On", Description = "Enabled only where your hardware can handle it smoothly.",
            Action = () => _optimize.SetTransparencyEnabledAsync(true)
        });
        VisualsItems.Add(new OptimizationItem
        {
            Id = "visual-animations", Category = ItemCategory.Visuals,
            Title = "Keep Smooth Animations On", Description = "Window and taskbar animations stay silky smooth.",
            Action = () => _optimize.SetAnimationsEnabledAsync(true)
        });
        VisualsItems.Add(new OptimizationItem
        {
            Id = "visual-dark", Category = ItemCategory.Visuals,
            Title = "Switch to Dark Mode", Description = "Applies dark mode across apps and the system.",
            Action = () => _optimize.SetAppsThemeAsync(true)
        });
        VisualsItems.Add(new OptimizationItem
        {
            Id = "visual-accent", Category = ItemCategory.Visuals,
            Title = "Set Accent Color", Description = "Applies a clean blue accent color.",
            Action = () => _optimize.SetAccentColorAsync(91, 140, 255)
        });
        VisualsItems.Add(new OptimizationItem
        {
            Id = "visual-taskbar", Category = ItemCategory.Visuals,
            Title = "Clean Up Taskbar Search", Description = "Shrinks the search box to an icon to reduce clutter.",
            Action = () => _optimize.SetTaskbarSearchCompactAsync()
        });
    }

    private void RecomputeRatingsAndImprovements()
    {
        var (_, _, diskPct) = _sysInfo.GetPrimaryDiskUsage();
        RatingStorage.Text = $"{diskPct:0}% used";

        var startupRaw = StartupItems.Select(i => (i.Title, "", i.Detail)).ToList();
        var startupRating = _sysInfo.RateStartupImpact(startupRaw);
        RatingStartup.Text = startupRating;

        int processCount = Process.GetProcesses().Length;
        RatingBackground.Text = processCount switch
        {
            < 90 => "Light",
            < 150 => "Moderate",
            _ => "Heavy"
        };

        RatingPerformance.Text = (startupRating, diskPct) switch
        {
            ("Excellent" or "Good", < 85) => "Good",
            (_, >= 95) => "Needs Attention",
            _ => "Fair"
        };

        ImprovementItems.Clear();
        foreach (var item in StartupItems.Where(i => i.Detail == "High").Take(3))
        {
            item.IsSelected = true;
            ImprovementItems.Add(item);
        }
        if (diskPct >= 80)
        {
            foreach (var item in CleanupItems.Where(i =>
                i.Id is "cleanup-temp" or "cleanup-updates" or "cleanup-bin"))
            {
                item.IsSelected = true;
                if (!ImprovementItems.Contains(item)) ImprovementItems.Add(item);
            }
        }
        else
        {
            var temp = CleanupItems.FirstOrDefault(i => i.Id == "cleanup-temp");
            if (temp != null) { temp.IsSelected = true; ImprovementItems.Add(temp); }
        }

        ImprovementsHeader.Text = $"Scan completed — Polish found {ImprovementItems.Count} improvement(s)";
    }

    // ===================== Button handlers =====================

    private void ScanNow_Click(object sender, RoutedEventArgs e) => _ = RunFullScanAsync();

    private void RescanCleanup_Click(object sender, RoutedEventArgs e) => _ = RescanCleanupAsync();

    private void WhatAreTheyButton_Click(object sender, RoutedEventArgs e)
    {
        ImprovementsList.Visibility = ImprovementsList.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void PolishButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = StartupItems.Concat(VisualsItems).Concat(CleanupItems)
            .Where(i => i.IsSelected)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Nothing is selected yet. Check a few items on any tab, or pick a profile, then try again.",
                "Nothing to polish", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await Overlay.RunAsync(selected, $"Applied {selected.Count} optimization(s) across your PC.");
    }

    private void PowerSaver_Click(object sender, RoutedEventArgs e) => _ = _optimize.SetPowerPlanAsync(OptimizationService.PowerPlanPowerSaver);
    private void PowerBalanced_Click(object sender, RoutedEventArgs e) => _ = _optimize.SetPowerPlanAsync(OptimizationService.PowerPlanBalanced);
    private void PowerHigh_Click(object sender, RoutedEventArgs e) => _ = _optimize.SetPowerPlanAsync(OptimizationService.PowerPlanHighPerformance);

    private void ChooseWallpaper_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp" };
        if (dialog.ShowDialog() == true)
        {
            SystemParametersInfoWallpaper(dialog.FileName);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    private void SystemParametersInfoWallpaper(string path)
    {
        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDCHANGE = 0x02;
        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    }

    // ===================== Profiles =====================

    private void ProfileBalanced_Click(object sender, RoutedEventArgs e) => ApplyProfile(ProfileType.Balanced);
    private void ProfilePerformance_Click(object sender, RoutedEventArgs e) => ApplyProfile(ProfileType.Performance);
    private void ProfileAesthetic_Click(object sender, RoutedEventArgs e) => ApplyProfile(ProfileType.Aesthetic);
    private void ProfileLowEnd_Click(object sender, RoutedEventArgs e) => ApplyProfile(ProfileType.LowEnd);

    private void ApplyProfile(ProfileType profile)
    {
        var all = StartupItems.Concat(VisualsItems).Concat(CleanupItems);
        _optimize.ApplyProfileSelection(profile, all);

        if (profile == ProfileType.LowEnd)
        {
            // Low-end also means trading some visuals away, unlike the other profiles.
            foreach (var v in VisualsItems) v.IsSelected = false;
        }

        ProfileConfirmText.Text = $"{profile} profile selected — head to \"Polish My PC\" whenever you're ready.";
    }

    // ===================== Sin tab links =====================

    private void GitHubLink_Click(object sender, RoutedEventArgs e) => OpenUrl("https://github.com/Sin10-max");
    private void DonateLink_Click(object sender, RoutedEventArgs e) => OpenUrl("https://linktr.ee/Sin10");

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* browser launch failed — nothing critical to recover */ }
    }
}
