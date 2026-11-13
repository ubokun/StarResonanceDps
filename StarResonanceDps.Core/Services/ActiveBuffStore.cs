// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 現在有効なバフ/デバフのライブ状態。
///
/// <para>
/// <see cref="Encounter"/> は計測の単位なので、戦闘終了やダンジョン勝利のたびに作り直され、
/// その <c>BuffEvents</c> は引き継がれない。しかしバフはゲーム世界側の状態であって
/// 計測の区切りでは消えないため、常時表示のウィジェットがエンカウンターを参照すると
/// 境界で表示が空になる。ここはその境界から独立した保管場所。
/// </para>
///
/// <para>
/// 残り時間の基準には<b>このストアが書き込みを受けたローカル時刻</b>を使う。
/// <see cref="BuffEvent.AddDateTime"/> はサーバ由来の <c>creationTime</c> が入る経路があり
/// ローカル時計と基準がずれるため、期限判定には使えない。
/// 自前で観測時刻を持つのが、この問題に影響されない唯一の方法。
/// </para>
///
/// <para>
/// <c>Encounter.Entities[].BuffEvents</c> は今付いている実体の記録だけで、除去で消す。ここはライブ表示専用。
/// </para>
///
/// <para>
/// 敵のバフを読むのは周りにいる間の窓だけなので、周りから消えたときに手放す(<see cref="RemoveEntity"/>)。
/// 他のプレイヤーのバフは周りにいる間の窓と、名簿の行の機能(技の枠のバッジ・蘇生不可・変身のアイコン)が読む。周りから消えても行があれば残すが、
/// 見えない人には除去の通知が届かないので、切れた項目は読んだときに手放し(<see cref="GetActive"/>)、行が無くなったら全部手放す(<see cref="ReleaseOutOfSightPlayersWithoutRow"/>)。
/// </para>
/// </summary>
public sealed class ActiveBuffStore
{
    private static readonly Lazy<ActiveBuffStore> LazyInstance = new(() => new ActiveBuffStore());

    private const int MaxStackSortCount = 128;

    private readonly object _sync = new();
    private readonly Dictionary<long, Dictionary<ulong, ActiveBuffEntry>> _buffsByEntity = [];

    /// <summary>初出順の採番。エンティティを跨いで単調増加すればよく、値そのものに意味は無い。</summary>
    private long _nextFirstSeenOrder;

    /// <summary>
    /// プレイヤーごとの BaseId -> 付与元(種類と番号)の対応。バフからスキルを逆引きする多段解決に使う。
    /// 読むのは技の枠のバッジだけなので、プレイヤーの分だけを持つ。
    /// 期限切れや解除で消えたバフの分も残す(索引であって表示ソースではない)。周りから消えても残し、マップ切替で消える(人数と番号の種類で頭打ち)。
    /// これが痩せると、直接一致しないスキル(ロールスキル等)の紐付けが解決できなくなる。
    /// <b>種類を落とさない。</b>付与元の番号はスキルの表とバフの表で重なるので、番号だけでは辿り先が決まらない。
    /// </summary>
    private readonly Dictionary<long, Dictionary<int, HashSet<BuffSource>>> _sourceParentsByEntity = [];

    /// <summary>
    /// このマップで出現時の全バフの一覧を受けた保持者(他のプレイヤーと敵。0件の一覧も含む)。
    /// ウィジェットの状態の印が「その相手のバフを受けたか」に使う。バフと同じく <see cref="Clear"/> と <see cref="RemoveEntity"/> で消える。
    /// </summary>
    private readonly HashSet<long> _appearListEntityUuids = [];

    /// <summary>周りから消えて、まだ名簿の行がある他のプレイヤー。また現れるか、行が無くなるか、マップ切替で外れる。</summary>
    private readonly HashSet<long> _outOfSightPlayerUuids = [];

    private ActiveBuffStore()
    {
    }

    public static ActiveBuffStore Instance => LazyInstance.Value;

