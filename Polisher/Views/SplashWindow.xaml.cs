using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Polisher.Views;

public partial class SplashWindow : Window
{
    public event EventHandler? SplashFinished;

    public SplashWindow()
    {
        InitializeComponent();
        Loaded += SplashWindow_Loaded;
    }

    private void SplashWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var root = (Grid)Content;
        var fadeInEase = new CubicEase { EasingMode = EasingMode.EaseOut };

        // 0.0s -> 0.6s : fade + scale the whole splash in smoothly
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.6)) { EasingFunction = fadeInEase };
        root.BeginAnimation(OpacityProperty, fadeIn);

        var scaleIn = new DoubleAnimation(0.92, 1.0, TimeSpan.FromSeconds(0.6)) { EasingFunction = fadeInEase };
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn.Clone());

        // Continuous slow rotation of the ring arc for the whole 10 seconds
        var rotate = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(2.2))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        ArcRotate.BeginAnimation(RotateTransform.AngleProperty, rotate);

        // Gentle pulsing of the logo mark, forever
        var pulse = new DoubleAnimation(1.0, 1.08, TimeSpan.FromSeconds(1.0))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        LogoPulse.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        LogoPulse.BeginAnimation(ScaleTransform.ScaleYProperty, pulse.Clone());

        // 0.4s -> 9.8s : progress bar fills smoothly, pure white, ends exactly at the 10s mark
        var fill = new DoubleAnimation(0, 260, TimeSpan.FromSeconds(9.4))
        {
            BeginTime = TimeSpan.FromSeconds(0.4),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        fill.Completed += (_, _) => OnSplashComplete();
        ProgressFill.BeginAnimation(WidthProperty, fill);
    }

    private void OnSplashComplete()
    {
        // Quick fade-out, then hand off to MainWindow.
        var root = (Grid)Content;
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.35));
        fadeOut.Completed += (_, _) => SplashFinished?.Invoke(this, EventArgs.Empty);
        root.BeginAnimation(OpacityProperty, fadeOut);
    }
}
