using Newtonsoft.Json.Linq;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

internal sealed record PlayerDataSourceSnapshot(
    long CharacterId,
    string Name,
    int ProfessionId,
    int CombatPower,
    int SubProfessionId,
    int SeasonStrength,
    int Level,
    int SeasonLevel,
    long CurrentHp,
    long MaxHp,
    bool IsIncapacitated,
    bool IsNpc,
    bool IsSpecAbilityUnequipped,
    PlayerClassSpec ClassSpec,
    PlayerEquipmentData? EquipmentData,
    bool IsLive,
    int SeasonTalentBuffId,
    bool IsSeasonTalentInactive,
    int? SeasonRankLevel,
    PlayerRootBuffData? RootBuffData,
    IReadOnlyList<int>? TalentNodeIds,
    IReadOnlyDictionary<int, int>? LastReplaceableSkills,
    PlayerDataGaps DataGaps);

internal static class PlayerDataSourceResolver
{
    public static PlayerDataSourceSnapshot Resolve(Entity entity, bool isSelf)
    {
        var characterId = entity.UID != 0
            ? entity.UID
            : Utils.UuidToEntityId(entity.UUID);

        return Resolve(characterId, entity, entity, isSelf);
    }

    public static PlayerDataSourceSnapshot Resolve(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf,
        bool recordedOnly = false)
    {
        // PT補完は自分にも使う。<b>ただし最後の手段</b>で、空の項目しか埋めない。
        // ロード済みで起動すると自分の HP/最大HP は AttrHp/AttrMaxHp が来ず、
        // マップ移動まで 0 のままになる。PT側は最初から値を持っている。
        var party = PartyStateStore.Instance.Current;
        PartyMemberSupplement? partySupplement = null;
        if (!recordedOnly && party.TryGetSupplement(characterId, out var supplement))
        {
            partySupplement = supplement;
        }

        var nearbyName = nearbyEntity is { Name.Length: > 0 }
            ? nearbyEntity.Name
            : GetString(nearbyEntity, "AttrName");
        var nearbyProfessionId = nearbyEntity is { ProfessionId: > 0 }
            ? nearbyEntity.ProfessionId
            : GetInt(nearbyEntity, "AttrProfessionId");
        var nearbyCombatPower = nearbyEntity is { AbilityScore: > 0 }
            ? nearbyEntity.AbilityScore
            : GetInt(nearbyEntity, "AttrFightPoint");
        var nearbySeasonStrength = nearbyEntity is { SeasonStrength: > 0 }
            ? ToInt32(nearbyEntity.SeasonStrength)
            : GetFirstNonZeroInt(
                nearbyEntity,
                "AttrSeasonStrength",
                "AttrSeasonStrengthTotal",
                "AttrSeasonStrengthAdd",
                "AttrSeasonStrengthExAdd",
                "AttrSeasonStrengthPer",
                "AttrSeasonStrengthExPer");
        var nearbyLevel = nearbyEntity is { Level: > 0 }
            ? nearbyEntity.Level
            : GetInt(nearbyEntity, "AttrLevel");
        var nearbySeasonLevel = ToInt32(nearbyEntity?.SeasonLevel);
        if (nearbySeasonLevel <= 0)
        {
            nearbySeasonLevel = GetInt(nearbyEntity, "AttrSeasonLevel");
        }

        var metadataName = metadataEntity is { Name.Length: > 0 }
            ? metadataEntity.Name
            : GetString(metadataEntity, "AttrName");
        var metadataProfessionId = metadataEntity is { ProfessionId: > 0 }
            ? metadataEntity.ProfessionId
            : GetInt(metadataEntity, "AttrProfessionId");
        var metadataCombatPower = metadataEntity is { AbilityScore: > 0 }
            ? metadataEntity.AbilityScore
            : GetInt(metadataEntity, "AttrFightPoint");
        var metadataSeasonStrength = metadataEntity is { SeasonStrength: > 0 }
            ? ToInt32(metadataEntity.SeasonStrength)
            : GetFirstNonZeroInt(
                metadataEntity,
                "AttrSeasonStrength",
                "AttrSeasonStrengthTotal",
                "AttrSeasonStrengthAdd",
                "AttrSeasonStrengthExAdd",
                "AttrSeasonStrengthPer",
                "AttrSeasonStrengthExPer");
        var metadataLevel = metadataEntity is { Level: > 0 }
            ? metadataEntity.Level
            : GetInt(metadataEntity, "AttrLevel");
        var metadataSeasonLevel = ToInt32(metadataEntity?.SeasonLevel);
        if (metadataSeasonLevel <= 0)
        {
            metadataSeasonLevel = GetInt(metadataEntity, "AttrSeasonLevel");
        }

        // AOI外だと nearbyEntity が無い。エンカウンターに残るエンティティが最後に観測したHPを
        // 持っているので、そこから拾う。PT外はこれが唯一の経路。
        //
        // hasNearbyMaxHp は nearbyEntity 基準のままにしてある。PTメンバーでは
        // 下の social data(AOI外でもライブに届く)が引き続き勝つ。
        var hpEntity = nearbyEntity ?? metadataEntity;
        var currentHp = hpEntity?.Hp ?? 0;
        var maxHp = hpEntity?.MaxHp ?? 0;
        var hasNearbyMaxHp = nearbyEntity is not null
            && TryGetPositiveInt64(nearbyEntity.GetAttrKV("AttrMaxHp"), out _);

        // 戦闘不能かは HP を取った出所で決める。実体の HP は戦闘不能なら 0 になる。
        // パーティの同期の HP は読み込み中などの生きている人にも 0 で届く(ゲームの HP バーも空になる)ので、同期の状態で決める。
        var isIncapacitated = maxHp > 0 && currentHp <= 0;
        if (!hasNearbyMaxHp
            && partySupplement is not null)
        {
            if (partySupplement.MaxHp > 0)
            {
                maxHp = partySupplement.MaxHp;
            }

            if (nearbyEntity is not null
                && TryGetNonNegativeInt64(nearbyEntity.GetAttrKV("AttrHp"), out var nearbyHp))
            {
                currentHp = nearbyHp;
                isIncapacitated = maxHp > 0 && currentHp <= 0;
            }
            else if (partySupplement.MaxHp > 0 || !partySupplement.IsNpc)
            {
                currentHp = Math.Max(partySupplement.CurrentHp, 0);
                isIncapacitated = partySupplement.IsDead;
            }
        }

        var professionId = nearbyProfessionId > 0
            ? nearbyProfessionId
            : partySupplement is { ProfessionId: > 0 }
                ? partySupplement.ProfessionId
                : metadataProfessionId;
        var subProfessionId = recordedOnly ? metadataEntity?.SubProfessionId ?? 0
            : ResolveSubProfessionId(characterId, nearbyEntity, metadataEntity, isSelf);
        var isSpecAbilityUnequipped = recordedOnly ? metadataEntity?.IsSpecAbilityUnequipped == true
            : ResolveSpecAbilityUnequipped(characterId, nearbyEntity, metadataEntity, isSelf);

        return new PlayerDataSourceSnapshot(
            characterId,
            !string.IsNullOrEmpty(nearbyName)
                ? nearbyName
                : partySupplement is { Name.Length: > 0 }
                    ? partySupplement.Name
                    : metadataName,
            professionId,
            nearbyCombatPower > 0
                ? nearbyCombatPower
                : partySupplement is { CombatPower: > 0 }
                    ? partySupplement.CombatPower
                    : metadataCombatPower,
            subProfessionId,
            nearbySeasonStrength > 0
                ? nearbySeasonStrength
                : partySupplement is { SeasonStrength: > 0 }
                    ? partySupplement.SeasonStrength
                    : metadataSeasonStrength,
            nearbyLevel > 0
                ? nearbyLevel
                : partySupplement is { Level: > 0 }
                    ? partySupplement.Level
                    : metadataLevel,
            nearbySeasonLevel > 0
                ? nearbySeasonLevel
                : partySupplement is { SeasonLevel: > 0 }
                    ? partySupplement.SeasonLevel
                    : metadataSeasonLevel,
            currentHp,
            maxHp,
            isIncapacitated,
            // NPC か。パーティの社交データ(BotAiId)、実体の AttrId(プレイヤーの実体ではボットの表の番号で、NPC だけが持つ)、
            // 実体に焼き付けた印のどれかで決める。パーティにいない NPC は AttrId だけが根拠になる。
            !isSelf && (partySupplement?.IsNpc == true
                || GetInt(nearbyEntity, "AttrId") > 0
                || GetInt(metadataEntity, "AttrId") > 0
                || metadataEntity?.IsNpc == true),
            isSpecAbilityUnequipped,
            PlayerClassSpecResolver.Resolve(
                professionId,
                subProfessionId,
                isSpecAbilityUnequipped,
                ResolveBuffTransformation(nearbyEntity, metadataEntity)),
            GetEquipmentData(nearbyEntity)
                ?? partySupplement?.EquipmentData
                ?? GetEquipmentData(metadataEntity),
            // いま値が供給されているか(=ライブ)。キャッシュしか無い行は表示側で灰色にする。
            //
            // 判定はパーティを特別扱いしない。<see cref="PartyStateStore.PartyStateSnapshot.TryGetSupplement"/> は
            // 在籍中のメンバーにしか返さないので、PT外では自動的に「AOIにいるか」だけになる。
            //   nearbyEntity … AOIから届いている現在値
            //   partySupplement … TeamMemberFastSyncData がHPを供給する。AOIとは独立に届く
            //   metadataEntity … 最後の観測値が居残っているだけ。これしか無ければキャッシュ
            //
            // <b>自分だけは常にライブ。</b> 作り直した直後のエンカウンターには自分の
            // エンティティがまだ無く、導出だと一瞬だけ灰に落ちる。
            isSelf || nearbyEntity is not null || partySupplement is not null,
            recordedOnly ? metadataEntity?.SeasonTalentBuffId ?? 0 : ResolveSeasonTalentBuffId(characterId, nearbyEntity, metadataEntity, isSelf),
            recordedOnly ? metadataEntity?.IsSeasonTalentInactive == true : ResolveSeasonTalentInactive(characterId, nearbyEntity, metadataEntity, isSelf),
            ResolveSeasonRankLevel(nearbyEntity, metadataEntity),
            ResolveRootBuffData(nearbyEntity, metadataEntity),
            ResolveTalentNodeIds(nearbyEntity, metadataEntity, professionId),
            nearbyEntity?.LastReplaceableSkills ?? metadataEntity?.LastReplaceableSkills,
            ResolveDataGaps(characterId, nearbyEntity, metadataEntity, partySupplement, isSelf));
    }

