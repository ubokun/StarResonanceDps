// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Controls;

namespace StarResonanceDps.App.Views.WidgetSettings;

/// <summary>
/// 書式のカスタマイズ欄へ項目を差し込む共通処理。
///
/// <para>
/// カーソル位置はViewModelから見えないので、挿入はビュー側でやる。
/// 書式の欄(プレイヤー名・エンティティ名・バフ名・装備名・スキル名・通知の文章)はどれも同じ振る舞いをする。
/// </para>
/// </summary>
internal static class FormatFieldInsertion
{
    /// <summary>カーソル位置へ差し込む。文字を選んでいればそれを置き換える。</summary>
    public static void Insert(TextBox textBox, string? placeholder)
    {
        if (string.IsNullOrEmpty(placeholder))
        {
            return;
        }

        var insertAt = textBox.SelectionStart;
        textBox.SelectedText = placeholder;
        textBox.CaretIndex = insertAt + placeholder.Length;
        textBox.Focus();
    }

    /// <summary>
    /// 入力欄に触れていない間はカーソルを末尾に置く。
    ///
    /// <para>
    /// WPFの初期カーソル位置は0なので、そのままだと<b>一度もクリックしていない欄で先頭に入る</b>。
    /// 従来の「末尾に追加」と食い違うので、フォーカスが無いうちは末尾へ寄せておく。
    /// 設定の読み込み直しやリセットで中身が入れ替わったときも同じ。
    /// </para>
    /// </summary>
    public static void MoveCaretToEndWhenUnfocused(TextBox textBox)
    {
        if (!textBox.IsKeyboardFocusWithin)
        {
            textBox.CaretIndex = textBox.Text.Length;
        }
    }
}
