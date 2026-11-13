// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

public enum TimelineRowKind
{
    Announcement,
    Cast,

    /// <summary>敵の行動の技の行(行動した敵・発生源ごと)。下に回復・ダメージ・バフの行を並べる。</summary>
    Skill,

    /// <summary>予告のバーが終わった時刻の行。技の行と同じ書式で、下に何も並べない。</summary>
    AnnouncementEnd,

    /// <summary>敵の回復の行(技の行の下、回復された相手1人につき1行)。</summary>
    Heal,

    /// <summary>敵から敵へのダメージの行(技の行の下、相手1体につき1行)。</summary>
    Damage,

    /// <summary>敵が付けたバフの行(技の行の下、相手とバフの組につき1行)。</summary>
    Buff
}

/// <summary>
/// タイムラインの画面の1行ぶんの中身。
///
/// <para>
/// <see cref="Line"/> は、予告行・予告のバーの終わりの行・詠唱行・技の行ならその記録、回復・ダメージ・バフの行なら畳んだもののうち最後のもの。
/// 回復とダメージの行の <see cref="Value"/> は畳んだ値の合計。<see cref="ShowsTime"/> はその時刻のジャンルごとの最初の行だけ真。
/// </para>
/// </summary>
public sealed record TimelineRow(
    TimelineRowKind Kind,
    TimelineLine Line,
    long Value,
    bool ShowsTime);

/// <summary>
/// <see cref="TimelineLayout.Append"/> で変わった行。<see cref="InsertedIndex"/> は前の時刻へ差し込んだ1行の位置(無ければ -1、差し込んだ後の位置で数える)、
/// <see cref="FirstChangedIndex"/> は差し込みの後で中身が変わった・増えた行の先頭(無ければ <see cref="TimelineLayout.Rows"/> の件数)。
/// </summary>
public readonly record struct TimelineLayoutChange(int InsertedIndex, int FirstChangedIndex);

/// <summary>
/// タイムラインの行の並べ方。同じ到着時刻の記録を1つの時刻として扱う。
///
/// <list type="bullet">
///   <item>同じ時刻の中はジャンルの順に並べる: ①敵の行動(予告・予告のバーの終わり・詠唱)→ ②ダメージ → ③回復 → ④バフ。ジャンルの中は届いた順</item>
///   <item>②〜④は、行動した敵・発生源ごとの技の行の下に並べ、まとまりはその最初の記録が届いた順に並べる。同じ技が回復とバフを起こせば、③と④にそれぞれ技の行が立つ。
///   同じ相手への回復・ダメージは1行に畳んで値を足し、同じ相手への同じバフは1行に畳む</item>
///   <item>時刻を出すのは、その時刻にあるジャンルのそれぞれ最初の行だけ</item>
/// </list>
///
/// <para>
/// <b>詠唱の行を出すのは、警告の技と、効果がつながった詠唱だけ。</b> 警告の技でない詠唱は届いた時点では出さずに控え、
/// その詠唱を指す効果(<see cref="TimelineLine.CastSequence"/>)が初めて届いたときに、詠唱の時刻の位置へ差し込む。
/// 効果の無いまま終わった詠唱は出ない。
/// </para>
///
/// <para>
/// 記録は1件ずつ足されるので、同じ到着時刻の記録が取得の区切りをまたいで分かれて届くことがある。
/// 到着時刻が同じ間はその時刻の行を開いたままにして、後から届いた記録も同じジャンル・同じ技の行・同じ畳みへ入れ、並べ直す。
/// 違う到着時刻の記録が来たら閉じる。
/// </para>
/// </summary>
public sealed class TimelineLayout
{
    private readonly List<TimelineRow> _rows = [];
    private readonly List<OpenBlock> _openBlocks = [];

    /// <summary>まだ出していない詠唱(警告の技でないもの)。鍵は詠唱の通し番号。効果がつながったら外して差し込む。</summary>
    private readonly Dictionary<long, TimelineLine> _pendingCasts = [];

