// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Diagnostics;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public readonly record struct SelfChargeCooldownState(
    int AvailableCharges,
    double NextChargeRemainingSeconds,
    double FullRecoveryRemainingSeconds);

public static class SkillCooldownStateStore
{
    private static readonly object Sync = new();
    private static readonly Dictionary<int, CooldownState> SelfCooldowns = [];

    /// <summary>
    /// 控えの上で終わった普通の CD の開始時刻(技ごと)。サーバーの CD の差分は値を作った時刻より遅れて届くことがあるので、
    /// 終わった後に同じ開始時刻の差分が届いても作り直さない。開始時刻が違えば次に使った CD として受ける。
    /// </summary>
    private static readonly Dictionary<int, long> FinishedBeginTimes = [];

    private static long _selfUuid;
    private static int _accelerationPct;
    private static long _clientServerTimeDeltaMilliseconds;
    private static bool _hasClientServerTimeDelta;

    /// <summary>
    /// 起動直後の状態へ戻したとき(<see cref="Reset"/>)に発火する。
    /// 他プレイヤーの残CDはアプリ側の推測値なので、購読側で全員の分を捨てる。
    /// </summary>
    public static event Action? CooldownsResetByGame;

    /// <summary>
    /// ゲームがその人のクールダウンをリセットするとき(クールダウンのリセットのバフが付いたとき)に、その人の実体の UUID と、消える時刻で発火する。
    /// 他プレイヤーの残CDはアプリ側の推測値なので、購読側でその時刻より前の発動を捨てる。
    /// </summary>
    public static event Action<long, DateTime>? CooldownsResetForPlayer;

    public static void Reset()
    {
        lock (Sync)
        {
            SelfCooldowns.Clear();
            FinishedBeginTimes.Clear();
            _selfUuid = 0;
            _accelerationPct = 0;
            _clientServerTimeDeltaMilliseconds = 0;
            _hasClientServerTimeDelta = false;
        }

        CooldownsResetByGame?.Invoke();
    }

    /// <summary>
    /// ゲームがその人のクールダウンをリセットすることを通知する。推測値の購読者に、その人の分の破棄を促す。
    /// </summary>
    /// <param name="resetAt">
    /// クールダウンが消える時刻。時計は発動の時刻(<see cref="SkillActivatedEventArgs.ActivationDateTime"/>)と同じ、メッセージの到着時刻(UTC)。
    /// バフによっては付与より後になる。
    /// </param>
    public static void NotifyCooldownsResetForPlayer(long uuid, DateTime resetAt)
    {
        CooldownsResetForPlayer?.Invoke(uuid, resetAt);
    }

    public static void SetSelfPlayer(long uuid)
    {
        if (uuid == 0)
        {
            return;
        }

        lock (Sync)
        {
            EnsureSelfPlayer(uuid);
        }
    }

    public static void UpdateServerTime(long clientMilliseconds, long serverMilliseconds)
    {
        if (clientMilliseconds <= 0 || serverMilliseconds <= 0)
        {
            return;
        }

        lock (Sync)
        {
            _clientServerTimeDeltaMilliseconds = clientMilliseconds - serverMilliseconds;
            _hasClientServerTimeDelta = true;
        }
    }

    /// <summary>
    /// いまのサーバ時刻(Unixミリ秒)。<c>SyncServerTime</c> をまだ受け取っていなければ false。
    ///
    /// <para>
    /// サーバ由来の絶対時刻(バフの <c>CreateTime</c>、CDの <c>BeginTime</c>)から
    /// <b>既に経過した分</b>を出すのに要る。ローカル時計との差を挟まずに引くと、
    /// 通信遅延と時計のずれがそのまま乗る(→ CLAUDE.md「バフの残り時間の起点は受信時刻」)。
    /// </para>
    ///
    /// <para>
    /// 差の出所は <c>SyncServerTime</c> の <c>ClientMilliseconds - ServerMilliseconds</c>。
    /// <c>ClientMilliseconds</c> がこの端末の Unix ミリ秒であることは、
    /// <see cref="ResolveInitialDoneFraction"/> が同じ式で本番のCD表示を出していることによる。
    /// </para>
    /// </summary>
    public static bool TryGetServerNowUnixMilliseconds(out long serverNowUnixMilliseconds)
    {
        lock (Sync)
        {
            if (!_hasClientServerTimeDelta)
            {
                serverNowUnixMilliseconds = 0;
                return false;
            }

            serverNowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                - _clientServerTimeDeltaMilliseconds;
            return true;
        }
    }

