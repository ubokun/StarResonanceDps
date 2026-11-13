// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

public enum PartyMembershipState
{
    Unknown,
    Member,
    NonMember
}

public sealed record PartyMemberSupplement(
    string Name = "",
    int ProfessionId = 0,
    int CombatPower = 0,
    int SeasonStrength = 0,
    int Level = 0,
    int SeasonLevel = 0,
    PlayerEquipmentData? EquipmentData = null,
    bool IsNpc = false,
    long CurrentHp = 0,
    long MaxHp = 0,
    bool HasTrustedSocialData = false,
    // いる場所(userSceneInfo)。マップ番号とチャンネル番号、0 は分からない。古い値を送る NotifySocialData からは入れない。
    int SceneId = 0,
    int LineId = 0,
    // パーティの社交データ(名前・クラス・能力スコアなど)を受けたか。HP の同期だけでも補完は作られるので、補完があることとは別。
    bool HasSocialData = false,
    // HP の同期の状態が戦闘不能か。同期の HP は読み込み中などの生きている人にも 0 で届くので、戦闘不能はこちらで決める。
    bool IsDead = false);

public sealed record PartyMemberPosition(int GroupId, int? GroupSlot);

public sealed class PartyStateSnapshot
{
    private readonly HashSet<long> _memberIds;

    internal PartyStateSnapshot(
        long teamId,
        bool hasCompleteMembership,
        bool isFivePersonParty,
        IReadOnlyList<long> orderedCharacterIds,
        IReadOnlySet<long> memberIds,
        IReadOnlySet<long> knownNonMemberIds,
        IReadOnlyDictionary<long, PartyMemberSupplement> supplements,
        IReadOnlyDictionary<long, PartyMemberPosition> positions)
    {
        TeamId = teamId;
        HasCompleteMembership = hasCompleteMembership;
        IsFivePersonParty = isFivePersonParty;
        OrderedCharacterIds = orderedCharacterIds;
        KnownNonMemberIds = knownNonMemberIds;
        Supplements = supplements;
        Positions = positions;
        _memberIds = memberIds.ToHashSet();
    }

    public long TeamId { get; }

    public bool HasCompleteMembership { get; }

    /// <summary>
    /// パーティに入っていると分かっているのに、顔ぶれがまだ確定していない(パーティ画面を開くと届く)。
    /// 始めた直後で入っているか分からない間(<see cref="TeamId"/> 0)は含めない。
    /// </summary>
    public bool IsMembershipPending => TeamId != 0 && !HasCompleteMembership;

    public bool IsFivePersonParty { get; }

    public IReadOnlyList<long> OrderedCharacterIds { get; }

    public IReadOnlySet<long> MemberIds => _memberIds;

    public IReadOnlySet<long> KnownNonMemberIds { get; }

    public IReadOnlyDictionary<long, PartyMemberSupplement> Supplements { get; }

    public IReadOnlyDictionary<long, PartyMemberPosition> Positions { get; }

    public int? GetPartyNumber(long characterId)
    {
        if (!_memberIds.Contains(characterId))
        {
            return null;
        }

        if (IsFivePersonParty)
        {
            if (HasCompleteMembership
                && _memberIds.Count == 1
                && (!Supplements.TryGetValue(characterId, out var singleSupplement) || !singleSupplement.IsNpc))
            {
                return 1;
            }

            var orderedIndex = -1;
            for (var index = 0; index < OrderedCharacterIds.Count; index++)
            {
                if (OrderedCharacterIds[index] == characterId)
                {
                    orderedIndex = index;
                    break;
                }
            }

            if (orderedIndex >= 0)
            {
                var isNpc = Supplements.TryGetValue(characterId, out var supplement) && supplement.IsNpc;
                if (isNpc)
                {
                    return orderedIndex + 1;
                }

                var humanOrderIsAuthoritative = OrderedCharacterIds
                    .Where(memberId => !Supplements.TryGetValue(memberId, out var memberSupplement) || !memberSupplement.IsNpc)
                    .All(memberId => Positions.TryGetValue(memberId, out var memberPosition)
                        && memberPosition.GroupId == 1
                        && memberPosition.GroupSlot is >= 1 and <= 5);
                return humanOrderIsAuthoritative ? orderedIndex + 1 : null;
            }
        }

        if (Positions.TryGetValue(characterId, out var position)
            && position.GroupId >= 1
            && position.GroupSlot is int groupSlot
            && groupSlot >= 1
            && groupSlot <= 5)
        {
            return ((position.GroupId - 1) * 5) + groupSlot;
        }

        return null;
    }

