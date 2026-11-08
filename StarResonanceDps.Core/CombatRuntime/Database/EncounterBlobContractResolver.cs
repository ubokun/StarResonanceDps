using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime.Database;

/// <summary>
/// エンカウンターの blob に書く項目を決める。<b>許可制</b>で、履歴表示が読むものだけを書く。
///
/// <para>
/// 読み手は <c>MeterSnapshotProvider</c> の履歴に追従する入口(メーター・スキル詳細・詳細・グラフ・タイムライン・被ダメログ)、
/// プレイヤーリストの顔ぶれ(<c>PlayerRosterProjection.RebuildRoster</c>)と、そこから呼ばれる
/// <c>PlayerDataSourceResolver</c>。<b>履歴で読む項目を足したら、ここにも足さないと履歴で既定値になる。</b>
/// </para>
///
/// <para>
/// 書き込み専用。読み込みは <see cref="PrivateResolver"/> で、書かれていない項目は既定値のまま残る。
/// </para>
/// </summary>
internal sealed class EncounterBlobContractResolver : DefaultContractResolver
{
    private static readonly Dictionary<Type, string[]> AllowedMembers = new()
    {
        [typeof(Entity)] =
        [
            nameof(Entity.UUID),
            nameof(Entity.UID),
            nameof(Entity.Name),
            nameof(Entity.EntityType),
            nameof(Entity.Level),
            nameof(Entity.AbilityScore),
            nameof(Entity.ProfessionId),
            nameof(Entity.SubProfessionId),
            nameof(Entity.HasBuffSnapshot),
            nameof(Entity.SeasonTalentBuffId),
            nameof(Entity.HasSeasonTalentSnapshot),
            nameof(Entity.IsNpc),
            nameof(Entity.SeasonLevel),
            nameof(Entity.SeasonStrength),
            nameof(Entity.Hp),
            nameof(Entity.MaxHp),
            nameof(Entity.TotalDamage),
            nameof(Entity.TotalHealing),
            nameof(Entity.TotalOverhealing),
            nameof(Entity.TotalShieldBreak),
            nameof(Entity.TotalCasts),
            nameof(Entity.TotalInactiveTime),
            nameof(Entity.FirstCombatActionTime),
            nameof(Entity.LastCombatActionTime),
            nameof(Entity.DamageStats),
            nameof(Entity.HealingStats),
            nameof(Entity.TakenStats),
            nameof(Entity.SkillMetrics),
            nameof(Entity.Attributes),
            nameof(Entity.SkillCasts),
            nameof(Entity.RecordedAvatar),
            nameof(Entity.RecordedBuild),
            nameof(Entity.RecordedBuffTransformation),
            nameof(Entity.RootBuffIds),
            nameof(Entity.TalentNodeIdsByProfession),
            nameof(Entity.LastReplaceableSkills),
        ],
        [typeof(SocialAvatarState)] =
        [
            nameof(SocialAvatarState.AvatarId),
            nameof(SocialAvatarState.ProfileUrl),
            nameof(SocialAvatarState.IsProfileReviewed),
            nameof(SocialAvatarState.HalfBodyUrl),
            nameof(SocialAvatarState.IsHalfBodyReviewed),
        ],
        [typeof(CombatStats)] =
        [
            nameof(CombatStats.Id),
            nameof(CombatStats.Name),
            nameof(CombatStats.Level),
            nameof(CombatStats.ValueTotal),
            nameof(CombatStats.ValueNormalTotal),
            nameof(CombatStats.ValueCritTotal),
            nameof(CombatStats.ValueLuckyTotal),
            nameof(CombatStats.ValueAverage),
            nameof(CombatStats.HitsCount),
            nameof(CombatStats.CritCount),
            nameof(CombatStats.CritRate),
            nameof(CombatStats.LuckyCount),
            nameof(CombatStats.LuckyHitCount),
            nameof(CombatStats.LuckyRate),
            nameof(CombatStats.NormalCount),
            nameof(CombatStats.NormalRate),
            nameof(CombatStats.ImmuneCount),
            nameof(CombatStats.SkillSnapshots),
            nameof(CombatStats.PerSecondTotals),
            nameof(CombatStats.LastPerSecondTimestamp),
            nameof(CombatStats.ValueTotalByElement),
            nameof(CombatStats.ValueTotalByMode),
        ],
        [typeof(MetricsContainer)] =
        [
            nameof(MetricsContainer.Damage),
            nameof(MetricsContainer.Healing),
            nameof(MetricsContainer.IsBuffSource),
            nameof(MetricsContainer.LandingKind),
            nameof(MetricsContainer.LandingId),
        ],
        [typeof(SkillSnapshot)] =
        [
            nameof(SkillSnapshot.Id),
            nameof(SkillSnapshot.OtherUUID),
            nameof(SkillSnapshot.Value),
            nameof(SkillSnapshot.DamageElement),
            nameof(SkillSnapshot.DamageMode),
            nameof(SkillSnapshot.Timestamp),
            nameof(SkillSnapshot.Sequence),
            nameof(SkillSnapshot.OwnerId),
            nameof(SkillSnapshot.DamageSource),
            nameof(SkillSnapshot.BuffSourceSkillId),
            nameof(SkillSnapshot.SummonSourceSkillId),
            nameof(SkillSnapshot.IsKill),
        ],
        [typeof(SkillCastRecord)] =
        [
            nameof(SkillCastRecord.SkillId),
            nameof(SkillCastRecord.Timestamp),
            nameof(SkillCastRecord.Sequence),
            nameof(SkillCastRecord.IsWarning),
        ],
    };

