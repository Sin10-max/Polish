namespace Polisher.Models;

public class ScanSummary
{
    public string PerformanceRating { get; set; } = "Unknown";
    public double StorageUsedPercent { get; set; }
    public string StartupRating { get; set; } = "Unknown";
    public string BackgroundActivityRating { get; set; } = "Unknown";
    public List<OptimizationItem> FoundImprovements { get; set; } = new();
}
