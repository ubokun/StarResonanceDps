// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

internal static class PlayerRosterProjection
{
    private static readonly object NearbyPlayerSync = new();
    private static readonly object PlayerEntitySync = new();
    private static readonly HashSet<long> NearbyPlayerUuids = [];
    private static readonly Dictionary<long, long> PlayerEntityUuidsByCharacterId = [];
    private static readonly PlayerRosterStore RosterStore = PlayerRosterStore.Instance;
    private static readonly object SelfAttributesSync = new();

    /// <summary>
    /// 最後に作った自分の属性の一覧。中身が同じなら同じ一覧を返し、名簿の行が「変わった」扱いにならないようにする
    /// (名簿の行の比べでは一覧は参照で比べるので、毎回新しい一覧だと値が同じでも名簿の通知が出て、
    /// 自分を映す窓とプレイヤーリストが作り直す)。名簿と同じ所(マップ移動・キャプチャの停止・ログアウト)で消す。
    /// </summary>
    private static IReadOnlyList<PlayerAttributeEntry>? _lastSelfAttributes;

    public static void BeginMap()
    {
        ForgetSelfAttributes();
        PreserveHumanPartySupplements();

        long[] previousNearbyPlayerUuids;
        lock (NearbyPlayerSync)
        {
            previousNearbyPlayerUuids = [.. NearbyPlayerUuids];
            NearbyPlayerUuids.Clear();
        }

        foreach (var playerUuid in previousNearbyPlayerUuids)
        {
            ClearTransientHumanSubProfession(playerUuid);
        }

        // マップ/チャンネルが変わったらAOI外の特化は持ち越さない。
        // 上のループがこのキャッシュへ書き込む側なので、必ずループの後で捨てる。
        MeterPlayerSpecCache.Instance.Clear();

        lock (PlayerEntitySync)
        {
            PlayerEntityUuidsByCharacterId.Clear();
        }

        RosterStore.BeginMap();
    }

    public static void UpdateMapName()
    {
        RosterStore.UpdateMapName(EncounterManager.SceneName, EncounterManager.ChannelLineId);
    }

    /// <summary>今のシーズンを一覧へ渡す。プレイヤー情報のシーズンとシーズンランクの表示に使う。</summary>
    public static void UpdateSeason()
    {
        RosterStore.UpdateSeason(SeasonStateStore.CurrentSeasonId);
    }

    /// <summary>
    /// 起動時の値に戻す(ログアウト)。<see cref="BeginMap"/> と違い、パーティの補完へ書き戻さず、自分の行も残さない。
    /// マップ名とチャンネルはシーン(起動時の値に戻した後)に合わせる。一覧の世代は進める。
    /// <b>エンカウンターの保存(表示値の焼き付け)より後に呼ぶこと。</b>
    /// </summary>
    public static void ResetToStartup()
    {
        ForgetSelfAttributes();
        lock (NearbyPlayerSync)
        {
            NearbyPlayerUuids.Clear();
        }

        lock (PlayerEntitySync)
        {
            PlayerEntityUuidsByCharacterId.Clear();
        }

        MeterPlayerSpecCache.Instance.Clear();
        RosterStore.Clear();
        UpdateMapName();
        UpdateSeason();
    }

    public static void UpsertSelf(long playerUuid)
    {
        UpsertPlayer(playerUuid, isSelfHint: true);
    }

    public static void AddOrUpdateNearbyPlayer(long playerUuid)
    {
        if (!TryGetCharacterEntity(playerUuid, out _))
        {
            return;
        }

        var characterId = Utils.UuidToEntityId(playerUuid);
        if (IsSelf(playerUuid, characterId))
        {
            UpsertPlayer(playerUuid, isSelfHint: true);
            return;
        }

        bool added;
        lock (NearbyPlayerSync)
        {
            added = NearbyPlayerUuids.Add(playerUuid);
        }

        if (added)
        {
            ActiveBuffStore.Instance.MarkPlayerInSight(playerUuid);
        }

        UpsertPlayer(playerUuid, isSelfHint: false);
    }

