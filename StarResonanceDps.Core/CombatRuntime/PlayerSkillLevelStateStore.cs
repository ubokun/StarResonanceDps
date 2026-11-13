// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Frozen;
using StarResonanceDps.Core.CombatRuntime.DataTypes.Enums;
using StarResonanceDps.Core.Models;
using BinaryDutyInfo = StarResonanceDps.Core.Protocols.Game.Binary.DutyInfo;
using BinaryDutyList = StarResonanceDps.Core.Protocols.Game.Binary.DutyList;
using BinaryProfessionInfo = StarResonanceDps.Core.Protocols.Game.Binary.ProfessionInfo;
using BinaryProfessionList = StarResonanceDps.Core.Protocols.Game.Binary.ProfessionList;
using BinaryProfessionSkillInfo = StarResonanceDps.Core.Protocols.Game.Binary.ProfessionSkillInfo;
using BinaryProtocol = StarResonanceDps.Core.Protocols.Game.Binary;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public readonly record struct PlayerRoleSkillLevelState(
    int SkillId,
    int CurrentLevel,
    int Tier);

public static class PlayerSkillLevelStateStore
{
    private static readonly object StateLock = new();
    private static readonly Dictionary<int, ProfessionState> SelfProfessionStates = [];
    private static readonly Dictionary<int, DutyState> SelfDutyStates = [];
    private static Dictionary<int, SkillState> _selfImagineSkills = [];
    private static Dictionary<int, SkillState> _selfRawSelectedImagineSkills = [];
    private static int _selfCurrentProfessionId;
    private static int _selfCurrentDutyId;
    private static bool _hasSelfDutyState;
    private static FrozenDictionary<int, int> _selfSkillLevels =
        new Dictionary<int, int>().ToFrozenDictionary();
    private static DataTypes.Skills.SkillLevelInfo[] _selfCurrentSkillLevels = [];
    private static PlayerRoleSkillLevelState[] _selfRoleSkillLevels = [];

    /// <summary>
    /// 自分のアクションバー。<c>{枠番号 → スキルID}</c> をそのまま持つ。
    ///
    /// <para>
    /// 出どころは AOI属性 <c>AttrSlot</c>(226)。自分にしか届かず、毎回 全枠が丸ごと来る
    /// (空枠も <c>skillId=0</c> として枠ごと入っている)。
    /// <b>フルコンテナの <c>CharSerialize.Slots</c> は使えない</b> — 空枠を一部落とすうえ、
    /// 特化の置換が反映されていない置換前のスキルIDを持っている。
    /// </para>
    ///
    /// <para><b>空枠は 0 のまま残す。</b> 落とすと枠の位置が失われる。</para>
    ///
    /// <para><c>null</c> は未受信。</para>
    /// </summary>
    private static FrozenDictionary<int, int>? _selfActionBarSlots;

    public static void ReplaceSelfSkillLevels(
        Zproto.ProfessionList? professionList,
        Zproto.DutyList? dutyList)
    {
        lock (StateLock)
        {
            SelfProfessionStates.Clear();
            _selfImagineSkills = [];
            _selfRawSelectedImagineSkills = [];
            _selfCurrentProfessionId = professionList?.CurProfessionId ?? 0;

            if (professionList is not null)
            {
                foreach (var pair in professionList.ProfessionList_)
                {
                    SelfProfessionStates[pair.Key] = CreateProfessionState(pair.Value);
                }

                foreach (var pair in professionList.AoyiSkillInfoMap)
                {
                    var skillId = ResolveSkillId(pair.Key, pair.Value.SkillId);
                    if (skillId > 0)
                    {
                        _selfImagineSkills[skillId] = new SkillState(
                            skillId,
                            pair.Value.Level,
                            pair.Value.RemodelLevel);
                    }
                }
            }

            SelfDutyStates.Clear();
            _selfCurrentDutyId = dutyList?.CurProfessionDutyId ?? 0;
            _hasSelfDutyState = dutyList is not null;

            if (dutyList is not null)
            {
                foreach (var pair in dutyList.DutyInfoMap)
                {
                    SelfDutyStates[pair.Key] = CreateDutyState(pair.Value);
                }
            }

            PublishState();
        }
    }

