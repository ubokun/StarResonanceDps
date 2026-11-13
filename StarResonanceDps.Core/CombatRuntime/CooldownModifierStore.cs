// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Frozen;
using Serilog;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 人ごとの、CD を率で短縮する一時属性(<c>TempAttrTable</c> の種類 100 <c>TempAttrSkillCD</c>)の控えと、その移り変わり。
///
/// <para>
/// 短縮は CD の長さを縮める(<see cref="ToReducedLength"/>。80秒の CD に率 80% なら 16秒)。CD は実時間で進み、加速の間だけ速く進む。
/// サーバーの CD の差分は短縮の前の長さ(<c>Duration</c>)と、その単位の進み(<c>ValidCDTime</c>)で届くので、使う側は済んだ割合(<c>ValidCDTime</c> ÷ <c>Duration</c>)にして受ける。
/// CD の途中で率が変わったときは、済んだ割合を保って新しい長さに切り替える(サーバーの値もこの動き)。
/// </para>
///
/// <para>
/// 技ごとの率は、一時属性の対象(<c>AttrParams</c>)にその技の番号があるもの、または対象が <see cref="ImagineCategoryParam"/> で
/// その技がイマジンのものの値(万分比)を足す。ほかの種類の指定(3・4・5・6 など)は何を指すか分かっていないので数えない。
/// 秒の短縮(種類 101)と加速(種類 103)は、進み方を確かめていないので持たない。
/// </para>
///
/// <para>
/// 入場(自分)・出現(他人)の一覧はその人の値を丸ごと置き換え(<see cref="Replace"/>)、周りの差分は重ねる(<see cref="Merge"/>)。値 0 は外す。
/// 持つのはプレイヤーだけ(ほかの実体の UUID は置き換える機会が無く、残り続ける)。
/// マップ移動では消さない(次の入場・出現が置き換える)。消すのはログアウトとキャプチャの停止(<see cref="Clear"/>)。
/// 移り変わりは届いた時刻(UTC)で控え、他人の CD の推定が発動から今までの進みを区間ごとに数えるのに使う。
/// 古い移り変わりは推定の側が要らなくなった時点で捨てる(<see cref="Prune"/>)。
/// </para>
/// </summary>
public static class CooldownModifierStore
{
    /// <summary>一時属性の対象のうち、イマジンの技を指す番号。</summary>
    public const int ImagineCategoryParam = 8;

    private const int ReductionRatioAttrType = 100;
    private const double RatioScale = 10000d;

    private static readonly object Sync = new();
    private static readonly Dictionary<long, List<Snapshot>> Histories = [];
    private static bool _hasWarnedFullReduction;

    /// <summary>その人の値を、入場・出現の一覧で丸ごと置き換える。</summary>
    public static void Replace(long uuid, IEnumerable<Zproto.TempAttr> tempAttrs, DateTime arrivalUtc)
    {
        var values = new Dictionary<int, int>();
        AddReductionValues(values, tempAttrs);
        Append(uuid, values, arrivalUtc);
    }