    public PartyMembershipState GetMembership(long characterId)
    {
        if (characterId != 0 && _memberIds.Contains(characterId))
        {
            return PartyMembershipState.Member;
        }

        return HasCompleteMembership || KnownNonMemberIds.Contains(characterId)
            ? PartyMembershipState.NonMember
            : PartyMembershipState.Unknown;
    }

    public bool ShouldInclude(long characterId, bool isSelf, PartyDisplayMode mode)
    {
        if (mode == PartyDisplayMode.SelfOnly)
        {
            return isSelf;
        }

        if (isSelf)
        {
            return mode != PartyDisplayMode.NonPartyMembersOnly;
        }

        var membership = GetMembership(characterId);
        return mode switch
        {
            PartyDisplayMode.PartyMembersOnly => membership == PartyMembershipState.Member,
            PartyDisplayMode.NonPartyMembersOnly => membership == PartyMembershipState.NonMember,
            _ => true
        };
    }

    public bool TryGetSupplement(long characterId, out PartyMemberSupplement supplement)
    {
        if (GetMembership(characterId) == PartyMembershipState.Member
            && Supplements.TryGetValue(characterId, out var found))
        {
            supplement = found;
            return true;
        }

        supplement = null!;
        return false;
    }
}

public sealed class PartyStateStore
{
    private static readonly Lazy<PartyStateStore> LazyInstance = new(() => new PartyStateStore());

    private readonly object _sync = new();
    private readonly HashSet<long> _memberIds = [];
    private readonly HashSet<long> _knownNonMemberIds = [];
    private readonly Dictionary<long, PartyMemberSupplement> _supplements = [];
    private readonly Dictionary<long, PartyMemberPosition> _positions = [];
    private readonly Dictionary<long, uint> _enterTimes = [];
    private long _teamId;
    private bool _hasCompleteMembership;
    private bool _isFivePersonParty;
    private PartyStateSnapshot _current;

    private PartyStateStore()
    {
        _current = CreateSnapshotNoLock();
    }

    public static PartyStateStore Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public PartyStateSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public void ResetUnknown()
    {
        PublishIfChanged(() =>
        {
            _teamId = 0;
            _hasCompleteMembership = false;
            _isFivePersonParty = false;
            _memberIds.Clear();
            _knownNonMemberIds.Clear();
            _supplements.Clear();
            _positions.Clear();
            _enterTimes.Clear();
        });
    }

    public void SetNoParty()
    {
        PublishIfChanged(() =>
        {
            _teamId = 0;
            _hasCompleteMembership = true;
            _isFivePersonParty = false;
            _memberIds.Clear();
            _knownNonMemberIds.Clear();
            _supplements.Clear();
            _positions.Clear();
            _enterTimes.Clear();
        });
    }

    public void ApplyAuthoritativeMembership(
        long teamId,
        bool isFivePersonParty,
        IEnumerable<(int GroupId, IEnumerable<long> CharacterIds)> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var materializedGroups = groups
            .OrderBy(group => group.GroupId)
            .Select(group => (
                GroupId: group.GroupId,
                CharacterIds: group.CharacterIds.ToArray()))
            .ToArray();
        var orderedIds = materializedGroups
            .SelectMany(group => group.CharacterIds)
            .Where(characterId => characterId > 0)
            .Distinct()
            .ToArray();

        PublishIfChanged(() =>
        {
            if (_teamId != 0 && teamId != 0 && _teamId != teamId)
            {
                _supplements.Clear();
                _knownNonMemberIds.Clear();
                _enterTimes.Clear();
            }

            _teamId = teamId;
            _hasCompleteMembership = true;
            _isFivePersonParty = isFivePersonParty;
            _memberIds.Clear();
            _memberIds.UnionWith(orderedIds);
            _positions.Clear();
            foreach (var group in materializedGroups)
            {
                var seenCharacterIds = new HashSet<long>();
                for (var index = 0; index < group.CharacterIds.Length; index++)
                {
                    var characterId = group.CharacterIds[index];
                    if (characterId <= 0 || !seenCharacterIds.Add(characterId))
                    {
                        continue;
                    }

                    var groupId = isFivePersonParty ? 1 : group.GroupId;
                    _positions[characterId] = new PartyMemberPosition(groupId, index + 1);
                }
            }
            _knownNonMemberIds.ExceptWith(orderedIds);
            RemoveNonMemberSupplementsNoLock();
        });
    }

