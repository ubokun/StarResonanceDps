// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class EquipmentWidgetDisplaySettingsView : UserControl
{
    public EquipmentWidgetDisplaySettingsView()
    {
        InitializeComponent();
    }

    private EquipmentWidgetSettingsViewModel? ViewModel => DataContext as EquipmentWidgetSettingsViewModel;

    /// <summary>選んだ項目をカーソル位置へ差し込む(スキル詳細の行名と同じ)。</summary>
    private void AddEquipmentInfoFormatField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            EquipmentInfoFormatTextBox,
            ViewModel?.GetSelectedFieldPlaceholder());
    }

    private void EquipmentInfoFormatTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(EquipmentInfoFormatTextBox);
    }

    private void EquipmentInfoFormatTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(EquipmentInfoFormatTextBox);
    }
}
