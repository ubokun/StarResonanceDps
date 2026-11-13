// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.DataTypes;

/// <summary>
/// ゲームの実行ファイルをどう特定するか。<c>Custom</c> のときだけ
/// <c>GameCaptureCustomExeName</c> を見る。
/// </summary>
public enum EGameCapturePreference
{
    Auto,
    Steam,
    Standalone,
    Epic,
    HaoPlaySea,
    XDG,
    HaoPlaySeaSteam,
    XDGSteam,
    WeGame,
    Custom = 200
}
