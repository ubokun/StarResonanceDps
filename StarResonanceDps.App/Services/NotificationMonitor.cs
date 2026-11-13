// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Notifications;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes.Enums;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Services;

/// <summary>
/// ウィジェットを開いていなくても通知する項目(入場確認・HP低下・プレイヤー出現・エンティティ出現・狙われるプレイヤー・
/// 自分の薬剤・料理効果切れ・予兆技・エンティティ詠唱・プライベートトーク・ワールドチャット)の判定を持つ。起動時から動き、画面のスレッドで使う。
/// バフ・デバフ効果切れと、ほかの人の薬剤・料理効果切れは、開いているバフ・デバフカードとバフ・デバフグループが判定する(<see cref="BuffNotificationRegistry"/>)。
///
/// <para>
/// 判定はその項目の通知がオンの間だけ動かす。オフにしたら控えを捨て、オンにした時点から見始める。
/// HP低下の控えはマップが替わったら捨てる(ログアウトとキャプチャの停止では名簿が空になり、判定の側で全員の控えが消える)。
/// HP低下と狙われるプレイヤーはプレイヤーの通知なので、NPC(助っ人)と判定された人は渡さない。
/// 出現の2つは、オンにした・対象(名前とID)を変えたときに今の一覧を「既にいる相手」として控え直す。起動した直後に見えた相手には出す。
/// 狙われるプレイヤーも、オンにした・設定を選び直したときに今の狙われ状態を控え直す。エンティティ詠唱も、オンにした・対象を変えたときに今の詠唱を控え直す。
/// 出現の2つとエンティティ詠唱は、対象の名前もIDも空なら判定を動かさない(<see cref="NotificationWatchTarget"/>)。ワールドチャットは一致ワードが空なら出さない。
/// 予兆技は、通知条件を選び直したときに予告を控え直す。
/// </para>
///
/// <para>
/// 自分の薬剤・料理効果切れは、項目がオンの間1秒ごとに自分のバフを読み、カード・グループと同じ判定を借りる(同じバフを窓でも見ていれば通知は1回)。
/// 借り方は効果切れを出さない形で、バフ・デバフ効果切れは窓が見ている間だけ出る。
/// </para>
/// </summary>
public sealed class NotificationMonitor
{
    /// <summary>予兆技を確かめる間隔。タイムラインの更新と同じ間隔。</summary>
    private static readonly TimeSpan TelegraphedSkillPollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>自分の料理・薬剤を確かめる間隔。バフ・デバフカードとグループの更新と同じ間隔。</summary>
    private static readonly TimeSpan CuisinePotionPollInterval = TimeSpan.FromSeconds(1);

    private static readonly BuffGroup[] CuisinePotionGroups = [BuffGroup.Cuisine, BuffGroup.Potion];

    private readonly HealthLowNotificationTracker _healthLowTracker = new();
    private readonly AppearanceNotificationTracker _playerAppearedTracker = new(NotificationKind.PlayerAppeared);
    private readonly AppearanceNotificationTracker _entityAppearedTracker = new(NotificationKind.EntityAppeared);
    private readonly TargetedNotificationTracker _targetedTracker = new();
    private readonly EntityCastNotificationTracker _entityCastTracker = new();
    private readonly PrivateChatNotificationTracker _privateChatTracker = new();
    private readonly WorldChatNotificationTracker _worldChatTracker = new();
    private readonly List<PlayerRosterEntry> _nearbyPlayers = [];
    private readonly DispatcherTimer _telegraphedSkillTimer;
    private readonly DispatcherTimer _cuisinePotionTimer;

    /// <summary>自分の料理・薬剤の判定。鍵はまとまり。項目がオフの間と自分が分からない間は空。</summary>
    private readonly Dictionary<BuffGroup, BuffNotificationWatch> _cuisinePotionWatches = [];

    private TelegraphedSkillNotificationTracker? _telegraphedSkillTracker;