    public static void RemoveNearbyPlayer(long playerUuid)
    {
        if (TryGetCharacterEntity(playerUuid, out var entity))
        {
            var characterId = entity.UID != 0
                ? entity.UID
                : Utils.UuidToEntityId(playerUuid);
            var party = PartyStateStore.Instance.Current;
            if (characterId > 0
                && !IsSelf(playerUuid, characterId)
                && party.GetMembership(characterId) == PartyMembershipState.Member
                && TryCreateEntry(characterId, entity, entity, isSelf: false, out var nearbyEntry))
            {
                RefreshPartyMemberSupplementFromNearby(nearbyEntry, entity);
            }
        }

        var removed = false;
        lock (NearbyPlayerSync)
        {
            removed = NearbyPlayerUuids.Remove(playerUuid);
        }

        if (removed)
        {
            ActiveBuffStore.Instance.MarkPlayerOutOfSight(playerUuid);
            ClearTransientHumanSubProfession(playerUuid);
            RebuildRoster();
        }
    }

    public static void ResetNearbyPlayers()
    {
        ForgetSelfAttributes();
        long[] previousNearbyPlayerUuids;
        lock (NearbyPlayerSync)
        {
            previousNearbyPlayerUuids = [.. NearbyPlayerUuids];
            NearbyPlayerUuids.Clear();
        }

        foreach (var playerUuid in previousNearbyPlayerUuids)
        {
            ClearTransientHumanSubProfession(playerUuid);
        }

        RebuildRoster();
    }

    public static void UpsertPlayer(long playerUuid)
    {
        UpsertPlayer(playerUuid, isSelfHint: false);
    }

    internal static bool TryGetPlayerEntityUuid(long characterId, out long entityUuid)
    {
        lock (PlayerEntitySync)
        {
            return PlayerEntityUuidsByCharacterId.TryGetValue(characterId, out entityUuid);
        }
    }

