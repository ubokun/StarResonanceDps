// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class EntityInfoWidgetDisplaySettingsView : UserControl
{
    public EntityInfoWidgetDisplaySettingsView()
    {
        InitializeComponent();
    }

    private EntityInfoWidgetSettingsViewModel? ViewModel => DataContext as EntityInfoWidgetSettingsViewModel;

    private void AddEntityInfoFormatField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            EntityInfoFormatTextBox,
            ViewModel?.SelectedEntityInfoFormatField?.Placeholder);
    }

    private void EntityInfoFormatTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(EntityInfoFormatTextBox);
    }

    private void EntityInfoFormatTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(EntityInfoFormatTextBox);
    }

    private void AddBuffInfoFormatField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            BuffInfoFormatTextBox,
            ViewModel?.SelectedBuffInfoFormatField?.Placeholder);
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
