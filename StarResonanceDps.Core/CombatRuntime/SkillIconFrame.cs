// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 技のアイコンの背景の枠。値は枠の画像の番号(<c>weap_role_skill_bg_0N</c>)。
/// 決め方は <see cref="CombatDataCatalog.GetSkillIconFrame"/>。
/// </summary>
public enum SkillIconFrame
{
    /// <summary>究極スキル・イマジンスキル以外の技(表に無い技も)。</summary>
    Standard = 1,

    /// <summary>イマジンスキル。</summary>
    Imagine = 2,

    /// <summary>究極スキル。</summary>
    Ultimate = 3
}