    public static void ApplySelfProfessionListChanges(BinaryProfessionList professionList)
    {
        lock (StateLock)
        {
            if (professionList.CurProfessionId is { } currentProfessionId)
            {
                _selfCurrentProfessionId = currentProfessionId;
            }

            if (professionList.ProfessionInfoChanges is { } professionChanges)
            {
                if (professionChanges.ReplacesExisting)
                {
                    SelfProfessionStates.Clear();
                }

                foreach (var professionId in professionChanges.Removed)
                {
                    SelfProfessionStates.Remove(professionId);
                }

                foreach (var pair in professionChanges.Added)
                {
                    var state = new ProfessionState();
                    ApplyProfessionInfoChanges(state, pair.Value);
                    SelfProfessionStates[pair.Key] = state;
                }

                foreach (var pair in professionChanges.Updated)
                {
                    if (!SelfProfessionStates.TryGetValue(pair.Key, out var state))
                    {
                        state = new ProfessionState();
                        SelfProfessionStates[pair.Key] = state;
                    }

                    ApplyProfessionInfoChanges(state, pair.Value);
                }
            }

            if (professionList.AoyiSkillInfoChanges is { } imagineChanges)
            {
                ApplySkillMapChanges(_selfImagineSkills, imagineChanges);
            }

            PublishState();
        }
    }

    public static void ApplySelfDutyListChanges(BinaryDutyList dutyList)
    {
        lock (StateLock)
        {
            _hasSelfDutyState = true;
            if (dutyList.CurProfessionDutyId is { } currentDutyId)
            {
                _selfCurrentDutyId = currentDutyId;
            }

            if (dutyList.DutyInfoChanges is { } dutyChanges)
            {
                if (dutyChanges.ReplacesExisting)
                {
                    SelfDutyStates.Clear();
                }

                foreach (var dutyId in dutyChanges.Removed)
                {
                    SelfDutyStates.Remove(dutyId);
                }

                foreach (var pair in dutyChanges.Added)
                {
                    var dutyState = new DutyState();
                    ApplyDutyInfoChanges(dutyState, pair.Value);
                    SelfDutyStates[pair.Key] = dutyState;
                }

                foreach (var pair in dutyChanges.Updated)
                {
                    if (!SelfDutyStates.TryGetValue(pair.Key, out var dutyState))
                    {
                        dutyState = new DutyState();
                        SelfDutyStates[pair.Key] = dutyState;
                    }

                    ApplyDutyInfoChanges(dutyState, pair.Value);
                }
            }

            PublishState();
        }
    }

    public static void UpdateSelfRawSkillLevels(
        IReadOnlyList<DataTypes.Skills.SkillLevelInfo> skillLevels)
    {
        lock (StateLock)
        {
            var selectedImagineSkills = new Dictionary<int, SkillState>();
            foreach (var skill in skillLevels)
            {
                if (skill.SkillId <= 0
                    || !CombatDataCatalog.IsSkillImagine(skill.SkillId, skill.Icon))
                {
                    continue;
                }

                selectedImagineSkills[skill.SkillId] = new SkillState(
                    skill.SkillId,
                    skill.CurrentLevel,
                    skill.Tier);
            }

            _selfRawSelectedImagineSkills = selectedImagineSkills;
            PublishState();
        }
    }

    public static void SetSelfCurrentProfessionId(int professionId)
    {
        if (professionId <= 0)
        {
            return;
        }

        lock (StateLock)
        {
            _selfCurrentProfessionId = professionId;
            PublishState();
        }
    }

