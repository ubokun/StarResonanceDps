// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Concurrent;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

public readonly record struct SkillCooldownDisplayState(
    int? AvailableCharges,
    double? CooldownRemainingSeconds);

/// <summary>
/// 他プレイヤーのクールダウンの推定(自分はサーバーの値)。
///
/// <para>
/// 時計はメッセージの到着時刻(UTC)で、推定の「今」は <see cref="DateTime.UtcNow"/>。発動・リセット・CD を早めるバフの区間はどれもこの時計で控え、
/// エンカウンターには依らない(CD はゲームの側の状態で、エンカウンターの作り直し・マップ移動・死亡では消えない)。
/// 控えを消すのはゲームのリセット(リセット系のバフ。それより前の発動を読むときに外す)と、ログアウト・キャプチャの停止だけ。
/// </para>
///
/// <para>
/// CD の長さは短縮の率で縮めた長さ(<see cref="CooldownModifierStore.ToReducedLength"/>。80秒に率 80% なら 16秒)。CD は実時間で進み、
/// 加速の間はその倍率で速く進む。進みは済んだ割合で数え、発動から今までを区間に分けて足す(短縮の率が変わっても済んだ割合のまま新しい長さに切り替わる)。
/// 短縮の率はその人の一時属性の移り変わり(<see cref="CooldownModifierStore"/>)、加速は CD を早めるバフの区間で、加速はイマジンにだけ効かせる。
/// </para>
/// </summary>
public sealed class SkillCooldownTracker
{
    private const int LowerCdBuffBaseId = 2110034;

    /// <summary>
    /// CD を早めるバフ(<see cref="LowerCdBuffBaseId"/>)の間の加速。自分にはこのバフの間 <c>AttrCdAcceleratePct</c> = 10000(2倍の速さ)が届く。
    /// 他人の <c>AttrCdAcceleratePct</c> は届かないので、バフの区間で代える。
    /// 効くのはイマジンと、表示しない通常攻撃・特殊攻撃・マスタリースキル・究極スキルで、ロールスキルには効かない。
    /// </summary>
    private const double LowerCdAcceleration = 1d;

    private static readonly Lazy<SkillCooldownTracker> LazyInstance = new(() => new SkillCooldownTracker());

    private readonly ConcurrentDictionary<SkillActivationKey, SkillActivationHistory> _activationHistories = new();
    private readonly ConcurrentDictionary<long, CooldownResetSchedule> _resetSchedules = new();
    private readonly ConcurrentDictionary<long, LowerCdIntervalLog> _lowerCdIntervals = new();
    private readonly object _subscriptionSync = new();

    private Encounter? _subscribedEncounter;
    private bool _isInitialized;

    private SkillCooldownTracker()
    {
    }

    public static SkillCooldownTracker Instance => LazyInstance.Value;

