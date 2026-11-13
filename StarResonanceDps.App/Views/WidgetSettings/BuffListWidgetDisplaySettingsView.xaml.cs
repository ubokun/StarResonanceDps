// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class BuffListWidgetDisplaySettingsView : UserControl
{
    public BuffListWidgetDisplaySettingsView()
    {
        InitializeComponent();
    }

    private BuffListWidgetSettingsViewModel? ViewModel => DataContext as BuffListWidgetSettingsViewModel;

    private void AddFormatField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            BuffInfoFormatTextBox,
            ViewModel?.SelectedFormatField?.Placeholder);
    }

    private void BuffInfoFormatTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(BuffInfoFormatTextBox);
    }

    private void BuffInfoFormatTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(BuffInfoFormatTextBox);
    }
}
