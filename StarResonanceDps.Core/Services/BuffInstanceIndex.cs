// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Zproto;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 生きているバフ実体の索引。<b>敵も含めた全エンティティ</b>の付与を持ち、除去で消す。
/// 被ダメログが、バフ由来の被ダメを受けた瞬間にそのバフの付与元(技)を引くのに使う。
///
/// <para>
/// <b>除去した実体は、除去の到着から <see cref="RemovedGrace"/> の間は引ける。</b>
/// 付与と除去が同じ差分で届く一瞬のバフがあり、そのダメージは同じ差分(バフ効果はダメージより先に処理する)や、
/// 周りの別の実体の差分・少し後のパケットで届く。すぐ消すとそのダメージの時点で引けない。
/// </para>
///
/// <para>
/// 実体の鍵は <c>(保持者, BuffUuid)</c>。<c>BuffUuid</c> は種別をまたいで使い回されるので単体では鍵にならない。
/// </para>
///
/// <para>
/// 除去が届かない実体があるので、持続が正のものは持続が切れて <see cref="ExpiryGrace"/> 過ぎたら掃除する。
/// 持続が0以下のものは除去かマップ切替でしか消えない。
/// 時刻はすべてパケットの到着時刻(UTC)で、付与と参照で同じ基準を使う。
/// </para>
///
/// <para>
/// <see cref="ActiveBuffStore"/>は画面に出すバフの控えで、敵の分は周りにいる間だけ持ち、除去した実体は引けない。別物。
/// </para>
/// </summary>
public sealed class BuffInstanceIndex
{
    private static readonly Lazy<BuffInstanceIndex> LazyInstance = new(() => new BuffInstanceIndex());

    private static readonly TimeSpan ExpiryGrace = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(10);

    /// <summary>除去した実体を引ける間。</summary>
    private static readonly TimeSpan RemovedGrace = TimeSpan.FromSeconds(1);

    private readonly object _sync = new();
    private readonly Dictionary<(long Holder, int BuffUuid), BuffInstance> _instances = [];
    private readonly Dictionary<int, HashSet<(long Holder, int BuffUuid)>> _keysByBaseId = [];
    private DateTime _lastSweep = DateTime.MinValue;

    /// <param name="RemovedAt">除去が届いた時刻。除去されていなければ <c>null</c>。</param>
    private readonly record struct BuffInstance(
        int BaseId,
        long FireUuid,
        int FightSourceType,
        int SourceConfigId,
        DateTime? ExpiresAt,
        DateTime? RemovedAt)
    {
        /// <summary><paramref name="time"/> の時点で引けるか。持続が切れて猶予を過ぎたもの、除去から <see cref="RemovedGrace"/> を過ぎたものは引けない。</summary>
        public bool IsAlive(DateTime time) => !(ExpiresAt < time) && !(RemovedAt + RemovedGrace < time);
    }

    private BuffInstanceIndex()
    {
    }

    public static BuffInstanceIndex Instance => LazyInstance.Value;

    /// <summary>付与(差分の <c>BuffEffectAddBuff</c> と、出現時の全バフスナップショット)。</summary>
    public void Add(
        long holderUuid,
        int buffUuid,
        int baseId,
        long fireUuid,
        int fightSourceType,
        int sourceConfigId,
        int durationMilliseconds,
        DateTime arrivalTime)
    {
        if (baseId <= 0)
        {
            return;
        }

        var key = (holderUuid, buffUuid);
        DateTime? expiresAt = durationMilliseconds > 0
            ? arrivalTime.AddMilliseconds(durationMilliseconds) + ExpiryGrace
            : null;

        lock (_sync)
        {
            if (_instances.TryGetValue(key, out var previous))
            {
                RemoveBaseIdKeyNoLock(previous.BaseId, key);
            }

            // 除去した実体と同じ鍵で付与が来たら、新しい実体に置き換わる(除去の時刻は持ち越さない)。
            _instances[key] = new BuffInstance(baseId, fireUuid, fightSourceType, sourceConfigId, expiresAt, RemovedAt: null);
            if (!_keysByBaseId.TryGetValue(baseId, out var keys))
            {
                keys = [];
                _keysByBaseId[baseId] = keys;
            }

            keys.Add(key);

            if (arrivalTime - _lastSweep >= SweepInterval)
            {
                SweepNoLock(arrivalTime);
                _lastSweep = arrivalTime;
            }
        }
    }

    /// <summary>
    /// 除去(<c>BuffEventRemove</c>)。除去イベントは <c>BuffUuid</c> しか運ばない。
    /// 除去の時刻を控えるだけで、実体は <see cref="RemovedGrace"/> の間は引ける。消すのは掃除。
    /// </summary>
    public void Remove(long holderUuid, int buffUuid, DateTime arrivalTime)
    {
        var key = (holderUuid, buffUuid);
        lock (_sync)
        {
            if (_instances.TryGetValue(key, out var instance) && instance.RemovedAt is null)
            {
                _instances[key] = instance with { RemovedAt = arrivalTime };
            }
        }
    }

