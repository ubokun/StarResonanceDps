using System.ComponentModel;
using System.Windows;

namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The content of one widget window, returned by <see cref="IPluginWidgetProvider.CreateWidget"/> and <see cref="IPluginMultiWidgetProvider.CreateWidget"/>.
/// The host places the elements without margins and does not set their <see cref="FrameworkElement.DataContext"/>.
/// The window does not set a text color or font; use <see cref="PluginResourceKeys.Text.WidgetWindow"/> for the white text with a shadow
/// that the host's widgets use.
/// </summary>
public sealed class PluginWidgetContent
{
    /// <summary>The body of the window.</summary>
    public required FrameworkElement Body { get; init; }

    /// <summary>The footer row. Required when <see cref="PluginWidgetDefinition.HasFooter"/> is <c>true</c>, otherwise must be <c>null</c>.</summary>
    public FrameworkElement? Footer { get; init; }

    /// <summary>
    /// Buttons shown in the title bar, left of the close button, in order. The host draws them like its own header buttons.
    /// The list is read when the window opens, so buttons cannot be added or removed later. They are hidden while the header is hidden.
    /// </summary>
    public IReadOnlyList<PluginWidgetHeaderButton> HeaderButtons { get; init; } = [];
}

/// <summary>
/// A text button in a widget window's title bar. Change <see cref="Text"/> on the UI thread; the host updates the button.
/// <see cref="Click"/> is raised on the UI thread.
/// </summary>
public sealed class PluginWidgetHeaderButton : INotifyPropertyChanged
{
    private string _text = string.Empty;

    /// <summary>Raised when <see cref="Text"/> changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the user clicks the button.</summary>
    public event EventHandler? Click;

    /// <summary>The button text. <c>null</c> becomes an empty string.</summary>
    public string Text
    {
        get => _text;
        set
        {
            var text = value ?? string.Empty;
            if (string.Equals(_text, text, StringComparison.Ordinal))
            {
                return;
            }

            _text = text;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    internal void RaiseClick()
    {
        Click?.Invoke(this, EventArgs.Empty);
    }
}
