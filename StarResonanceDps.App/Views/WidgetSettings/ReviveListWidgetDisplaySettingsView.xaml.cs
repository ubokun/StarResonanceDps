// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class ReviveListWidgetDisplaySettingsView : UserControl
{
    public ReviveListWidgetDisplaySettingsView()
    {
        InitializeComponent();
    }

    private ReviveListWidgetSettingsViewModel? ViewModel => DataContext as ReviveListWidgetSettingsViewModel;

    /// <summary>選んだ項目をカーソル位置へ差し込む(プレイヤーリストのプレイヤー名と同じ)。</summary>
    private void AddPlayerInfoFormatField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            PlayerInfoFormatTextBox,
            ViewModel?.SelectedPlayerInfoFormatField?.Placeholder);
    }

    private void PlayerInfoFormatTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(PlayerInfoFormatTextBox);
    }

    private void PlayerInfoFormatTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused(PlayerInfoFormatTextBox);
    }
}
