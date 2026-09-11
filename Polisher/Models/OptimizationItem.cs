using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Polisher.Models;

public enum ItemCategory
{
    Performance,
    Visuals,
    Cleanup,
    Optimization
}

/// <summary>
/// A single toggle-able item shown in a tab (a startup program, a cleanup
/// bucket, a visual setting, or a scan recommendation). IsSelected drives
/// checkboxes in the UI; Action is what actually runs when "Polish My PC"
/// is pressed.
/// </summary>
public class OptimizationItem : INotifyPropertyChanged
{
    public string Id { get; init; } = string.Empty;
    public ItemCategory Category { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// Human readable size of impact, e.g. "2.3 GB" or "High Impact"
    public string Detail { get; set; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /// The actual work performed when this item is applied. Populated by the services.
    public Func<Task>? Action { get; set; }

    /// Filled in after Action runs, shown in the "What did we optimize" summary.
    public string ResultSummary { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
