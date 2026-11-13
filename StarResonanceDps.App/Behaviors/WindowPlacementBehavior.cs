// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Behaviors;

public static class WindowPlacementBehavior
{
    public static readonly DependencyProperty UseAppConfigStartUpStateProperty = DependencyProperty.RegisterAttached(
        "UseAppConfigStartUpState",
        typeof(bool),
        typeof(WindowPlacementBehavior),
        new PropertyMetadata(false, OnUseAppConfigStartUpStateChanged));

    public static bool GetUseAppConfigStartUpState(DependencyObject obj)
    {
        return (bool)obj.GetValue(UseAppConfigStartUpStateProperty);
    }

    public static void SetUseAppConfigStartUpState(DependencyObject obj, bool value)
    {
        obj.SetValue(UseAppConfigStartUpStateProperty, value);
    }

    private static void OnUseAppConfigStartUpStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            window.SourceInitialized += Window_SourceInitialized;
            window.Closing += Window_Closing;
        }
        else
        {
            window.SourceInitialized -= Window_SourceInitialized;
            window.Closing -= Window_Closing;
        }
    }

    private static void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        var bounds = ConfigManager.Instance.AppConfig.StartUpState;
        if (bounds is null || !IsValidBounds(bounds))
        {
            return;
        }

        var restoredBounds = MoveIntoVirtualScreen(bounds);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = restoredBounds.X;
        window.Top = restoredBounds.Y;
        window.Width = restoredBounds.Width;
        window.Height = restoredBounds.Height;
    }

    private static void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        ConfigManager.Instance.AppConfig.StartUpState = new WindowBounds
        {
            X = (int)Math.Round(bounds.Left),
            Y = (int)Math.Round(bounds.Top),
            Width = (int)Math.Round(bounds.Width),
            Height = (int)Math.Round(bounds.Height)
        };

        ConfigManager.Instance.Save();
    }

    private static bool IsValidBounds(WindowBounds bounds)
    {
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static WindowBounds MoveIntoVirtualScreen(WindowBounds bounds)
    {
        var minX = SystemParameters.VirtualScreenLeft;
        var minY = SystemParameters.VirtualScreenTop;
        var maxX = minX + SystemParameters.VirtualScreenWidth;
        var maxY = minY + SystemParameters.VirtualScreenHeight;

        var width = Math.Min(bounds.Width, (int)SystemParameters.VirtualScreenWidth);
        var height = Math.Min(bounds.Height, (int)SystemParameters.VirtualScreenHeight);
        var x = Math.Clamp(bounds.X, (int)minX, (int)Math.Max(minX, maxX - width));
        var y = Math.Clamp(bounds.Y, (int)minY, (int)Math.Max(minY, maxY - height));

        return new WindowBounds
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
    }
}