    public void ApplyCompleteMembership(
        long teamId,
        bool isFivePersonParty,
        IEnumerable<long> characterIds,
        IEnumerable<(long CharacterId, int GroupId)>? groupAssignments = null)
    {
        ArgumentNullException.ThrowIfNull(characterIds);
        var memberIds = characterIds
            .Where(characterId => characterId > 0)
            .Distinct()
            .ToArray();
        var assignedGroups = (groupAssignments ?? [])
            .Where(assignment => assignment.CharacterId > 0 && assignment.GroupId > 0)
            .GroupBy(assignment => assignment.CharacterId)
            .ToDictionary(group => group.Key, group => group.First().GroupId);

        PublishIfChanged(() =>
        {
            if (_teamId != 0 && teamId != 0 && _teamId != teamId)
            {
                _supplements.Clear();
                _knownNonMemberIds.Clear();
                _positions.Clear();
                _enterTimes.Clear();
            }

            _teamId = teamId;
            _hasCompleteMembership = true;
            _isFivePersonParty = isFivePersonParty;
            _memberIds.Clear();
            _memberIds.UnionWith(memberIds);
            foreach (var characterId in _positions.Keys.Where(characterId => !_memberIds.Contains(characterId)).ToArray())
            {
                _positions.Remove(characterId);
            }

            // 位置は権威側(TeamMemberGroupInfos)だけが決める。
            //
            // ここへ来る経路(コンテナ同期・GetTeamInfo の前半)は TeamMemData.GroupId しか持たず、
            // グループ内の順番を持たない。しかもそのグループIDが権威側と食い違うことがあり、
            // 上書きすると順番ごと捨てられて番号が ? に戻る。
            //
            // したがって既存の位置には触らない。持っていないメンバーにだけ、
            // グループIDだけを記録する(順番は権威側が来るまで空)。
            foreach (var characterId in memberIds)
            {
                if (_positions.ContainsKey(characterId))
                {
                    continue;
                }

                var groupId = isFivePersonParty
                    ? 1
                    : assignedGroups.GetValueOrDefault(characterId);
                if (groupId <= 0)
                {
                    continue;
                }

                _positions[characterId] = new PartyMemberPosition(groupId, null);
            }
            _knownNonMemberIds.ExceptWith(memberIds);
            RemoveNonMemberSupplementsNoLock();
        });
    }

    /// <summary>
    /// メンバーの加入時刻を取り込む。5人PTの表示順はこの値の昇順で決まる
    /// (TeamMemberGroupInfos.CharIds の配列位置は5人PTでは表示順ではない)。
    /// 再加入で EnterTime は更新されるため、常に上書きする。
    /// </summary>
    public void ApplyMemberEnterTimes(long teamId, IEnumerable<(long CharacterId, uint EnterTime)> enterTimes)
    {
        ArgumentNullException.ThrowIfNull(enterTimes);
        var materialized = enterTimes
            .Where(entry => entry.CharacterId > 0 && entry.EnterTime > 0)
            .ToArray();
        if (materialized.Length == 0)
        {
            return;
        }

        PublishIfChanged(() =>
        {
            PrepareKnownMemberUpdateNoLock(teamId);
            foreach (var entry in materialized)
            {
                _enterTimes[entry.CharacterId] = entry.EnterTime;
            }
        });
    }

    public void ApplyKnownMembers(long teamId, IEnumerable<long> characterIds)
    {
        ArgumentNullException.ThrowIfNull(characterIds);
        var knownIds = characterIds
            .Where(characterId => characterId > 0)
            .Distinct()
            .ToArray();
        if (knownIds.Length == 0 && teamId == 0)
        {
            return;
        }

        PublishIfChanged(() =>
        {
            PrepareKnownMemberUpdateNoLock(teamId);

            foreach (var characterId in knownIds)
            {
                if (!_hasCompleteMembership)
                {
                    _memberIds.Add(characterId);
                }

                if (_memberIds.Contains(characterId))
                {
                    _knownNonMemberIds.Remove(characterId);
                }
            }
        });
    }

    public void MarkNonMember(long characterId)
    {
        if (characterId <= 0)
        {
            return;
        }

        PublishIfChanged(() =>
        {
            _memberIds.Remove(characterId);
            _supplements.Remove(characterId);
            _positions.Remove(characterId);
            _enterTimes.Remove(characterId);
            _knownNonMemberIds.Add(characterId);
        });
    }

    public void RemoveMember(long characterId)
    {
        if (characterId <= 0)
        {
            return;
        }

        PublishIfChanged(() =>
        {
            _memberIds.Remove(characterId);
            _supplements.Remove(characterId);
            _positions.Remove(characterId);
            _enterTimes.Remove(characterId);
            _knownNonMemberIds.Add(characterId);
        });
    }

