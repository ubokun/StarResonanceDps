// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 自分の能力値が差分で最後に変わった向きと、その差分の到着時刻の控え。
/// ステータス詳細が、変わった行を一定の間だけ増減の色と三角で示すのに使う。
///
/// <para>
/// 記録するのは能力値の表(<c>FightAttrTable</c> の <c>IsClass</c>)の番号だけ。
/// 変化はパケット1つを処理し終えてから記録に入れる(<see cref="CommitPacket"/>)。
/// </para>
///
/// <para>
/// <b>マップに入ったことに伴う変化は記録しない。</b>入場の全属性(<see cref="MarkFullSnapshot"/>)と、
/// シーン切り替えのたびにサーバーが自分のバフを番号1から付け直す差分(<see cref="MarkBuffReinitialized"/>)。
/// 付け直しの差分は、付け直したバフに応じた能力値を送り直すだけで、値は移動の前とほぼ同じ。
/// 差分の中では値がバフより先に当たるので、付け直しはパケットの単位で見分ける(そのパケットの変化は、その番号の記録ごと消す)。
/// </para>
///
/// <para>
/// 前の値が無い番号は、入場の全属性を受けた後なら 0 と比べる(全属性は値が 0 の属性を含まない)。
/// 受ける前(途中から起動した直後)は比べない — 届き始めの値は変化ではない。
/// 消すのはログアウトとキャプチャの停止(<see cref="Clear"/>)。マップ移動は入場の全属性で消える。
/// </para>
/// </summary>
public static class SelfAttributeChangeStore
{
    /// <param name="Sign">1 は増、-1 は減。</param>
    /// <param name="ArrivalUtc">変えた差分の到着時刻。</param>
    public readonly record struct AttributeChange(int Sign, DateTime ArrivalUtc);

    private static readonly object Sync = new();

    /// <summary>鍵は属性の鍵(列挙名。列挙に無い番号はその数字)。</summary>
    private static readonly Dictionary<string, AttributeChange> Changes = new(StringComparer.Ordinal);

    /// <summary>処理中のパケットで変わった番号。<see cref="CommitPacket"/> で記録に入れる。</summary>
    private static readonly List<(string Key, AttributeChange Change)> PendingChanges = [];

    /// <summary>処理中のパケットが入場の全属性か、バフの付け直しを含むか。</summary>
    private static bool _isPacketBulk;

    private static bool _hasFullSnapshot;

    /// <summary>入場の全属性をもう受けたか。前の値が無い番号を 0 と比べてよいかの判定に使う。</summary>
    public static bool HasFullSnapshot
    {
        get
        {
            lock (Sync)
            {
                return _hasFullSnapshot;
            }
        }
    }

    /// <summary>処理中のパケットで、その番号が変わった。</summary>
    public static void AddPending(string key, int sign, DateTime arrivalUtc)
    {
        lock (Sync)
        {
            PendingChanges.Add((key, new AttributeChange(sign, arrivalUtc)));
        }
    }

    /// <summary>入場の全属性を当てた。今までの記録を全部消し、このパケットの変化も記録しない。</summary>
    public static void MarkFullSnapshot()
    {
        lock (Sync)
        {
            Changes.Clear();
            _isPacketBulk = true;
            _hasFullSnapshot = true;
        }
    }

    /// <summary>処理中のパケットに、自分のバフの付け直しがあった。このパケットで変わった番号は記録から消す。</summary>
    public static void MarkBuffReinitialized()
    {
        lock (Sync)
        {
            _isPacketBulk = true;
        }
    }

    /// <summary>
    /// パケットを処理し終えた。変化を記録に入れる(入場か付け直しのパケットなら、変わった番号の記録を消す)。
    /// 記録が変わったら真(名簿の自分の行を作り直す)。
    /// </summary>
    public static bool CommitPacket()
    {
        lock (Sync)
        {
            var changed = false;
            foreach (var (key, change) in PendingChanges)
            {
                if (_isPacketBulk)
                {
                    changed |= Changes.Remove(key);
                    continue;
                }

                Changes[key] = change;
                changed = true;
            }

            PendingChanges.Clear();
            _isPacketBulk = false;
            return changed;
        }
    }

    public static bool TryGet(string key, out AttributeChange change)
    {
        lock (Sync)
        {
            return Changes.TryGetValue(key, out change);
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Changes.Clear();
            PendingChanges.Clear();
            _isPacketBulk = false;
            _hasFullSnapshot = false;
        }
    }
}