    public static void UpdateSelfCooldowns(long uuid, IEnumerable<SkillCD> cooldowns)
    {
        if (uuid == 0)
        {
            return;
        }

        var nowTimestamp = Stopwatch.GetTimestamp();

        lock (Sync)
        {
            EnsureSelfPlayer(uuid);

            foreach (var cooldown in cooldowns)
            {
                if (!TryResolveSkillId(cooldown.SkillLevelId, out var skillId))
                {
                    continue;
                }

                switch (cooldown.SkillCDType)
                {
                    case ESkillCDType.EskillCdnormal:
                        UpdateNormalCooldown(skillId, cooldown, nowTimestamp);
                        break;
                    case ESkillCDType.EskillCdcharge:
                        UpdateChargeCooldown(skillId, cooldown, nowTimestamp);
                        break;
                    default:
                        FinishCooldown(skillId);
                        break;
                }
            }
        }
    }

    public static void UpdateSelfAcceleration(long uuid, int accelerationPct)
    {
        if (uuid == 0)
        {
            return;
        }

        var nowTimestamp = Stopwatch.GetTimestamp();

        lock (Sync)
        {
            EnsureSelfPlayer(uuid);
            SettleProgressNoLock(nowTimestamp);
            _accelerationPct = Math.Max(accelerationPct, 0);
        }
    }

    /// <summary>
    /// 自分の CD の長さか進む速さが変わる直前に呼ぶ。今までの長さと速さでここまでに済んだ割合を確定し、ここから新しい長さと速さで進める。
    /// CD を率で短縮する一時属性(<see cref="CooldownModifierStore"/>)を変える前に呼ぶ。自分でなければ何もしない。
    /// </summary>
    public static void SettleSelfProgress(long uuid)
    {
        var nowTimestamp = Stopwatch.GetTimestamp();

        lock (Sync)
        {
            if (uuid == 0 || uuid != _selfUuid)
            {
                return;
            }

            SettleProgressNoLock(nowTimestamp);
        }
    }

    private static void SettleProgressNoLock(long nowTimestamp)
    {
        foreach (var cooldown in SelfCooldowns.Values)
        {
            cooldown.DoneFraction = GetDoneFraction(cooldown, nowTimestamp);
            cooldown.AnchorTimestamp = nowTimestamp;
        }
    }

    public static double? GetSelfRemainingSeconds(long uuid, int skillId)
    {
        if (uuid == 0 || skillId <= 0)
        {
            return null;
        }

        var nowTimestamp = Stopwatch.GetTimestamp();

        lock (Sync)
        {
            if (uuid != _selfUuid
                || !SelfCooldowns.TryGetValue(skillId, out var cooldown)
                || cooldown.Type != ESkillCDType.EskillCdnormal)
            {
                return null;
            }

            var doneFraction = GetDoneFraction(cooldown, nowTimestamp);
            if (doneFraction >= 1d)
            {
                FinishCooldown(skillId);
                return null;
            }

            return (1d - doneFraction) * GetReducedLengthMilliseconds(cooldown) / 1000d;
        }
    }

    public static SelfChargeCooldownState GetSelfChargeCooldownState(
        long uuid,
        int skillId,
        int fallbackMaxCharges)
    {
        var normalizedFallbackCharges = Math.Max(fallbackMaxCharges, 0);
        if (uuid == 0 || skillId <= 0 || normalizedFallbackCharges <= 1)
        {
            return default;
        }

        var nowTimestamp = Stopwatch.GetTimestamp();

        lock (Sync)
        {
            if (uuid != _selfUuid
                || !SelfCooldowns.TryGetValue(skillId, out var cooldown)
                || cooldown.Type != ESkillCDType.EskillCdcharge
                || cooldown.DurationMilliseconds <= 0
                || cooldown.MaxCharges <= 1)
            {
                return new SelfChargeCooldownState(
                    normalizedFallbackCharges,
                    0d,
                    0d);
            }

            // 済んだ割合はチャージの回数ぶんまで(1回ぶんで 1)。
            var maxCharges = cooldown.MaxCharges;
            var doneCharges = Math.Clamp(
                GetDoneFraction(cooldown, nowTimestamp),
                0d,
                maxCharges);

            if (doneCharges >= maxCharges)
            {
                SelfCooldowns.Remove(skillId);
                return new SelfChargeCooldownState(
                    maxCharges,
                    0d,
                    0d);
            }

            var lengthMilliseconds = GetReducedLengthMilliseconds(cooldown);
            var availableCharges = Math.Clamp(
                (int)Math.Floor(doneCharges),
                0,
                maxCharges);
            var nextChargeRemainingMilliseconds = availableCharges >= maxCharges
                ? 0d
                : (1d - (doneCharges - availableCharges)) * lengthMilliseconds;
            var fullRecoveryRemainingMilliseconds = (maxCharges - doneCharges) * lengthMilliseconds;

            return new SelfChargeCooldownState(
                availableCharges,
                nextChargeRemainingMilliseconds / 1000d,
                fullRecoveryRemainingMilliseconds / 1000d);
        }
    }