    public void Initialize()
    {
        lock (_subscriptionSync)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            EncounterManager.EncounterStart += EncounterManager_EncounterStart;
            SkillCooldownStateStore.CooldownsResetByGame += SkillCooldownStateStore_CooldownsResetByGame;
            SkillCooldownStateStore.CooldownsResetForPlayer += SkillCooldownStateStore_CooldownsResetForPlayer;
            AttachToCurrentEncounter();
        }
    }

    public void Shutdown()
    {
        lock (_subscriptionSync)
        {
            if (!_isInitialized)
            {
                return;
            }

            EncounterManager.EncounterStart -= EncounterManager_EncounterStart;
            SkillCooldownStateStore.CooldownsResetByGame -= SkillCooldownStateStore_CooldownsResetByGame;
            SkillCooldownStateStore.CooldownsResetForPlayer -= SkillCooldownStateStore_CooldownsResetForPlayer;
            if (_subscribedEncounter is not null)
            {
                _subscribedEncounter.SkillActivated -= Encounter_SkillActivated;
                _subscribedEncounter.BuffUpdated -= Encounter_BuffUpdated;
                _subscribedEncounter = null;
            }

            _isInitialized = false;
            _activationHistories.Clear();
            _resetSchedules.Clear();
            _lowerCdIntervals.Clear();
        }
    }

    public SkillCooldownDisplayState GetDisplayState(
        long entityUuid,
        int skillId,
        double estimatedCooldownSeconds,
        bool isImagine,
        int maxCharges,
        double chargeCooldownSeconds)
    {
        var key = new SkillActivationKey(entityUuid, skillId);
        if (maxCharges > 1 && chargeCooldownSeconds > 0)
        {
            _activationHistories.TryGetValue(key, out var chargeHistory);
            var chargeState = IsSelf(entityUuid)
                ? GetSelfChargeState(entityUuid, skillId, maxCharges)
                : GetEstimatedChargeState(
                    entityUuid,
                    skillId,
                    ExcludeBeforeReset(entityUuid, chargeHistory?.GetSnapshot() ?? []),
                    maxCharges,
                    chargeCooldownSeconds,
                    isImagine);
            double? cooldownSeconds = chargeState.AvailableCharges == 0
                && chargeState.NextChargeRemainingSeconds > 0
                    ? chargeState.NextChargeRemainingSeconds
                    : null;

            return new SkillCooldownDisplayState(
                chargeState.AvailableCharges,
                cooldownSeconds);
        }

        var remainingSeconds = IsSelf(entityUuid)
            ? SkillCooldownStateStore.GetSelfRemainingSeconds(entityUuid, skillId)
            : GetEstimatedRemainingSeconds(
                entityUuid,
                key,
                estimatedCooldownSeconds,
                isImagine);
        return new SkillCooldownDisplayState(null, remainingSeconds);
    }

    private double? GetEstimatedRemainingSeconds(
        long entityUuid,
        SkillActivationKey key,
        double estimatedCooldownSeconds,
        bool isImagine)
    {
        if (estimatedCooldownSeconds <= 0
            || !_activationHistories.TryGetValue(key, out var history)
            || history.GetLatest() is not { } activation
            || GetResetCutoff(entityUuid) is { } cutoff && activation.ActivationUtc < cutoff)
        {
            return null;
        }

        var doneFraction = GetDoneCooldowns(
            entityUuid,
            key.SkillId,
            activation.ActivationUtc,
            DateTime.UtcNow,
            isImagine,
            estimatedCooldownSeconds);
        return doneFraction < 1d
            ? (1d - doneFraction) * CooldownModifierStore.ToReducedLength(
                estimatedCooldownSeconds,
                CooldownModifierStore.GetReductionRatio(entityUuid, key.SkillId))
            : null;
    }

    /// <summary>
    /// 起動直後の状態へ戻したときに、他プレイヤーの推測値の元になる発動履歴を全員分破棄する。
    /// 自分の残CDはサーバ真値なのでここでは何もしない。
    /// </summary>
    private void SkillCooldownStateStore_CooldownsResetByGame()
    {
        _activationHistories.Clear();
        _resetSchedules.Clear();
        _lowerCdIntervals.Clear();
    }

    /// <summary>
    /// ゲームがその人のクールダウンをリセットする時刻を控える。消えるのが付与より後のバフがあるので、履歴はここでは消さず、
    /// 読むときに「過ぎたリセットのうち最後の時刻」より前の発動を無視する(<see cref="GetResetCutoff"/>)。
    /// </summary>
    private void SkillCooldownStateStore_CooldownsResetForPlayer(long entityUuid, DateTime resetAt)
    {
        _resetSchedules.GetOrAdd(entityUuid, static _ => new CooldownResetSchedule()).Add(resetAt);
    }

    /// <summary>その人のクールダウンが最後にリセットされた時刻(いまより前のもの)。無ければ null。</summary>
    private DateTime? GetResetCutoff(long entityUuid)
    {
        return _resetSchedules.TryGetValue(entityUuid, out var schedule)
            ? schedule.GetLatestPassed(DateTime.UtcNow)
            : null;
    }

    private void EncounterManager_EncounterStart(EncounterStartEventArgs e)
    {
        lock (_subscriptionSync)
        {
            AttachToCurrentEncounter();
        }

        PruneReductionHistories();
    }

    /// <summary>
    /// 短縮の率の移り変わり(<see cref="CooldownModifierStore"/>)のうち、もう使わないものを捨てる。
    /// 人ごとに、控えている発動のうち一番古いものより前に終わったもの(発動の控えが無い人は今より前に終わったもの)。
    /// </summary>
    private void PruneReductionHistories()
    {
        var nowUtc = DateTime.UtcNow;
        var earliestByEntity = new Dictionary<long, DateTime>();
        foreach (var (key, history) in _activationHistories)
        {
            if (history.GetEarliest() is { } earliest
                && (!earliestByEntity.TryGetValue(key.EntityUuid, out var current) || earliest.ActivationUtc < current))
            {
                earliestByEntity[key.EntityUuid] = earliest.ActivationUtc;
            }
        }

        CooldownModifierStore.Prune(uuid => earliestByEntity.TryGetValue(uuid, out var earliest) ? earliest : nowUtc);
    }

    private void AttachToCurrentEncounter()
    {
        var currentEncounter = EncounterManager.Current;
        if (ReferenceEquals(_subscribedEncounter, currentEncounter))
        {
            return;
        }

        if (_subscribedEncounter is not null)
        {
            _subscribedEncounter.SkillActivated -= Encounter_SkillActivated;
            _subscribedEncounter.BuffUpdated -= Encounter_BuffUpdated;
        }

        _subscribedEncounter = currentEncounter;
        _subscribedEncounter.SkillActivated += Encounter_SkillActivated;
        _subscribedEncounter.BuffUpdated += Encounter_BuffUpdated;
    }

    private void Encounter_SkillActivated(object sender, SkillActivatedEventArgs e)
    {
        // 履歴を読むのはプレイヤーの行だけ。ほかの実体の分はログアウトまで残り続ける。
        if (Utils.UuidToEntityType(e.CasterUuid) != (long)Zproto.EEntityType.EntChar)
        {
            return;
        }

        var activation = new SkillActivationState(e.ActivationDateTime);
        var maxCharges = CombatDataCatalog.GetSkillMaxCharges(e.SkillId);
        var rawChargeCooldownSeconds = maxCharges > 1
            ? CombatDataCatalog.GetSkillChargeCooldownSeconds(e.SkillId, 0)
            : 0d;
        var history = _activationHistories.GetOrAdd(
            new SkillActivationKey(e.CasterUuid, e.SkillId),
            static _ => new SkillActivationHistory());
        history.Record(activation, maxCharges, rawChargeCooldownSeconds);
    }

    /// <summary>
    /// CD を早めるバフ(<see cref="LowerCdBuffBaseId"/>)の区間を、通知の到着時刻で控える。
    /// 付与は区間を開き、層・持続の変化はその時刻から新しい持続で開き直し、除去は閉じる(除去は BaseId を運ばないので実体の UUID で突き合わせる)。
    /// 中身の無い通知(<see cref="BuffEventPayload.None"/>)の 0 はバフの状態ではないので見ない。
    /// </summary>
    private void Encounter_BuffUpdated(object sender, BuffUpdatedEventArgs e)
    {
        if (e.BuffEventType == Zproto.EBuffEventType.BuffEventRemove)
        {
            if (_lowerCdIntervals.TryGetValue(e.EntityUuid, out var removedLog))
            {
                removedLog.Close(e.BuffUuid, e.UpdateDateTime);
            }

            return;
        }

        if (e.Payload == BuffEventPayload.BuffInfo && e.BaseId == LowerCdBuffBaseId && e.Duration > 0)
        {
            _lowerCdIntervals
                .GetOrAdd(e.EntityUuid, static _ => new LowerCdIntervalLog())
                .Open(e.BuffUuid, e.UpdateDateTime, e.Duration, e.CreationDateTime);
            PruneLowerCdIntervals(e.EntityUuid, e.UpdateDateTime);
            return;
        }

        if (e.Payload == BuffEventPayload.BuffChange
            && e.Duration > 0
            && _lowerCdIntervals.TryGetValue(e.EntityUuid, out var changedLog))
        {
            changedLog.Change(e.BuffUuid, e.UpdateDateTime, e.Duration);
        }
    }

    /// <summary>
    /// その人の区間のうち、もう重なり得ないもの(控えている発動のうち一番古いものより前に終わった区間)を捨てる。
    /// 発動の控えが無ければ、今より前に終わった区間を捨てる(この後の発動は今より後)。
    /// </summary>
    private void PruneLowerCdIntervals(long entityUuid, DateTime nowUtc)
    {
        if (!_lowerCdIntervals.TryGetValue(entityUuid, out var log))
        {
            return;
        }

        var cutoff = nowUtc;
        foreach (var (key, history) in _activationHistories)
        {
            if (key.EntityUuid == entityUuid && history.GetEarliest() is { } earliest && earliest.ActivationUtc < cutoff)
            {
                cutoff = earliest.ActivationUtc;
            }
        }

        log.PruneEndedBefore(cutoff);
    }

    /// <summary>最後に過ぎたリセットより前の発動を除く。</summary>
    private IReadOnlyList<SkillActivationState> ExcludeBeforeReset(long entityUuid, IReadOnlyList<SkillActivationState> activations)
    {
        return GetResetCutoff(entityUuid) is { } cutoff
            ? activations.Where(activation => activation.ActivationUtc >= cutoff).ToArray()
            : activations;
    }

    private static bool IsSelf(long entityUuid)
    {
        return entityUuid == MessageManager.currentUserUuid
            || entityUuid == AppState.PlayerUUID;
    }

    private static ChargeCooldownState GetSelfChargeState(
        long entityUuid,
        int skillId,
        int maxCharges)
    {
        var state = SkillCooldownStateStore.GetSelfChargeCooldownState(
            entityUuid,
            skillId,
            maxCharges);
        return new ChargeCooldownState(
            state.AvailableCharges,
            state.NextChargeRemainingSeconds,
            state.FullRecoveryRemainingSeconds);
    }

    private ChargeCooldownState GetEstimatedChargeState(
        long entityUuid,
        int skillId,
        IReadOnlyList<SkillActivationState> activationHistory,
        int maxCharges,
        double chargeCooldownSeconds,
        bool isImagine)
    {
        if (activationHistory.Count == 0)
        {
            return ChargeCooldownState.FullyRecovered(maxCharges);
        }

        var activations = activationHistory
            .OrderBy(activation => activation.ActivationUtc)
            .ToArray();
        // 進みはチャージ1回ぶんを 1 とした済んだ回数で数え、残りは今の縮めた長さを掛けて秒にする。
        var origin = activations[0];
        var activationDoneCharges = activations
            .Select(activation => GetDoneCooldowns(
                entityUuid,
                skillId,
                origin.ActivationUtc,
                activation.ActivationUtc,
                isImagine,
                chargeCooldownSeconds))
            .ToArray();
        var currentDoneCharges = GetDoneCooldowns(
            entityUuid,
            skillId,
            origin.ActivationUtc,
            DateTime.UtcNow,
            isImagine,
            chargeCooldownSeconds);

        var state = CalculateChargeState(
            activationDoneCharges,
            currentDoneCharges,
            maxCharges,
            1d);
        var chargeLengthSeconds = CooldownModifierStore.ToReducedLength(
            chargeCooldownSeconds,
            CooldownModifierStore.GetReductionRatio(entityUuid, skillId));
        return state with
        {
            NextChargeRemainingSeconds = state.NextChargeRemainingSeconds * chargeLengthSeconds,
            FullRecoveryRemainingSeconds = state.FullRecoveryRemainingSeconds * chargeLengthSeconds,
        };
    }

    private static ChargeCooldownState CalculateChargeState(
        IReadOnlyList<double> activationProgressTimes,
        double currentProgressTime,
        int maxCharges,
        double chargeCooldownSeconds)
    {
        if (maxCharges <= 1 || chargeCooldownSeconds <= 0)
        {
            return default;
        }

        var availableCharges = maxCharges;
        var missingCharges = 0;
        var nextRecoveryProgressTime = 0d;

        void RecoverThrough(double progressTime)
        {
            while (missingCharges > 0 && nextRecoveryProgressTime <= progressTime)
            {
                missingCharges--;
                availableCharges++;
                if (missingCharges > 0)
                {
                    nextRecoveryProgressTime += chargeCooldownSeconds;
                }
                else
                {
                    nextRecoveryProgressTime = 0d;
                }
            }
        }

        foreach (var activationProgressTime in activationProgressTimes)
        {
            RecoverThrough(activationProgressTime);
            if (availableCharges <= 0)
            {
                continue;
            }

            availableCharges--;
            missingCharges++;
            if (missingCharges == 1)
            {
                nextRecoveryProgressTime = activationProgressTime + chargeCooldownSeconds;
            }
        }

        RecoverThrough(currentProgressTime);
        if (missingCharges == 0)
        {
            return ChargeCooldownState.FullyRecovered(maxCharges);
        }

        var nextChargeRemainingSeconds = Math.Max(
            nextRecoveryProgressTime - currentProgressTime,
            0d);
        var fullRecoveryRemainingSeconds = nextChargeRemainingSeconds
            + chargeCooldownSeconds * (missingCharges - 1);
        return new ChargeCooldownState(
            availableCharges,
            nextChargeRemainingSeconds,
            fullRecoveryRemainingSeconds);
    }

    /// <summary>
    /// [<paramref name="startUtc"/>, <paramref name="endUtc"/>] に済んだ CD の回数(長さ <paramref name="lengthSeconds"/> の CD を 1 とする)。
    /// 短縮の率が変わった時刻と、CD を早めるバフの区間の端で区切り、区間ごとに「実時間 ×(1 + 加速)÷ 縮めた長さ」を足す。
    /// 加速はイマジンにだけ効かせる。縮めた長さが 0 の区間があれば正の無限大(すぐ終わる)。
    /// </summary>
    private double GetDoneCooldowns(
        long entityUuid,
        int skillId,
        DateTime startUtc,
        DateTime endUtc,
        bool isImagine,
        double lengthSeconds)
    {
        if (endUtc <= startUtc)
        {
            return 0d;
        }

        // 率が変わらず CD を早めるバフも無ければ区間は1つだけ。描画のたびに通るので、一覧を作らずに数える。
        LowerCdIntervalLog? log = null;
        var hasLowerCdInterval = isImagine
            && _lowerCdIntervals.TryGetValue(entityUuid, out log)
            && log.HasIntervalIn(startUtc, endUtc);
        if (!hasLowerCdInterval
            && CooldownModifierStore.TryGetConstantReductionRatio(entityUuid, skillId, startUtc, out var constantRatio))
        {
            var constantLengthSeconds = CooldownModifierStore.ToReducedLength(lengthSeconds, constantRatio);
            return constantLengthSeconds <= 0d
                ? double.PositiveInfinity
                : endUtc.Subtract(startUtc).TotalSeconds / constantLengthSeconds;
        }

        var ratioChanges = CooldownModifierStore.GetReductionChanges(entityUuid, skillId, startUtc);
        IReadOnlyList<LowerCdInterval> lowerCdIntervals = hasLowerCdInterval
            ? log!.GetMergedIntervals(startUtc, endUtc)
            : [];

        var boundaries = new SortedSet<DateTime> { startUtc, endUtc };
        foreach (var (changeUtc, _) in ratioChanges)
        {
            if (changeUtc > startUtc && changeUtc < endUtc)
            {
                boundaries.Add(changeUtc);
            }
        }

        foreach (var interval in lowerCdIntervals)
        {
            boundaries.Add(interval.Start);
            boundaries.Add(interval.End);
        }

        var total = 0d;
        DateTime? previous = null;
        foreach (var boundary in boundaries)
        {
            if (previous is { } segmentStart)
            {
                var ratio = GetRatioAt(ratioChanges, segmentStart);
                var acceleration = IsInLowerCdInterval(lowerCdIntervals, segmentStart)
                    ? LowerCdAcceleration
                    : 0d;
                var reducedLengthSeconds = CooldownModifierStore.ToReducedLength(lengthSeconds, ratio);
                if (reducedLengthSeconds <= 0d)
                {
                    return double.PositiveInfinity;
                }

                total += boundary.Subtract(segmentStart).TotalSeconds * (1d + acceleration) / reducedLengthSeconds;
            }

            previous = boundary;
        }

        return total;
    }

    /// <summary>その時刻に効いている短縮の率。<paramref name="changes"/> は時刻の順で、最初の要素が区間の始まり。</summary>
    private static double GetRatioAt(IReadOnlyList<(DateTime StartUtc, double Ratio)> changes, DateTime time)
    {
        for (var index = changes.Count - 1; index >= 0; index--)
        {
            if (changes[index].StartUtc <= time)
            {
                return changes[index].Ratio;
            }
        }

        throw new InvalidOperationException($"No cooldown reduction ratio applies at {time:o}.");
    }

    private static bool IsInLowerCdInterval(IReadOnlyList<LowerCdInterval> intervals, DateTime time)
    {
        for (var index = 0; index < intervals.Count; index++)
        {
            if (intervals[index].Start <= time && time < intervals[index].End)
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct SkillActivationKey(long EntityUuid, int SkillId);

    /// <param name="ActivationUtc">発動が届いたメッセージの到着時刻。</param>
    private readonly record struct SkillActivationState(DateTime ActivationUtc);

    private readonly record struct LowerCdInterval(DateTime Start, DateTime End);

    /// <summary>
    /// 1人ぶんの CD を早めるバフの区間(到着時刻、UTC)。開いている区間は実体の UUID ごとに持ち、持続の分だけ続くものとして数える。
    /// </summary>
    private sealed class LowerCdIntervalLog
    {
        private readonly object _sync = new();
        private readonly Dictionary<int, OpenInterval> _openByBuffUuid = [];
        private readonly List<LowerCdInterval> _closed = [];

        private readonly record struct OpenInterval(DateTime Start, DateTime ScheduledEnd, int DurationMilliseconds, DateTime? CreationUtc);

        /// <summary>
        /// 付与で区間を開く。同じ実体の付与が届き直したとき(出現のたびの一覧など)は区間を伸ばさない。
        /// 同じ実体かは、付与時刻と持続が一致するかで見る(ライブのバフの控えと同じ決め方)。
        /// </summary>
        public void Open(int buffUuid, DateTime startUtc, int durationMilliseconds, DateTime? creationUtc)
        {
            lock (_sync)
            {
                if (_openByBuffUuid.TryGetValue(buffUuid, out var open))
                {
                    if (open.DurationMilliseconds == durationMilliseconds && open.CreationUtc == creationUtc)
                    {
                        return;
                    }

                    CloseNoLock(buffUuid, open, startUtc);
                }

                _openByBuffUuid[buffUuid] = new OpenInterval(startUtc, startUtc.AddMilliseconds(durationMilliseconds), durationMilliseconds, creationUtc);
            }
        }

        /// <summary>層・持続の変化。その時刻で区間を閉じ、新しい持続で開き直す。</summary>
        public void Change(int buffUuid, DateTime changeUtc, int durationMilliseconds)
        {
            lock (_sync)
            {
                if (!_openByBuffUuid.TryGetValue(buffUuid, out var open))
                {
                    return;
                }

                CloseNoLock(buffUuid, open, changeUtc);
                _openByBuffUuid[buffUuid] = new OpenInterval(changeUtc, changeUtc.AddMilliseconds(durationMilliseconds), durationMilliseconds, open.CreationUtc);
            }
        }

        public void Close(int buffUuid, DateTime removeUtc)
        {
            lock (_sync)
            {
                if (_openByBuffUuid.TryGetValue(buffUuid, out var open))
                {
                    CloseNoLock(buffUuid, open, removeUtc);
                }
            }
        }

        /// <summary><paramref name="cutoffUtc"/> より前に終わった区間を捨てる(持続の終わりを過ぎた開いている区間も)。</summary>
        public void PruneEndedBefore(DateTime cutoffUtc)
        {
            lock (_sync)
            {
                _closed.RemoveAll(interval => interval.End <= cutoffUtc);
                foreach (var (buffUuid, open) in _openByBuffUuid.Where(pair => pair.Value.ScheduledEnd <= cutoffUtc).ToArray())
                {
                    _openByBuffUuid.Remove(buffUuid);
                }
            }
        }

        /// <summary>[<paramref name="rangeStartUtc"/>, <paramref name="rangeEndUtc"/>] に重なる区間があるか。一覧を作らない。</summary>
        public bool HasIntervalIn(DateTime rangeStartUtc, DateTime rangeEndUtc)
        {
            lock (_sync)
            {
                foreach (var interval in _closed)
                {
                    if (interval.End > rangeStartUtc && interval.Start < rangeEndUtc)
                    {
                        return true;
                    }
                }

                foreach (var open in _openByBuffUuid.Values)
                {
                    if (open.ScheduledEnd > rangeStartUtc && open.Start < rangeEndUtc)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>[<paramref name="rangeStartUtc"/>, <paramref name="rangeEndUtc"/>] に切り詰め、重なる区間をまとめた区間(始まりの順)。</summary>
        public IReadOnlyList<LowerCdInterval> GetMergedIntervals(DateTime rangeStartUtc, DateTime rangeEndUtc)
        {
            List<LowerCdInterval> intervals;
            lock (_sync)
            {
                intervals = [.. _closed, .. _openByBuffUuid.Values.Select(open => new LowerCdInterval(open.Start, open.ScheduledEnd))];
            }

            var clipped = intervals
                .Select(interval => new LowerCdInterval(
                    interval.Start > rangeStartUtc ? interval.Start : rangeStartUtc,
                    interval.End < rangeEndUtc ? interval.End : rangeEndUtc))
                .Where(interval => interval.End > interval.Start)
                .OrderBy(interval => interval.Start)
                .ToList();
            var merged = new List<LowerCdInterval>();
            foreach (var interval in clipped)
            {
                if (merged.Count > 0 && interval.Start <= merged[^1].End)
                {
                    if (interval.End > merged[^1].End)
                    {
                        merged[^1] = merged[^1] with { End = interval.End };
                    }

                    continue;
                }

                merged.Add(interval);
            }

            return merged;
        }

        private void CloseNoLock(int buffUuid, OpenInterval open, DateTime endUtc)
        {
            _openByBuffUuid.Remove(buffUuid);
            var end = endUtc < open.ScheduledEnd ? endUtc : open.ScheduledEnd;
            if (end > open.Start)
            {
                _closed.Add(new LowerCdInterval(open.Start, end));
            }
        }
    }

    /// <summary>
    /// 1人ぶんのクールダウンのリセットの時刻。まだ来ていない時刻(付与より後に消えるバフ)も持つ。
    /// </summary>
    private sealed class CooldownResetSchedule
    {
        private readonly object _sync = new();
        private readonly List<DateTime> _times = [];

        public void Add(DateTime resetAt)
        {
            lock (_sync)
            {
                _times.Add(resetAt);
            }
        }

        /// <summary>
        /// <paramref name="now"/> までに過ぎたリセットのうち最後の時刻。無ければ null。
        /// それより前の時刻はもう使わないので捨て、まだ来ていない時刻は残す。
        /// </summary>
        public DateTime? GetLatestPassed(DateTime now)
        {
            lock (_sync)
            {
                DateTime? latest = null;
                foreach (var time in _times)
                {
                    if (time <= now && (latest is null || time > latest))
                    {
                        latest = time;
                    }
                }

                if (latest is { } kept)
                {
                    _times.RemoveAll(time => time < kept);
                }

                return latest;
            }
        }
    }

    private readonly record struct ChargeCooldownState(
        int AvailableCharges,
        double NextChargeRemainingSeconds,
        double FullRecoveryRemainingSeconds)
    {
        public static ChargeCooldownState FullyRecovered(int maxCharges)
        {
            return new ChargeCooldownState(Math.Max(maxCharges, 0), 0d, 0d);
        }
    }

    private sealed class SkillActivationHistory
    {
        private readonly object _sync = new();
        private readonly List<SkillActivationState> _activations = [];

        public void Record(
            SkillActivationState activation,
            int maxCharges,
            double rawChargeCooldownSeconds)
        {
            lock (_sync)
            {
                if (maxCharges <= 1 || rawChargeCooldownSeconds <= 0)
                {
                    _activations.Clear();
                    _activations.Add(activation);
                    return;
                }

                if (AreAllChargesRecovered(
                    activation.ActivationUtc,
                    maxCharges,
                    rawChargeCooldownSeconds))
                {
                    _activations.Clear();
                }

                _activations.Add(activation);
            }
        }

        private bool AreAllChargesRecovered(
            DateTime targetDateTime,
            int maxCharges,
            double chargeCooldownSeconds)
        {
            if (_activations.Count == 0)
            {
                return true;
            }

            var activations = _activations
                .OrderBy(activation => activation.ActivationUtc)
                .ToArray();
            var originDateTime = activations[0].ActivationUtc;
            var activationProgressTimes = activations
                .Select(activation => Math.Max(
                    activation.ActivationUtc.Subtract(originDateTime).TotalSeconds,
                    0d))
                .ToArray();
            var currentProgressTime = Math.Max(
                targetDateTime.Subtract(originDateTime).TotalSeconds,
                0d);

            return CalculateChargeState(
                activationProgressTimes,
                currentProgressTime,
                maxCharges,
                chargeCooldownSeconds).FullRecoveryRemainingSeconds <= 0;
        }

        public SkillActivationState? GetLatest()
        {
            lock (_sync)
            {
                if (_activations.Count == 0)
                {
                    return null;
                }

                var latest = _activations[0];
                for (var index = 1; index < _activations.Count; index++)
                {
                    if (_activations[index].ActivationUtc > latest.ActivationUtc)
                    {
                        latest = _activations[index];
                    }
                }

                return latest;
            }
        }

        public SkillActivationState? GetEarliest()
        {
            lock (_sync)
            {
                return _activations.Count == 0
                    ? null
                    : _activations.MinBy(activation => activation.ActivationUtc);
            }
        }

        public SkillActivationState[] GetSnapshot()
        {
            lock (_sync)
            {
                return [.. _activations];
            }
        }
    }
}
