// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 推移グラフの横軸の下に出す技のアイコン1つ。値は整形済みで、作るのは ViewModel。
/// </summary>
/// <param name="Seconds">技の開始の、戦闘の時計の経過(秒)。</param>
/// <param name="IconPath">技のアイコンのファイル(<c>CombatIconResolver.ResolveSkillIcon</c>)。無ければ null で、クラス不明のアイコンで出す。</param>
/// <param name="Name">TIPS に出す技の名前。名前が無ければ「不明」。</param>
/// <param name="Frame">アイコンの背景の枠。</param>
/// <param name="UsesImagineAsset">アイコンがイマジンの絵か(大きさと位置の調整を変える)。</param>
public sealed record MetricGraphSkillMarker(
    double Seconds,
    string? IconPath,
    string Name,
    SkillIconFrame Frame,
    bool UsesImagineAsset);
