// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// 窓の上で回したホイールを受ける中身。窓のどこで回しても(ヘッダー・余白・スクロールバーの上も)窓がここへ渡す。
/// 中の ScrollViewer などが先に受けたホイールは来ない。
/// </summary>
public interface IWidgetMouseWheelContent
{
    /// <summary>ホイールを受ける。<paramref name="delta"/> は奥へ回すと正。受けたら true(窓はそれ以上流さない)。</summary>
    bool HandleWidgetMouseWheel(int delta);
}