    /// <summary>
    /// プレイヤー以外のエンティティで書く項目。履歴で読むのはタイムライン・被ダメログの加害者名(<c>AttrId</c>)と詠唱だけ。
    /// <c>UUID</c> はコンストラクタの引数なので外せない。
    /// </summary>
    private static readonly HashSet<string> NonPlayerEntityMembers =
    [
        nameof(Entity.UUID),
        nameof(Entity.Hp),
        nameof(Entity.MaxHp),
        nameof(Entity.Attributes),
        nameof(Entity.SkillCasts),
    ];

    /// <summary>
    /// <see cref="Entity.Attributes"/> のうち書くキー。装備(<c>AttrEquipData</c>)、HP、種別ID、シーズン階級(<c>AttrRankLevel</c>)、習得スキル。
    /// </summary>
    private static readonly HashSet<string> PersistedAttributeKeys =
    [
        "AttrEquipData",
        "AttrHp",
        "AttrId",
        "AttrMaxHp",
        "AttrRankLevel",
        "AttrSkillLevelIdList",
        "AttrTargetUuid",
        "AttrTargetId",
    ];

    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        var properties = base.CreateProperties(type, memberSerialization);
        if (!AllowedMembers.TryGetValue(type, out var allowed))
        {
            return properties;
        }

        var kept = properties
            .Where(property => allowed.Contains(property.UnderlyingName))
            .ToList();

        // nameof で書いてあるので改名は追従するが、JsonIgnore などで契約から消えた項目はここで分かる。
        var missing = allowed.Except(kept.Select(property => property.UnderlyingName!)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Encounter blob allowlist for {type.Name} names members that are not serializable: {string.Join(", ", missing)}");
        }

        if (type == typeof(Entity))
        {
            foreach (var property in kept)
            {
                if (property.UnderlyingName == nameof(Entity.Attributes))
                {
                    property.ValueProvider = new PersistedAttributesValueProvider(property.ValueProvider!);
                }

                if (!NonPlayerEntityMembers.Contains(property.UnderlyingName!))
                {
                    var shouldSerialize = property.ShouldSerialize;
                    property.ShouldSerialize = instance =>
                        ((Entity)instance).EntityType == EEntityType.EntChar
                        && (shouldSerialize is null || shouldSerialize(instance));
                }
            }
        }

        return kept;
    }

    /// <summary>書くときだけ <see cref="PersistedAttributeKeys"/> に絞った複製を返す。元の辞書には触らない。</summary>
    private sealed class PersistedAttributesValueProvider(IValueProvider inner) : IValueProvider
    {
        public object? GetValue(object target)
        {
            var attributes = (Dictionary<string, object>?)inner.GetValue(target);
            return attributes?
                .Where(pair => PersistedAttributeKeys.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        public void SetValue(object target, object? value)
        {
            inner.SetValue(target, value);
        }
    }
}