    /// <summary>
    /// バフで決まる変身(ミーン・ゴーレム)。保存した回の実体は記録で決まり、今のバフを見ない。
    /// ライブの実体は記録を持たないので今のバフで決まる。周りにいない人の今のバフは見ない。
    /// </summary>
    private static PlayerClassSpec? ResolveBuffTransformation(Entity? nearbyEntity, Entity? metadataEntity)
    {
        return nearbyEntity is not null
            ? nearbyEntity.RecordedBuffTransformation ?? PlayerClassSpecResolver.GetBuffTransformation(nearbyEntity.UUID)
            : metadataEntity?.RecordedBuffTransformation;
    }

    /// <summary>
    /// 他のプレイヤーについて、まだ届いていない情報(<see cref="PlayerDataGaps"/>)。自分はいつも無し。
    /// 出現時の情報は周りの実体 → エンカウンターの実体の順に見る(実体は周りから外れても印を残す)。
    /// </summary>
    private static PlayerDataGaps ResolveDataGaps(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        PartyMemberSupplement? partySupplement,
        bool isSelf)
    {
        if (isSelf)
        {
            return PlayerDataGaps.None;
        }

        var gaps = PlayerDataGaps.None;
        var hasAppearAttributes = nearbyEntity?.HasAppearAttributes == true || metadataEntity?.HasAppearAttributes == true;
        if (!hasAppearAttributes)
        {
            gaps |= PlayerDataGaps.AppearAttributes;
            if (partySupplement?.HasSocialData != true)
            {
                gaps |= PlayerDataGaps.Identity;
            }
        }

        var hasSkillLevels = (nearbyEntity ?? metadataEntity)?.GetAttrKV("AttrSkillLevelIdList") is not null
            || PartyMemberCache.Instance.TryGetSkillLevels(characterId, out _);
        if (!hasSkillLevels)
        {
            gaps |= PlayerDataGaps.SkillLevels;
        }

        return gaps;
    }

