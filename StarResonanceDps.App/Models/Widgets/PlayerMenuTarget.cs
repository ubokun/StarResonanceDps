// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// プレイヤーリストの行から開く窓の相手。行は位置に結びつき別の人に替わるので、右クリックのメニューは開いた時の値を持つ。
/// </summary>
/// <param name="CharacterId">相手のキャラID。</param>
/// <param name="IsSelf">自分か。メニューの「ステータス詳細」の出し分けに使う。</param>
public sealed record PlayerMenuTarget(long CharacterId, bool IsSelf);