    private static void UpdateNormalCooldown(
        int skillId,
        SkillCD cooldown,
        long nowTimestamp)
    {
        if (cooldown.BeginTime <= 0
            || cooldown.Duration <= 0
            || cooldown.ValidCDTime >= cooldown.Duration)
        {
            FinishCooldown(skillId);
            return;
        }

        var packetDoneFraction = Math.Max(cooldown.ValidCDTime, 0) / (double)cooldown.Duration;
        if (SelfCooldowns.TryGetValue(skillId, out var existing)
            && existing.Type == ESkillCDType.EskillCdnormal
            && existing.BeginTimeMilliseconds == cooldown.BeginTime)
        {
            // 差分は値を作った時刻より遅れて届くことがあるので、控えの進みより戻さない。
            existing.DoneFraction = Math.Max(packetDoneFraction, GetDoneFraction(existing, nowTimestamp));
            existing.DurationMilliseconds = cooldown.Duration;
            existing.AnchorTimestamp = nowTimestamp;
            return;
        }

        if (FinishedBeginTimes.TryGetValue(skillId, out var finishedBeginTime)
            && finishedBeginTime == cooldown.BeginTime)
        {
            return;
        }

        var doneFraction = ResolveInitialDoneFraction(cooldown, skillId);
        if (doneFraction >= 1d)
        {
            SelfCooldowns.Remove(skillId);
            FinishedBeginTimes[skillId] = cooldown.BeginTime;
            return;
        }

        SelfCooldowns[skillId] = new CooldownState
        {
            SkillId = skillId,
            Type = ESkillCDType.EskillCdnormal,
            BeginTimeMilliseconds = cooldown.BeginTime,
            DurationMilliseconds = cooldown.Duration,
            MaxCharges = 1,
            DoneFraction = doneFraction,
            AnchorTimestamp = nowTimestamp
        };
        FinishedBeginTimes.Remove(skillId);
    }

    private static void UpdateChargeCooldown(
        int skillId,
        SkillCD cooldown,
        long nowTimestamp)
    {
        var maxCharges = CombatDataCatalog.GetSkillMaxCharges(skillId);
        if (cooldown.BeginTime <= 0
            || cooldown.Duration <= 0
            || maxCharges <= 1)
        {
            FinishCooldown(skillId);
            return;
        }

        var packetDoneCharges = Math.Clamp(
            Math.Max(cooldown.ValidCDTime, 0) / (double)cooldown.Duration,
            0d,
            maxCharges);

        if (SelfCooldowns.TryGetValue(skillId, out var existing)
            && existing.Type == ESkillCDType.EskillCdcharge
            && existing.BeginTimeMilliseconds == cooldown.BeginTime)
        {
            existing.DoneFraction = Math.Max(
                packetDoneCharges,
                Math.Min(
                    GetDoneFraction(existing, nowTimestamp),
                    maxCharges));
            existing.DurationMilliseconds = cooldown.Duration;
            existing.MaxCharges = maxCharges;
            existing.AnchorTimestamp = nowTimestamp;
            return;
        }

        var doneCharges = Math.Clamp(
            ResolveInitialDoneFraction(cooldown, skillId),
            0d,
            maxCharges);
        if (doneCharges >= maxCharges)
        {
            SelfCooldowns.Remove(skillId);
            return;
        }

        SelfCooldowns[skillId] = new CooldownState
        {
            SkillId = skillId,
            Type = ESkillCDType.EskillCdcharge,
            BeginTimeMilliseconds = cooldown.BeginTime,
            DurationMilliseconds = cooldown.Duration,
            MaxCharges = maxCharges,
            DoneFraction = doneCharges,
            AnchorTimestamp = nowTimestamp
        };
    }

    /// <summary>
    /// 新しく控える CD の、済んだ割合。差分の値(<c>ValidCDTime</c> ÷ <c>Duration</c>)を使い、使った直後の差分(値 0)なら
    /// 開始から今までを今の長さと速さで進める。値があるときは、開始からの実時間を短縮の前の長さで割った割合を下限にする。
    /// </summary>
    private static double ResolveInitialDoneFraction(SkillCD cooldown, int skillId)
    {
        var durationMilliseconds = (double)cooldown.Duration;
        var packetDoneFraction = Math.Max(cooldown.ValidCDTime, 0) / durationMilliseconds;
        if (!_hasClientServerTimeDelta)
        {
            return packetDoneFraction;
        }

        var serverNowMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            - _clientServerTimeDeltaMilliseconds;
        var elapsedSinceBeginMilliseconds = Math.Max(
            serverNowMilliseconds - cooldown.BeginTime,
            0);

        return cooldown.ValidCDTime == 0
            ? ScaleDone(elapsedSinceBeginMilliseconds, GetDoneRate(skillId, cooldown.Duration))
            : Math.Max(packetDoneFraction, elapsedSinceBeginMilliseconds / durationMilliseconds);
    }