    /// <summary>
    /// 根のバフの一覧(<see cref="Entity.RootBuffIds"/>)。周りの実体 → エンカウンターの実体の順。
    /// 実体は周りから外れても一覧を消さないので、エンカウンターの実体には最後に受けた値が残る。履歴の行は保存した値。
    /// 全部をまだ受けていなければ null。
    /// </summary>
    private static PlayerRootBuffData? ResolveRootBuffData(Entity? nearbyEntity, Entity? metadataEntity)
    {
        var buffIds = nearbyEntity?.RootBuffIds ?? metadataEntity?.RootBuffIds;
        return buffIds is null ? null : PlayerRootBuffData.FromSorted(buffIds);
    }

    /// <summary>
    /// 今の職業の取ったタレントの木の節(<see cref="Entity.TalentNodeIdsByProfession"/>)。周りの実体 → エンカウンターの実体の順。
    /// 自分にしか届かないので、ほかのプレイヤーはいつも null。届いた一覧にその職業が無くても null。履歴の行は保存した値。
    /// 配列は実体の持つものをそのまま渡す(中身が変わらなければ同じ参照で、名簿の行の知らせが出ない)。
    /// </summary>
    private static IReadOnlyList<int>? ResolveTalentNodeIds(Entity? nearbyEntity, Entity? metadataEntity, int professionId)
    {
        var nodeIdsByProfession = nearbyEntity?.TalentNodeIdsByProfession ?? metadataEntity?.TalentNodeIdsByProfession;
        return nodeIdsByProfession is not null && nodeIdsByProfession.TryGetValue(professionId, out var nodeIds)
            ? nodeIds
            : null;
    }