    private int _openStart;
    private DateTime? _openTimestamp;

    public IReadOnlyList<TimelineRow> Rows => _rows;

    public void Clear()
    {
        _rows.Clear();
        _openBlocks.Clear();
        _pendingCasts.Clear();
        _openStart = 0;
        _openTimestamp = null;
    }

    /// <summary>1件足す。表示設定で出さない敵の回復は、呼び出し側が渡さない。</summary>
    public TimelineLayoutChange Append(TimelineLine line)
    {
        switch (line.Kind)
        {
            case CombatEventLogRecordKind.Announcement:
            case CombatEventLogRecordKind.AnnouncementEnd:
                return new TimelineLayoutChange(-1, AppendAction(line));
            case CombatEventLogRecordKind.Cast:
                if (!line.IsWarning)
                {
                    _pendingCasts[line.Sequence] = line;
                    return new TimelineLayoutChange(-1, _rows.Count);
                }

                return new TimelineLayoutChange(-1, AppendAction(line));
            case CombatEventLogRecordKind.EnemyHeal:
            case CombatEventLogRecordKind.EnemyDamage:
            case CombatEventLogRecordKind.EnemyBuff:
                return AppendEffect(line);
            default:
                throw new InvalidOperationException($"Timeline does not handle {line.Kind} records (sequence {line.Sequence}).");
        }
    }

    /// <summary>予告・予告のバーの終わり・詠唱の1行を、その記録の時刻に足す。</summary>
    private int AppendAction(TimelineLine line)
    {
        OpenTime(line.Timestamp);
        _openBlocks.Add(OpenBlock.ForAction(CreateHeadRow(GetActionRowKind(line), line)));
        return RebuildOpenRows();
    }

    private TimelineLayoutChange AppendEffect(TimelineLine line)
    {
        var insertedIndex = -1;
        var firstChanged = _rows.Count;
        if (line.CastSequence != 0 && _pendingCasts.Remove(line.CastSequence, out var cast))
        {
            if (_openTimestamp is { } openTimestamp && cast.Timestamp < openTimestamp)
            {
                (insertedIndex, firstChanged) = InsertClosedCast(cast);
            }
            else
            {
                firstChanged = AppendAction(cast);
            }
        }

        OpenTime(line.Timestamp);

        var groupKey = new EffectGroupKey(
            GetEffectGenre(line),
            line.Attacker.Uuid,
            line.IsBuffSource,
            line.SourceId,
            line.Timestamp);
        var group = _openBlocks.Find(block => block.GroupKey == groupKey);
        if (group is null)
        {
            group = OpenBlock.ForEffectGroup(groupKey, CreateHeadRow(TimelineRowKind.Skill, line));
            _openBlocks.Add(group);
        }

        group.AddFold(line);
        return new TimelineLayoutChange(insertedIndex, Math.Min(firstChanged, RebuildOpenRows()));
    }

    /// <summary>
    /// 閉じた時刻へ詠唱の行を差し込む。同じ時刻があれば、その時刻の①のうち後から届いたものの前(①が無ければ時刻の先頭)へ、
    /// 無ければ詠唱の時刻より後の最初の時刻の前へ置く。時刻の先頭になったら時刻を出し、それまで先頭だった①の行の時刻を消す。
    /// </summary>
    /// <returns>差し込んだ位置と、差し込んだ後で中身が変わった行の先頭。</returns>
    private (int InsertedIndex, int FirstChanged) InsertClosedCast(TimelineLine cast)
    {
        var index = _openStart;
        while (index > 0 && _rows[index - 1].Line.Timestamp > cast.Timestamp)
        {
            index--;
        }

        var groupStart = index;
        while (groupStart > 0 && _rows[groupStart - 1].Line.Timestamp == cast.Timestamp)
        {
            groupStart--;
        }

        var position = groupStart;
        while (position < index
            && IsActionRow(_rows[position].Kind)
            && _rows[position].Line.Sequence < cast.Sequence)
        {
            position++;
        }

        var isFirstAction = position == groupStart;
        _rows.Insert(position, CreateHeadRow(TimelineRowKind.Cast, cast) with { ShowsTime = isFirstAction });
        _openStart++;

        var firstChanged = _rows.Count;
        var next = position + 1;
        if (isFirstAction
            && next < _rows.Count
            && _rows[next].Line.Timestamp == cast.Timestamp
            && IsActionRow(_rows[next].Kind)
            && _rows[next].ShowsTime)
        {
            _rows[next] = _rows[next] with { ShowsTime = false };
            firstChanged = next;
        }

        return (position, firstChanged);
    }

