// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class MeterWidgetDisplaySettingsView : UserControl
{
    public MeterWidgetDisplaySettingsView()
    {
        InitializeComponent();
    }

    private MeterWidgetSettingsViewModel? ViewModel => DataContext as MeterWidgetSettingsViewModel;

    /// <summary>
    /// 選んだ項目を<b>カーソル位置へ</b>差し込む。
    ///
    /// <para>
    /// <c>SelectedText</c> への代入なので、文字を選んでいればそれを置き換える(入力と同じ挙動)。
    /// カーソルは差し込んだ文字列の直後へ move する。
    /// </para>
    /// </summary>
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