    /// <summary>
    /// シーズンランクの段階(属性 <c>AttrRankLevel</c> = <c>SeasonRankTable.RankToLevel</c>)。
    /// シーズンレベルと同じ順で、周りの実体 → エンカウンターの実体。段階 0 があるので、属性が届いていなければ null。
    /// </summary>
    private static int? ResolveSeasonRankLevel(Entity? nearbyEntity, Entity? metadataEntity)
    {
        var value = nearbyEntity?.GetAttrKV("AttrRankLevel") ?? metadataEntity?.GetAttrKV("AttrRankLevel");
        return value is null ? null : ToInt32(value);
    }

    /// <summary>
    /// 有効化しているシーズンタレントの型(根ノードのバフID)を解決する。特化(<see cref="ResolveSubProfessionId"/>)と同じ順で、
    /// 実体 → 控え(パーティ → メーター)。自分は控えを見ない。変身中でも型は変わらないので、変身の関門は置かない。
    /// </summary>
    private static int ResolveSeasonTalentBuffId(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        if (nearbyEntity is { SeasonTalentBuffId: > 0 })
        {
            return nearbyEntity.SeasonTalentBuffId;
        }

        if (metadataEntity is { SeasonTalentBuffId: > 0 })
        {
            return metadataEntity.SeasonTalentBuffId;
        }

        if (isSelf)
        {
            return 0;
        }

        return TryResolveCachedSeasonTalent(characterId, out var cached, out _) ? cached : 0;
    }

