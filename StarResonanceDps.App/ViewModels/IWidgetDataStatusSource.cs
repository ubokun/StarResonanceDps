// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// ウィジェットの窓の中身が持つ状態の印(<see cref="WidgetDataStatus"/>)。窓のタイトルの円とメイン窓のカードが読む。
///
/// <para>
/// 中身は自分の更新(名簿・一覧・定期の読み)のたびに計算し直し、変わったときだけ <see cref="DataStatusChanged"/> を上げる(画面のスレッドで)。
/// 受信の状態(<see cref="Core.CombatRuntime.GameReceptionStateStore"/>)が変わったときは、窓を束ねる側が <see cref="RefreshDataStatus"/> を呼ぶ。
/// 判定の条件は <see cref="WidgetDataStatusRules"/> にだけ書く。
/// </para>
/// </summary>
public interface IWidgetDataStatusSource
{
    WidgetDataStatus DataStatus { get; }

    event EventHandler? DataStatusChanged;

    /// <summary>今の値で状態を計算し直す。</summary>
    void RefreshDataStatus();
}
