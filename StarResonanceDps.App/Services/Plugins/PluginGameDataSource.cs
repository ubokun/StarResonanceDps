// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Threading;
using System.Windows;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 本体が通信から控えた値を、プラグインへ渡す形(SDK の型)へ写して持つ。全部のプラグインで1つ。
/// 知らせは画面のスレッドで出し、プラグインへの知らせはプラグインごとの口(<see cref="PluginGameDataService"/>)が出す。
///
/// <para>
/// 新しい控えは作らない。名簿・周りの敵・モジュールの写しは、本体の控えの中身が同じなら前回の写しを返し、
/// 本体の控えの知らせで作り直す(ログアウト・キャプチャの停止・マップ移動で本体の控えが消えれば、写しも空になる)。
/// 名簿・周りの敵・接続とマップの知らせは画面の1回の更新につき1回にまとめ、合図(回の始まりと終わり・マップ移動・ログアウト)はまとめずに起きた順に出す。
/// </para>
/// </summary>
internal sealed class PluginGameDataSource
{
    private readonly object _sync = new();
    private readonly CoalescedUiNotification _selfModulesNotification;
    private readonly CoalescedUiNotification _sessionNotification;
    private readonly CoalescedUiNotification _playersNotification;
    private readonly CoalescedUiNotification _entitiesNotification;

    private IReadOnlyList<PlayerRosterEntry>? _playersSource;
    private IReadOnlyList<PluginPlayer> _players = Array.Empty<PluginPlayer>();
    private IReadOnlyList<NearbyEntityEntry>? _entitiesSource;
    private IReadOnlyList<PluginEntity> _entities = Array.Empty<PluginEntity>();

    // 最後に知らせた接続とマップ(画面のスレッドだけが読み書きする)。中身が変わったときだけ知らせる。
    private PluginSessionState _notifiedSession;

    private PluginGameDataSource()
    {
        _selfModulesNotification = new CoalescedUiNotification(RefreshSelfModules);
        _sessionNotification = new CoalescedUiNotification(RefreshSession);
        _playersNotification = new CoalescedUiNotification(() => PlayersChanged?.Invoke(this, EventArgs.Empty));
        _entitiesNotification = new CoalescedUiNotification(() => EntitiesChanged?.Invoke(this, EventArgs.Empty));

        SelfModules = ToPluginInventory(SelfModuleStore.GetSnapshot());
        _notifiedSession = ReadSession();

        SelfModuleStore.Changed += _selfModulesNotification.Queue;
        PlayerRosterPresentationStore.Instance.RosterChanged += (_, _) => _playersNotification.Queue();
        NearbyEntityStore.Instance.EntitiesChanged += (_, _) => _entitiesNotification.Queue();
        GameReceptionStateStore.Changed += _sessionNotification.Queue;
        SeasonStateStore.Changed += _sessionNotification.Queue;
        BattleStateMachine.DungeonStateChanged += _sessionNotification.Queue;
        MessageManager.SceneApplied += MessageManager_SceneApplied;
        MessageManager.ResetToStartupCompleted += MessageManager_ResetToStartupCompleted;
        EncounterManager.EncounterStart += _ => Post(() => EncounterStarted?.Invoke(this, EventArgs.Empty));
        EncounterManager.EncounterClosing += EncounterManager_EncounterClosing;
    }

    public static PluginGameDataSource Instance { get; } = new();

    public PluginGameServer CurrentServer => ToPluginServer(GameServerStore.Current);

    public PluginModuleInventory? SelfModules { get; private set; }

    public PluginSessionState Session => ReadSession();

    public IReadOnlyList<PluginPlayer> Players
    {
        get
        {
            var source = PlayerRosterPresentationStore.Instance.Current.Entries;
            lock (_sync)
            {
                if (!ReferenceEquals(source, _playersSource))
                {
                    _players = source.Select(ToPluginPlayer).ToArray();
                    _playersSource = source;
                }

                return _players;
            }
        }
    }

    public IReadOnlyList<PluginEntity> Entities
    {
        get
        {
            var source = NearbyEntityStore.Instance.Current.Entries;
            lock (_sync)
            {
                if (!ReferenceEquals(source, _entitiesSource))
                {
                    _entities = source.Select(ToPluginEntity).ToArray();
                    _entitiesSource = source;
                }

                return _entities;
            }
        }
    }

    /// <summary>以下の知らせは全部画面のスレッドで上がる。</summary>
    public event EventHandler? SelfModulesChanged;

    public event EventHandler? SessionChanged;

    public event EventHandler? PlayersChanged;

    public event EventHandler? EntitiesChanged;

    public event EventHandler? EncounterStarted;

    public event EventHandler<PluginEncounterResult>? EncounterClosed;

    public event EventHandler? MapEntered;

    public event EventHandler? LoggedOut;

