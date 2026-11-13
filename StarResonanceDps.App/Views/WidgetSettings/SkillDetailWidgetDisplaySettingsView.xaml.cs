// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class SkillDetailWidgetDisplaySettingsView : UserControl
{
    public SkillDetailWidgetDisplaySettingsView()
    {
        InitializeComponent();
    }

    private SkillDetailWidgetSettingsViewModel? ViewModel => DataContext as SkillDetailWidgetSettingsViewModel;

    /// <summary>選んだ項目をカーソル位置へ差し込む(メーターのプレイヤー名と同じ)。</summary>
    private void AddSkillInfoFormatField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            SkillInfoFormatTextBox,
            ViewModel?.GetSelectedFieldPlaceholder());
    }

    private void SkillInfoFormatTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(SkillInfoFormatTextBox);
    }

    private void SkillInfoFormatTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(SkillInfoFormatTextBox);
    }
}
