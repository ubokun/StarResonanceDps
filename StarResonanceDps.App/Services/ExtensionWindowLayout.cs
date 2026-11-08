using System.Windows;
namespace StarResonanceDps.App.Services;

internal static class ExtensionWindowLayout
{
    public static void Attach(Window window)
    {
        window.Loaded += (_, _) =>
        {
            if (window.WindowState != WindowState.Normal) return;
            var area = StarResonanceDps.App.Behaviors.WindowWorkingAreaBehavior.GetWorkingAreaForWindow(window);
            var maxWidth = Math.Max(300, area.Width - 16);
            var maxHeight = Math.Max(250, area.Height - 16);
            window.MinWidth = Math.Min(window.MinWidth, maxWidth);
            window.MinHeight = Math.Min(window.MinHeight, maxHeight);
            window.Width = Math.Min(window.Width, maxWidth);
            window.Height = Math.Min(window.Height, maxHeight);
            window.Left = Math.Clamp(double.IsNaN(window.Left) ? area.Left : window.Left, area.Left, Math.Max(area.Left, area.Right - window.Width));
            window.Top = Math.Clamp(double.IsNaN(window.Top) ? area.Top : window.Top, area.Top, Math.Max(area.Top, area.Bottom - window.Height));
        };
    }
}