    public void UpdateSupplement(long teamId, long characterId, Func<PartyMemberSupplement, PartyMemberSupplement> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (characterId <= 0)
        {
            return;
        }

        PublishIfChanged(() =>
        {
            PrepareKnownMemberUpdateNoLock(teamId);
            if (!_hasCompleteMembership)
            {
                _memberIds.Add(characterId);
            }

            if (!_memberIds.Contains(characterId))
            {
                return;
            }

            _knownNonMemberIds.Remove(characterId);

            var current = _supplements.TryGetValue(characterId, out var existing)
                ? existing
                : new PartyMemberSupplement();
            _supplements[characterId] = update(current);
        }, raiseChanged: false);
    }

    private void PrepareKnownMemberUpdateNoLock(long teamId)
    {
        if (_teamId != 0 && teamId != 0 && _teamId != teamId)
        {
            _memberIds.Clear();
            _supplements.Clear();
            _positions.Clear();
            _enterTimes.Clear();
            _knownNonMemberIds.Clear();
            _hasCompleteMembership = false;
            _isFivePersonParty = false;
        }

        if (teamId != 0)
        {
            _teamId = teamId;
        }
    }

    private void PublishIfChanged(Action update, bool raiseChanged = true)
    {
        var changed = false;

        lock (_sync)
        {
            var previous = _current;
            update();
            var next = CreateSnapshotNoLock();
            changed = !SnapshotsEqual(previous, next);
            if (changed)
            {
                _current = next;
            }
        }

        if (changed && raiseChanged)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private PartyStateSnapshot CreateSnapshotNoLock()
    {
        var positions = BuildEffectivePositionsNoLock();
        var orderedIds = _memberIds
            .GroupBy(characterId => positions.TryGetValue(characterId, out var position) ? position.GroupId : int.MaxValue)
            .OrderBy(group => group.Key)
            .SelectMany(group => OrderGroupNoLock(group.Key, group, positions))
            .ToArray();

        return new PartyStateSnapshot(
            _teamId,
            _hasCompleteMembership,
            _isFivePersonParty,
            Array.AsReadOnly(orderedIds),
            new HashSet<long>(_memberIds),
            new HashSet<long>(_knownNonMemberIds),
            new System.Collections.ObjectModel.ReadOnlyDictionary<long, PartyMemberSupplement>(
                new Dictionary<long, PartyMemberSupplement>(_supplements)),
            new System.Collections.ObjectModel.ReadOnlyDictionary<long, PartyMemberPosition>(positions));
    }

    private IEnumerable<long> OrderGroupNoLock(
        int groupId,
        IEnumerable<long> characterIds,
        IReadOnlyDictionary<long, PartyMemberPosition> positions)
    {
        var groupPositions = characterIds
            .Select(characterId => (CharacterId: characterId, Position: positions.GetValueOrDefault(characterId)))
            .ToArray();
        if (_isFivePersonParty && groupId == 1)
        {
            var npcMembers = groupPositions
                .Where(item => _supplements.TryGetValue(item.CharacterId, out var supplement) && supplement.IsNpc)
                .OrderBy(item => item.CharacterId)
                .ToArray();
            if (npcMembers.Length > 0)
            {
                var humanMembers = groupPositions
                    .Where(item => !_supplements.TryGetValue(item.CharacterId, out var supplement) || !supplement.IsNpc)
                    .ToArray();
                var orderedHumanIds = humanMembers.All(item => item.Position?.GroupSlot is not null)
                    ? humanMembers
                        .OrderBy(item => item.Position!.GroupSlot)
                        .ThenBy(item => item.CharacterId)
                        .Select(item => item.CharacterId)
                    : humanMembers
                        .OrderBy(item => item.CharacterId)
                        .Select(item => item.CharacterId);

                return orderedHumanIds.Concat(npcMembers.Select(item => item.CharacterId));
            }
        }

        if (groupPositions.All(item => item.Position?.GroupSlot is not null))
        {
            return groupPositions
                .OrderBy(item => item.Position!.GroupSlot)
                .ThenBy(item => item.CharacterId)
                .Select(item => item.CharacterId);
        }

        var unknownMembers = groupPositions
            .Where(item => item.Position?.GroupSlot is null)
            .OrderBy(item => item.CharacterId)
            .ToArray();
        var confirmedMembers = groupPositions
            .Where(item => item.Position?.GroupSlot is not null)
            .OrderBy(item => item.Position!.GroupSlot)
            .ToArray();
        var hasNpcTailOrder = groupId == 1
            && groupPositions.Length == 5
            && confirmedMembers.Length > 0
            && confirmedMembers.All(item =>
                _supplements.TryGetValue(item.CharacterId, out var supplement) && supplement.IsNpc)
            && confirmedMembers.Select(item => item.Position!.GroupSlot!.Value)
                .SequenceEqual(Enumerable.Range(unknownMembers.Length + 1, confirmedMembers.Length));

        return hasNpcTailOrder
            ? unknownMembers.Select(item => item.CharacterId)
                .Concat(confirmedMembers.Select(item => item.CharacterId))
            : groupPositions.OrderBy(item => item.CharacterId).Select(item => item.CharacterId);
    }

    private Dictionary<long, PartyMemberPosition> BuildEffectivePositionsNoLock()
    {
        if (TryBuildFivePersonEnterTimePositionsNoLock(out var enterTimePositions))
        {
            return enterTimePositions;
        }

        var positions = _positions
            .Where(pair => _memberIds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        if (_memberIds.Count != 5
            || positions.Count != 5
            || positions.Values.Any(position => position.GroupId != 1 || position.GroupSlot is not null))
        {
            return positions;
        }

        var npcIds = _memberIds
            .Where(characterId => _supplements.TryGetValue(characterId, out var supplement) && supplement.IsNpc)
            .OrderBy(characterId => characterId)
            .ToArray();
        if (npcIds.Length == 0)
        {
            return positions;
        }

        var humanIds = _memberIds.Except(npcIds).OrderBy(characterId => characterId).ToArray();
        for (var index = 0; index < npcIds.Length; index++)
        {
            positions[npcIds[index]] = new PartyMemberPosition(1, humanIds.Length + index + 1);
        }

        if (humanIds.Length == 1)
        {
            positions[humanIds[0]] = new PartyMemberPosition(1, 1);
        }

        return positions;
    }

    /// <summary>
    /// 5人PTの表示スロットを EnterTime 昇順から導出する。
    /// <b><c>TeamMemberGroupInfos.CharIds</c> の配列位置は5人PTの表示順ではない</b> —
    /// 加入順が入れ替わってもあの配列は更新されない。
    /// <c>_positions</c> は書き換えず導出値を上書きするだけなので、
    /// EnterTime が揃わなければ従来の並び順に戻る。NPC を含む編成は既存ロジックに委ねる。
    /// </summary>
    private bool TryBuildFivePersonEnterTimePositionsNoLock(
        out Dictionary<long, PartyMemberPosition> positions)
    {
        positions = [];
        if (!_isFivePersonParty || _memberIds.Count == 0)
        {
            return false;
        }

        var hasNpcMember = _memberIds.Any(characterId =>
            _supplements.TryGetValue(characterId, out var supplement) && supplement.IsNpc);
        if (hasNpcMember)
        {
            return false;
        }

        if (!_memberIds.All(characterId => _enterTimes.ContainsKey(characterId)))
        {
            return false;
        }

        var orderedIds = _memberIds
            .OrderBy(characterId => _enterTimes[characterId])
            .ThenBy(characterId => characterId)
            .ToArray();
        for (var index = 0; index < orderedIds.Length; index++)
        {
            positions[orderedIds[index]] = new PartyMemberPosition(1, index + 1);
        }

        return true;
    }

    private void RemoveNonMemberSupplementsNoLock()
    {
        foreach (var characterId in _supplements.Keys.Where(characterId => !_memberIds.Contains(characterId)).ToArray())
        {
            _supplements.Remove(characterId);
        }
    }

    private static bool SnapshotsEqual(PartyStateSnapshot left, PartyStateSnapshot right)
    {
        return left.TeamId == right.TeamId
            && left.HasCompleteMembership == right.HasCompleteMembership
            && left.IsFivePersonParty == right.IsFivePersonParty
            && left.OrderedCharacterIds.SequenceEqual(right.OrderedCharacterIds)
            && left.MemberIds.SetEquals(right.MemberIds)
            && left.KnownNonMemberIds.SetEquals(right.KnownNonMemberIds)
            && left.Supplements.Count == right.Supplements.Count
            && left.Supplements.All(pair => right.Supplements.TryGetValue(pair.Key, out var value) && pair.Value == value)
            && left.Positions.Count == right.Positions.Count
            && left.Positions.All(pair => right.Positions.TryGetValue(pair.Key, out var value) && pair.Value == value);
    }
}