    /// <param name="serverCreateTimeUtc">
    /// サーバが名乗るバフの付与時刻(<c>BuffInfo.CreateTime</c> 由来)。<c>null</c> なら未提供。
    /// <b>既に経過している分を差し引くためだけに使う。</b>
    /// AOI出現のスナップショットは「見る前から乗っているバフ」を運ぶので、これが無いと
    /// 料理・薬剤が入ってきた瞬間に満額から数え直しになる。
    /// </param>
    /// <param name="createsInstance">
    /// バフの中身(<c>BuffInfo</c>)を伴う付与か。<b>項目を新しく作るのはこのときだけ。</b>
    /// 層の変化などの通知は、除去済みの実体へ後から届いても項目を作り直さない。
    /// </param>
    public void AddOrUpdate(long entityUuid, ulong buffUuid, BuffEvent buffEvent, DateTime? serverCreateTimeUtc, bool createsInstance)
    {
        if (entityUuid == 0 || buffEvent is null)
        {
            return;
        }

        lock (_sync)
        {
            if (!_buffsByEntity.TryGetValue(entityUuid, out var buffs))
            {
                if (!createsInstance)
                {
                    return;
                }

                buffs = [];
                _buffsByEntity.Add(entityUuid, buffs);
            }
            else if (!createsInstance && !buffs.ContainsKey(buffUuid))
            {
                return;
            }

            // 起点は受信時刻。サーバの付与時刻を絶対時刻として使うと、
            // 通信遅延と時計のずれがそのまま差し引かれ、
            // 持続の短いバフが受信した瞬間に期限切れになって行ごと出なくなる。
            //
            // ただし<b>同じバフ実体の再送では起点を据え置く</b>。マップ移動のたびに
            // 自分の全バフが1デルタで再送されるので、毎回受信時刻で置き直すと
            // 残り時間が満額へ巻き戻る(料理なら1800秒に戻る)。
            //
            // 同一実体かどうかは、サーバの付与時刻と持続時間が両方一致するかで見る。
            // 掛け直しなら付与時刻が変わるので、そのときは受信時刻から数え直す。
            var serverAddTime = buffEvent.AddDateTime;
            var isSameInstance = buffs.TryGetValue(buffUuid, out var existing)
                && existing.ServerAddTime == serverAddTime
                && existing.DurationMilliseconds == buffEvent.Duration;

            var now = DateTime.UtcNow;
            var elapsed = ResolveElapsedNoLock(serverCreateTimeUtc, buffEvent.Duration);

            DateTime observedAt;
            bool remainingUnknown;

            if (elapsed.Known)
            {
                // 経過が分かるなら、それを差し引いた瞬間を起点にする。
                // <b>この計算は何回やっても同じ絶対時刻を指す</b>ので、再送で巻き戻らない。
                // だから同一実体の据え置きより優先してよく、不明のまま作られた項目もここで直る。
                observedAt = now.AddMilliseconds(-elapsed.Milliseconds);
                remainingUnknown = false;
            }
            else if (isSameInstance)
            {
                observedAt = existing!.ObservedAt;
                remainingUnknown = existing.RemainingUnknown;
            }
            else
            {
                observedAt = now;
                remainingUnknown = elapsed.Unknown;
            }

            buffs[buffUuid] = new ActiveBuffEntry(
                buffEvent,
                observedAt,
                buffEvent.Duration,
                serverAddTime,
                serverCreateTimeUtc ?? existing?.ServerCreateTimeUtc,
                existing?.FirstSeenOrder ?? _nextFirstSeenOrder++,
                remainingUnknown);
            RecordSourceParentNoLock(entityUuid, buffEvent);
        }
    }