    public static void RebuildRoster()
    {
        var party = PartyStateStore.Instance.Current;

        // AOIの実体は常にライブ側から引く。行の中身(HP・バフ・スキル枠)は全部ライブで、
        // 履歴を開いても止まらない。
        var encounter = EncounterManager.Current;

        // <b>メーターUIに乗っている人は、履歴表示中かどうかに関わらずリストにも居なければならない。</b>
        // だから「メーターの人」はメーターが映しているエンカウンター(履歴、保持している前の回、今の回の順)から取る。
        // ライブ供給が無ければ行は灰色(IsLive=false)になるだけで、固まるわけではない。
        var meterEncounter = MeterSnapshotProvider.ResolveActiveEncounter();
        long[] nearbyPlayerUuids;
        lock (NearbyPlayerSync)
        {
            nearbyPlayerUuids = [.. NearbyPlayerUuids];
        }

        var nearbyPlayersByCharacterId = nearbyPlayerUuids
            .Select(entityUuid => (EntityUuid: entityUuid, CharacterId: Utils.UuidToEntityId(entityUuid)))
            .Where(player => player.CharacterId > 0)
            .GroupBy(player => player.CharacterId)
            .ToDictionary(group => group.Key, group => group.Last().EntityUuid);
        var metadataPlayersByCharacterId = meterEncounter?.Entities
            .Select(pair => pair.Value)
            .Where(IsCharacterEntity)
            .Select(entity => (
                Entity: entity,
                CharacterId: entity.UID != 0
                    ? entity.UID
                    : Utils.UuidToEntityId(entity.UUID)))
            .Where(player => player.CharacterId > 0)
            .GroupBy(player => player.CharacterId)
            .ToDictionary(group => group.Key, group => group.Last().Entity)
            ?? new Dictionary<long, Entity>();

        var characterIds = new List<long>();
        var addedCharacterIds = new HashSet<long>();
        var selfCharacterId = AppState.PlayerUID != 0
            ? AppState.PlayerUID
            : Utils.UuidToEntityId(MessageManager.currentUserUuid != 0
                ? MessageManager.currentUserUuid
                : AppState.PlayerUUID);
        foreach (var characterId in party.OrderedCharacterIds)
        {
            AddCharacterId(characterId);
        }

        AddCharacterId(selfCharacterId);

        foreach (var characterId in nearbyPlayersByCharacterId.Keys)
        {
            AddCharacterId(characterId);
        }

        // AOI外でもメーター・被ダメログ・タイムラインに残っている人。ここで候補に入れないと下の関門まで届かない。
        foreach (var pair in metadataPlayersByCharacterId)
        {
            if (IsShownInMeterOrTimeline(meterEncounter, pair.Value))
            {
                AddCharacterId(pair.Key);
            }
        }

        var entries = new List<PlayerRosterEntry>(characterIds.Count);
        var entityUuidsByCharacterId = new Dictionary<long, long>();
        var existingSelf = RosterStore.Current.Entries.FirstOrDefault(entry => entry.IsSelf);
        foreach (var characterId in characterIds)
        {
            var isSelf = characterId == selfCharacterId;
            Entity? nearbyEntity = null;
            Entity? metadataEntity = null;
            if (isSelf)
            {
                var selfUuid = MessageManager.currentUserUuid != 0
                    ? MessageManager.currentUserUuid
                    : AppState.PlayerUUID != 0
                        ? AppState.PlayerUUID
                        : Utils.EntityIdToUuid(characterId, (long)EEntityType.EntChar, false, false);
                if (encounter != null)
                {
                    encounter.Entities.TryGetValue(selfUuid, out nearbyEntity);
                }

                metadataEntity = nearbyEntity;
            }
            else if (nearbyPlayersByCharacterId.TryGetValue(characterId, out var nearbyUuid)
                && encounter != null)
            {
                encounter.Entities.TryGetValue(nearbyUuid, out nearbyEntity);
                metadataEntity = nearbyEntity;
            }

            // 素性が履歴の記録か保持している前の回から来たか。どちらもHPは「その戦闘が終わった時点の値」で
            // いまの状態ではないので、表示では最大値に置く(下の useHistoryHealth)。
            var isMetadataFromHistory = false;
            if (metadataEntity is null
                && metadataPlayersByCharacterId.TryGetValue(characterId, out metadataEntity))
            {
                isMetadataFromHistory = !ReferenceEquals(meterEncounter, encounter);
            }

            // そのうち、開いている履歴の回(DB から読んだ記録)から来た行。保持している前の回は同じ実行中の値なので含めない。
            var isFromRecord = isMetadataFromHistory
                && meterEncounter is not null
                && ReferenceEquals(meterEncounter, AppState.OpenedHistoricalEncounter);

            var isPartyMember = party.GetMembership(characterId) == PartyMembershipState.Member;
            // AOI外でもメーター・被ダメログ・タイムラインに残る人はリストにも残す。エンカウンターが作り直されて
            // 統計と記録が無くなれば、メーター・被ダメログ・タイムラインと同時にここからも消える。
            if (!isSelf
                && !isPartyMember
                && nearbyEntity is null
                && !IsShownInMeterOrTimeline(meterEncounter, metadataEntity))
            {
                continue;
            }

            if (isSelf && nearbyEntity is null && existingSelf?.CharacterId == characterId)
            {
                entries.Add(existingSelf);
                continue;
            }

            if (TryCreateEntry(
                characterId,
                nearbyEntity,
                metadataEntity,
                isSelf,
                out var entry,
                useHistoryHealth: isMetadataFromHistory,
                seasonId: isMetadataFromHistory ? meterEncounter?.ExData.SeasonId ?? 0 : null,
                gameServer: isMetadataFromHistory ? meterEncounter?.ExData.GameServer ?? GameServer.Unknown : null,
                recordedAvatar: isMetadataFromHistory ? metadataEntity?.RecordedAvatar : null,
                isFromRecord: isFromRecord))
            {
                if (nearbyEntity is not null)
                {
                    RefreshPartyMemberSupplementFromNearby(entry, nearbyEntity);
                }

                entries.Add(entry);
                var resolvedEntityUuid = nearbyEntity?.UUID ?? metadataEntity?.UUID ?? 0;
                if (resolvedEntityUuid != 0)
                {
                    entityUuidsByCharacterId[characterId] = resolvedEntityUuid;
                }
            }
        }

        lock (PlayerEntitySync)
        {
            foreach (var pair in entityUuidsByCharacterId)
            {
                PlayerEntityUuidsByCharacterId[pair.Key] = pair.Value;
            }
        }

        RosterStore.Replace(entries);

        // 行の機能だけが読む、周りにいない人のバフを手放す。履歴を開いている間は行が開いた回で決まるので、閉じて作り直すときに見る。
        if (AppState.OpenedHistoricalEncounter is null)
        {
            ActiveBuffStore.Instance.ReleaseOutOfSightPlayersWithoutRow(addedCharacterIds);
        }

        void AddCharacterId(long characterId)
        {
            if (characterId > 0 && addedCharacterIds.Add(characterId))
            {
                characterIds.Add(characterId);
            }
        }
    }