    /// <summary>
    /// 控えからシーズンタレントの型を補完する。<b>型と無効の印を必ず同じ出所から返す</b>(特化の <see cref="TryResolveCachedSpec"/> と同じ理由)。
    /// </summary>
    private static bool TryResolveCachedSeasonTalent(
        long characterId,
        out int rootBuffId,
        out bool isInactive)
    {
        var partyCache = PartyMemberCache.Instance;
        if (partyCache.TryGetSeasonTalent(characterId, out rootBuffId))
        {
            isInactive = false;
            return true;
        }

        if (partyCache.IsSeasonTalentInactive(characterId))
        {
            rootBuffId = 0;
            isInactive = true;
            return true;
        }

        var meterCache = MeterPlayerSpecCache.Instance;
        if (meterCache.TryGetSeasonTalent(characterId, out rootBuffId))
        {
            isInactive = false;
            return true;
        }

        if (meterCache.IsSeasonTalentInactive(characterId))
        {
            rootBuffId = 0;
            isInactive = true;
            return true;
        }

        rootBuffId = 0;
        isInactive = false;
        return false;
    }

    /// <summary>
    /// シーズンタレントの型が無効と確定しているか。全バフスナップショットを受け取った上で根ノードのバフが無かったときだけ true
    /// (<see cref="Entity.IsSeasonTalentInactive"/>)。
    /// </summary>
    private static bool ResolveSeasonTalentInactive(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        if (nearbyEntity?.IsSeasonTalentInactive == true
            || metadataEntity?.IsSeasonTalentInactive == true)
        {
            return true;
        }

        return !isSelf
            && TryResolveCachedSeasonTalent(characterId, out _, out var cachedInactive)
            && cachedInactive;
    }

    /// <summary>
    /// 職業特化を解決する。
    ///
    /// <para>
    /// 真値は特化マーカーバフの観測結果で、エンティティが持っている値。相手がAOI外に出ると
    /// 観測できなくなるので、自分以外のパーティメンバーだけキャッシュから補完する
    /// (イマジン/ロールスキルと同じ扱い)。自分はキャッシュを見ない。
    /// </para>
    /// </summary>
    private static int ResolveSubProfessionId(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        // 変身クラス(8/14/15)は特化を持たない。キャッシュも見ない。
        // Entity 側の関門だけでは、変身前の特化がキャッシュから補完されて残ってしまう。
        var professionId = nearbyEntity is { ProfessionId: > 0 }
            ? nearbyEntity.ProfessionId
            : metadataEntity?.ProfessionId ?? 0;
        if (Models.PlayerClassSpecResolver.TryResolveTransformation(professionId, out _))
        {
            return 0;
        }

        if (nearbyEntity is { SubProfessionId: > 0 })
        {
            return nearbyEntity.SubProfessionId;
        }

        if (metadataEntity is { SubProfessionId: > 0 })
        {
            return metadataEntity.SubProfessionId;
        }

        if (isSelf)
        {
            return 0;
        }

        return TryResolveCachedSpec(characterId, out var cached, out _) ? cached : 0;
    }

    /// <summary>
    /// 保持している特化から補完する。<b>特化IDと未装着フラグを必ず同じソースから返す。</b>
    ///
    /// <para>
    /// 別々に引くと、片方がパーティのキャッシュ・もう片方がメーターのキャッシュに当たって
    /// 「特化はXだが未装着」という成立しない組み合わせが出る。
    /// </para>
    ///
    /// <para>
    /// 順序はパーティ(<see cref="PartyMemberCache"/>)が先。在籍中はそちらのほうが
    /// 更新機会が多く、パーティを抜けるまで生きる。次に
    /// <see cref="MeterPlayerSpecCache"/>(AOI外でもメーター・被ダメログ・タイムラインに残る人ぶん)。
    /// </para>
    /// </summary>
    private static bool TryResolveCachedSpec(
        long characterId,
        out int subProfessionId,
        out bool isSpecAbilityUnequipped)
    {
        var partyCache = PartyMemberCache.Instance;
        if (partyCache.TryGetSubProfession(characterId, out subProfessionId))
        {
            isSpecAbilityUnequipped = false;
            return true;
        }

        if (partyCache.IsSpecAbilityUnequipped(characterId))
        {
            subProfessionId = 0;
            isSpecAbilityUnequipped = true;
            return true;
        }

        var meterCache = MeterPlayerSpecCache.Instance;
        if (meterCache.TryGetSubProfession(characterId, out subProfessionId))
        {
            isSpecAbilityUnequipped = false;
            return true;
        }

        if (meterCache.IsSpecAbilityUnequipped(characterId))
        {
            subProfessionId = 0;
            isSpecAbilityUnequipped = true;
            return true;
        }

        subProfessionId = 0;
        isSpecAbilityUnequipped = false;
        return false;
    }