    /// <summary>狙われるプレイヤーの今の設定。変わったら今の狙われ状態を控え直す。</summary>
    private (bool IsEnabled, int TargetedPlayerModeIndex, int TargetEntityModeIndex, int ConditionIndex) _targetedSettings;

    /// <summary>エンティティ詠唱で見ているスキル(オフか名前もIDも空なら null)。変わったら今の詠唱を控え直す。</summary>
    private NotificationWatchTarget? _entityCastTarget;

    private PlayerRosterSnapshot? _lastRoster;
    private IReadOnlyList<NearbyEntityEntry> _lastNearbyEntities = [];
    private long _healthLowMapGeneration = -1;
    private bool _isHealthLowEnabled;

    /// <summary>HP低下の判定に使っている「HP低下判定」と「通知条件」の番号。選び直したら控えを捨てて見始め直す。</summary>
    private (int ThresholdIndex, int ConditionIndex) _healthLowCriteria;

    /// <summary>プレイヤー出現で見ている相手(オフか名前もIDも空なら null)。変わったら控え直す。</summary>
    private NotificationWatchTarget? _playerAppearedTarget;

    /// <summary>エンティティ出現で見ている相手(オフか名前もIDも空なら null)。変わったら控え直す。</summary>
    private NotificationWatchTarget? _entityAppearedTarget;

    public NotificationMonitor()
    {
        _telegraphedSkillTimer = new DispatcherTimer
        {
            Interval = TelegraphedSkillPollInterval
        };
        _telegraphedSkillTimer.Tick += TelegraphedSkillTimer_Tick;
        _cuisinePotionTimer = new DispatcherTimer
        {
            Interval = CuisinePotionPollInterval
        };
        _cuisinePotionTimer.Tick += CuisinePotionTimer_Tick;

        // 起動時の設定は控え直さずに見始める(起動した直後に見えた相手にも出す)。
        var notifications = ConfigManager.Instance.GetNotificationsSnapshot();
        _playerAppearedTarget = NotificationWatchTarget.From(notifications.Get(NotificationKind.PlayerAppeared));
        _entityAppearedTarget = NotificationWatchTarget.From(notifications.Get(NotificationKind.EntityAppeared));

        ConfigManager.Instance.NotificationsChanged += ConfigManager_NotificationsChanged;
        ApplySettings();
    }

    /// <summary>マッチングが成立して承諾待ちになった(入場の承認画面が開く)。画面のスレッドで呼ぶ。</summary>
    public void NotifyMatchFound(Zproto.EMatchType matchType, long matchTypeUuid)
    {
        if (ConfigManager.Instance.GetNotificationItem(NotificationKind.EntryConfirmation).IsEnabled)
        {
            EntryConfirmationNotifier.NotifyMatch(matchType, matchTypeUuid);
        }
    }

    /// <summary>パーティでコンテンツに入るときの承認が始まった(入場の承認画面が開く)。画面のスレッドで呼ぶ。</summary>
    public void NotifyTeamEntryVoteStarted(int sceneId, int masterModeDifficulty)
    {
        if (ConfigManager.Instance.GetNotificationItem(NotificationKind.EntryConfirmation).IsEnabled)
        {
            EntryConfirmationNotifier.NotifyTeamEntry(sceneId, masterModeDifficulty);
        }
    }

    /// <summary>ほかの人からプライベートトークが届いた。画面のスレッドで呼ぶ。</summary>
    public void NotifyPrivateChatMessage(long partnerId, long messageId, string senderName)
    {
        if (ConfigManager.Instance.GetNotificationItem(NotificationKind.PrivateChat).IsEnabled)
        {
            _privateChatTracker.Observe(partnerId, messageId, senderName);
        }
    }

    /// <summary>ゲームがプライベートトークを既読にした。画面のスレッドで呼ぶ。</summary>
    public void NotifyPrivateChatRead(long partnerId, long maxReadMessageId)
    {
        _privateChatTracker.MarkRead(partnerId, maxReadMessageId);
    }