    /// <summary>マップ切替と計測の停止で空にする。</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _instances.Clear();
            _keysByBaseId.Clear();
            _lastSweep = DateTime.MinValue;
        }
    }

    /// <summary>
    /// バフ由来のダメージ1件について、そのバフを付けた技を引く。
    ///
    /// <para>
    /// 候補は、同じ <paramref name="baseId"/> の生きている実体のうち術者が
    /// <paramref name="attackerRawUuid"/>(ダメージを出した実体そのもの)か
    /// <paramref name="attackerUuid"/>(召喚元へ寄せた加害者)のもの。
    /// <b>候補の付与元が1つにそろい、その種別が技のときだけ</b>その技IDを返す。
    /// 候補が無い・割れる・技以外(親のバフ・タレントなど)なら false。
    /// </para>
    ///
    /// <para>
    /// 保持者は条件にしない。被弾した本人に乗っていない実体が多い(召喚体が自分に掛けて持つオーラなど)。
    /// </para>
    ///
    /// <para>
    /// <paramref name="fireUuid"/> は最初に当たった候補の術者。付与元の技のレベルをこの術者から引く。
    /// </para>
    /// </summary>
    public bool TryResolveSourceSkill(
        int baseId,
        long attackerRawUuid,
        long attackerUuid,
        DateTime arrivalTime,
        out int skillId,
        out long fireUuid)
    {
        skillId = 0;
        fireUuid = 0;
        if (!TryResolveSource(baseId, attackerRawUuid, attackerUuid, arrivalTime, out var fightSourceType, out var sourceConfigId, out var firstFireUuid)
            || fightSourceType != (int)EFightSource.Skill
            || sourceConfigId <= 0)
        {
            return false;
        }

        skillId = sourceConfigId;
        fireUuid = firstFireUuid;
        return true;
    }

    /// <summary>
    /// <see cref="TryResolveSourceSkill"/> の付与元を種類を問わず返す版。候補の選び方と「そろわなければ false」は同じ。
    /// <paramref name="fireUuid"/> は最初に当たった候補の術者。
    /// </summary>
    public bool TryResolveSource(
        int baseId,
        long attackerRawUuid,
        long attackerUuid,
        DateTime arrivalTime,
        out int fightSourceType,
        out int sourceConfigId,
        out long fireUuid)
    {
        fightSourceType = 0;
        sourceConfigId = 0;
        fireUuid = 0;
        lock (_sync)
        {
            if (!_keysByBaseId.TryGetValue(baseId, out var keys))
            {
                return false;
            }

            (int FightSourceType, int SourceConfigId)? source = null;
            long firstFireUuid = 0;
            foreach (var key in keys)
            {
                var instance = _instances[key];
                if (!instance.IsAlive(arrivalTime)
                    || (instance.FireUuid != attackerRawUuid && instance.FireUuid != attackerUuid))
                {
                    continue;
                }

                var candidate = (instance.FightSourceType, instance.SourceConfigId);
                if (source is null)
                {
                    source = candidate;
                    firstFireUuid = instance.FireUuid;
                }
                else if (source.Value != candidate)
                {
                    return false;
                }
            }

            if (source is null)
            {
                return false;
            }

            fightSourceType = source.Value.FightSourceType;
            sourceConfigId = source.Value.SourceConfigId;
            fireUuid = firstFireUuid;
            return true;
        }
    }

    /// <summary>
    /// <paramref name="holderUuid"/> が持っている <paramref name="baseId"/> の生きている実体の術者(同じ術者は1つにまとめる)。無ければ空。
    ///
    /// <para>
    /// 親のバフの中で付いた子のバフは、親を持っている実体が術者として届く(光のプリズムの子のバフは、プレイヤーが付けた親を持つモンスターが術者)。
    /// その親を付けた本当の術者を引くのに使う。<see cref="TryResolveSource"/> は術者で候補を選ぶので、この形の親は引けない。
    /// </para>
    /// </summary>
    public IReadOnlyList<long> GetHeldInstanceFireUuids(int baseId, long holderUuid, DateTime arrivalTime)
    {
        lock (_sync)
        {
            if (!_keysByBaseId.TryGetValue(baseId, out var keys))
            {
                return [];
            }

            List<long>? fireUuids = null;
            foreach (var key in keys)
            {
                if (key.Holder != holderUuid)
                {
                    continue;
                }

                var instance = _instances[key];
                if (!instance.IsAlive(arrivalTime))
                {
                    continue;
                }

                fireUuids ??= [];
                if (!fireUuids.Contains(instance.FireUuid))
                {
                    fireUuids.Add(instance.FireUuid);
                }
            }

            return fireUuids ?? [];
        }
    }

    private void SweepNoLock(DateTime arrivalTime)
    {
        List<(long Holder, int BuffUuid)>? expired = null;
        foreach (var (key, instance) in _instances)
        {
            if (!instance.IsAlive(arrivalTime))
            {
                (expired ??= []).Add(key);
            }
        }

        if (expired is null)
        {
            return;
        }

        foreach (var key in expired)
        {
            var instance = _instances[key];
            _instances.Remove(key);
            RemoveBaseIdKeyNoLock(instance.BaseId, key);
        }
    }

    private void RemoveBaseIdKeyNoLock(int baseId, (long Holder, int BuffUuid) key)
    {
        if (_keysByBaseId.TryGetValue(baseId, out var keys) && keys.Remove(key) && keys.Count == 0)
        {
            _keysByBaseId.Remove(baseId);
        }
    }
}