    /// <summary>
    /// 自分のスキルの控えを起動時の値に戻す。キャプチャを止めたとき(止めている間の差分が届かず古くなる)と
    /// ログアウト(キャラが替わりうる)に呼ぶ。アクションバーも「一度も受信していない」に戻す。
    /// </summary>
    public static void ResetSelfToStartup()
    {
        ReplaceSelfSkillLevels(null, null);
        Volatile.Write(ref _selfActionBarSlots, null);
    }

    /// <summary>
    /// 自分のアクションバーを丸ごと差し替える。<c>AttrSlot</c> は毎回全枠を運ぶので、
    /// 差分を持たず置き換えるだけでよい。
    /// </summary>
    public static void ReplaceSelfActionBarSlots(IReadOnlyDictionary<int, int> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        Volatile.Write(ref _selfActionBarSlots, slots.ToFrozenDictionary());
    }

    /// <summary>
    /// アクションバーを一度でも受信したか。
    ///
    /// <para>
    /// 未受信と「全枠が空」を区別するために要る。未受信のときに空欄を並べると、
    /// 受信できていないことが空欄と同じ見た目になって気付けない。
    /// </para>
    /// </summary>
    public static bool HasSelfActionBarSlots => Volatile.Read(ref _selfActionBarSlots) is not null;

    /// <summary>
    /// アクションバーの控えの版。差し替えるたびに別の参照になる。未受信は <c>null</c>。
    /// 行の組み直しの合図に使う。
    /// </summary>
    public static object? SelfActionBarToken => Volatile.Read(ref _selfActionBarSlots);

    /// <summary>
    /// 枠番号のスキルID。枠が無い場合も 0 を返すので、
    /// 呼び出し側は <see cref="HasSelfActionBarSlots"/> で受信済みかを先に見ること。
    /// </summary>
    public static int GetSelfActionBarSkillId(int slotId)
    {
        return Volatile.Read(ref _selfActionBarSlots) is { } slots
            && slots.TryGetValue(slotId, out var skillId)
            ? skillId
            : 0;
    }

