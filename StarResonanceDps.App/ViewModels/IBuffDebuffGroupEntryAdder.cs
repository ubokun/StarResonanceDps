// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// バフ・デバフ一覧とカードの右クリック「グループに追加」の行き先(バフ・デバフグループのウィジェット)。
/// 窓の管理(<c>Services.WidgetWindowManager</c>)が実装し、一覧とカードの ViewModel へ渡す。
/// </summary>
public interface IBuffDebuffGroupEntryAdder
{
    /// <summary>その鍵の行を足せるか。グループにまだオンで入っていなければ真(オフの行はオンに戻せるので真)。</summary>
    bool CanAdd(BuffEntryKey key);

    /// <summary>その鍵の行をグループに足す(オフの行ならオンに戻す)。保存まで行う。</summary>
    void Add(BuffEntryKey key);
}
