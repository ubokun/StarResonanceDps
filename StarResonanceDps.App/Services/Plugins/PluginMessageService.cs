// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Views;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

internal sealed class PluginMessageService : IPluginMessageService
{
    public void Show(string title, string message)
    {
        Show(title, message, null);
    }

    public void Show(string title, string message, string? detail)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        if (application.Dispatcher.CheckAccess())
        {
            ShowCore(title, message, detail);
            return;
        }

        application.Dispatcher.Invoke(() => ShowCore(title, message, detail));
    }

    private static void ShowCore(string title, string message, string? detail)
    {
        var owner = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive && window.Visibility == Visibility.Visible)
            ?? Application.Current?.MainWindow;

        MessageWindow.Show(owner, title, message, detail);
    }
}
