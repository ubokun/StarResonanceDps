// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

public sealed class PlayerRosterStore
{
    private static readonly Lazy<PlayerRosterStore> LazyInstance = new(() => new PlayerRosterStore());

    private readonly object _sync = new();
    private readonly Dictionary<long, PlayerRosterEntry> _entries = [];

    /// <summary>
    /// 初めて現れた順。<b>行はソートせず、新しい行を末尾に足すためにこれが要る。</b>
    ///
    /// <para>
    /// <see cref="_entries"/> は <see cref="Replace"/> のたびに丸ごと入れ替わるうえ、
    /// <c>Dictionary</c> の列挙順は契約ではない(削除で空いた枠に新しい要素が入る)。
    /// だから順序はストア側で明示的に持つ。居なくなったら番号も捨てる。
    /// </para>
    /// </summary>
    private readonly Dictionary<long, long> _firstSeenOrderByCharacterId = [];
    private long _nextFirstSeenOrder;
    private IReadOnlyList<PlayerRosterEntry> _snapshot = Array.AsReadOnly(Array.Empty<PlayerRosterEntry>());
    private string _mapName = string.Empty;
    private uint _mapChannel;
    private long _mapGeneration;
    private int _seasonId;
    private readonly Comparison<PlayerRosterEntry> _snapshotOrder;

    private PlayerRosterStore()
    {
        _snapshotOrder = CompareSnapshotOrderNoLock;
    }

    public static PlayerRosterStore Instance => LazyInstance.Value;

    public event EventHandler<PlayerRosterChangedEventArgs>? RosterChanged;

    public IReadOnlyList<PlayerRosterEntry> Snapshot
    {
        get
        {
            lock (_sync)
            {
                return _snapshot;
            }
        }
    }