    private static void EnsureSelfPlayer(long uuid)
    {
        if (_selfUuid == uuid)
        {
            return;
        }

        SelfCooldowns.Clear();
        FinishedBeginTimes.Clear();
        _selfUuid = uuid;
        _accelerationPct = 0;
    }

    /// <summary>控えを消す。普通の CD なら、遅れて届く差分で作り直さないよう開始時刻を覚える(<see cref="FinishedBeginTimes"/>)。</summary>
    private static void FinishCooldown(int skillId)
    {
        if (!SelfCooldowns.Remove(skillId, out var cooldown))
        {
            return;
        }

        if (cooldown.Type == ESkillCDType.EskillCdnormal)
        {
            FinishedBeginTimes[skillId] = cooldown.BeginTimeMilliseconds;
        }
    }

    /// <summary>今の時点で済んだ割合(1 で終わり。チャージは回数ぶんまで)。</summary>
    private static double GetDoneFraction(CooldownState cooldown, long nowTimestamp)
    {
        var elapsedMilliseconds = (nowTimestamp - cooldown.AnchorTimestamp)
            * 1000d
            / Stopwatch.Frequency;
        return cooldown.DoneFraction + ScaleDone(elapsedMilliseconds, GetDoneRate(cooldown.SkillId, cooldown.DurationMilliseconds));
    }

    /// <summary>
    /// 自分のその技の CD の長さ(ミリ秒)。CD の長さ(<c>Duration</c>)を、率の短縮の一時属性(<see cref="CooldownModifierStore"/>)で縮めた長さ。
    /// </summary>
    private static double GetReducedLengthMilliseconds(CooldownState cooldown)
    {
        return CooldownModifierStore.ToReducedLength(
            cooldown.DurationMilliseconds,
            CooldownModifierStore.GetReductionRatio(_selfUuid, cooldown.SkillId));
    }

    /// <summary>
    /// 実時間 1ミリ秒あたりに済む割合。縮めた長さを実時間で進み、加速(<c>AttrCdAcceleratePct</c>、10000 で 2倍)の間はその倍率で速く進む。
    /// 縮めた長さが 0 なら無限大(すぐ終わる)。
    /// </summary>
    private static double GetDoneRate(int skillId, int durationMilliseconds)
    {
        var lengthMilliseconds = CooldownModifierStore.ToReducedLength(
            durationMilliseconds,
            CooldownModifierStore.GetReductionRatio(_selfUuid, skillId));
        return lengthMilliseconds <= 0d
            ? double.PositiveInfinity
            : (1d + _accelerationPct / 10000d) / lengthMilliseconds;
    }

    /// <summary>実時間の経過を済んだ割合にする。速さが無限大(すぐ終わる)なら、経過が 0 でも終わった扱い。</summary>
    private static double ScaleDone(double elapsedMilliseconds, double rate)
    {
        return double.IsPositiveInfinity(rate)
            ? double.PositiveInfinity
            : elapsedMilliseconds * rate;
    }

    private static bool TryResolveSkillId(int skillLevelId, out int skillId)
    {
        skillId = 0;
        if (skillLevelId <= 0
            || !HelperMethods.DataTables.SkillFightLevels.Data.TryGetValue(
                skillLevelId.ToString(),
                out var skillFightLevel)
            || skillFightLevel.SkillId <= 0)
        {
            return false;
        }

        skillId = skillFightLevel.SkillId;
        return true;
    }

    private sealed class CooldownState
    {
        public int SkillId { get; init; }

        public ESkillCDType Type { get; init; }

        public long BeginTimeMilliseconds { get; init; }

        public int DurationMilliseconds { get; set; }

        public int MaxCharges { get; set; }

        /// <summary>
        /// <see cref="AnchorTimestamp"/> の時点で済んだ割合(1 で終わり。チャージは回数ぶんまで)。
        /// 割合で持つので、CD の途中で短縮の率が変わっても済んだ割合のまま新しい長さに切り替わる。
        /// </summary>
        public double DoneFraction { get; set; }

        public long AnchorTimestamp { get; set; }
    }
}
