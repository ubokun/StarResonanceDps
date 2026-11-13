// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// 選択リストを開いたときに項目が1つも無ければ、その場で閉じる(空のリストを出さない)。
///
/// <para>
/// 選択リストのスタイルが付けるので、画面に書かない。プラグインの選択リストにもスタイルを通して効く。
/// XAMLに書いた<c>DropDownOpened</c>の処理のほうが先に走るので、開いたときに項目を作り直す画面は、作り直した後の項目で数える。
/// </para>
/// </summary>
public static class EmptyDropDownBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(EmptyDropDownBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ComboBox comboBox)
        {
            return;
        }

        comboBox.DropDownOpened -= ComboBox_DropDownOpened;
        if ((bool)e.NewValue)
        {
            comboBox.DropDownOpened += ComboBox_DropDownOpened;
        }
    }

    private static void ComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        if (sender is ComboBox { HasItems: false } comboBox)
        {
            comboBox.IsDropDownOpen = false;
        }
    }
}
