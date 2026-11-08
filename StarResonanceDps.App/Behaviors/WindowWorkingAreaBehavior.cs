using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace StarResonanceDps.App.Behaviors;

public static class WindowWorkingAreaBehavior
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;

    private static readonly DependencyProperty IsHookAttachedProperty =
        DependencyProperty.RegisterAttached(
            "IsHookAttached",
            typeof(bool),
            typeof(WindowWorkingAreaBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ConstrainMaximizeToWorkingAreaProperty =
        DependencyProperty.RegisterAttached(
            "ConstrainMaximizeToWorkingArea",
            typeof(bool),
            typeof(WindowWorkingAreaBehavior),
            new PropertyMetadata(false, OnConstrainMaximizeToWorkingAreaChanged));

    public static bool GetConstrainMaximizeToWorkingArea(DependencyObject element)
    {
        return (bool)element.GetValue(ConstrainMaximizeToWorkingAreaProperty);
    }

    public static void SetConstrainMaximizeToWorkingArea(DependencyObject element, bool value)
    {
        element.SetValue(ConstrainMaximizeToWorkingAreaProperty, value);
    }

    private static bool GetIsHookAttached(DependencyObject element)
    {
        return (bool)element.GetValue(IsHookAttachedProperty);
    }

    private static void SetIsHookAttached(DependencyObject element, bool value)
    {
        element.SetValue(IsHookAttachedProperty, value);
    }

    private static void OnConstrainMaximizeToWorkingAreaChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not Window window)
        {
            return;
        }

        if (eventArgs.NewValue is not true || GetIsHookAttached(window))
        {
            return;
        }

        if (PresentationSource.FromVisual(window) is HwndSource)
        {
            AttachWindowHook(window);
            return;
        }

        window.SourceInitialized -= Window_SourceInitialized;
        window.SourceInitialized += Window_SourceInitialized;
    }

    private static void Window_SourceInitialized(object? sender, EventArgs eventArgs)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.SourceInitialized -= Window_SourceInitialized;
        AttachWindowHook(window);
    }

    private static void AttachWindowHook(Window window)
    {
        if (GetIsHookAttached(window)
            || PresentationSource.FromVisual(window) is not HwndSource source)
        {
            return;
        }

        source.AddHook((IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (message == WmGetMinMaxInfo && lParam != IntPtr.Zero)
            {
                ApplyGetMinMaxInfo(window, hwnd, lParam);
                handled = true;
            }

            return IntPtr.Zero;
        });

        SetIsHookAttached(window, true);
    }

    private static void ApplyGetMinMaxInfo(Window window, IntPtr hwnd, IntPtr lParam)
    {
        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var transform = GetDeviceTransform(window);

        ApplyMinTrackSize(window, transform, ref minMaxInfo);
        ApplyWorkAreaMaximizeSize(window, hwnd, transform, ref minMaxInfo);

        Marshal.StructureToPtr(minMaxInfo, lParam, false);
    }

    private static Matrix GetDeviceTransform(Window window)
    {
        return (PresentationSource.FromVisual(window) as HwndSource)
            ?.CompositionTarget
            ?.TransformToDevice
            ?? Matrix.Identity;
    }

    private static void ApplyMinTrackSize(
        Window window,
        Matrix transform,
        ref MinMaxInfo minMaxInfo)
    {
        if (double.IsFinite(window.MinWidth) && window.MinWidth > 0)
        {
            minMaxInfo.MinTrackSize.X = Math.Max(
                minMaxInfo.MinTrackSize.X,
                ToDevicePixels(window.MinWidth, transform.M11));
        }

        if (double.IsFinite(window.MinHeight) && window.MinHeight > 0)
        {
            minMaxInfo.MinTrackSize.Y = Math.Max(
                minMaxInfo.MinTrackSize.Y,
                ToDevicePixels(window.MinHeight, transform.M22));
        }
    }

    private static void ApplyWorkAreaMaximizeSize(
        Window window,
        IntPtr hwnd,
        Matrix transform,
        ref MinMaxInfo minMaxInfo)
    {
        var monitorHandle = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitorHandle == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            return;
        }

        var outerMargin = GetOuterWindowMargin(window);
        var leftMargin = ToDevicePixels(outerMargin.Left, transform.M11);
        var topMargin = ToDevicePixels(outerMargin.Top, transform.M22);
        var rightMargin = ToDevicePixels(outerMargin.Right, transform.M11);
        var bottomMargin = ToDevicePixels(outerMargin.Bottom, transform.M22);

        minMaxInfo.MaxPosition.X =
            monitorInfo.WorkArea.Left - monitorInfo.MonitorArea.Left - leftMargin;
        minMaxInfo.MaxPosition.Y =
            monitorInfo.WorkArea.Top - monitorInfo.MonitorArea.Top - topMargin;
        minMaxInfo.MaxSize.X =
            monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left + leftMargin + rightMargin;
        minMaxInfo.MaxSize.Y =
            monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top + topMargin + bottomMargin;
    }

    private static Thickness GetOuterWindowMargin(Window window)
    {
        var resource = window.TryFindResource("DefaultMargin");

        return resource switch
        {
            Thickness thickness => thickness,
            double value => new Thickness(value),
            int value => new Thickness(value),
            _ => new Thickness(0)
        };
    }

    private static int ToDevicePixels(double value, double scale)
    {
        return (int)Math.Ceiling(value * scale);
    }

    internal static Rect GetWorkingAreaForWindow(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (handle == IntPtr.Zero || !GetMonitorInfo(monitor, ref info) || !GetWindowRect(handle, out var bounds)
            || !double.IsFinite(window.Left) || !double.IsFinite(window.Top)) return SystemParameters.WorkArea;
        var transform = GetDeviceTransform(window);
        var scaleX = transform.M11 > 0 ? transform.M11 : 1;
        var scaleY = transform.M22 > 0 ? transform.M22 : 1;
        return new Rect(window.Left + (info.WorkArea.Left - bounds.Left) / scaleX,
            window.Top + (info.WorkArea.Top - bounds.Top) / scaleY,
            (info.WorkArea.Right - info.WorkArea.Left) / scaleX,
            (info.WorkArea.Bottom - info.WorkArea.Top) / scaleY);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out Rectangle bounds);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rectangle MonitorArea;
        public Rectangle WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }
}