    private static void UpsertPlayer(long playerUuid, bool isSelfHint)
    {
        if (!TryGetCharacterEntity(playerUuid, out var entity))
        {
            return;
        }

        var characterId = entity.UID != 0
            ? entity.UID
            : Utils.UuidToEntityId(playerUuid);
        if (characterId == 0)
        {
            return;
        }

        var isSelf = isSelfHint || IsSelf(playerUuid, characterId);
        var isPartyMember = PartyStateStore.Instance.Current.GetMembership(characterId)
            == PartyMembershipState.Member;
        var isNearby = IsNearbyPlayer(playerUuid);
        if (!isSelf && !isPartyMember && !isNearby && !IsShownInMeterOrTimeline(EncounterManager.Current, entity))
        {
            return;
        }

        var nearbyEntity = isSelf || isNearby ? entity : null;
        if (TryCreateEntry(characterId, nearbyEntity, entity, isSelf, out var entry))
        {
            if (nearbyEntity is not null)
            {
                RefreshPartyMemberSupplementFromNearby(entry, nearbyEntity);
            }

            lock (PlayerEntitySync)
            {
                PlayerEntityUuidsByCharacterId[characterId] = playerUuid;
            }
            RosterStore.Upsert(entry);
        }
    }

    /// <param name="useHistoryHealth">
    /// 素性の出所が履歴の記録のとき true。<b>そのときHPは無条件に最大値にする。</b>
    /// 履歴に入っているのは「その戦闘が終わった時点の値」で、いまの状態ではない。
    /// AOIやパーティから供給が戻れば(=この行が緑になれば)実値に切り替わる。
    /// </param>
    /// <param name="seasonId">
    /// 素性の出所が履歴の記録のとき、その回のシーズン番号(<see cref="EncounterExData.SeasonId"/>、0 は分からない)。
    /// ライブの行は null で、一覧の今のシーズンを使う。
    /// </param>
    /// <param name="gameServer">
    /// 素性の出所が履歴の記録のとき、その回のゲームのサーバー(<see cref="EncounterExData.GameServer"/>)。
    /// ライブの行は null で、今のサーバーを使う。
    /// </param>
    /// <param name="recordedAvatar">
    /// 素性の出所が履歴の記録のとき、その回に保存した顔と名刺の写真のリンク(<see cref="Entity.RecordedAvatar"/>)。
    /// ライブの行は null で、名刺の店の今の値を使う。
    /// </param>
    /// <param name="isFromRecord">
    /// 素性が開いている履歴の回の記録から来たとき true(<see cref="PlayerRosterEntry.IsFromRecord"/>)。
    /// 欠けの印は付けない(欠けは実行中にしか分からない印で決まり、記録の実体では意味を持たない)。
    /// </param>
    private static bool TryCreateEntry(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf,
        out PlayerRosterEntry entry,
        bool useHistoryHealth = false,
        int? seasonId = null,
        GameServer? gameServer = null,
        SocialAvatarState? recordedAvatar = null,
        bool isFromRecord = false)
    {
        if (characterId == 0)
        {
            entry = null!;
            return false;
        }

        var source = PlayerDataSourceResolver.Resolve(characterId, nearbyEntity, metadataEntity, isSelf);
        var party = PartyStateStore.Instance.Current;
        var (partySceneId, partyLineId) = party.TryGetSupplement(characterId, out var partySupplement)
            ? (partySupplement.SceneId, partySupplement.LineId)
            : (0, 0);
        var combatAttributes = nearbyEntity is null
            ? default
            : new PlayerCombatAttributes(
                PhysicalAttack: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrAttack"),
                MagicalAttack: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrMattack"),
                Strength: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrStrength"),
                Dexterity: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrDexterity"),
                Intelligence: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrIntelligence"),
                Endurance: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrVitality"),
                Armor: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrDefense"),
                Resistance: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrMdefense"),
                Critical: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrCri"),
                CriticalPercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrCrit"),
                Haste: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrHaste"),
                HastePercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrHastePct"),
                Luck: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrLuck"),
                LuckPercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrLuckyStrikeProb"),
                Mastery: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrMastery"),
                MasteryPercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrMasteryPct"),
                Versatility: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrVersatility"),
                VersatilityPercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrVersatilityPct"),
                BlockPercent: PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrBlockPct"));

        entry = new PlayerRosterEntry(
            source.CharacterId,
            source.Name,
            source.ProfessionId,
            source.CombatPower,
            source.SeasonStrength,
            useHistoryHealth ? source.MaxHp : source.CurrentHp,
            source.MaxHp,
            source.ClassSpec,
            isSelf,
            combatAttributes,
            source.SubProfessionId,
            source.Level,
            source.SeasonLevel,
            source.EquipmentData,
            source.IsNpc,
            nearbyEntity is null ? 0 : Utils.GetCurrentShield(nearbyEntity),
            party.GetMembership(characterId) == PartyMembershipState.Member,
            party.GetPartyNumber(characterId),
            source.IsLive,
            CreateAttributeEntries(isSelf, nearbyEntity),
            source.SeasonTalentBuffId,
            source.IsSeasonTalentInactive,
            source.SeasonRankLevel,
            nearbyEntity is not null,
            partySceneId,
            partyLineId,
            seasonId,
            gameServer,
            recordedAvatar,
            source.RootBuffData,
            nearbyEntity is not null && PlayerDataSourceResolver.GetInt(nearbyEntity, "AttrCombatState") > 0,
            isFromRecord ? PlayerDataGaps.None : source.DataGaps,
            isFromRecord,
            !useHistoryHealth && source.IsIncapacitated,
            source.TalentNodeIds,
            source.LastReplaceableSkills);

        // 空の項目は名刺照会とチャットの値で埋める(名簿の外の人と同じ決まり)。記録から来た行はその回の値のまま。
        if (!isFromRecord)
        {
            entry = PlayerDirectory.ApplyProfile(entry);
        }

        return true;
    }

    /// <summary>
    /// 自分の実体に届いている属性を番号順に全部並べる。ステータス詳細が使う。
    /// 差分で変わった能力値には、変わった向きと時刻(<see cref="SelfAttributeChangeStore"/>)を添える。
    ///
    /// <para>
    /// <b>自分だけ</b>。他人には能力値が届かない(notes/109)。
    /// 属性の鍵は列挙名の文字列だが、列挙に無い番号はその数字が鍵になっている。
    /// <c>Enum.TryParse</c> はどちらも受けるので、そのまま番号へ戻せる。
    /// </para>
    /// </summary>
    private static IReadOnlyList<PlayerAttributeEntry>? CreateAttributeEntries(bool isSelf, Entity? nearbyEntity)
    {
        if (!isSelf || nearbyEntity is null)
        {
            return null;
        }

        var entries = new List<PlayerAttributeEntry>(nearbyEntity.Attributes.Count);
        foreach (var (key, value) in nearbyEntity.Attributes)
        {
            if (!Enum.TryParse<Zproto.EAttrType>(key, out var attrType))
            {
                continue;
            }

            var valueText = value?.ToString() ?? string.Empty;
            entries.Add(SelfAttributeChangeStore.TryGet(key, out var change)
                ? new PlayerAttributeEntry((int)attrType, key, valueText, change.Sign, change.ArrivalUtc)
                : new PlayerAttributeEntry((int)attrType, key, valueText));
        }

        entries.Sort((left, right) => left.AttrId.CompareTo(right.AttrId));
        lock (SelfAttributesSync)
        {
            if (_lastSelfAttributes is not null && _lastSelfAttributes.SequenceEqual(entries))
            {
                return _lastSelfAttributes;
            }

            _lastSelfAttributes = entries;
            return entries;
        }
    }

    private static void ForgetSelfAttributes()
    {
        lock (SelfAttributesSync)
        {
            _lastSelfAttributes = null;
        }
    }

    private static void RefreshPartyMemberSupplementFromNearby(
        PlayerRosterEntry player,
        Entity nearbyEntity)
    {
        var partyStore = PartyStateStore.Instance;
        var party = partyStore.Current;
        if (party.TeamId <= 0
            || !player.IsPartyMember
            || player.IsSelf
            || player.CharacterId <= 0)
        {
            return;
        }

        var knownSupplement = party.TryGetSupplement(player.CharacterId, out var existingSupplement)
            ? existingSupplement
            : new PartyMemberSupplement();
        if (knownSupplement.IsNpc)
        {
            return;
        }

        var observedCurrentHp = nearbyEntity.GetAttrKV("AttrHp") is long currentHpValue
            ? currentHpValue
            : -1;
        var observedMaxHp = nearbyEntity.GetAttrKV("AttrMaxHp") is long maxHpValue
            ? maxHpValue
            : 0;
        var hasCurrentHp = observedCurrentHp >= 0;
        var hasMaxHp = observedMaxHp > 0;

        var name = !string.IsNullOrEmpty(player.Name) ? player.Name : knownSupplement.Name;
        var professionId = player.ProfessionId > 0 ? player.ProfessionId : knownSupplement.ProfessionId;
        var combatPower = player.CombatPower > 0 ? player.CombatPower : knownSupplement.CombatPower;
        var seasonStrength = player.SeasonStrength > 0 ? player.SeasonStrength : knownSupplement.SeasonStrength;
        var level = player.Level > 0 ? player.Level : knownSupplement.Level;
        var seasonLevel = player.SeasonLevel > 0 ? player.SeasonLevel : knownSupplement.SeasonLevel;
        var equipmentData = player.EquipmentData ?? knownSupplement.EquipmentData;
        var currentHp = hasCurrentHp ? observedCurrentHp : knownSupplement.CurrentHp;
        var maxHp = hasMaxHp ? observedMaxHp : knownSupplement.MaxHp;

        // 値が一致していても、まだ「確かなソースで取得済み」の印が付いていなければ書き込む。
        // ここで印を付けておかないと、後から NotifySocialData の古い内容で上書きされてしまう。
        if (knownSupplement.HasTrustedSocialData
            && name == knownSupplement.Name
            && professionId == knownSupplement.ProfessionId
            && combatPower == knownSupplement.CombatPower
            && seasonStrength == knownSupplement.SeasonStrength
            && level == knownSupplement.Level
            && seasonLevel == knownSupplement.SeasonLevel
            && object.Equals(equipmentData, knownSupplement.EquipmentData)
            && currentHp == knownSupplement.CurrentHp
            && maxHp == knownSupplement.MaxHp)
        {
            return;
        }

        partyStore.UpdateSupplement(
            party.TeamId,
            player.CharacterId,
            current => current with
            {
                Name = !string.IsNullOrEmpty(player.Name) ? player.Name : current.Name,
                ProfessionId = player.ProfessionId > 0 ? player.ProfessionId : current.ProfessionId,
                CombatPower = player.CombatPower > 0 ? player.CombatPower : current.CombatPower,
                SeasonStrength = player.SeasonStrength > 0 ? player.SeasonStrength : current.SeasonStrength,
                Level = player.Level > 0 ? player.Level : current.Level,
                SeasonLevel = player.SeasonLevel > 0 ? player.SeasonLevel : current.SeasonLevel,
                EquipmentData = player.EquipmentData ?? current.EquipmentData,
                CurrentHp = hasCurrentHp ? observedCurrentHp : current.CurrentHp,
                MaxHp = hasMaxHp ? observedMaxHp : current.MaxHp,
                // AOIでの実測は最も確かなソース。以後 NotifySocialData の古い内容で上書きさせない。
                HasTrustedSocialData = true
            });
    }

    private static void PreserveHumanPartySupplements()
    {
        var partyStore = PartyStateStore.Instance;
        var party = partyStore.Current;
        if (party.TeamId <= 0)
        {
            return;
        }

        foreach (var player in RosterStore.Current.Entries)
        {
            if (!player.IsPartyMember || player.IsSelf || player.IsNpc || player.CharacterId <= 0)
            {
                continue;
            }

            partyStore.UpdateSupplement(
                party.TeamId,
                player.CharacterId,
                current => current with
                {
                    Name = !string.IsNullOrEmpty(player.Name) ? player.Name : current.Name,
                    ProfessionId = player.ProfessionId > 0 ? player.ProfessionId : current.ProfessionId,
                    CombatPower = player.CombatPower > 0 ? player.CombatPower : current.CombatPower,
                    SeasonStrength = player.SeasonStrength > 0 ? player.SeasonStrength : current.SeasonStrength,
                    Level = player.Level > 0 ? player.Level : current.Level,
                    SeasonLevel = player.SeasonLevel > 0 ? player.SeasonLevel : current.SeasonLevel,
                    EquipmentData = player.EquipmentData ?? current.EquipmentData,
                    CurrentHp = player.MaxHp > 0 ? Math.Max(player.CurrentHp, 0) : current.CurrentHp,
                    MaxHp = player.MaxHp > 0 ? player.MaxHp : current.MaxHp
                });
        }
    }

    private static void ClearTransientHumanSubProfession(long playerUuid)
    {
        var characterId = Utils.UuidToEntityId(playerUuid);
        if (characterId <= 0)
        {
            return;
        }

        var party = PartyStateStore.Instance.Current;
        if (party.TryGetSupplement(characterId, out var supplement) && supplement.IsNpc)
        {
            return;
        }

        var encounter = EncounterManager.Current;
        if (encounter is not null
            && encounter.Entities.TryGetValue(playerUuid, out var entity)
            && IsCharacterEntity(entity))
        {
            CaptureMeterPlayerSpec(entity);
            entity.SetSubProfessionUnknown();
            entity.SetSeasonTalentUnknown();
            return;
        }

        // Entity 側の推定値は破棄するが、保持している補完値は残す。
        // マップ切替で observable でなくなっただけで、特化が変わったわけではない。
    }

    /// <summary>
    /// AOI外へ出る直前の特化を控える。<b>メーター・被ダメログ・タイムラインに残り続ける人だけ</b>が対象。
    ///
    /// <para>
    /// 「分からない」も含めて必ず現在値で置き換える。何も書かずに素通りさせると、
    /// 職業変更などでエンティティ側が不明に戻った後も、キャッシュだけ古い特化を持ち続ける。
    /// </para>
    /// </summary>
    private static void CaptureMeterPlayerSpec(Entity entity)
    {
        if (!IsShownInMeterOrTimeline(EncounterManager.Current, entity))
        {
            return;
        }

        var characterId = entity.UID != 0
            ? entity.UID
            : Utils.UuidToEntityId(entity.UUID);
        var cache = MeterPlayerSpecCache.Instance;
        if (entity.SubProfessionId > 0)
        {
            cache.SetSubProfession(characterId, entity.SubProfessionId);
        }
        else if (entity.IsSpecAbilityUnequipped)
        {
            cache.SetSpecAbilityUnequipped(characterId);
        }
        else
        {
            cache.Remove(characterId);
        }

        // シーズンタレントの型も同じ規則で控える。
        if (entity.SeasonTalentBuffId > 0)
        {
            cache.SetSeasonTalent(characterId, entity.SeasonTalentBuffId);
        }
        else if (entity.IsSeasonTalentInactive)
        {
            cache.SetSeasonTalentInactive(characterId);
        }
        else
        {
            cache.RemoveSeasonTalent(characterId);
        }
    }

    /// <summary>
    /// メーター(DPS/HPS/ヘイト)に出る人か、その回の被ダメログかタイムラインに出る人か。名簿に載せる条件。
    ///
    /// <para>
    /// メーターは <see cref="MeterSnapshotProvider"/> の <c>TotalValue &gt; 0</c> と同じ(与ダメ・回復・ヘイトのどれかを持っていればどれかのメーターには出る)。
    /// 被ダメログは被弾の記録があるか(<see cref="CombatStats.HasSkillSnapshots"/>)、
    /// タイムラインはダメージの無い戦闘不能の記録と、敵の回復・敵が付けたバフの相手(<see cref="Encounter.HasPlayerTimelineRecord"/>)。
    /// 被ダメログの絞り込みの設定は見ない。
    /// </para>
    /// </summary>
    /// <param name="encounter">その実体がいる回。タイムラインの記録を引く。</param>
    private static bool IsShownInMeterOrTimeline(Encounter? encounter, Entity? entity)
    {
        return entity is not null
            && (entity.TotalDamage > 0
                || entity.TotalHealing > 0
                || entity.TotalHate > 0
                || entity.TotalBreak > 0
                || entity.TakenStats.HasSkillSnapshots()
                || encounter?.HasPlayerTimelineRecord(entity.UUID) == true);
    }

    private static bool TryGetCharacterEntity(long playerUuid, out Entity entity)
    {
        entity = null!;
        var encounter = EncounterManager.Current;
        if (playerUuid == 0
            || encounter is null
            || !encounter.Entities.TryGetValue(playerUuid, out entity))
        {
            return false;
        }

        return IsCharacterEntity(entity);
    }

    private static bool IsCharacterEntity(Entity entity)
    {
        return entity.EntityType == EEntityType.EntChar
            || Utils.UuidToEntityType(entity.UUID) == (long)EEntityType.EntChar;
    }

    internal static bool IsNearbyPlayer(long playerUuid)
    {
        lock (NearbyPlayerSync)
        {
            return NearbyPlayerUuids.Contains(playerUuid);
        }
    }

    private static bool IsSelf(long playerUuid, long characterId)
    {
        return playerUuid == MessageManager.currentUserUuid
            || playerUuid == AppState.PlayerUUID
            || (AppState.PlayerUID != 0 && characterId == AppState.PlayerUID);
    }
}
