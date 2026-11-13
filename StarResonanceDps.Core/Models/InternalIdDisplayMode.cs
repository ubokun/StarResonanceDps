// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Models;

/// <summary>
/// 4言語テーブルから名前を引いたときに、参照した内部IDを添えるか。
/// 診断用の表示なので、既定は <see cref="Hidden"/>。
/// </summary>
public enum InternalIdDisplayMode
{
    Hidden = 0,
    All = 1,
    SkillOnly = 2,
    BuffOnly = 3,
    EntityOnly = 4,
    MapOnly = 5
}