    private static bool IsActionRow(TimelineRowKind kind)
    {
        return kind is TimelineRowKind.Announcement or TimelineRowKind.AnnouncementEnd or TimelineRowKind.Cast;
    }

    private static LogGenre GetEffectGenre(TimelineLine line)
    {
        return line.Kind switch
        {
            CombatEventLogRecordKind.EnemyDamage => LogGenre.Damage,
            CombatEventLogRecordKind.EnemyHeal => LogGenre.Heal,
            CombatEventLogRecordKind.EnemyBuff => LogGenre.Buff,
            _ => throw new InvalidOperationException($"Timeline {line.Kind} record is not an effect (sequence {line.Sequence}).")
        };
    }

    private static TimelineRowKind GetActionRowKind(TimelineLine line)
    {
        return line.Kind switch
        {
            CombatEventLogRecordKind.Announcement => TimelineRowKind.Announcement,
            CombatEventLogRecordKind.AnnouncementEnd => TimelineRowKind.AnnouncementEnd,
            CombatEventLogRecordKind.Cast => TimelineRowKind.Cast,
            _ => throw new InvalidOperationException($"Timeline {line.Kind} record is not an action (sequence {line.Sequence}).")
        };
    }

    private void OpenTime(DateTime timestamp)
    {
        if (_openTimestamp != timestamp)
        {
            Close();
            _openTimestamp = timestamp;
        }
    }

    private void Close()
    {
        _openBlocks.Clear();
        _openStart = _rows.Count;
        _openTimestamp = null;
    }

    /// <summary>まとまりの先頭の行。時刻を出すかは並べ直すときに決める(<see cref="RebuildOpenRows"/>)。</summary>
    private static TimelineRow CreateHeadRow(TimelineRowKind kind, TimelineLine line)
    {
        return new TimelineRow(kind, line, 0, false);
    }

    /// <summary>
    /// 開いている時刻の行を並べ直す。ジャンルの順に、まとまりの行を置く。
    /// 各ジャンルの最初の行だけ時刻を出す(先頭の行の印が変わったら、そのまとまりの先頭の行を作り直す)。開いている範囲の行数は減らない。
    /// </summary>
    private int RebuildOpenRows()
    {
        var index = _openStart;
        var firstChanged = -1;

        void Place(TimelineRow row)
        {
            if (index < _rows.Count)
            {
                if (!ReferenceEquals(_rows[index], row))
                {
                    _rows[index] = row;
                    firstChanged = firstChanged < 0 ? index : firstChanged;
                }
            }
            else
            {
                _rows.Add(row);
                firstChanged = firstChanged < 0 ? index : firstChanged;
            }

            index++;
        }

        foreach (var genre in GenreOrder)
        {
            var isFirstInGenre = true;
            foreach (var block in _openBlocks)
            {
                if (block.Genre != genre)
                {
                    continue;
                }

                if (block.Head.ShowsTime != isFirstInGenre)
                {
                    block.Head = block.Head with { ShowsTime = isFirstInGenre };
                }

                isFirstInGenre = false;
                foreach (var row in block.EnumerateRows())
                {
                    Place(row);
                }
            }
        }

        if (index != _rows.Count)
        {
            throw new InvalidOperationException(
                $"Timeline open rows shrank (expected {_rows.Count}, rebuilt {index}).");
        }

        return firstChanged < 0 ? _rows.Count : firstChanged;
    }