    /// <summary>ワールドチャットにほかの人の文の発言が届いた。画面のスレッドで呼ぶ。</summary>
    public void NotifyWorldChatMessage(long senderId, string senderName, string message)
    {
        if (ConfigManager.Instance.GetNotificationItem(NotificationKind.WorldChat).IsEnabled)
        {
            _worldChatTracker.Observe(senderId, senderName, message);
        }
    }

    /// <summary>名簿が届いた。画面のスレッドで呼ぶ。</summary>
    public void ObserveRoster(PlayerRosterSnapshot roster)
    {
        _lastRoster = roster;
        ObserveHealthLow();
        ObservePlayerAppearance();
        ObserveTargeted();
    }

    /// <summary>周りのエンティティの一覧(エンティティリストの元)が届いた。画面のスレッドで呼ぶ。</summary>
    public void ObserveNearbyEntities(IReadOnlyList<NearbyEntityEntry> entities)
    {
        _lastNearbyEntities = entities;
        ObserveEntityAppearance();
        ObserveTargeted();
        ObserveEntityCast();
    }

    private void ConfigManager_NotificationsChanged(object? sender, EventArgs e)
    {
        ApplySettings();
    }

    private void ApplySettings()
    {
        var notifications = ConfigManager.Instance.GetNotificationsSnapshot();

        var telegraphedSkill = notifications.Get(NotificationKind.TelegraphedSkill);
        var telegraphedSkillLeadSeconds = AppConfigDefaults.TelegraphedSkillLeadSeconds[telegraphedSkill.TelegraphedSkillConditionIndex];
        if (telegraphedSkill.IsEnabled && _telegraphedSkillTracker is null)
        {
            _telegraphedSkillTracker = new TelegraphedSkillNotificationTracker(telegraphedSkillLeadSeconds);
            _telegraphedSkillTimer.Start();
        }
        else if (telegraphedSkill.IsEnabled
            && _telegraphedSkillTracker is { } telegraphedSkillTracker
            && telegraphedSkillTracker.LeadSeconds != telegraphedSkillLeadSeconds)
        {
            // 選び直した時点で時機を過ぎた予告は出さない。
            telegraphedSkillTracker.Rebaseline(telegraphedSkillLeadSeconds);
        }
        else if (!telegraphedSkill.IsEnabled && _telegraphedSkillTracker is not null)
        {
            _telegraphedSkillTimer.Stop();
            _telegraphedSkillTracker = null;
        }

        var healthLow = notifications.Get(NotificationKind.HealthLow);
        _isHealthLowEnabled = healthLow.IsEnabled;
        var healthLowCriteria = (healthLow.HealthLowThresholdIndex, healthLow.HealthLowConditionIndex);
        if (healthLowCriteria != _healthLowCriteria)
        {
            // 前の割合で決めた構えと控えを新しい割合に持ち越さない(選び直した時点で既に割合以下の人には出さない)。
            _healthLowTracker.Reset();
            _healthLowCriteria = healthLowCriteria;
        }

        // 対象を変えたときも、次の名簿を待たずに今の名簿で絞り直す。
        ObserveHealthLow();

        var playerAppearedTarget = NotificationWatchTarget.From(notifications.Get(NotificationKind.PlayerAppeared));
        if (playerAppearedTarget != _playerAppearedTarget)
        {
            _playerAppearedTarget = playerAppearedTarget;
            _playerAppearedTracker.Rebaseline();
            ObservePlayerAppearance();
        }

        var entityAppearedTarget = NotificationWatchTarget.From(notifications.Get(NotificationKind.EntityAppeared));
        if (entityAppearedTarget != _entityAppearedTarget)
        {
            _entityAppearedTarget = entityAppearedTarget;
            _entityAppearedTracker.Rebaseline();
            ObserveEntityAppearance();
        }

        var targeted = notifications.Get(NotificationKind.Targeted);
        var targetedSettings = (
            targeted.IsEnabled,
            targeted.TargetedPlayerModeIndex,
            targeted.TargetEntityModeIndex,
            targeted.TargetedConditionIndex);
        if (targetedSettings != _targetedSettings)
        {
            // オンにした・選び直した時点で既に狙われている人には出さない(出現と同じ)。
            _targetedSettings = targetedSettings;
            _targetedTracker.Rebaseline(targetedSettings.IsEnabled ? CollectTargetedStates() : []);
        }

        var entityCastTarget = NotificationWatchTarget.From(notifications.Get(NotificationKind.EntityCast));
        if (entityCastTarget != _entityCastTarget)
        {
            // オンにした・対象を変えた時点で既に詠唱中の敵には出さない(狙われるプレイヤーと同じ)。
            _entityCastTarget = entityCastTarget;
            _entityCastTracker.Rebaseline(entityCastTarget is null ? [] : CollectEntityCasts());
        }

        if (notifications.Get(NotificationKind.CuisinePotionLow).IsEnabled)
        {
            _cuisinePotionTimer.Start();
        }
        else
        {
            _cuisinePotionTimer.Stop();
            ReleaseCuisinePotionWatches();
        }

        if (!notifications.Get(NotificationKind.PrivateChat).IsEnabled)
        {
            _privateChatTracker.Clear();
        }

        _worldChatTracker.SetMatchWords(notifications.Get(NotificationKind.WorldChat).TargetNames);
    }