    /// <summary>
    /// 習得済みのプール(職業・職務・イマジン)からレベルと改造値を引く。
    ///
    /// <para>
    /// アクションバーは「どの枠に何が入っているか」しか持たないので、
    /// レベルはここから補う。<b>装備の選択には関与しない。</b>
    /// </para>
    /// </summary>
    public static bool TryGetSelfLearnedSkill(int skillId, out int level, out int tier)
    {
        level = 0;
        tier = 0;
        if (skillId <= 0)
        {
            return false;
        }

        lock (StateLock)
        {
            foreach (var skill in _selfImagineSkills.Values)
            {
                if (skill.SkillId == skillId)
                {
                    level = skill.Level;
                    tier = skill.Tier;
                    return true;
                }
            }

            foreach (var duty in SelfDutyStates.Values)
            {
                foreach (var skill in duty.Skills.Values)
                {
                    if (skill.SkillId == skillId)
                    {
                        level = skill.Level;
                        tier = skill.Tier;
                        return true;
                    }
                }
            }

            foreach (var profession in SelfProfessionStates.Values)
            {
                foreach (var skill in profession.Skills.Values)
                {
                    if (skill.SkillId == skillId)
                    {
                        level = skill.Level;
                        tier = skill.Tier;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static bool TryGetSelfCurrentProfessionId(out int professionId)
    {
        lock (StateLock)
        {
            professionId = _selfCurrentProfessionId;
            return professionId > 0;
        }
    }

    public static bool TryGetSelfCurrentSkillLevels(
        out IReadOnlyList<DataTypes.Skills.SkillLevelInfo> skillLevels)
    {
        lock (StateLock)
        {
            skillLevels = _selfCurrentSkillLevels;
            return _selfCurrentProfessionId > 0
                && SelfProfessionStates.ContainsKey(_selfCurrentProfessionId);
        }
    }

    public static bool TryGetSelfSkillLevel(int skillId, out int level)
    {
        return Volatile.Read(ref _selfSkillLevels).TryGetValue(skillId, out level);
    }

    public static bool TryGetSelfRoleSkillLevels(
        out IReadOnlyList<PlayerRoleSkillLevelState> roleSkillLevels)
    {
        lock (StateLock)
        {
            roleSkillLevels = _selfRoleSkillLevels;
            return roleSkillLevels.Count > 0;
        }
    }

    private static ProfessionState CreateProfessionState(Zproto.ProfessionInfo professionInfo)
    {
        var state = new ProfessionState();
        foreach (var pair in professionInfo.SkillInfoMap)
        {
            var skillId = ResolveSkillId(pair.Key, pair.Value.SkillId);
            state.Skills[pair.Key] = new SkillState(
                skillId,
                pair.Value.Level,
                pair.Value.RemodelLevel);
        }

        return state;
    }

    private static void ApplyProfessionInfoChanges(
        ProfessionState state,
        BinaryProfessionInfo changes)
    {
        if (changes.SkillInfoChanges is { } skillChanges)
        {
            ApplySkillMapChanges(state.Skills, skillChanges);
        }

    }

    private static DutyState CreateDutyState(Zproto.DutyInfo dutyInfo)
    {
        var state = new DutyState();
        foreach (var pair in dutyInfo.DutySkillInfoMap)
        {
            state.Skills[pair.Key] = new SkillState(
                ResolveSkillId(pair.Key, pair.Value.SkillId),
                pair.Value.Level,
                pair.Value.RemodelLevel);
        }

        foreach (var pair in dutyInfo.DutySkillSlotInfoMap)
        {
            state.Slots[pair.Key] = pair.Value;
        }

        return state;
    }

    private static void ApplyDutyInfoChanges(
        DutyState dutyState,
        BinaryDutyInfo dutyInfo)
    {
        if (dutyInfo.DutySkillInfoChanges is { } skillChanges)
        {
            ApplySkillMapChanges(dutyState.Skills, skillChanges);
        }

        if (dutyInfo.DutySkillSlotInfoChanges is { } slotChanges)
        {
            ApplyMapChanges(dutyState.Slots, slotChanges);
        }
    }

    private static void ApplySkillMapChanges(
        IDictionary<int, SkillState> skills,
        BinaryProtocol.BlobHashMapDelta<int, BinaryProfessionSkillInfo> changes)
    {
        if (changes.ReplacesExisting)
        {
            skills.Clear();
        }

        foreach (var skillKey in changes.Removed)
        {
            skills.Remove(skillKey);
        }

        foreach (var pair in changes.Added)
        {
            ApplySkillChange(skills, pair.Key, pair.Value, false);
        }

        foreach (var pair in changes.Updated)
        {
            ApplySkillChange(skills, pair.Key, pair.Value, true);
        }
    }

    private static void ApplyMapChanges<TKey, TValue>(
        IDictionary<TKey, TValue> values,
        BinaryProtocol.BlobHashMapDelta<TKey, TValue> changes)
        where TKey : notnull
    {
        if (changes.ReplacesExisting)
        {
            values.Clear();
        }

        foreach (var key in changes.Removed)
        {
            values.Remove(key);
        }

        foreach (var pair in changes.Added)
        {
            values[pair.Key] = pair.Value;
        }

        foreach (var pair in changes.Updated)
        {
            values[pair.Key] = pair.Value;
        }
    }

    private static void ApplySkillChange(
        IDictionary<int, SkillState> skills,
        int skillKey,
        BinaryProfessionSkillInfo change,
        bool preserveExisting)
    {
        var existing = preserveExisting && skills.TryGetValue(skillKey, out var current)
            ? current
            : default;
        var skillId = change.SkillId
            ?? (existing.SkillId > 0 ? existing.SkillId : skillKey);
        skills[skillKey] = new SkillState(
            skillId,
            change.Level ?? existing.Level,
            change.RemodelLevel ?? existing.Tier);
    }

    private static void PublishState()
    {
        var roleSkills = BuildCurrentRoleSkillLevels();
        var currentSkills = BuildCurrentSkillLevels(roleSkills);
        var levels = new Dictionary<int, int>();
        foreach (var skill in currentSkills)
        {
            if (skill.SkillId > 0 && skill.CurrentLevel > 0)
            {
                levels[skill.SkillId] = skill.CurrentLevel;
            }
        }

        Volatile.Write(ref _selfRoleSkillLevels, roleSkills);
        Volatile.Write(ref _selfCurrentSkillLevels, currentSkills);
        Volatile.Write(ref _selfSkillLevels, levels.ToFrozenDictionary());
    }

    private static DataTypes.Skills.SkillLevelInfo[] BuildCurrentSkillLevels(
        IReadOnlyList<PlayerRoleSkillLevelState> roleSkills)
    {
        var result = new List<DataTypes.Skills.SkillLevelInfo>();
        var includedSkillIds = new HashSet<int>();

        if (_selfCurrentProfessionId > 0
            && SelfProfessionStates.TryGetValue(_selfCurrentProfessionId, out var professionState))
        {
            foreach (var pair in professionState.Skills.OrderBy(pair => pair.Key))
            {
                AddSkill(result, includedSkillIds, pair.Value);
            }
        }

        foreach (var roleSkill in roleSkills)
        {
            AddSkill(result, includedSkillIds, new SkillState(
                roleSkill.SkillId,
                roleSkill.CurrentLevel,
                roleSkill.Tier));
        }

        foreach (var imagineSkill in EnumerateSelectedImagineSkills())
        {
            AddSkill(result, includedSkillIds, imagineSkill);
        }

        return [.. result];
    }

    private static void AddSkill(
        ICollection<DataTypes.Skills.SkillLevelInfo> result,
        ISet<int> includedSkillIds,
        SkillState skill)
    {
        if (skill.SkillId <= 0 || !includedSkillIds.Add(skill.SkillId))
        {
            return;
        }

        result.Add(new DataTypes.Skills.SkillLevelInfo
        {
            SkillId = skill.SkillId,
            CurrentLevel = skill.Level,
            Tier = skill.Tier
        });
    }

    private static IEnumerable<SkillState> EnumerateSelectedImagineSkills()
    {
        foreach (var skillId in _selfRawSelectedImagineSkills.Keys.Distinct())
        {
            if (_selfImagineSkills.TryGetValue(skillId, out var imagineSkill))
            {
                yield return imagineSkill;
            }
            else if (_selfRawSelectedImagineSkills.TryGetValue(skillId, out imagineSkill))
            {
                yield return imagineSkill;
            }
            else
            {
                yield return new SkillState(skillId, 0, 0);
            }
        }
    }

    private static PlayerRoleSkillLevelState[] BuildCurrentRoleSkillLevels()
    {
        if (!_hasSelfDutyState)
        {
            return [];
        }

        DutyState? dutyState = null;
        var professionDutyId = ResolveDutyIdForProfession(_selfCurrentProfessionId);
        if (professionDutyId != 0)
        {
            SelfDutyStates.TryGetValue(professionDutyId, out dutyState);
        }

        if (dutyState is null && _selfCurrentDutyId != 0)
        {
            SelfDutyStates.TryGetValue(_selfCurrentDutyId, out dutyState);
        }

        if (dutyState is null && SelfDutyStates.Count == 1)
        {
            dutyState = SelfDutyStates.Values.First();
        }

        if (dutyState is null)
        {
            return [];
        }

        return EnumerateSelectedDutySkills(dutyState)
            .Where(skill => skill.SkillId > 0)
            .DistinctBy(skill => skill.SkillId)
            .Select(skill => new PlayerRoleSkillLevelState(
                skill.SkillId,
                skill.Level,
                skill.Tier))
            .ToArray();
    }

    private static IEnumerable<SkillState> EnumerateSelectedDutySkills(DutyState dutyState)
    {
        // **スロット割り当てが空 = 1つも装備していない。「未受信」ではない。**
        // フルコンテナは全職務のスロットを同時に運び、装備0の職務だけが空で届く。
        // 習得済みの一覧で埋めないこと。
        if (dutyState.Slots.Count == 0)
        {
            return [];
        }

        if (dutyState.Skills.Count <= 4)
        {
            return dutyState.Skills
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value);
        }

        var skillKeyById = dutyState.Skills
            .Where(pair => pair.Value.SkillId > 0)
            .GroupBy(pair => pair.Value.SkillId)
            .ToDictionary(group => group.Key, group => group.First().Key);
        var directMatches = dutyState.Slots.Count(pair =>
            dutyState.Skills.ContainsKey(pair.Key)
            || skillKeyById.ContainsKey(pair.Key));
        var valueMatches = dutyState.Slots.Count(pair =>
            dutyState.Skills.ContainsKey(pair.Value)
            || skillKeyById.ContainsKey(pair.Value));
        var keysLookLikeSlots = dutyState.Slots.Keys.All(IsDutySlotId);
        var valuesLookLikeSlots = dutyState.Slots.Values.All(IsDutySlotId);

        IEnumerable<int> selectedKeys;
        if ((valuesLookLikeSlots && !keysLookLikeSlots)
            || (directMatches > 0
                && directMatches >= valueMatches
                && !keysLookLikeSlots))
        {
            selectedKeys = dutyState.Slots
                .Select(pair => new
                {
                    Slot = pair.Value,
                    SkillKey = dutyState.Skills.ContainsKey(pair.Key)
                        ? pair.Key
                        : skillKeyById.GetValueOrDefault(pair.Key)
                })
                .Where(pair => pair.SkillKey != 0)
                .OrderBy(pair => pair.Slot)
                .ThenBy(pair => pair.SkillKey)
                .Select(pair => pair.SkillKey);
        }
        else if ((keysLookLikeSlots && !valuesLookLikeSlots)
            || valueMatches > 0)
        {
            selectedKeys = dutyState.Slots
                .Select(pair => new
                {
                    Slot = pair.Key,
                    SkillKey = dutyState.Skills.ContainsKey(pair.Value)
                        ? pair.Value
                        : skillKeyById.GetValueOrDefault(pair.Value)
                })
                .Where(pair => pair.SkillKey != 0)
                .OrderBy(pair => pair.Slot)
                .Select(pair => pair.SkillKey);
        }
        else
        {
            return dutyState.Skills
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value);
        }

        return selectedKeys
            .Distinct()
            .Select(skillKey => dutyState.Skills[skillKey]);
    }

    private static int ResolveDutyIdForProfession(int professionId)
    {
        return DataTypes.Professions.GetRoleFromBaseProfessionId(professionId) switch
        {
            Professions.ERoleType.DPS => 1,
            Professions.ERoleType.Healer => 2,
            Professions.ERoleType.Tank => 3,
            _ => 0
        };
    }

    private static bool IsDutySlotId(int value)
    {
        return value is >= 0 and <= 4 or >= 21 and <= 24;
    }

    private static int ResolveSkillId(int skillKey, int skillId)
    {
        return skillId > 0 ? skillId : skillKey;
    }

    private sealed class ProfessionState
    {
        public Dictionary<int, SkillState> Skills { get; } = [];
    }

    private sealed class DutyState
    {
        public Dictionary<int, SkillState> Skills { get; } = [];
        public Dictionary<int, int> Slots { get; } = [];
    }

    private readonly record struct SkillState(
        int SkillId,
        int Level,
        int Tier);
}
