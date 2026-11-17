// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// チャットで共有された位置(リンクの発言の差し込みの値)。マップの窓がその場所と階層の地図を出し、印を置く。
/// ゲームもリンクから開いたマップでは、リンクの階層の地図を出す。
/// 高さは地図に要らないので持たない。
/// </summary>
/// <param name="SceneId">その位置のあるシーンの番号。</param>
/// <param name="LineId">そのシーンのチャンネル。</param>
/// <param name="X">ワールド座標の x(ゲームが自分の位置を切り上げた整数)。</param>
/// <param name="Z">ワールド座標の z(ゲームが自分の位置を切り上げた整数)。</param>
/// <param name="Layer">共有した人がそのときいた階層(地上 0、地下 -1)。</param>
public sealed record MapSharedPosition(int SceneId, uint LineId, float X, float Z, int Layer);
