// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>保存した回の、自分のアクションバーの1枠。レベルと改造段階は保存の時点の値。</summary>
public sealed record RecordedActionBarSlot(int SlotId, int SkillId, int Level, int Tier);

/// <summary>
/// 保存した回の、自分にしか届かない値(<see cref="Entity.RecordedSelf"/>)。保存の直前に自分の控えから写す。
/// 履歴から来た行(そのキャラで今ログインしていないとき)は、この値で詳しく出す。項目ごとの null は、保存の時点でその控えが値を持っていなかった。
/// </summary>
/// <param name="Equipment">部位ごとの装備の値(<see cref="SelfEquipmentStore.GetSlots"/>)。</param>
/// <param name="ModuleEffects">装着中のモジュールの、効果ごとのリンク数の和(<see cref="SelfModuleStore.GetEquippedEffectLinks"/>)。</param>
/// <param name="Factors">その回のシーズンの、有効な型に付けている因子(<see cref="SelfSeasonCultivateStore.GetActiveFactors"/>)。</param>
/// <param name="ActionBar">アクションバーのイマジンとロールスキルの枠。空の枠は入れない。</param>
public sealed record RecordedSelfData(
    IReadOnlyList<SelfEquipmentSlot>? Equipment,
    IReadOnlyList<SelfModuleEffect>? ModuleEffects,
    IReadOnlyList<SelfSeasonFactor>? Factors,
    IReadOnlyList<RecordedActionBarSlot>? ActionBar)
{
    /// <summary>
    /// 今の自分の控えから写す。保存の直前の焼き付け(<c>EncounterManager.ApplyDisplayedIdentitiesForRecord</c>)だけが呼ぶ。
    /// 一覧は配列にして持つ(履歴の blob は具体的な型の名前で書くので、控えの一覧の型に依らない形にする)。
    /// </summary>
    internal static RecordedSelfData Capture(int seasonId, long selfUuid)
    {
        return new RecordedSelfData(
            SelfEquipmentStore.GetSlots()?.ToArray(),
            SelfModuleStore.GetEquippedEffectLinks()?.ToArray(),
            seasonId > 0 ? SelfSeasonCultivateStore.GetActiveFactors(seasonId)?.ToArray() : null,
            MeterSnapshotProvider.CaptureSelfActionBar(selfUuid));
    }
}