    /// <summary>名簿のうち、周りに見えているほかのプレイヤー(自分と履歴の行は除く)を渡す。IDはUID(キャラクターID)で比べる。</summary>
    private void ObservePlayerAppearance()
    {
        if (_playerAppearedTarget is not { } target || _lastRoster is not { } roster)
        {
            return;
        }

        _nearbyPlayers.Clear();
        foreach (var entry in roster.Entries)
        {
            if (entry.IsNearby && !entry.IsSelf && !entry.IsFromRecord)
            {
                _nearbyPlayers.Add(entry);
            }
        }

        var nameDisplayMode = PlayerRosterPresentationStore.Instance.NameDisplayMode;
        _playerAppearedTracker.Observe(
            _nearbyPlayers,
            target,
            entry => entry.CharacterId,
            entry => entry.CharacterId,
            entry => entry.Name,
            entry => PlayerInfoFormatFormatter.GetDisplayName(
                entry.Name,
                entry.CharacterId,
                entry.IsSelf,
                entry.IsNpc,
                entry.ProfessionId,
                nameDisplayMode));
    }

    /// <summary>
    /// 周りのエンティティを、表示言語の名前(内部IDの注記なし)で渡す。名前の無い実体は名前では当たらない。
    /// IDは種別ID(内部IDの注記に出る番号)で比べ、種別IDが未着(0)の実体は当たらない。
    /// </summary>
    private void ObserveEntityAppearance()
    {
        if (_entityAppearedTarget is not { } target)
        {
            return;
        }

        _entityAppearedTracker.Observe(
            _lastNearbyEntities,
            target,
            entity => entity.EntityUuid,
            entity => entity.EntityId,
            GetEntityName,
            GetEntityName);

        static string GetEntityName(NearbyEntityEntry entity)
            => CombatDataCatalog.GetEntityNameWithoutInternalId(entity.EntityType, entity.EntityId);
    }

    private void ObserveHealthLow()
    {
        if (!_isHealthLowEnabled)
        {
            _healthLowTracker.Reset();
            _healthLowMapGeneration = -1;
            return;
        }

        if (_lastRoster is not { } roster)
        {
            return;
        }

        if (_healthLowMapGeneration != roster.MapGeneration)
        {
            _healthLowTracker.Reset();
            _healthLowMapGeneration = roster.MapGeneration;
        }

        var mode = (PartyDisplayMode)ConfigManager.Instance.GetNotificationItem(NotificationKind.HealthLow).TargetModeIndex;
        var party = PartyStateStore.Instance.Current;
        var targets = roster.Entries
            .Where(entry => !entry.IsNpc && party.ShouldInclude(entry.CharacterId, entry.IsSelf, mode))
            .ToArray();
        _healthLowTracker.Observe(targets, PlayerRosterPresentationStore.Instance.NameDisplayMode);
    }