    /// <summary>
    /// サーバ時刻を受け取った後に呼ぶ。経過不明のまま入った項目の起点を、サーバの付与時刻から計算し直す。
    /// 直さないと「?」のまま時間切れの保険も効かない。
    /// </summary>
    public void ResolveUnknownElapsed()
    {
        lock (_sync)
        {
            foreach (var buffs in _buffsByEntity.Values)
            {
                foreach (var buffUuid in buffs.Keys.ToArray())
                {
                    var entry = buffs[buffUuid];
                    if (!entry.RemainingUnknown)
                    {
                        continue;
                    }

                    var elapsed = ResolveElapsedNoLock(entry.ServerCreateTimeUtc, entry.DurationMilliseconds);
                    if (!elapsed.Known)
                    {
                        continue;
                    }

                    buffs[buffUuid] = entry with
                    {
                        ObservedAt = DateTime.UtcNow.AddMilliseconds(-elapsed.Milliseconds),
                        RemainingUnknown = false,
                    };
                }
            }
        }
    }

    /// <summary>
    /// 出現時に届いた全バフの一覧で、その保持者の項目を置き換える。一覧に無い項目は消し、一覧を受けた印を立てる(0件でも)。
    /// 一覧にある項目の追加・更新は、呼び出し側が通常の付与と同じ入口で先に行う。
    /// </summary>
    public void ApplyAppearList(long entityUuid, IReadOnlySet<ulong> listedBuffUuids)
    {
        ArgumentNullException.ThrowIfNull(listedBuffUuids);
        if (entityUuid == 0)
        {
            return;
        }

        lock (_sync)
        {
            _appearListEntityUuids.Add(entityUuid);
            if (!_buffsByEntity.TryGetValue(entityUuid, out var buffs))
            {
                return;
            }

            foreach (var buffUuid in buffs.Keys.ToArray())
            {
                if (!listedBuffUuids.Contains(buffUuid))
                {
                    buffs.Remove(buffUuid);
                }
            }

            if (buffs.Count == 0)
            {
                _buffsByEntity.Remove(entityUuid);
            }
        }
    }

    /// <summary>このマップで、その保持者の出現時の全バフの一覧を受けたか。</summary>
    public bool HasReceivedAppearList(long entityUuid)
    {
        lock (_sync)
        {
            return _appearListEntityUuids.Contains(entityUuid);
        }
    }

