using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Polisher.Models;

namespace Polisher.Views;

public partial class PolishingOverlay : UserControl
{
    private readonly ObservableCollection<OptimizationItem> _completedItems = new();

    public PolishingOverlay()
    {
        InitializeComponent();
        SummaryList.ItemsSource = _completedItems;
    }

    /// Shows the overlay in its "working" state and begins the spinner.
    public void ShowWorking()
    {
        _completedItems.Clear();
        WorkingPanel.Visibility = Visibility.Visible;
        CompletePanel.Visibility = Visibility.Collapsed;
        CompleteButtons.Visibility = Visibility.Collapsed;
        SummaryPopup.Visibility = Visibility.Collapsed;
        WorkingStepText.Text = "Getting started";
        Visibility = Visibility.Visible;

        var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.9))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        SpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    /// Runs the given items' actions in sequence, updating the step label as it goes,
    /// then flips the card to the completed state.
    public async Task RunAsync(IEnumerable<OptimizationItem> items, string completeSubtitle)
    {
        ShowWorking();

        foreach (var item in items)
        {
            WorkingStepText.Text = item.Title;
            try
            {
                if (item.Action != null)
                    await item.Action();
                if (string.IsNullOrEmpty(item.ResultSummary))
                    item.ResultSummary = "Applied successfully.";
            }
            catch (Exception ex)
            {
                item.ResultSummary = $"Skipped — {ex.Message}";
            }
            _completedItems.Add(item);

            // Small pause so each step is visible rather than flashing by instantly.
            await Task.Delay(350);
        }

        SpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        CompleteSubText.Text = completeSubtitle;
        WorkingPanel.Visibility = Visibility.Collapsed;
        CompletePanel.Visibility = Visibility.Visible;
        CompleteButtons.Visibility = Visibility.Visible;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Visibility = Visibility.Collapsed;

    private void UnderstandButton_Click(object sender, RoutedEventArgs e) => Visibility = Visibility.Collapsed;

    private void WhatWeOptimizedButton_Click(object sender, RoutedEventArgs e)
        => SummaryPopup.Visibility = Visibility.Visible;

    private void CloseSummary_Click(object sender, RoutedEventArgs e)
        => SummaryPopup.Visibility = Visibility.Collapsed;
}