    /// <summary>
    /// 特化アビリティ未装着が確定しているか。
    ///
    /// <para>
    /// 「マーカーバフが無い」だけでは未装着と言えない。単に観測していないだけの可能性がある。
    /// 全バフスナップショットを受け取った上でマーカーが1つも無かったときだけ true になる
    /// (<see cref="Entity.IsSpecAbilityUnequipped"/>)。
    /// </para>
    /// </summary>
    private static bool ResolveSpecAbilityUnequipped(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        if (nearbyEntity?.IsSpecAbilityUnequipped == true
            || metadataEntity?.IsSpecAbilityUnequipped == true)
        {
            return true;
        }

        return !isSelf
            && TryResolveCachedSpec(characterId, out _, out var cachedUnequipped)
            && cachedUnequipped;
    }

    public static int GetInt(Entity? entity, string key)
    {
        return entity is null ? 0 : ToInt32(entity.GetAttrKV(key));
    }

    private static PlayerEquipmentData? GetEquipmentData(Entity? entity)
    {
        var rawData = entity?.GetAttrKV("AttrEquipData");
        if (rawData is null)
        {
            return null;
        }

        if (rawData is JArray serializedItems)
        {
            var parsedItems = serializedItems.ToObject<List<EquipNine>>();
            return parsedItems is null
                ? PlayerEquipmentData.Invalid
                : CreateEquipmentData(parsedItems);
        }

        if (rawData is IEnumerable<EquipNine> items)
        {
            return CreateEquipmentData(items);
        }

        return PlayerEquipmentData.Invalid;
    }

    private static PlayerEquipmentData CreateEquipmentData(IEnumerable<EquipNine> items)
    {
        return PlayerEquipmentData.Create(
            items.Select(item => new PlayerEquipmentItem(item.Slot, item.EquipID)));
    }

    private static int GetFirstNonZeroInt(Entity? entity, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = GetInt(entity, key);
            if (value != 0)
            {
                return value;
            }
        }

        return 0;
    }

    private static string GetString(Entity? entity, string key)
    {
        return entity?.GetAttrKV(key) as string ?? string.Empty;
    }

    private static bool TryGetPositiveInt64(object? value, out long result)
    {
        if (TryGetNonNegativeInt64(value, out result) && result > 0)
        {
            return true;
        }

        result = 0;
        return false;
    }

    private static bool TryGetNonNegativeInt64(object? value, out long result)
    {
        switch (value)
        {
            case long integer when integer >= 0:
                result = integer;
                return true;
            case int integer when integer >= 0:
                result = integer;
                return true;
            case uint integer:
                result = integer;
                return true;
            case ulong integer when integer <= long.MaxValue:
                result = (long)integer;
                return true;
            case short integer when integer >= 0:
                result = integer;
                return true;
            case ushort integer:
                result = integer;
                return true;
            case byte integer:
                result = integer;
                return true;
            case sbyte integer when integer >= 0:
                result = integer;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static int ToInt32(object? value)
    {
        return value switch
        {
            int integer => integer,
            long integer => ClampToInt32(integer),
            uint integer => integer > int.MaxValue ? int.MaxValue : (int)integer,
            ulong integer => integer > int.MaxValue ? int.MaxValue : (int)integer,
            short integer => integer,
            ushort integer => integer,
            byte integer => integer,
            sbyte integer => integer,
            _ => 0
        };
    }

    private static int ClampToInt32(long value)
    {
        return value switch
        {
            > int.MaxValue => int.MaxValue,
            < int.MinValue => int.MinValue,
            _ => (int)value
        };
    }
}