    /// <summary>
    /// サーバの付与時刻から「もう何ミリ秒経ったか」を出す。
    ///
    /// <para>
    /// <c>Known</c> … 差し引ける。<c>Unknown</c> … 付与時刻はあるのにサーバ時刻が無く、
    /// <b>残り時間を名乗れない</b>(表示は「?」)。どちらでもない場合(付与時刻そのものが無い、
    /// 持続時間なし)は従来どおり受信時刻を起点にする。
    /// </para>
    /// </summary>
    private static (bool Known, bool Unknown, double Milliseconds) ResolveElapsedNoLock(
        DateTime? serverCreateTimeUtc,
        int durationMilliseconds)
    {
        if (serverCreateTimeUtc is not { } createdAtUtc || durationMilliseconds <= 0)
        {
            return (false, false, 0d);
        }

        if (!CombatRuntime.SkillCooldownStateStore.TryGetServerNowUnixMilliseconds(out var serverNowMilliseconds))
        {
            // SyncServerTime をまだ受け取っていない。付与時刻をローカル時計と引き算すると
            // 通信遅延と時計のずれが混ざるので、推測せず「不明」にする。
            return (false, true, 0d);
        }

        var createdAtMilliseconds = new DateTimeOffset(createdAtUtc, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var elapsedMilliseconds = serverNowMilliseconds - createdAtMilliseconds;

        // 付いた直後(デルタ経路)は 0 前後。負に振れるのは時刻差の誤差なので 0 に丸める。
        return elapsedMilliseconds <= 0d
            ? (true, false, 0d)
            : (true, false, (double)elapsedMilliseconds);
    }

    private void RecordSourceParentNoLock(long entityUuid, BuffEvent buffEvent)
    {
        if (Utils.UuidToEntityType(entityUuid) != (long)Zproto.EEntityType.EntChar
            || buffEvent.BaseId <= 0
            || buffEvent.SourceConfigId <= 0
            || (buffEvent.FightSourceType == (int)Zproto.EFightSource.Buff && buffEvent.BaseId == buffEvent.SourceConfigId))
        {
            return;
        }

        if (!_sourceParentsByEntity.TryGetValue(entityUuid, out var parents))
        {
            parents = [];
            _sourceParentsByEntity.Add(entityUuid, parents);
        }

        if (!parents.TryGetValue(buffEvent.BaseId, out var sources))
        {
            sources = [];
            parents.Add(buffEvent.BaseId, sources);
        }

        sources.Add(new BuffSource(buffEvent.FightSourceType, buffEvent.SourceConfigId));
    }

    /// <summary>
    /// 蓄積した BaseId -> 付与元の対応を呼び出し側の索引へ合流させる。
    /// </summary>
    public void CopySourceParentsInto(long entityUuid, Dictionary<int, HashSet<BuffSource>> target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (entityUuid == 0)
        {
            return;
        }

        lock (_sync)
        {
            if (!_sourceParentsByEntity.TryGetValue(entityUuid, out var parents))
            {
                return;
            }

            foreach (var pair in parents)
            {
                if (!target.TryGetValue(pair.Key, out var sourceIds))
                {
                    sourceIds = [];
                    target.Add(pair.Key, sourceIds);
                }

                sourceIds.UnionWith(pair.Value);
            }
        }
    }

    public void Remove(long entityUuid, ulong buffUuid)
    {
        if (entityUuid == 0)
        {
            return;
        }

        lock (_sync)
        {
            if (!_buffsByEntity.TryGetValue(entityUuid, out var buffs))
            {
                return;
            }

            buffs.Remove(buffUuid);
            if (buffs.Count == 0)
            {
                _buffsByEntity.Remove(entityUuid);
            }
        }
    }

    /// <summary>
    /// 項目のバフの記録(番号・付与元などを持つもの)を返す。<b>期限切れでも返す</b>(番号などを引き継ぐための読み出しで、表示の判定ではない)。
    /// 回(エンカウンター)が作り直されて、今の回の記録がまだ知らない実体に通知が来たときに使う。
    /// </summary>
    public bool TryGetBuffEvent(long entityUuid, ulong buffUuid, out BuffEvent buffEvent)
    {
        lock (_sync)
        {
            if (_buffsByEntity.TryGetValue(entityUuid, out var buffs) && buffs.TryGetValue(buffUuid, out var entry))
            {
                buffEvent = entry.BuffEvent;
                return true;
            }
        }

        buffEvent = null!;
        return false;
    }

    /// <summary>
    /// 期限切れを除いた、現在有効なバフを付いた順に返す。何度も通る道で呼ばれるので、作るのは返す配列だけにする。
    /// 見えない他のプレイヤーの切れた項目はここで手放す(除去の通知が届かない)。周りにいる実体の切れた項目は、遅れて届く通知の番号の引き継ぎに要るので残す。
    /// </summary>
    public BuffEvent[] GetActive(long entityUuid)
    {
        if (entityUuid == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        lock (_sync)
        {
            if (!_buffsByEntity.TryGetValue(entityUuid, out var buffs) || buffs.Count == 0)
            {
                return [];
            }

            var releasesExpired = _outOfSightPlayerUuids.Contains(entityUuid);
            var events = new BuffEvent[buffs.Count];
            Span<long> orders = buffs.Count <= MaxStackSortCount ? stackalloc long[buffs.Count] : new long[buffs.Count];
            var count = 0;
            foreach (var (buffUuid, entry) in buffs)
            {
                if (!entry.IsExpired(now))
                {
                    events[count] = entry.BuffEvent;
                    orders[count] = entry.FirstSeenOrder;
                    count++;
                }
                else if (releasesExpired)
                {
                    buffs.Remove(buffUuid);
                }
            }

            if (buffs.Count == 0)
            {
                _buffsByEntity.Remove(entityUuid);
            }

            orders[..count].Sort(events.AsSpan(0, count));
            if (count != events.Length)
            {
                Array.Resize(ref events, count);
            }

            return events;
        }
    }

    /// <summary>その番号のバフが今付いているか(期限切れを除く)。一覧を作らない。</summary>
    public bool HasActive(long entityUuid, int baseId)
    {
        if (entityUuid == 0)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        lock (_sync)
        {
            if (!_buffsByEntity.TryGetValue(entityUuid, out var buffs))
            {
                return false;
            }

            foreach (var entry in buffs.Values)
            {
                if (entry.BuffEvent.BaseId == baseId && !entry.IsExpired(now))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 残り秒数を返す。期限切れは false。
    ///
    /// <para>
    /// 持続時間が 0 以下で届いたものは <paramref name="remainingSeconds"/> に <c>null</c> を入れて
    /// true を返す。<b>「残り0秒」と「持続時間が無い」を同じ 0 で表すと区別できない</b>ため、
    /// 後者は null で表す(表示側は数字を出さない)。
    /// </para>
    /// </summary>
    /// <param name="remainingUnknown">
    /// <b>残り時間を名乗れない</b>状態。持続時間なし(<paramref name="remainingSeconds"/> が null)とは別物で、
    /// 表示側は「?」を出す。サーバ時刻をまだ受け取っていないうちに
    /// 「見る前から乗っているバフ」を受け取ると立つ。
    /// </param>
    /// <param name="durationSeconds">
    /// 残り時間を出すのに使う効果時間の総量(秒、通信の <c>BuffInfo.Duration</c>)。延長すると伸びる。項目が無ければ 0。
    /// </param>
    /// <param name="endUtc">
    /// 切れる時刻(起点＋持続、UTC)。<paramref name="remainingSeconds"/> があるときだけ値を持つ。
    /// 残り秒は読んだ瞬間で変わるので、複数のバフの切れる順を比べるときはこちらを使う(読む瞬間のずれで大小が入れ替わらない)。
    /// </param>
    public bool TryGetRemainingSeconds(
        long entityUuid,
        ulong buffUuid,
        out double? remainingSeconds,
        out bool remainingUnknown,
        out double durationSeconds,
        out DateTime? endUtc)
    {
        remainingSeconds = null;
        remainingUnknown = false;
        durationSeconds = 0d;
        endUtc = null;
        if (entityUuid == 0)
        {
            return false;
        }

        ActiveBuffEntry? entry;
        lock (_sync)
        {
            entry = _buffsByEntity.TryGetValue(entityUuid, out var buffs)
                && buffs.TryGetValue(buffUuid, out var found)
                    ? found
                    : null;
        }

        if (entry is null)
        {
            return false;
        }

        durationSeconds = entry.DurationMilliseconds / 1000d;

        if (entry.RemainingUnknown)
        {
            // 経過が分からないので秒数を出さない。行は残す。
            remainingUnknown = true;
            return true;
        }

        if (entry.DurationMilliseconds <= 0)
        {
            // 持続時間なし。null のまま true を返し、表示は残す。
            return true;
        }

        var seconds = entry.GetRemainingSeconds(DateTime.UtcNow);
        if (seconds <= 0d)
        {
            return false;
        }

        remainingSeconds = seconds;
        endUtc = entry.GetEndUtc();
        return true;
    }

    /// <summary>
    /// 周りから消えた、プレイヤー以外の実体の分を手放す(<c>Encounter.RemoveDisappearedEntity</c>)。
    /// その実体が付けたバフは付いた側の項目なので消えない。
    /// </summary>
    public void RemoveEntity(long entityUuid)
    {
        lock (_sync)
        {
            _buffsByEntity.Remove(entityUuid);
            _appearListEntityUuids.Remove(entityUuid);
        }
    }

    /// <summary>他のプレイヤーが周りから消えた。名簿を作り直す前に呼ぶ(<see cref="ReleaseOutOfSightPlayersWithoutRow"/>が見る)。</summary>
    public void MarkPlayerOutOfSight(long playerUuid)
    {
        lock (_sync)
        {
            _outOfSightPlayerUuids.Add(playerUuid);
        }
    }

    public void MarkPlayerInSight(long playerUuid)
    {
        lock (_sync)
        {
            _outOfSightPlayerUuids.Remove(playerUuid);
        }
    }

    /// <summary>周りから消えた他のプレイヤーのうち、名簿の行が無くなった人のバフを手放す。付与元の索引はまた現れたときのバッジに要るので残す。</summary>
    public void ReleaseOutOfSightPlayersWithoutRow(IReadOnlySet<long> rosterCharacterIds)
    {
        lock (_sync)
        {
            _outOfSightPlayerUuids.RemoveWhere(playerUuid =>
            {
                if (rosterCharacterIds.Contains(Utils.UuidToEntityId(playerUuid)))
                {
                    return false;
                }

                _buffsByEntity.Remove(playerUuid);
                _appearListEntityUuids.Remove(playerUuid);
                return true;
            });
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _buffsByEntity.Clear();
            _sourceParentsByEntity.Clear();
            _appearListEntityUuids.Clear();
            _outOfSightPlayerUuids.Clear();
        }
    }

    /// <param name="ObservedAt">残り時間の起点。受信時刻。<b>UTC</b>。</param>
    /// <param name="ServerAddTime">
    /// サーバが送ってきた付与時刻。<b>同一実体の判定にだけ使う</b>(残り時間の計算には使わない)。
    /// </param>
    /// <param name="ServerCreateTimeUtc">
    /// サーバが名乗る付与時刻(<c>BuffInfo.CreateTime</c>)。経過不明の項目を、サーバ時刻が届いた後に計算し直すのに使う。
    /// </param>
    /// <param name="FirstSeenOrder">
    /// 初めて現れた順。<b>バフはソートせず、新しいものを末尾に出すためにこれが要る。</b>
    /// 入れ物が <c>Dictionary</c> で、除去で空いた枠に新しいバフが入るため列挙順は当てにできない。
    /// 掛け直し(同じ buffUuid の更新)では番号を据え置き、並びを動かさない。
    /// </param>
    /// <param name="RemainingUnknown">
    /// 経過した分が分からないまま入った項目。<b>残り時間を名乗らない。</b>
    /// サーバの付与時刻はあるのに <c>SyncServerTime</c> をまだ受け取っていないときに立つ。
    /// </param>
    private sealed record ActiveBuffEntry(
        BuffEvent BuffEvent,
        DateTime ObservedAt,
        int DurationMilliseconds,
        DateTime ServerAddTime,
        DateTime? ServerCreateTimeUtc,
        long FirstSeenOrder,
        bool RemainingUnknown)
    {
        public DateTime GetEndUtc()
        {
            return ObservedAt + TimeSpan.FromMilliseconds(DurationMilliseconds);
        }

        public double GetRemainingSeconds(DateTime now)
        {
            return (GetEndUtc() - now).TotalSeconds;
        }

        /// <summary>
        /// 持続を過ぎたら消す。除去の通知が届かなかったときの最後の保険で、保持者が見えているかは問わない。
        /// 持続時間なし(0以下)は時間では消えない。
        /// <b>経過が分からない項目も時間では消さない</b> — 起点が当てにならないので、
        /// そこから数えた期限も当てにならない。サーバ時刻が届けば計算し直される。
        /// </summary>
        public bool IsExpired(DateTime now)
        {
            return !RemainingUnknown
                && DurationMilliseconds > 0
                && GetRemainingSeconds(now) <= 0d;
        }
    }
}

/// <summary>バフ実体の付与元。<c>SourceConfigId</c> の中身はスキルIDかバフIDかを種類(<c>EFightSource</c>)で決める。</summary>
public readonly record struct BuffSource(int FightSourceType, int SourceConfigId);
