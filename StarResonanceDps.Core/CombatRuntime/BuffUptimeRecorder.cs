// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// バフ稼働率の1行。
/// </summary>
/// <param name="BuffId">畳み先のバフの番号(<see cref="CombatDataCatalog.GetBuffDisplayId"/>)。料理・薬剤のまとまりは 0。</param>
/// <param name="Group">料理・薬剤のまとまり。それ以外は <see cref="BuffGroup.None"/>。</param>
/// <param name="IsPermanent">付き始めた実体の持続が負(永続)か(常駐バフ)。番号ごとに最後に付き始めた実体で決め、畳んだ番号・まとまりは全部の番号がそうなら真。</param>
/// <param name="FirstOrder">その回の中で最初に付いた順。小さいほど先。</param>
/// <param name="Spans">付いていた区間。戦闘の経過の秒で [始まり, 終わり] を交互に並べる。始まりの昇順で、重ならない。</param>
/// <param name="ActiveSeconds">付いていた時間の合計(秒)。</param>
public sealed record BuffUptimeRow(int BuffId, BuffGroup Group, bool IsPermanent, int FirstOrder, double[] Spans, double ActiveSeconds);

/// <summary>
/// 1人のバフ稼働率。戦闘の時計の起点の後だけを数える。
/// </summary>
/// <param name="Encounter">読んだ回。</param>
/// <param name="ElapsedSeconds">戦闘の時計の経過(秒)。</param>
/// <param name="AbsentSpans">その人が周りにいなかった区間。<see cref="BuffUptimeRow.Spans"/> と同じ形。</param>
/// <param name="AbsentSeconds">周りにいなかった時間の合計(秒)。</param>
/// <param name="Rows">数える時間の中で付いていた番号の行。並びは決めない(読む側が決める)。</param>
public sealed record BuffUptimeSnapshot(Encounter Encounter, double ElapsedSeconds, double[] AbsentSpans, double AbsentSeconds, IReadOnlyList<BuffUptimeRow> Rows);

/// <summary>
/// プレイヤーごとの、バフが付いていた区間と周りにいた区間の記録(バフ稼働率)。回ごとに1つ持ち、回と一緒に手放す。
///
/// <para>
/// 残すのはバフリストに出るバフ(<see cref="BuffDisplayRules.IsShownByGameRule"/>)のうちバフの側だけ。番号は元の番号で残し、畳み先と料理・薬剤のまとまりは読むときにまとめる。
/// 同じ番号の実体(付与者違い)を数え、0 → 1 で付き始め、1 → 0 で終わりを残す。除去の通知が来ない実体は、持続の分だけ経った時刻(サーバの付与時刻から)で終わったことにする。
/// </para>
///
/// <para>
/// 周りにいた区間は、その人の通知(差分・出現・バフ)が届いたときに始まり、周りから消えたときに終わる。消えたときは付いているバフも全部そこで終える(見えない間は分からない)。
/// </para>
///
/// <para>
/// 回のあいだ全部残し、開いている窓がその人の分を読むだけなので、人ごとに圧縮して持つ(<see cref="CompressedRecordLog{T}"/>)。排他はこの中で持つ。
/// </para>
/// </summary>
internal sealed class BuffUptimeRecorder
{
    /// <summary>前の回から運んだ行の時刻(Unix 時刻の 0)。戦闘の時計の起点より前なので、回の始まりから付いていたことになる。</summary>
    private const long CarriedUnixMilliseconds = 0;

    private readonly object _gate = new();
    private readonly Dictionary<long, PlayerLog> _players = [];

    public void Apply(long playerUuid, int buffUuid, int buffId, int durationMilliseconds, DateTime? serverCreateTimeUtc, DateTime arrivalUtc)
    {
        var now = ToUnixMilliseconds(arrivalUtc);
        var expiry = ResolveExpiry(durationMilliseconds, serverCreateTimeUtc, now);
        var isPermanent = durationMilliseconds < 0;
        lock (_gate)
        {
            var player = GetOrCreatePlayer(playerUuid);
            player.EndExpired(now);
            player.MarkPresent(now);
            if (player.Instances.TryGetValue(buffUuid, out var existing))
            {
                if (existing.BuffId == buffId)
                {
                    existing.ExpiryUnixMilliseconds = expiry;
                    return;
                }

                // 番号の使い回し(前の実体の除去が届かなかった)。前の実体はここで終える。
                player.RemoveInstance(buffUuid, now);
            }

            player.AddInstance(buffUuid, new Instance(buffId, expiry), isPermanent, now);
        }
    }