    /// <summary>その人の値に、周りの差分を重ねる。</summary>
    public static void Merge(long uuid, IEnumerable<Zproto.TempAttr> tempAttrs, DateTime arrivalUtc)
    {
        Dictionary<int, int> values;
        lock (Sync)
        {
            values = Histories.TryGetValue(uuid, out var history)
                ? history[^1].Values.ToDictionary()
                : [];
        }

        AddReductionValues(values, tempAttrs);
        Append(uuid, values, arrivalUtc);
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Histories.Clear();
        }
    }

    /// <summary>その人のその技の、いまの短縮の率(0 以上、1 = 100%)。</summary>
    public static double GetReductionRatio(long uuid, int skillId)
    {
        lock (Sync)
        {
            return Histories.TryGetValue(uuid, out var history)
                ? SumRatio(history[^1], skillId)
                : 0d;
        }
    }

    /// <summary>
    /// その人のその技の短縮の率の移り変わり。最初の要素は <paramref name="fromUtc"/> の時点の率(控えより前なら 0)、
    /// 続く要素は <paramref name="fromUtc"/> より後に率が変わった時刻と、そこからの率。
    /// </summary>
    public static IReadOnlyList<(DateTime StartUtc, double Ratio)> GetReductionChanges(long uuid, int skillId, DateTime fromUtc)
    {
        lock (Sync)
        {
            if (!Histories.TryGetValue(uuid, out var history))
            {
                return [(fromUtc, 0d)];
            }

            var changes = new List<(DateTime StartUtc, double Ratio)> { (fromUtc, 0d) };
            foreach (var snapshot in history)
            {
                var ratio = SumRatio(snapshot, skillId);
                if (snapshot.StartUtc <= fromUtc)
                {
                    changes[0] = (fromUtc, ratio);
                }
                else if (ratio != changes[^1].Ratio)
                {
                    changes.Add((snapshot.StartUtc, ratio));
                }
            }

            return changes;
        }
    }

    /// <summary>
    /// その人のその技の短縮の率が <paramref name="fromUtc"/> より後に変わっていなければ、その率を返す。一覧を作らない。
    /// 変わっていれば偽で、区間ごとに数えるときは <see cref="GetReductionChanges"/> を使う。
    /// </summary>
    public static bool TryGetConstantReductionRatio(long uuid, int skillId, DateTime fromUtc, out double ratio)
    {
        ratio = 0d;
        lock (Sync)
        {
            if (!Histories.TryGetValue(uuid, out var history))
            {
                return true;
            }

            foreach (var snapshot in history)
            {
                var snapshotRatio = SumRatio(snapshot, skillId);
                if (snapshot.StartUtc <= fromUtc)
                {
                    ratio = snapshotRatio;
                }
                else if (snapshotRatio != ratio)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// 人ごとに、<paramref name="cutoffOf"/> の時刻より前に終わった移り変わりを捨てる(その時刻に効いている値は残す)。
    /// 残るのが値の無い1つだけになった人は控えごと捨てる。
    /// </summary>
    public static void Prune(Func<long, DateTime> cutoffOf)
    {
        lock (Sync)
        {
            foreach (var (uuid, history) in Histories.ToArray())
            {
                var cutoff = cutoffOf(uuid);
                var lastStartedIndex = history.FindLastIndex(snapshot => snapshot.StartUtc <= cutoff);
                if (lastStartedIndex > 0)
                {
                    history.RemoveRange(0, lastStartedIndex);
                }

                if (history.Count == 1 && history[0].Values.Count == 0)
                {
                    Histories.Remove(uuid);
                }
            }
        }
    }

    /// <summary>
    /// 短縮した CD の長さ。CD の長さ ×(1 − 短縮の率)。80秒の CD に率 80% なら 16秒。
    /// 率が 1 以上なら 0(すぐに終わる)で、起きたことを1回だけ警告に残す。
    /// </summary>
    /// <param name="length">短縮の前の長さ(単位は呼び出し側のまま返す)。</param>
    /// <param name="reductionRatio">短縮の率(<see cref="GetReductionRatio"/>)。</param>
    public static double ToReducedLength(double length, double reductionRatio)
    {
        if (reductionRatio >= 1d)
        {
            if (!_hasWarnedFullReduction)
            {
                _hasWarnedFullReduction = true;
                Log.Warning("Cooldown reduction ratio {Ratio} is 100% or more; the cooldown is treated as finished immediately", reductionRatio);
            }

            return 0d;
        }

        return length * (1d - reductionRatio);
    }

    private static void AddReductionValues(Dictionary<int, int> values, IEnumerable<Zproto.TempAttr> tempAttrs)
    {
        foreach (var tempAttr in tempAttrs)
        {
            if (!HelperMethods.DataTables.TempAttrs.Data.TryGetValue(tempAttr.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), out var row)
                || row.AttrType != ReductionRatioAttrType)
            {
                continue;
            }

            if (tempAttr.Value == 0)
            {
                values.Remove(tempAttr.Id);
            }
            else
            {
                values[tempAttr.Id] = tempAttr.Value;
            }
        }
    }

    private static void Append(long uuid, Dictionary<int, int> values, DateTime arrivalUtc)
    {
        lock (Sync)
        {
            if (!Histories.TryGetValue(uuid, out var history))
            {
                if (values.Count == 0)
                {
                    return;
                }

                history = [];
                Histories[uuid] = history;
            }
            else if (HasSameValues(history[^1].Values, values))
            {
                return;
            }

            history.Add(new Snapshot(arrivalUtc, values.ToFrozenDictionary()));
        }
    }

    private static bool HasSameValues(FrozenDictionary<int, int> current, Dictionary<int, int> next)
    {
        return current.Count == next.Count
            && next.All(pair => current.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

    private static double SumRatio(Snapshot snapshot, int skillId)
    {
        var total = 0;
        foreach (var (row, value) in snapshot.Rows)
        {
            if (row.AttrParams.Contains(skillId)
                || row.AttrParams.Contains(ImagineCategoryParam) && CombatDataCatalog.IsSkillImagine(skillId))
            {
                total += value;
            }
        }

        return total / RatioScale;
    }

    /// <param name="StartUtc">この値になった時刻(届いた時刻、UTC)。</param>
    /// <param name="Values">一時属性の番号 → 値(万分比)。種類 100 だけ。</param>
    private sealed record Snapshot(DateTime StartUtc, FrozenDictionary<int, int> Values)
    {
        /// <summary>値の一時属性の行と値。率を足すたびに表を引かないよう、作るときに1回だけ引く。</summary>
        public (DataTypes.TempAttr Row, int Value)[] Rows { get; } = Values
            .Select(pair => (
                HelperMethods.DataTables.TempAttrs.Data[pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)],
                pair.Value))
            .ToArray();
    }
}
