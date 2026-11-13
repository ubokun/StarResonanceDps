// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 見出し表で名前を持たないメーターの行について、記録時に付与元をたどって着いた先の種類。
/// 名前はこの先から引く(<see cref="CombatDataCatalog.GetSourceDisplayName"/>)。
/// </summary>
public enum SourceLandingKind
{
    /// <summary>着いていない。</summary>
    None = 0,

    /// <summary>オプションのバフ(<c>RogueEntryTable.BuffId</c>)。名前は <c>RogueEntryNames</c>。</summary>
    RogueEntry = 1,

    /// <summary>プレイヤーが使った技。名前は技の名前(<c>SkillTalkNames</c> → <c>SkillNames</c>)。</summary>
    Skill = 2,

    /// <summary>
    /// モジュール効果の根のバフ(<c>ModEffectTable.EffectConfig</c> の <c>[3, バフID, 1]</c>)。名前はその効果の名前(<c>ModuleEffectNames</c>)。
    /// バフの由来(<c>SourceLandingResolver.ResolveBuffOrigin</c>)だけが着く。メーターの行は着かない。
    /// </summary>
    Module = 3,
}

/// <summary>着地先。<paramref name="Id"/> はオプションのバフID、技ID、モジュール効果の根のバフIDのどれか(<paramref name="Kind"/> で決まる)。</summary>
public readonly record struct SourceLanding(SourceLandingKind Kind, int Id)
{
    public static SourceLanding None => default;

    public override string ToString() => Kind switch
    {
        SourceLandingKind.RogueEntry => $"option {Id}",
        SourceLandingKind.Skill => $"skill {Id}",
        SourceLandingKind.Module => $"module root {Id}",
        _ => "none"
    };
}