    /// <summary>層・持続の変化(延長)。持続は最初の付与からの合計で届くので、終わりの見込みを置き直す。</summary>
    public void Change(long playerUuid, int buffUuid, int durationMilliseconds, DateTime? serverCreateTimeUtc, DateTime arrivalUtc)
    {
        var now = ToUnixMilliseconds(arrivalUtc);
        lock (_gate)
        {
            if (!_players.TryGetValue(playerUuid, out var player))
            {
                return;
            }

            player.EndExpired(now);
            if (player.Instances.TryGetValue(buffUuid, out var instance))
            {
                instance.ExpiryUnixMilliseconds = ResolveExpiry(durationMilliseconds, serverCreateTimeUtc, now);
            }
        }
    }

    public void Remove(long playerUuid, int buffUuid, DateTime arrivalUtc)
    {
        var now = ToUnixMilliseconds(arrivalUtc);
        lock (_gate)
        {
            if (!_players.TryGetValue(playerUuid, out var player))
            {
                return;
            }

            player.EndExpired(now);
            player.RemoveInstance(buffUuid, now);
        }
    }

    /// <summary>出現時の全バフの一覧に無い実体は、見えない間に消えたもの。一覧の付与を流した後で呼ぶ。</summary>
    public void KeepOnly(long playerUuid, IReadOnlySet<int> listedBuffUuids, DateTime arrivalUtc)
    {
        var now = ToUnixMilliseconds(arrivalUtc);
        lock (_gate)
        {
            if (!_players.TryGetValue(playerUuid, out var player))
            {
                return;
            }

            foreach (var buffUuid in player.Instances.Keys.Where(uuid => !listedBuffUuids.Contains(uuid)).ToArray())
            {
                player.RemoveInstance(buffUuid, now);
            }
        }
    }

    /// <summary>その人の通知が届いた。周りにいなかったなら、ここから周りにいる。</summary>
    public void MarkPresent(long playerUuid, DateTime arrivalUtc)
    {
        var now = ToUnixMilliseconds(arrivalUtc);
        lock (_gate)
        {
            GetOrCreatePlayer(playerUuid).MarkPresent(now);
        }
    }

    /// <summary>周りから消えた。付いているバフはここで全部終える。</summary>
    public void MarkAbsent(long playerUuid, DateTime arrivalUtc)
    {
        var now = ToUnixMilliseconds(arrivalUtc);
        lock (_gate)
        {
            if (_players.TryGetValue(playerUuid, out var player))
            {
                player.MarkAbsent(now);
            }
        }
    }

    /// <summary>
    /// 前の回で周りにいた人と付いていたバフを、この回の始まりから続いているものとして運ぶ(同じマップの中の区切り)。
    /// 終わりの見込みを過ぎた実体は運ばない。
    /// </summary>
    public void CarryFrom(BuffUptimeRecorder prior, DateTime utcNow)
    {
        var now = ToUnixMilliseconds(utcNow);
        lock (prior._gate)
        {
            lock (_gate)
            {
                foreach (var (playerUuid, priorPlayer) in prior._players)
                {
                    if (!priorPlayer.IsPresent && priorPlayer.Instances.Count == 0)
                    {
                        continue;
                    }

                    var player = GetOrCreatePlayer(playerUuid);
                    if (priorPlayer.IsPresent)
                    {
                        player.MarkPresent(CarriedUnixMilliseconds);
                    }

                    foreach (var (buffUuid, instance) in priorPlayer.Instances)
                    {
                        if (instance.ExpiryUnixMilliseconds is { } expiry && expiry <= now)
                        {
                            continue;
                        }

                        player.AddInstance(
                            buffUuid,
                            new Instance(instance.BuffId, instance.ExpiryUnixMilliseconds),
                            priorPlayer.IsPermanent(instance.BuffId),
                            CarriedUnixMilliseconds);
                    }
                }
            }
        }
    }

