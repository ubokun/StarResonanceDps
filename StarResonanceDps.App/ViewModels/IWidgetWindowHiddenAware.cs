// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// ウィジェットの窓が隠れている間(全体設定「ゲーム非アクティブ時にウィジェットを隠す」)、定期の更新を止める中身。
/// 窓(<c>WidgetWindow</c>)が中身の画面とそのビューモデルに、隠れた・見えたを知らせる。
/// 見えたら1回読み直してから更新を再開する。
/// </summary>
public interface IWidgetWindowHiddenAware
{
    void SetWindowHidden(bool isHidden);
}