    public IReadOnlyList<PluginBuff> GetPlayerBuffs(long characterId)
    {
        return ToPluginBuffs(MeterSnapshotProvider.GetActivePlayerBuffs(characterId));
    }

    public IReadOnlyList<PluginBuff> GetEntityBuffs(long entityUuid)
    {
        return ToPluginBuffs(MeterSnapshotProvider.GetActiveEntityBuffs(entityUuid));
    }

    public PluginEncounterSnapshot GetEncounter()
    {
        return ToPluginEncounter(MeterSnapshotProvider.GetEncounterTotals());
    }

    public IReadOnlyList<PluginSkillTotals> GetSkillTotals(long characterId, PluginCombatKind kind)
    {
        return ToPluginSkills(MeterSnapshotProvider.GetPlayerSkillTable(ToMeterKind(kind), characterId));
    }

    private void RefreshSelfModules()
    {
        SelfModules = ToPluginInventory(SelfModuleStore.GetSnapshot());
        SelfModulesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshSession()
    {
        var session = ReadSession();
        if (session == _notifiedSession)
        {
            return;
        }

        _notifiedSession = session;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MessageManager_SceneApplied(bool isNewMap)
    {
        _sessionNotification.Queue();
        if (isNewMap)
        {
            Post(() => MapEntered?.Invoke(this, EventArgs.Empty));
        }
    }

    private void MessageManager_ResetToStartupCompleted()
    {
        _sessionNotification.Queue();
        Post(() => LoggedOut?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>
    /// パケットのスレッドから来る。閉じた回にはもう記録が入らないので、結果は画面のスレッドで作る(パケットの処理を待たせない)。
    /// </summary>
    private void EncounterManager_EncounterClosing(Encounter encounter)
    {
        Post(() => EncounterClosed?.Invoke(this, CreateEncounterResult(encounter)));
    }

    private static void Post(Action action)
    {
        // 終了の途中で画面が無ければ、知らせる相手も無い。
        Application.Current?.Dispatcher.BeginInvoke(action);
    }

    private static PluginSessionState ReadSession()
    {
        var selfCharacterId = AppState.PlayerUID;
        var sceneId = EncounterManager.LevelMapId;
        var seasonId = SeasonStateStore.CurrentSeasonId;
        var dungeonState = BattleStateMachine.IsInOpenWorld()
            ? PluginDungeonState.NotInDungeon
            : BattleStateMachine.IsDungeonFinished()
                ? PluginDungeonState.Finished
                : PluginDungeonState.InDungeon;
        return new PluginSessionState(
            selfCharacterId != 0 ? selfCharacterId : null,
            sceneId != 0 ? (int)sceneId : null,
            seasonId != 0 ? seasonId : null,
            dungeonState,
            GameReceptionStateStore.Current.IsMidLaunch);
    }

    private static PluginPlayer ToPluginPlayer(PlayerRosterEntry entry)
    {
        var specState = entry.ClassSpec switch
        {
            PlayerClassSpec.Unknown => PluginSpecState.Unknown,
            PlayerClassSpec.Rank1 => PluginSpecState.NotEquipped,
            _ when PlayerClassSpecResolver.IsTransformation(entry.ClassSpec) => PluginSpecState.Transformation,
            _ => PluginSpecState.Specialized,
        };

        return new PluginPlayer(
            entry.CharacterId,
            entry.Name,
            entry.ProfessionId,
            specState,
            specState == PluginSpecState.Specialized ? entry.SubProfessionId : null,
            entry.Level,
            entry.CombatPower,
            entry.SeasonStrength,
            entry.CurrentHp,
            entry.MaxHp,
            entry.CurrentShield,
            entry.PartyNumber,
            entry.IsSelf,
            entry.IsNpc,
            entry.IsPartyMember,
            entry.IsNearby,
            entry.IsInCombat,
            entry.IsIncapacitated,
            entry.SeasonTalentBuffId != 0 ? entry.SeasonTalentBuffId : null,
            entry.IsSeasonTalentInactive,
            entry.IsFromRecord);
    }

    private static PluginEntity ToPluginEntity(NearbyEntityEntry entry)
    {
        var hateRanking = new PluginHateRank[entry.HateRanking.Count];
        for (var index = 0; index < hateRanking.Length; index++)
        {
            var rank = entry.HateRanking[index];
            hateRanking[index] = new PluginHateRank(rank.CharacterId, rank.HateValue);
        }

        return new PluginEntity(
            entry.EntityUuid,
            entry.EntityId > 0 ? (int)entry.EntityId : null,
            entry.CurrentHp,
            entry.MaxHp,
            entry.IsInCombat,
            entry.Cast is { } cast ? new PluginEntityCast(cast.SkillId, cast.StartedAtUtc, cast.TotalSeconds) : null,
            entry.TargetCharacterId != 0 ? entry.TargetCharacterId : null,
            hateRanking);
    }

    private static IReadOnlyList<PluginBuff> ToPluginBuffs(IReadOnlyList<ActiveBuffSnapshot> buffs)
    {
        return buffs.Count == 0
            ? Array.Empty<PluginBuff>()
            : buffs
                .Select(buff => new PluginBuff(
                    buff.BuffUuid,
                    buff.BuffId,
                    buff.Level,
                    buff.Layer,
                    buff.DurationSeconds,
                    buff.EndUtc,
                    buff.IsRemainingUnknown,
                    buff.CasterEntityUuid,
                    buff.CasterCharacterId))
                .ToArray();
    }

    private static PluginEncounterSnapshot ToPluginEncounter(EncounterTotalsSnapshot totals)
    {
        return new PluginEncounterSnapshot(
            totals.IsHistory,
            totals.StartUtc,
            totals.Elapsed,
            totals.IsClosed,
            totals.Players
                .Select(player => new PluginEncounterPlayer(
                    player.CharacterId,
                    ToPluginTotals(player.Damage),
                    ToPluginTotals(player.Healing),
                    ToPluginTotals(player.DamageTaken)))
                .ToArray());
    }

    private static PluginCombatTotals ToPluginTotals(CombatTotalsSnapshot totals)
    {
        return new PluginCombatTotals(
            totals.Total,
            totals.PerSecond,
            totals.ActivePerSecond,
            totals.Hits,
            totals.CritTotal,
            totals.CritCount,
            totals.LuckyTotal,
            totals.LuckyCount);
    }

    private static IReadOnlyList<PluginSkillTotals> ToPluginSkills(MetricSkillTableSnapshot table)
    {
        return table.Entries.Count == 0
            ? Array.Empty<PluginSkillTotals>()
            : table.Entries
                .Select(row => new PluginSkillTotals(
                    row.SkillIdText,
                    string.IsNullOrEmpty(row.PlainName) ? null : row.PlainName,
                    row.TotalValue,
                    row.ValuePerSecond,
                    row.ValuePerSecondActive,
                    row.HitCount,
                    row.CritRate,
                    row.Percentage))
                .ToArray();
    }

    private static PluginEncounterResult CreateEncounterResult(Encounter encounter)
    {
        var totals = ToPluginEncounter(MeterSnapshotProvider.GetEncounterTotals(encounter));
        var skills = totals.Players.ToDictionary(
            player => player.CharacterId,
            player => new PluginPlayerSkills(
                ToPluginSkills(MeterSnapshotProvider.GetPlayerSkillTable(encounter, MeterSnapshotKind.Damage, player.CharacterId)),
                ToPluginSkills(MeterSnapshotProvider.GetPlayerSkillTable(encounter, MeterSnapshotKind.Healing, player.CharacterId))));
        return new PluginEncounterResult(totals, skills);
    }

    private static MeterSnapshotKind ToMeterKind(PluginCombatKind kind)
    {
        return kind switch
        {
            PluginCombatKind.Damage => MeterSnapshotKind.Damage,
            PluginCombatKind.Healing => MeterSnapshotKind.Healing,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown combat kind."),
        };
    }

    private static PluginModuleInventory? ToPluginInventory(SelfModuleInventory? inventory)
    {
        if (inventory is null)
        {
            return null;
        }

        var modules = inventory.Modules
            .Select(module => new PluginModuleItem(
                module.Uuid,
                module.ConfigId,
                module.Quality,
                module.Effects.Select(effect => new PluginModuleEffect(effect.EffectId, effect.LinkCount)).ToArray()))
            .ToArray();
        return new PluginModuleInventory(modules, inventory.EquippedUuidBySlot);
    }

    private static PluginGameServer ToPluginServer(GameServer server)
    {
        return server switch
        {
            GameServer.Unknown => PluginGameServer.Unknown,
            GameServer.Asia => PluginGameServer.Asia,
            GameServer.China => PluginGameServer.China,
            GameServer.Global => PluginGameServer.Global,
            GameServer.SoutheastAsia => PluginGameServer.SoutheastAsia,
            _ => throw new ArgumentOutOfRangeException(nameof(server), server, "The game server has no plugin equivalent."),
        };
    }

    /// <summary>
    /// ほかのスレッドから来た知らせを、画面のスレッドへの予約1回にまとめる。予約が走った時点の値で知らせる。
    /// </summary>
    private sealed class CoalescedUiNotification
    {
        private readonly Action _notify;
        private int _isQueued;

        public CoalescedUiNotification(Action notify)
        {
            _notify = notify;
        }

        public void Queue()
        {
            if (Interlocked.Exchange(ref _isQueued, 1) == 1)
            {
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                // 終了の途中で画面が無い。知らせる相手も無い。
                Interlocked.Exchange(ref _isQueued, 0);
                return;
            }

            dispatcher.BeginInvoke(() =>
            {
                Interlocked.Exchange(ref _isQueued, 0);
                _notify();
            });
        }
    }
}