    /// <summary>同じ時刻の中のジャンル。並べる順は <see cref="GenreOrder"/>。</summary>
    private enum LogGenre
    {
        /// <summary>①敵の行動(予告・予告のバーの終わり・詠唱)。</summary>
        Action,

        /// <summary>②敵から敵へのダメージ(技の行と、その下のダメージ)。</summary>
        Damage,

        /// <summary>③敵の回復(技の行と、その下の回復)。</summary>
        Heal,

        /// <summary>④敵が付けたバフ(技の行と、その下のバフ)。</summary>
        Buff
    }

    private static readonly LogGenre[] GenreOrder = [LogGenre.Action, LogGenre.Damage, LogGenre.Heal, LogGenre.Buff];

    /// <summary>
    /// 効果の技の行のまとまりの鍵。ジャンルも鍵に入れるので、技の行は効果の種類ごとに立つ
    /// (発生源が分からない(番号 0)ときの技の行の文言も種類で違う)。
    /// </summary>
    private readonly record struct EffectGroupKey(
        LogGenre Genre,
        long ActorUuid,
        bool IsBuffSource,
        int SourceId,
        DateTime Timestamp);

    /// <summary>畳んだ効果の行の鍵。回復とダメージは相手ごと、バフは相手とバフの組ごと。</summary>
    private readonly record struct FoldKey(long TargetUuid, CombatEventLogRecordKind Kind, int BuffId);

    /// <summary>畳んだ効果の行。</summary>
    private sealed record Fold(FoldKey Key, TimelineRow Row);

    /// <summary>開いている到着時刻の行のまとまり。予告・予告のバーの終わり・詠唱の1行か、効果の技の行とその下の効果。</summary>
    private sealed class OpenBlock
    {
        private OpenBlock(LogGenre genre, TimelineRow head, EffectGroupKey? groupKey)
        {
            Genre = genre;
            Head = head;
            GroupKey = groupKey;
        }

        public LogGenre Genre { get; }

        /// <summary>先頭の行。時刻を出すかが変わったときだけ、並べ直しが作り直して入れ替える。</summary>
        public TimelineRow Head { get; set; }

        public EffectGroupKey? GroupKey { get; }

        private List<Fold> Folds { get; } = [];

        public static OpenBlock ForAction(TimelineRow row)
        {
            return new OpenBlock(LogGenre.Action, row, null);
        }

        public static OpenBlock ForEffectGroup(EffectGroupKey key, TimelineRow skillRow)
        {
            return new OpenBlock(key.Genre, skillRow, key);
        }

        /// <summary>効果を1件畳む。同じ鍵の行があれば外して(回復とダメージは値を足して)最後に置き直す。</summary>
        public void AddFold(TimelineLine line)
        {
            var (kind, buffId) = line.Kind switch
            {
                CombatEventLogRecordKind.EnemyHeal => (TimelineRowKind.Heal, 0),
                CombatEventLogRecordKind.EnemyDamage => (TimelineRowKind.Damage, 0),
                CombatEventLogRecordKind.EnemyBuff => (TimelineRowKind.Buff, line.BuffId),
                _ => throw new InvalidOperationException($"Timeline {line.Kind} record is not an effect (sequence {line.Sequence}).")
            };
            var target = line.Target
                ?? throw new InvalidOperationException($"Timeline {line.Kind} record has no target (sequence {line.Sequence}).");
            var key = new FoldKey(target.Uuid, line.Kind, buffId);
            var value = line.Value;
            var foldIndex = Folds.FindIndex(fold => fold.Key == key);
            if (foldIndex >= 0)
            {
                if (kind != TimelineRowKind.Buff)
                {
                    value += Folds[foldIndex].Row.Value;
                }

                Folds.RemoveAt(foldIndex);
            }

            Folds.Add(new Fold(key, new TimelineRow(kind, line, value, false)));
        }

        public IEnumerable<TimelineRow> EnumerateRows()
        {
            yield return Head;
            foreach (var fold in Folds)
            {
                yield return fold.Row;
            }
        }
    }
}