    private void ObserveTargeted()
    {
        if (_targetedSettings.IsEnabled)
        {
            _targetedTracker.Observe(CollectTargetedStates());
        }
    }

    private void ObserveEntityCast()
    {
        if (_entityCastTarget is { } target)
        {
            _entityCastTracker.Observe(CollectEntityCasts(), target);
        }
    }

    /// <summary>
    /// 今詠唱している周りの敵(エンティティリストの詠唱ゲージと同じ詠唱)。HP バーのある敵だけ
    /// (表示設定に関係なく、エンティティリストのフィルター「オブジェクト以外」と同じ条件)。
    /// </summary>
    private List<EntityCastObservation> CollectEntityCasts()
    {
        var casts = new List<EntityCastObservation>();
        foreach (var entity in _lastNearbyEntities)
        {
            if (entity.HasHpBar && entity.Cast is { } cast)
            {
                casts.Add(new EntityCastObservation(entity.EntityUuid, cast, GetEntityDisplayName(entity)));
            }
        }

        return casts;
    }

    /// <summary>
    /// 今の狙われ状態。周りの敵のうち対象エンティティの種類のものが狙っている、対象プレイヤーに入るプレイヤー(NPC を除く)。
    /// </summary>
    private List<TargetedState> CollectTargetedStates()
    {
        var targets = new List<TargetedState>();
        if (_lastRoster is not { } roster)
        {
            return targets;
        }

        var playersByCharacterId = new Dictionary<long, PlayerRosterEntry>();
        foreach (var entry in roster.Entries)
        {
            playersByCharacterId.TryAdd(entry.CharacterId, entry);
        }

        var party = PartyStateStore.Instance.Current;
        var nameDisplayMode = PlayerRosterPresentationStore.Instance.NameDisplayMode;
        foreach (var entity in _lastNearbyEntities)
        {
            if (entity.TargetCharacterId == 0
                || !IsTargetEntity(entity.MonsterType, _targetedSettings.TargetEntityModeIndex)
                || !playersByCharacterId.TryGetValue(entity.TargetCharacterId, out var player)
                || player.IsNpc
                || !IsTargetedPlayer(player, party, _targetedSettings.TargetedPlayerModeIndex))
            {
                continue;
            }

            targets.Add(new TargetedState(
                entity.EntityUuid,
                player.CharacterId,
                PlayerInfoFormatFormatter.GetDisplayName(
                    player.Name,
                    player.CharacterId,
                    player.IsSelf,
                    player.IsNpc,
                    player.ProfessionId,
                    nameDisplayMode),
                GetEntityDisplayName(entity)));
        }

        return targets;
    }

    /// <summary>
    /// 対象プレイヤー(すべて / パーティメンバー / 防御クラス以外 / 防御クラス以外のパーティメンバー)に入る人か。
    /// パーティメンバーは HP低下の「パーティメンバーのみ」と同じ判定(自分を含む)。
    /// 防御クラス以外は職業の役割が防御でない人で、職業の分からない人は防御クラスかどうか言えないので入れない。
    /// </summary>
    private static bool IsTargetedPlayer(PlayerRosterEntry player, PartyStateSnapshot party, int targetedPlayerModeIndex)
    {
        var requiresPartyMember = targetedPlayerModeIndex is AppConfigDefaults.TargetedPlayerPartyMembersIndex
            or AppConfigDefaults.TargetedPlayerNonGuardianPartyMembersIndex;
        if (requiresPartyMember && !party.ShouldInclude(player.CharacterId, player.IsSelf, PartyDisplayMode.PartyMembersOnly))
        {
            return false;
        }

        var requiresNonGuardian = targetedPlayerModeIndex is AppConfigDefaults.TargetedPlayerNonGuardianIndex
            or AppConfigDefaults.TargetedPlayerNonGuardianPartyMembersIndex;
        return !requiresNonGuardian
            || (CombatDataCatalog.TryGetProfessionRole(player.ProfessionId, out var role) && role != Professions.ERoleType.Tank);
    }

