// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views;

public partial class ColorPickerWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigManager _configManager = ConfigManager.Instance;

    public ColorPickerWindow()
        : this(null)
    {
    }

    public ColorPickerWindow(Color? initialColor)
    {
        RecentColors = new ColorPaletteViewModel([], AppConfigDefaults.MaxRecentColorCount);
        RecentColors.PaletteChanged += (_, _) => OnPropertyChanged(nameof(RecentColorsVisibility));

        InitializeComponent();

        var colorPickerConfig = _configManager.GetColorPickerSnapshot();
        RecentColors.LoadRecent(colorPickerConfig.RecentColors);

        if (initialColor.HasValue)
        {
            ColorPicker.SelectedColor = initialColor.Value;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ColorPaletteViewModel RecentColors { get; }

    public Visibility RecentColorsVisibility => RecentColors.Colors.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Color SelectedColor { get; private set; }

    /// <summary>
    /// OKで閉じたか。<see cref="Window.DialogResult"/> は <see cref="Window.ShowDialog"/> で
    /// 表示した窓にしか設定できないため、非モーダル表示に合わせて自前で持つ。
    /// </summary>
    public bool IsConfirmed { get; private set; }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RecentColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ColorOptionViewModel option })
        {
            return;
        }

        ColorPicker.SelectedColor = option.Color;
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!ColorPicker.IsHexTextInputFocused
            || e.OriginalSource is not DependencyObject source
            || IsInsideTextBox(source))
        {
            return;
        }

        MoveFocusToSink();
    }

    private void MoveFocusToSink()
    {
        Keyboard.ClearFocus();
        FocusManager.SetFocusedElement(this, KeyboardFocusSink);
        Keyboard.Focus(KeyboardFocusSink);
    }

    private static bool IsInsideTextBox(DependencyObject source)
    {
        for (DependencyObject? current = source; current is not null; current = ElementTree.GetParent(current))
        {
            if (current is TextBox)
            {
                return true;
            }
        }

        return false;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        ColorPicker.CommitTextInput();
        SelectedColor = ColorPicker.SelectedColor;

        RecentColors.AddRecent(SelectedColor);
        _configManager.SaveColorPicker(new ColorPickerConfig
        {
            RecentColors = [.. RecentColors.GetHexColors()]
        });

        IsConfirmed = true;
        Close();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
