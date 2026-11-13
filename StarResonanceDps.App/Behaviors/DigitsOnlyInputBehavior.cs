// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// 数字(0〜9)だけを受ける入力欄。数字以外の文字・数字以外を含む貼り付け・スペースは入れずに警告音を鳴らす。
/// 文字を伴わない入力(Ctrl との組み合わせなど)は止めない。IME は欄の側で切る(<c>InputMethod.IsInputMethodEnabled="False"</c>)。
/// <see cref="ListSeparatorProperty"/> を指定した欄は、区切りの文字で並べた複数の数を受け、区切りのあいだの数は
/// long に必ず収まる <see cref="MaxDigitsPerNumber"/> 桁まで(超える入力も止める)。
/// 使うのは全体設定の計測時間と、通知タブの対象のID(区切りあり)。
/// </summary>
public static class DigitsOnlyInputBehavior
{
    private const int MaxDigitsPerNumber = 18;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(DigitsOnlyInputBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>数のあいだに入れてよい区切りの文字。空なら区切らない(数1つだけ)。</summary>
    public static readonly DependencyProperty ListSeparatorProperty = DependencyProperty.RegisterAttached(
        "ListSeparator",
        typeof(string),
        typeof(DigitsOnlyInputBehavior),
        new PropertyMetadata(string.Empty));

    public static bool GetIsEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsEnabledProperty, value);
    }

    public static string GetListSeparator(DependencyObject obj)
    {
        return (string)obj.GetValue(ListSeparatorProperty);
    }

    public static void SetListSeparator(DependencyObject obj, string value)
    {
        obj.SetValue(ListSeparatorProperty, value);
    }

    /// <summary>入力を受けなかったときの警告音。範囲の外の値を戻すときも同じ音にする(計測時間)。</summary>
    public static void PlayInvalidInputSound()
    {
        SystemSounds.Beep.Play();
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            DataObject.AddPastingHandler(textBox, OnPasting);
        }
        else
        {
            textBox.PreviewTextInput -= OnPreviewTextInput;
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            DataObject.RemovePastingHandler(textBox, OnPasting);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox
            && e.Text.Length > 0
            && !IsAccepted(textBox, e.Text))
        {
            e.Handled = true;
            PlayInvalidInputSound();
        }
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox
            || !e.DataObject.GetDataPresent(DataFormats.Text)
            || e.DataObject.GetData(DataFormats.Text) is not string pastedText
            || pastedText.Length == 0
            || !IsAccepted(textBox, pastedText))
        {
            e.CancelCommand();
            PlayInvalidInputSound();
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // スペースは文字の入力(PreviewTextInput)を通らないので、ここで止める。
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            PlayInvalidInputSound();
        }
    }

    /// <summary>入れる文字(選んでいる範囲と入れ替わる)を受けるか。区切りのある欄は、入れた後の文字で区切りのあいだの桁も見る。</summary>
    private static bool IsAccepted(TextBox textBox, string insertedText)
    {
        var separator = GetListSeparator(textBox);
        if (separator.Length == 0)
        {
            return insertedText.All(char.IsAsciiDigit);
        }

        if (!insertedText.All(character => char.IsAsciiDigit(character) || separator.Contains(character)))
        {
            return false;
        }

        var resultText = textBox.Text
            .Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, insertedText);
        return resultText
            .Split(separator)
            .All(number => number.Length <= MaxDigitsPerNumber);
    }
}