    /// <summary>運んだものを全部捨てる(マップ移動は前のマップのバフを持ち越さない)。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _players.Clear();
        }
    }

    /// <summary>DB から読んだ回の、その人の記録を戻す。もう記録があれば何もせず false。</summary>
    public bool LoadIfAbsent(long playerUuid, long[] packedRows)
    {
        lock (_gate)
        {
            if (_players.ContainsKey(playerUuid))
            {
                return false;
            }

            var player = GetOrCreatePlayer(playerUuid);
            for (var index = 0; index + 1 < packedRows.Length; index += 2)
            {
                player.Rows.Add(Row.Unpack(packedRows[index], packedRows[index + 1]));
            }

            return true;
        }
    }

    /// <summary>
    /// 保存の形(<see cref="Row.Pack"/> を2つずつ並べた配列)。終わりの見込みが <paramref name="endUtc"/> より前の実体は、見込みの時刻で終えた行を足す
    /// (保存した後は見込みが無いので、読むと回の終わりまで付いていたことになる)。記録の無い人は null。
    /// </summary>
    public long[]? PackForSave(long playerUuid, DateTime endUtc)
    {
        var end = ToUnixMilliseconds(endUtc);
        lock (_gate)
        {
            if (!_players.TryGetValue(playerUuid, out var player))
            {
                return null;
            }

            var rows = player.Rows.ToArray(0);
            var expiredEnds = player.GetExpiredEnds(end);
            var packed = new long[(rows.Length + expiredEnds.Count) * 2];
            var position = 0;
            foreach (var row in rows.Concat(expiredEnds))
            {
                (packed[position], packed[position + 1]) = row.Pack();
                position += 2;
            }

            return packed;
        }
    }

    /// <summary>
    /// その人の区間を読む。時刻は Unix ミリ秒のまま返し、戦闘の経過へは読む側(<see cref="Encounter"/>)が直す。
    /// まだ終わっていない区間は、終わりの見込みか <paramref name="clockEndUtc"/> の早いほうで閉じる。
    /// </summary>
    public RawBuffUptime? Read(long playerUuid, DateTime clockEndUtc)
    {
        var clockEnd = ToUnixMilliseconds(clockEndUtc);
        lock (_gate)
        {
            if (!_players.TryGetValue(playerUuid, out var player))
            {
                return null;
            }

            var buffs = new Dictionary<int, RawBuffSpans>();
            var openStarts = new Dictionary<int, long>();
            var present = new List<(long Start, long End)>();
            long? presentSince = null;
            var rows = player.Rows.ToArray(0);
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                switch (row.Kind)
                {
                    case RowKind.Start:
                    case RowKind.StartPermanent:
                        if (!buffs.TryGetValue(row.BuffId, out var spans))
                        {
                            spans = new RawBuffSpans(row.BuffId, index);
                            buffs.Add(row.BuffId, spans);
                        }

                        spans.IsPermanent = row.Kind == RowKind.StartPermanent;
                        openStarts.TryAdd(row.BuffId, row.UnixMilliseconds);
                        break;
                    case RowKind.End:
                        if (openStarts.Remove(row.BuffId, out var start) && buffs.TryGetValue(row.BuffId, out var ended))
                        {
                            ended.Spans.Add((start, row.UnixMilliseconds));
                        }

                        break;
                    case RowKind.Present:
                        presentSince ??= row.UnixMilliseconds;
                        break;
                    case RowKind.Absent:
                        if (presentSince is { } since)
                        {
                            present.Add((since, row.UnixMilliseconds));
                            presentSince = null;
                        }

                        break;
                }
            }

            foreach (var (buffId, start) in openStarts)
            {
                var end = Math.Min(player.GetOpenEnd(buffId) ?? clockEnd, clockEnd);
                buffs[buffId].Spans.Add((start, end));
            }

            if (presentSince is { } openSince)
            {
                present.Add((openSince, clockEnd));
            }

            return new RawBuffUptime([.. buffs.Values], present);
        }
    }

    public static DateTime FromUnixMilliseconds(long unixMilliseconds)
    {
        return DateTime.UnixEpoch.AddMilliseconds(unixMilliseconds);
    }

    private static long ToUnixMilliseconds(DateTime utc)
    {
        return (utc.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;
    }

    /// <summary>
    /// 除去が届かなかったときに終えた扱いにする時刻。持続が無いか、サーバの時刻が無くて経過が分からなければ null(除去を待つ)。
    /// 経過の決め方はバフリストの残り時間と同じ(<see cref="Services.ActiveBuffStore.GetKnownElapsedMilliseconds"/>)。
    /// </summary>
    private static long? ResolveExpiry(int durationMilliseconds, DateTime? serverCreateTimeUtc, long arrivalUnixMilliseconds)
    {
        if (durationMilliseconds <= 0
            || Services.ActiveBuffStore.GetKnownElapsedMilliseconds(serverCreateTimeUtc, durationMilliseconds) is not { } elapsed)
        {
            return null;
        }

        return arrivalUnixMilliseconds - (long)elapsed + durationMilliseconds;
    }

    private PlayerLog GetOrCreatePlayer(long playerUuid)
    {
        if (!_players.TryGetValue(playerUuid, out var player))
        {
            player = new PlayerLog();
            _players.Add(playerUuid, player);
        }

        return player;
    }

    private enum RowKind : byte
    {
        Start = 1,

        /// <summary>付き始めた実体の持続が負(永続)。</summary>
        StartPermanent = 2,
        End = 3,
        Present = 4,
        Absent = 5,
    }

    private readonly record struct Row(long UnixMilliseconds, int BuffId, RowKind Kind)
    {
        public (long Time, long Value) Pack()
        {
            return (UnixMilliseconds, ((long)BuffId << 8) | (byte)Kind);
        }

        public static Row Unpack(long time, long value)
        {
            return new Row(time, (int)(value >> 8), (RowKind)(byte)(value & 0xFF));
        }
    }

    private sealed class Instance(int buffId, long? expiryUnixMilliseconds)
    {
        public int BuffId { get; } = buffId;

        public long? ExpiryUnixMilliseconds { get; set; } = expiryUnixMilliseconds;
    }

    private sealed class PlayerLog
    {
        public CompressedRecordLog<Row> Rows { get; } = new();

        public Dictionary<int, Instance> Instances { get; } = [];

        public bool IsPresent { get; private set; }

        private readonly Dictionary<int, int> _openCounts = [];
        private readonly HashSet<int> _permanentBuffIds = [];

        public bool IsPermanent(int buffId) => _permanentBuffIds.Contains(buffId);

        public void MarkPresent(long now)
        {
            if (IsPresent)
            {
                return;
            }

            IsPresent = true;
            Rows.Add(new Row(now, 0, RowKind.Present));
        }

        public void MarkAbsent(long now)
        {
            EndExpired(now);
            foreach (var buffUuid in Instances.Keys.ToArray())
            {
                RemoveInstance(buffUuid, now);
            }

            if (IsPresent)
            {
                IsPresent = false;
                Rows.Add(new Row(now, 0, RowKind.Absent));
            }
        }

        public void AddInstance(int buffUuid, Instance instance, bool isPermanent, long now)
        {
            Instances[buffUuid] = instance;
            var count = _openCounts.GetValueOrDefault(instance.BuffId) + 1;
            _openCounts[instance.BuffId] = count;
            if (count != 1)
            {
                return;
            }

            if (isPermanent)
            {
                _permanentBuffIds.Add(instance.BuffId);
            }
            else
            {
                _permanentBuffIds.Remove(instance.BuffId);
            }

            Rows.Add(new Row(now, instance.BuffId, isPermanent ? RowKind.StartPermanent : RowKind.Start));
        }

        public void RemoveInstance(int buffUuid, long now)
        {
            if (!Instances.Remove(buffUuid, out var instance))
            {
                return;
            }

            EndOne(instance.BuffId, now);
        }

        /// <summary>終わりの見込みを過ぎた実体を、見込みの時刻で終える。</summary>
        public void EndExpired(long now)
        {
            foreach (var (buffUuid, instance) in Instances.ToArray())
            {
                if (instance.ExpiryUnixMilliseconds is { } expiry && expiry <= now)
                {
                    Instances.Remove(buffUuid);
                    EndOne(instance.BuffId, expiry);
                }
            }
        }

        /// <summary>まだ付いている番号の、終わりの見込みの一番遅い時刻。見込みの無い実体が1つでもあれば null。</summary>
        public long? GetOpenEnd(int buffId)
        {
            long? latest = null;
            foreach (var instance in Instances.Values)
            {
                if (instance.BuffId != buffId)
                {
                    continue;
                }

                if (instance.ExpiryUnixMilliseconds is not { } expiry)
                {
                    return null;
                }

                latest = latest is { } current ? Math.Max(current, expiry) : expiry;
            }

            return latest;
        }

        /// <summary>まだ付いていて、どの実体も <paramref name="end"/> より前に見込みが来る番号の、見込みの時刻で終えた行。記録は変えない。</summary>
        public List<Row> GetExpiredEnds(long end)
        {
            var ends = new List<Row>();
            foreach (var buffId in _openCounts.Where(pair => pair.Value > 0).Select(pair => pair.Key))
            {
                if (GetOpenEnd(buffId) is { } expiry && expiry < end)
                {
                    ends.Add(new Row(expiry, buffId, RowKind.End));
                }
            }

            return ends;
        }

        private void EndOne(int buffId, long time)
        {
            var count = _openCounts.GetValueOrDefault(buffId) - 1;
            if (count > 0)
            {
                _openCounts[buffId] = count;
                return;
            }

            _openCounts.Remove(buffId);
            Rows.Add(new Row(time, buffId, RowKind.End));
        }
    }
}

/// <summary>1つの番号の付いていた区間(Unix ミリ秒)。</summary>
internal sealed class RawBuffSpans(int buffId, int firstOrder)
{
    public int BuffId { get; } = buffId;

    public int FirstOrder { get; } = firstOrder;

    public bool IsPermanent { get; set; }

    public List<(long Start, long End)> Spans { get; } = [];
}

/// <summary>1人の区間(Unix ミリ秒)。</summary>
internal sealed record RawBuffUptime(IReadOnlyList<RawBuffSpans> Buffs, IReadOnlyList<(long Start, long End)> PresentSpans);
