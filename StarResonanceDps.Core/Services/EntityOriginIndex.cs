// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 実体の出どころの控え。キーは実体のUUID。同じ中身の実体は1つの項目を共有し、実体ごとには項目への参照だけを持つ。
///
/// <para>
/// 項目の中身は2つ。<see cref="EntityOrigin"/>と着地先は<see cref="SourceLandingResolver"/>が出現の瞬間に書き、メーターの行名とバフの由来を決めるときに読む。
/// 弾は消えた後にダメージが届くことがあるので、消える通知では消さず、同じUUIDの出現で置き換える。
/// <see cref="DisappearedEntity"/>は<c>Encounter.RemoveDisappearedEntity</c>が実体を回(エンカウンター)から外すときに書き、
/// その実体が付けたバフの付与者と、後から届く通知で実体を作り直すときに読む。同じUUIDがまた現れたら消す。
/// </para>
///
/// <para>
/// どちらもいつ読まれるかが決まらない(遅れて届くダメージ・残っているバフ)ので、解放はマップ切替・ログアウト・キャプチャの停止だけ。
/// エンカウンターの作り直しでは消さない(区切りをまたいでバフが残る)。マップにいる間に増えるのは、実体ごとの参照と中身の種類の数だけ。
/// </para>
/// </summary>
public sealed class EntityOriginIndex
{
    private static readonly Lazy<EntityOriginIndex> LazyInstance = new(() => new EntityOriginIndex());

    private readonly object _sync = new();
    private readonly Dictionary<long, Entry> _entryByUuid = [];
    private readonly Dictionary<EntryKey, Entry> _entryByKey = [];

    private EntityOriginIndex()
    {
    }

    public static EntityOriginIndex Instance => LazyInstance.Value;

    /// <summary>
    /// 出どころと着地先を書く。同じ中身の項目があれば共有して真。無ければ何も書かずに偽で、呼び出し側が鎖の文字列を作って<see cref="AddOrigin"/>で書く
    /// (鎖の文字列は新しい中身のときにしか要らない)。
    /// </summary>
    public bool TrySetOrigin(long uuid, EntityOrigin origin, SourceLanding landing)
    {
        lock (_sync)
        {
            var disappeared = _entryByUuid.TryGetValue(uuid, out var current) ? current.Key.Disappeared : null;
            if (!_entryByKey.TryGetValue(new EntryKey(origin, landing, disappeared), out var shared))
            {
                return false;
            }

            _entryByUuid[uuid] = shared;
            return true;
        }
    }

    /// <param name="trace">たどった鎖。項目を共有するので、鎖の中の実体の番号は同じ中身で最初に書いた実体のもの。</param>
    public void AddOrigin(long uuid, EntityOrigin origin, SourceLanding landing, string trace)
    {
        lock (_sync)
        {
            var disappeared = _entryByUuid.TryGetValue(uuid, out var current) ? current.Key.Disappeared : null;
            _entryByUuid[uuid] = GetOrAddEntryNoLock(new EntryKey(origin, landing, disappeared), trace);
        }
    }

    public void RemoveOrigin(long uuid)
    {
        lock (_sync)
        {
            if (_entryByUuid.TryGetValue(uuid, out var current) && current.Key.Origin is not null)
            {
                ReplaceNoLock(uuid, current, current.Key with { Origin = null, Landing = SourceLanding.None });
            }
        }
    }

    public bool TryGetOrigin(long uuid, out EntityOrigin origin, out SourceLanding landing, out string trace)
    {
        lock (_sync)
        {
            if (_entryByUuid.TryGetValue(uuid, out var entry) && entry.Key.Origin is { } found)
            {
                origin = found;
                landing = entry.Key.Landing;
                trace = entry.Trace;
                return true;
            }
        }

        origin = default;
        landing = SourceLanding.None;
        trace = string.Empty;
        return false;
    }

    public void SetDisappeared(long uuid, DisappearedEntity disappeared)
    {
        lock (_sync)
        {
            _entryByUuid.TryGetValue(uuid, out var current);
            var key = (current?.Key ?? default) with { Disappeared = disappeared };
            _entryByUuid[uuid] = GetOrAddEntryNoLock(key, current?.Trace ?? string.Empty);
        }
    }

    public void RemoveDisappeared(long uuid)
    {
        lock (_sync)
        {
            if (_entryByUuid.TryGetValue(uuid, out var current) && current.Key.Disappeared is not null)
            {
                ReplaceNoLock(uuid, current, current.Key with { Disappeared = null });
            }
        }
    }

    public bool TryGetDisappeared(long uuid, out DisappearedEntity disappeared)
    {
        lock (_sync)
        {
            if (_entryByUuid.TryGetValue(uuid, out var entry) && entry.Key.Disappeared is { } found)
            {
                disappeared = found;
                return true;
            }
        }

        disappeared = default;
        return false;
    }

    /// <summary>周りから消えて回から外したモンスターの種別ID。種別IDが分からなければ偽。</summary>
    public bool TryGetDisappearedMonsterId(long uuid, out int monsterId)
    {
        if (TryGetDisappeared(uuid, out var disappeared) && disappeared.AttrId > 0)
        {
            monsterId = disappeared.AttrId;
            return true;
        }

        monsterId = 0;
        return false;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entryByUuid.Clear();
            _entryByUuid.TrimExcess();
            _entryByKey.Clear();
            _entryByKey.TrimExcess();
        }
    }

    private void ReplaceNoLock(long uuid, Entry current, EntryKey key)
    {
        if (key.Origin is null && key.Disappeared is null)
        {
            _entryByUuid.Remove(uuid);
            return;
        }

        _entryByUuid[uuid] = GetOrAddEntryNoLock(key, current.Trace);
    }

    private Entry GetOrAddEntryNoLock(EntryKey key, string trace)
    {
        if (!_entryByKey.TryGetValue(key, out var entry))
        {
            // 鎖を読むのは着地した項目だけ。
            entry = new Entry(key, key.Landing.Kind == SourceLandingKind.None ? string.Empty : trace);
            _entryByKey.Add(key, entry);
        }

        return entry;
    }

    /// <summary>項目を共有する単位。鎖の文字列は実体の番号を含むので入れない。</summary>
    private readonly record struct EntryKey(EntityOrigin? Origin, SourceLanding Landing, DisappearedEntity? Disappeared);

    private sealed record Entry(EntryKey Key, string Trace);
}

/// <summary>実体の付与元と召喚者。着地できなかったときのたどり直しに使うので、控えたときの値のまま持つ。</summary>
public readonly record struct EntityOrigin(int AttrId, int SourceType, int SourceId, long SummonerUuid, long TopSummonerUuid);

/// <summary>周りから消えて回から外した実体の値。無い値は0。弾・仮想体・領域は大元の召喚者だけ、モンスターは種別IDと召喚者も持つ。</summary>
public readonly record struct DisappearedEntity(int AttrId, long SummonerUuid, long TopSummonerUuid);
