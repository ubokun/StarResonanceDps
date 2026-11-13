// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 周囲にいるモンスターの索引。鍵は実体の UUID、値は種別ID(<c>AttrId</c> = <c>MonsterTable</c> のキー)と召喚者(<c>AttrSummonerId</c>・<c>AttrTopSummonerId</c>)。
/// ボス大技の予告は誰が構えたかを運ばないので、タイムラインがその技を持つモンスターを周囲から探すのに使う。
/// エンカウンターを作り直した後に実体が作られたとき、<c>AttrId</c> と召喚者を戻すのにも使う
/// (区切りの前から居た召喚体のバフ・詠唱を、タイムラインとバフの付与者で召喚者へ寄せられるように)。
///
/// <para>
/// <c>AttrId</c> と召喚者は出現時にしか届かず、エンカウンターを作り直すとプレイヤー以外の属性は運ばれないことがあるので、
/// エンカウンターとは別に持つ。出現・属性の到着で入れ、消える通知・マップ切替・計測の停止で消す。
/// </para>
///
/// <para>
/// エンティティリストの索引(<c>NearbyEntityStore</c>)は <c>HudShowParam</c> が 0 のボスを外すので使えない。
/// </para>
/// </summary>
public sealed class NearbyMonsterIndex
{
    private static readonly Lazy<NearbyMonsterIndex> LazyInstance = new(() => new NearbyMonsterIndex());

    private readonly object _sync = new();
    private readonly Dictionary<long, NearbyMonster> _monsterByUuid = [];

    private NearbyMonsterIndex()
    {
    }

    public static NearbyMonsterIndex Instance => LazyInstance.Value;

    /// <param name="MonsterId">種別ID(<c>AttrId</c>)。</param>
    /// <param name="SummonerUuid">召喚者(<c>AttrSummonerId</c>)。無ければ 0。</param>
    /// <param name="TopSummonerUuid">大元の召喚者(<c>AttrTopSummonerId</c>)。無ければ 0。</param>
    public readonly record struct NearbyMonster(int MonsterId, long SummonerUuid, long TopSummonerUuid);

    public void Set(long uuid, NearbyMonster monster)
    {
        lock (_sync)
        {
            _monsterByUuid[uuid] = monster;
        }
    }

    public void Remove(long uuid)
    {
        lock (_sync)
        {
            _monsterByUuid.Remove(uuid);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _monsterByUuid.Clear();
        }
    }

    public bool TryGet(long uuid, out NearbyMonster monster)
    {
        lock (_sync)
        {
            return _monsterByUuid.TryGetValue(uuid, out monster);
        }
    }

    public bool TryGetMonsterId(long uuid, out int monsterId)
    {
        lock (_sync)
        {
            if (_monsterByUuid.TryGetValue(uuid, out var monster))
            {
                monsterId = monster.MonsterId;
                return true;
            }
        }

        monsterId = 0;
        return false;
    }

    /// <summary>周囲にいるモンスターのうち、<paramref name="match"/> に当たる種別IDを1つ返す。</summary>
    public bool TryFindMonsterId(Func<int, bool> match, out int monsterId)
    {
        lock (_sync)
        {
            foreach (var candidate in _monsterByUuid.Values)
            {
                if (match(candidate.MonsterId))
                {
                    monsterId = candidate.MonsterId;
                    return true;
                }
            }
        }

        monsterId = 0;
        return false;
    }
}