    public PlayerRosterSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return new PlayerRosterSnapshot(_snapshot, _mapName, _mapChannel, _mapGeneration, _seasonId);
            }
        }
    }

    public void BeginMap()
    {
        PublishIfChanged(
            () =>
            {
                _mapGeneration++;
                _entries.Clear();
                ClearFirstSeenOrderNoLock();
            },
            forcePublish: true);
    }

    /// <summary>
    /// マップ名とチャンネル番号。<b>合成した文字列にはしない。</b>
    /// 「ch1」の表記は言語ごとに違う(中国語は「1线」)ので、組み立ては翻訳資源を持つ App 側でやる。
    /// </summary>
    public void UpdateMapName(string? mapName, uint mapChannel)
    {
        var normalizedMapName = mapName ?? string.Empty;
        PublishIfChanged(
            () =>
            {
                _mapName = normalizedMapName;
                _mapChannel = mapChannel;
            },
            forcePublish: false);
    }

    /// <summary>今のシーズン番号。まだ分からなければ 0。</summary>
    public void UpdateSeason(int seasonId)
    {
        PublishIfChanged(
            () => _seasonId = seasonId,
            forcePublish: false);
    }

    public void Upsert(PlayerRosterEntry entry)
    {
        if (entry.CharacterId == 0)
        {
            return;
        }

        // 差分のたびに通る。中身が同じなら並べ直さない。
        lock (_sync)
        {
            if (_entries.TryGetValue(entry.CharacterId, out var existing)
                && existing.Equals(ApplyKnownSelf(entry, existing)))
            {
                return;
            }
        }

        UpsertChanged(entry);
    }

    private void UpsertChanged(PlayerRosterEntry entry)
    {
        PublishIfChanged(
            () =>
            {
                var stored = _entries.TryGetValue(entry.CharacterId, out var existing)
                    ? ApplyKnownSelf(entry, existing)
                    : entry;
                _entries[stored.CharacterId] = stored;
                EnsureFirstSeenOrderNoLock(stored.CharacterId);
            },
            forcePublish: false);
    }

    private static PlayerRosterEntry ApplyKnownSelf(PlayerRosterEntry entry, PlayerRosterEntry existing)
    {
        return existing.IsSelf && !entry.IsSelf
            ? entry with { IsSelf = true }
            : entry;
    }

    public void Replace(IEnumerable<PlayerRosterEntry> entries, string? mapName = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var nextEntries = entries
            .Where(entry => entry.CharacterId != 0)
            .GroupBy(entry => entry.CharacterId)
            .Select(group =>
            {
                var selected = group.LastOrDefault(entry => entry.IsSelf) ?? group.Last();
                return selected with { IsSelf = group.Any(entry => entry.IsSelf) };
            })
            .ToArray();

        PublishIfChanged(
            () =>
            {
                _entries.Clear();
                foreach (var entry in nextEntries)
                {
                    _entries[entry.CharacterId] = entry;
                    EnsureFirstSeenOrderNoLock(entry.CharacterId);
                }

                // 居なくなった人の番号は捨てる。戻ってきたら末尾に付き直す。
                foreach (var characterId in _firstSeenOrderByCharacterId.Keys.ToArray())
                {
                    if (!_entries.ContainsKey(characterId))
                    {
                        _firstSeenOrderByCharacterId.Remove(characterId);
                    }
                }

                if (mapName is not null)
                {
                    _mapName = mapName;
                }
            },
            forcePublish: false);
    }

    public void Clear()
    {
        PublishIfChanged(
            () =>
            {
                _mapGeneration++;
                _entries.Clear();
                ClearFirstSeenOrderNoLock();
                _mapName = string.Empty;
            },
            forcePublish: true);
    }

    private void PublishIfChanged(Action update, bool forcePublish)
    {
        PlayerRosterSnapshot? changedSnapshot = null;

        lock (_sync)
        {
            var previousEntries = _snapshot;
            var previousMapName = _mapName;
            var previousMapChannel = _mapChannel;
            var previousMapGeneration = _mapGeneration;
            var previousSeasonId = _seasonId;

            update();

            var nextSnapshot = CreateSnapshotNoLock();
            if (!forcePublish
                && previousEntries.SequenceEqual(nextSnapshot)
                && string.Equals(previousMapName, _mapName, StringComparison.Ordinal)
                && previousMapChannel == _mapChannel
                && previousMapGeneration == _mapGeneration
                && previousSeasonId == _seasonId)
            {
                return;
            }

            _snapshot = nextSnapshot;
            changedSnapshot = new PlayerRosterSnapshot(_snapshot, _mapName, _mapChannel, _mapGeneration, _seasonId);
        }

        RosterChanged?.Invoke(this, new PlayerRosterChangedEventArgs(changedSnapshot!));
    }

    /// <summary>
    /// 並びは <b>自分 → パーティ(PT順) → PT外の灰色 → PT外の戦闘中 → PT外のその他(それぞれ初出順)</b>。
    ///
    /// <para>
    /// <b>名前や値でソートしない。</b> 新しい行はその段の末尾に付く。
    /// 段が変わるのは、パーティの出入り・灰色とライブの切り替わり・戦闘状態の出入りのときだけ。
    /// </para>
    /// </summary>
    private IReadOnlyList<PlayerRosterEntry> CreateSnapshotNoLock()
    {
        var entries = new PlayerRosterEntry[_entries.Count];
        _entries.Values.CopyTo(entries, 0);
        Array.Sort(entries, _snapshotOrder);
        return Array.AsReadOnly(entries);
    }

    private int CompareSnapshotOrderNoLock(PlayerRosterEntry left, PlayerRosterEntry right)
    {
        var order = GetGroupRank(left).CompareTo(GetGroupRank(right));
        if (order != 0)
        {
            return order;
        }

        order = GetPartyOrder(left).CompareTo(GetPartyOrder(right));
        return order != 0
            ? order
            : GetFirstSeenOrderNoLock(left).CompareTo(GetFirstSeenOrderNoLock(right));
    }

    /// <summary>パーティ内だけは PT番号順。番号が無い人はその後ろへ初出順で続く。</summary>
    private static int GetPartyOrder(PlayerRosterEntry entry)
    {
        return entry.IsPartyMember && entry.PartyNumber.HasValue
            ? entry.PartyNumber!.Value
            : int.MaxValue;
    }

    /// <summary>
    /// 群の順。<b>自分 → パーティ → PT外の灰色 → PT外の戦闘中 → PT外のその他。</b>並び替えの設定(発見順 / 名前順)は
    /// この中だけを決めるので、表示側もこの関数を使う(同じ規則を2か所に書かない)。
    /// 上から順に当てるので、戦闘中の自分とパーティの人は自分・パーティの群のまま。
    /// </summary>
    public static int GetGroupRank(PlayerRosterEntry entry)
    {
        if (entry.IsSelf)
        {
            return 0;
        }

        if (entry.IsPartyMember)
        {
            return 1;
        }

        // 灰色(キャッシュしか無い行)はライブより上。
        if (!entry.IsLive)
        {
            return 2;
        }

        return entry.IsInCombat ? 3 : 4;
    }

    private long GetFirstSeenOrderNoLock(PlayerRosterEntry entry)
    {
        return _firstSeenOrderByCharacterId.TryGetValue(entry.CharacterId, out var order)
            ? order
            : long.MaxValue;
    }

    private void EnsureFirstSeenOrderNoLock(long characterId)
    {
        if (characterId != 0 && !_firstSeenOrderByCharacterId.ContainsKey(characterId))
        {
            _firstSeenOrderByCharacterId[characterId] = _nextFirstSeenOrder++;
        }
    }

    private void ClearFirstSeenOrderNoLock()
    {
        _firstSeenOrderByCharacterId.Clear();
        _nextFirstSeenOrder = 0;
    }
}

/// <param name="SeasonId">今のシーズン番号。まだ分からなければ 0。</param>
public sealed record PlayerRosterSnapshot(
    IReadOnlyList<PlayerRosterEntry> Entries,
    string MapName,
    uint MapChannel,
    long MapGeneration,
    int SeasonId);

public sealed class PlayerRosterChangedEventArgs(PlayerRosterSnapshot roster) : EventArgs
{
    public IReadOnlyList<PlayerRosterEntry> Snapshot { get; } = roster.Entries;

    /// <summary>チャンネル番号。チャンネルの無い場所では 0。</summary>
    public uint MapChannel { get; } = roster.MapChannel;

    public string MapName { get; } = roster.MapName;

    public long MapGeneration { get; } = roster.MapGeneration;

    /// <summary>今のシーズン番号。まだ分からなければ 0。</summary>
    public int SeasonId { get; } = roster.SeasonId;
}
