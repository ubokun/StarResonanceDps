// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 設定の一覧に入れるバフの鍵。バフの番号(畳んだバフは畳み先の番号、一覧の行の <c>BaseId</c>)か、
/// 料理・薬剤のまとまり(<see cref="BuffGroup"/>)のどちらか。まとまりの鍵は番号を持たない(料理・薬剤は品ごとに番号が違う)。
/// </summary>
public readonly record struct BuffEntryKey(int BuffId, BuffGroup Group)
{
    public static BuffEntryKey ForBuff(int buffId) => new(buffId, BuffGroup.None);

    public static BuffEntryKey ForGroup(BuffGroup group) => new(0, group);

    /// <summary>
    /// バフ・デバフグループの行の鍵。料理・薬剤のバフはまとまりの鍵にする(同じ料理・薬剤が品ごとに別の行にならないように)。
    /// </summary>
    public static BuffEntryKey ForBuffDebuffGroup(int buffId)
    {
        var group = CombatDataCatalog.GetBuffGroup(buffId);
        return group != BuffGroup.None ? ForGroup(group) : ForBuff(buffId);
    }

    public bool IsGroup => Group != BuffGroup.None;

    /// <summary>番号の鍵なら番号が正、まとまりの鍵なら料理か薬剤。</summary>
    public bool IsValid => IsGroup
        ? BuffId == 0 && (Group is BuffGroup.Cuisine or BuffGroup.Potion)
        : BuffId > 0;
}
