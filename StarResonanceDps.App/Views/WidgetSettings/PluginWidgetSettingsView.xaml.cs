// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarResonanceDps.App.Behaviors;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.ViewModels.Plugins;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class PluginWidgetSettingsView : UserControl
{
    public PluginWidgetSettingsView()
    {
        InitializeComponent();
    }

    /// <summary>節の要素(設定の窓のナビで、その節までスクロールする先)。まだ作られていなければ null。</summary>
    public FrameworkElement? FindSectionElement(PluginWidgetSettingSectionViewModel section)
    {
        return SectionsHost.ItemContainerGenerator.ContainerFromItem(section) as FrameworkElement;
    }

    private void ColorOptionRadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ColorOptionViewModel option })
        {
            option.Select();
        }
    }

    private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PluginColorSettingViewModel setting })
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var window = new ColorPickerWindow(setting.Colors.SelectedColor);
        if (owner is not null)
        {
            window.Owner = owner;
        }

        OwnerModalWindow.Show(window, owner, () =>
        {
            if (window.IsConfirmed)
            {
                setting.ApplyPickedColor(window.SelectedColor);
            }
        });
    }

    // --- 数の欄 ---
    // 文字はバインドせず、出すときと確定のたびに今の値で書き直す。空なら打つ前の値に戻して警告音(全体設定の計測時間の欄と同じ)。

    private void NumberTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        var textBox = (TextBox)sender;
        textBox.Text = ((PluginNumberSettingViewModel)textBox.DataContext).DisplayText;
    }

    private void NumberTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitNumber((TextBox)sender);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void NumberTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommitNumber((TextBox)sender);
    }

    private static void CommitNumber(TextBox textBox)
    {
        if (textBox.DataContext is not PluginNumberSettingViewModel setting)
        {
            return;
        }

        if (!setting.TryCommitText(textBox.Text))
        {
            DigitsOnlyInputBehavior.PlayInvalidInputSound();
        }

        textBox.Text = setting.DisplayText;
    }
}
