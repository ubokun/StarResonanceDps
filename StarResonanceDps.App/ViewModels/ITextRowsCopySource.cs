// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Behaviors;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 行をまたいで文字を選べる一覧の ViewModel(一覧の <c>DataContext</c>)が実装する取り決め。
/// 選ぶ・地を描く・コピーの操作(右クリックの「コピー」と Ctrl+C)・クリップボードへ入れるのは画面の振る舞い(<c>Behaviors.TextRowsSelectionBehavior</c>)が受け持ち、
/// ここでは選んだ範囲を文字にするだけを決める。
/// </summary>
public interface ITextRowsCopySource
{
    /// <summary>
    /// 選んだ範囲の文字。行の文字の数え方は振る舞いと同じ(部品の文字を名前の順に並べ、空でない部品の後ろに区切りの1文字。最後の部品の後ろには付けない)。
    /// </summary>
    string CreateCopyText(TextRowsSelection selection);
}
