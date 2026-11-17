// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 敵ごとの、ヘイトの一覧(<c>AttrHateList</c>、474)のプレイヤーの値と強靭(<c>AttrStunned</c>、443)の直前の値。
/// 差分で届いた新しい値と比べて、人ごとのヘイトの増え(ヘイトメーター)と強靭の減り(ブレイクの計測)を出す。
///
/// <para>
/// エンカウンターとは別に持つ。回を作り直すとプレイヤー以外の属性は運ばれないことがあり、
/// 運ばれなかった敵の次の一覧を「初めて見た一覧」と数えてしまうため(<see cref="NearbyMonsterIndex"/> と同じ理由)。
/// <see cref="NearbyMonsterIndex"/> は出現時にしか届かない種別IDと召喚者を戻すための変わらない値の索引で、
/// 差分ごとに置き換える人ごとの値はここに分ける。
/// </para>
///
/// <para>
/// 出現で基準を置き(<see cref="SetBaseline"/>)、差分で比べて置き換える。消える通知で外し、マップ移動・キャプチャの停止・
/// ログアウトで全部消す(<see cref="NearbyMonsterIndex"/> と同じ時機)。持つのは周りにいる敵の数 × 一覧の人数までで、
/// 長く続く狩場でも周りの敵の数で頭打ちになる。
/// </para>
/// </summary>
public sealed class EnemyContributionBaseline
{
    private static readonly Lazy<EnemyContributionBaseline> LazyInstance = new(() => new EnemyContributionBaseline());

    private readonly object _sync = new();
    private readonly Dictionary<long, Dictionary<long, ulong>> _hateByEnemy = [];
    private readonly Dictionary<long, int> _stunnedByEnemy = [];

    /// <summary>
    /// <see cref="UpdateHate"/> が次の基準を作る作業用の辞書。直前の基準の辞書と入れ替えて使い回し、使っていない間は空。
    /// 大きさは一覧の人数の最大まで。
    /// </summary>
    private Dictionary<long, ulong> _scratchHate = [];

    private EnemyContributionBaseline()
    {
    }

    public static EnemyContributionBaseline Instance => LazyInstance.Value;

    /// <param name="PlayerUuid">ヘイトが増えたプレイヤーの実体の UUID。</param>
    /// <param name="Increase">直前の一覧からの増え。一覧に新しく入った人は、0 からの増え。</param>
    /// <param name="IsNewEntry">直前の一覧にその人がいなかった(一覧に初めて入った・外れてから入り直した)。</param>
    public readonly record struct HateIncrease(long PlayerUuid, ulong Increase, bool IsNewEntry);

    /// <summary>
    /// 出現の一覧で基準を置く。それまでの累計が入っているので、増えとしては数えない。
    /// 一覧か強靭が届いていなければ、その分の前の基準を消す(次に届いた値が基準になる)。
    /// </summary>
    public void SetBaseline(long enemyUuid, IReadOnlyList<HateInfo>? hateList, int? stunned)
    {
        lock (_sync)
        {
            if (hateList is null)
            {
                _hateByEnemy.Remove(enemyUuid);
            }
            else
            {
                _hateByEnemy[enemyUuid] = ToPlayerValues(hateList);
            }

            if (stunned is { } value)
            {
                _stunnedByEnemy[enemyUuid] = value;
            }
            else
            {
                _stunnedByEnemy.Remove(enemyUuid);
            }
        }
    }

    /// <summary>
    /// 差分で届いた一覧を、直前の一覧と比べて人ごとの増えを <paramref name="increases"/> に入れ(先に空にする)、基準を置き換える。
    /// 初めての一覧(出現を見ていない敵)と、誰かの値が減った一覧(10億の上限を越えた・ローグライクの一部の敵の戻り、notes/174)は、
    /// その更新の増えが分からないので、基準を置き直すだけで何も入れない。
    ///
    /// <para>
    /// 差分ごとに届くので、何も作らない。次の基準は作業用の辞書(<see cref="_scratchHate"/>)に作って直前の基準と入れ替え、
    /// 直前の基準の辞書は空にして次の作業用に回す。辞書を新しく作るのは、その敵の初めての一覧のときだけ。
    /// </para>
    /// </summary>
    /// <param name="increases">増えを入れる一覧。呼び手が使い回す。</param>
    public void UpdateHate(long enemyUuid, IReadOnlyList<HateInfo> hateList, List<HateIncrease> increases)
    {
        increases.Clear();
        lock (_sync)
        {
            if (!_hateByEnemy.TryGetValue(enemyUuid, out var previous))
            {
                _hateByEnemy[enemyUuid] = ToPlayerValues(hateList);
                return;
            }

            var current = _scratchHate;
            FillPlayerValues(hateList, current);
            _hateByEnemy[enemyUuid] = current;
            _scratchHate = previous;

            var hasDecrease = false;
            foreach (var (playerUuid, value) in current)
            {
                if (previous.TryGetValue(playerUuid, out var previousValue) && value < previousValue)
                {
                    hasDecrease = true;
                    break;
                }
            }

            if (!hasDecrease)
            {
                foreach (var (playerUuid, value) in current)
                {
                    var wasListed = previous.TryGetValue(playerUuid, out var previousValue);
                    if (value > previousValue)
                    {
                        increases.Add(new HateIncrease(playerUuid, value - previousValue, !wasListed));
                    }
                }
            }

            previous.Clear();
        }
    }

    /// <summary>差分で届いた強靭で基準を置き換え、直前の値を返す。基準が無ければ null。</summary>
    public int? UpdateStunned(long enemyUuid, int stunned)
    {
        lock (_sync)
        {
            int? previous = _stunnedByEnemy.TryGetValue(enemyUuid, out var value) ? value : null;
            _stunnedByEnemy[enemyUuid] = stunned;
            return previous;
        }
    }

    public void Remove(long enemyUuid)
    {
        lock (_sync)
        {
            _hateByEnemy.Remove(enemyUuid);
            _stunnedByEnemy.Remove(enemyUuid);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _hateByEnemy.Clear();
            _stunnedByEnemy.Clear();
        }
    }

    /// <summary>一覧のプレイヤーの値だけを持つ新しい辞書。その敵の初めての一覧と出現のときに使う。</summary>
    private static Dictionary<long, ulong> ToPlayerValues(IReadOnlyList<HateInfo> hateList)
    {
        var values = new Dictionary<long, ulong>(hateList.Count);
        FillPlayerValues(hateList, values);
        return values;
    }

    /// <summary><paramref name="values"/> を空にして、一覧のプレイヤーの値だけを入れる。同じ人が2度あれば後の値。</summary>
    private static void FillPlayerValues(IReadOnlyList<HateInfo> hateList, Dictionary<long, ulong> values)
    {
        values.Clear();
        foreach (var hate in hateList)
        {
            if (hate.Uuid != 0 && Utils.UuidToEntityType(hate.Uuid) == (long)EEntityType.EntChar)
            {
                values[hate.Uuid] = hate.HateVal;
            }
        }
    }
}