    /// <summary>対象エンティティ(ボスのみ / ボスと精鋭 / すべて)に入る敵の種類か。</summary>
    private static bool IsTargetEntity(EMonsterType monsterType, int targetEntityModeIndex)
    {
        return targetEntityModeIndex switch
        {
            AppConfigDefaults.TargetEntityBossOnlyIndex => monsterType == EMonsterType.Boss,
            AppConfigDefaults.TargetEntityBossAndEliteIndex => monsterType is EMonsterType.Boss or EMonsterType.Elite,
            _ => true
        };
    }

    /// <summary>
    /// 通知に入れる敵の名前(狙われるプレイヤーの狙っている敵、エンティティ詠唱の詠唱した敵)。
    /// エンティティ出現と同じ表示言語の名前(内部IDの注記なし)で、名前の無い敵はエンティティリストと同じ「敵」、種別ID の分からない敵は「未知の敵」。
    /// </summary>
    private static string GetEntityDisplayName(NearbyEntityEntry entity)
    {
        if (entity.EntityId == 0)
        {
            return LocalizationManager.Instance.GetString("EntityList_UnknownEnemy");
        }

        var name = CombatDataCatalog.GetEntityNameWithoutInternalId(entity.EntityType, entity.EntityId);
        return string.IsNullOrWhiteSpace(name)
            ? LocalizationManager.Instance.GetString("EntityList_UnnamedEnemy")
            : name;
    }

    /// <summary>
    /// 自分の料理・薬剤を判定へ渡す。読み方はバフ・デバフグループと同じ(同じ番号は残り時間の一番長い実体、料理・薬剤はまとまり)。
    /// 自分が分からない間は判定を返す。
    /// </summary>
    private void CuisinePotionTimer_Tick(object? sender, EventArgs e)
    {
        var characterId = AppState.PlayerUID;
        if (characterId == 0
            || !MeterSnapshotProvider.TryGetPlayerBuffs(characterId, PlayerBuffListKind.Buff, groupSameBuffs: true, out var buffs))
        {
            ReleaseCuisinePotionWatches();
            return;
        }

        var target = BuffNotificationTarget.ForPlayer(characterId);
        var targetName = GetSelfName(characterId);
        foreach (var group in CuisinePotionGroups)
        {
            if (_cuisinePotionWatches.TryGetValue(group, out BuffNotificationWatch? watch) && watch.Target != target)
            {
                watch.Dispose();
                _cuisinePotionWatches.Remove(group);
                watch = null;
            }

            if (watch is null)
            {
                watch = BuffNotificationRegistry.Instance.Watch(
                    target,
                    BuffNotificationRegistry.CreateBuffKey(group, string.Empty),
                    notifiesExpiry: false);
                _cuisinePotionWatches.Add(group, watch);
            }

            watch.Observe(BuffDebuffCardContent.FindByGroup(buffs, group), targetName);
        }
    }

    private void ReleaseCuisinePotionWatches()
    {
        foreach (var watch in _cuisinePotionWatches.Values)
        {
            watch.Dispose();
        }

        _cuisinePotionWatches.Clear();
    }

    /// <summary>通知に入れる自分の名前。バフ・デバフグループと同じ規則(名簿の自分の行が無ければ素性から)。</summary>
    private string GetSelfName(long characterId)
    {
        var self = _lastRoster?.Entries.FirstOrDefault(entry => entry.CharacterId == characterId);
        var identity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        return PlayerInfoFormatFormatter.GetDisplayName(
            self?.Name ?? identity?.Name,
            identity?.UserId ?? characterId,
            true,
            self?.IsNpc ?? identity?.IsNpc ?? false,
            self?.ProfessionId ?? identity?.ProfessionId ?? 0,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);
    }

    private void TelegraphedSkillTimer_Tick(object? sender, EventArgs e)
    {
        _telegraphedSkillTracker?.Poll();
    }
}
