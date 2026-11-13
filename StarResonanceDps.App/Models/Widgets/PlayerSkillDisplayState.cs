// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// プレイヤーリストの1人ぶんの技の枠の値。行は位置に結びつき別の人に替わるので、Core から読むのは人ごとに定期の更新だけにして、
/// 人が替わった行はここの値を写す。持ち主は <c>WidgetListItemViewModel</c> で、一覧から外れた人はその同期で、全員はマップ移動で手放す。
/// </summary>
public sealed class PlayerSkillDisplayState(long characterId)
{
    private static readonly IReadOnlyDictionary<int, PlayerSkillEffectSnapshot> NoEffects =
        new Dictionary<int, PlayerSkillEffectSnapshot>();

    private readonly HashSet<int> _trackedSkillIds = [];
    private IReadOnlyList<PlayerCooldownSkillSnapshot?> _imagineSkills = Array.Empty<PlayerCooldownSkillSnapshot?>();
    private IReadOnlyList<PlayerCooldownSkillSnapshot?> _roleSkills = Array.Empty<PlayerCooldownSkillSnapshot?>();
    private IReadOnlyList<PlayerCooldownSkillSnapshot?> _filteredRoleSkills = Array.Empty<PlayerCooldownSkillSnapshot?>();
    private SkillCooldownDisplayState[] _imagineCooldowns = [];
    private SkillCooldownDisplayState[] _roleCooldowns = [];

    /// <summary>
    /// 他人のロールスキル表示設定を当てた時の設定。設定が変わると別インスタンスが渡ってくるので、
    /// 参照の一致で「設定が変わったか」を判定する。<b>自分には適用しない。</b>
    /// </summary>
    private IReadOnlyDictionary<string, bool>? _appliedRoleSkillVisibility;

    private long _entityUuid;
    private int _professionId;
    private object? _skillSourceToken;
    private object? _roleFilterToken;
    private bool _isLoadoutInitialized;

    /// <summary>技の一覧・CD・バフを一通り読んだか。読んでいない人は、行に写す前に読む。</summary>
    public bool HasRead { get; private set; }

    public IReadOnlyList<PlayerCooldownSkillSnapshot?> ImagineSkills => _imagineSkills;

    /// <summary>表示設定を当てたロールスキル。</summary>
    public IReadOnlyList<PlayerCooldownSkillSnapshot?> RoleSkills => _filteredRoleSkills;

    /// <summary><see cref="ImagineSkills"/> と同じ並びの CD。</summary>
    public IReadOnlyList<SkillCooldownDisplayState> ImagineCooldowns => _imagineCooldowns;

    /// <summary><see cref="RoleSkills"/> と同じ並びの CD。</summary>
    public IReadOnlyList<SkillCooldownDisplayState> RoleCooldowns => _roleCooldowns;

    public IReadOnlyDictionary<int, PlayerSkillEffectSnapshot> Effects { get; private set; } = NoEffects;

    /// <summary>蘇生不可デバフの残り秒。取れていなければ <c>null</c>。</summary>
    public double? ReviveBlockSeconds { get; private set; }

    /// <summary>
    /// 技の一覧を読み直させる。技の枠の名前は一覧を読んだときに焼き込むので、
    /// 言語・内部IDの表示・名前の無い技の内部名の設定が変わったら呼ぶ(一覧の元のデータは変わらないので、そのままでは読み直さない)。
    /// </summary>
    public void InvalidateLoadout()
    {
        _isLoadoutInitialized = false;
    }

    /// <summary>技の一覧と表示設定を今の値にする。枠に出す中身が変わったら true。</summary>
    public bool RefreshSkills(bool isSelf, IReadOnlyDictionary<string, bool>? roleSkillVisibility)
    {
        var loadoutChanged = RefreshLoadoutIfChanged();
        if (!loadoutChanged && ReferenceEquals(_appliedRoleSkillVisibility, roleSkillVisibility))
        {
            return false;
        }

        ApplyRoleSkillVisibility(isSelf, roleSkillVisibility);
        return true;
    }

    /// <summary>Core を読まずに表示設定だけ今の値にする(人が替わった行が、読み済みの人を写すとき)。</summary>
    public void ApplyRoleSkillVisibilityIfChanged(bool isSelf, IReadOnlyDictionary<string, bool>? roleSkillVisibility)
    {
        if (ReferenceEquals(_appliedRoleSkillVisibility, roleSkillVisibility))
        {
            return;
        }

        ApplyRoleSkillVisibility(isSelf, roleSkillVisibility);
        RefreshCooldowns();
    }

    public void RefreshCooldowns()
    {
        ReadCooldowns(_imagineSkills, ref _imagineCooldowns);
        ReadCooldowns(_filteredRoleSkills, ref _roleCooldowns);
    }

    public void RefreshEffects()
    {
        Effects = MeterSnapshotProvider.GetPlayerListSkillEffects(
            characterId,
            _entityUuid,
            _trackedSkillIds);

        // 蘇生不可はスキル枠のバッジには出さず、HPテキストのほうへ回す。
        ReviveBlockSeconds = MeterSnapshotProvider.TryGetReviveBlockSeconds(
            characterId,
            _entityUuid,
            out var reviveBlockSeconds)
            ? reviveBlockSeconds
            : null;
        HasRead = true;
    }

    private void ReadCooldowns(
        IReadOnlyList<PlayerCooldownSkillSnapshot?> skills,
        ref SkillCooldownDisplayState[] cooldowns)
    {
        if (cooldowns.Length != skills.Count)
        {
            cooldowns = new SkillCooldownDisplayState[skills.Count];
        }

        for (var index = 0; index < skills.Count; index++)
        {
            var skill = skills[index];
            cooldowns[index] = skill is null || _entityUuid == 0
                ? default
                : SkillCooldownTracker.Instance.GetDisplayState(
                    _entityUuid,
                    skill.SkillId,
                    skill.CooldownSeconds,
                    skill.IsImagine,
                    skill.MaxCharges,
                    skill.ChargeCooldownSeconds);
        }
    }

    /// <summary>
    /// 他人のロールスキル枠に、表示オンのものだけを残す。
    ///
    /// <para>
    /// <b>自分は対象外。</b> 自分はアクションバーの枠をそのまま出すので、
    /// ここで間引くと枠の位置がゲームとずれる。
    /// </para>
    /// </summary>
    private void ApplyRoleSkillVisibility(bool isSelf, IReadOnlyDictionary<string, bool>? roleSkillVisibility)
    {
        _appliedRoleSkillVisibility = roleSkillVisibility;

        if (isSelf || roleSkillVisibility is null)
        {
            _filteredRoleSkills = _roleSkills;
            return;
        }

        var visible = new List<PlayerCooldownSkillSnapshot?>(_roleSkills.Count);
        foreach (var skill in _roleSkills)
        {
            if (skill is null)
            {
                continue;
            }

            var key = skill.SkillId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // 設定に無いスキルは既定どおり表示する。設定は20件ぶんしか持たない。
            if (!roleSkillVisibility.TryGetValue(key, out var isVisible) || isVisible)
            {
                visible.Add(skill);
            }
        }

        _filteredRoleSkills = visible;
    }

    private bool RefreshLoadoutIfChanged()
    {
        if (!MeterSnapshotProvider.TryGetPlayerListImagineRoleSkillSourceState(
                characterId,
                _entityUuid,
                out var entityUuid,
                out var professionId,
                out var skillSourceToken,
                out var roleFilterToken))
        {
            return ResetLoadout();
        }

        if (_isLoadoutInitialized
            && entityUuid == _entityUuid
            && professionId == _professionId
            && ReferenceEquals(skillSourceToken, _skillSourceToken)
            && ReferenceEquals(roleFilterToken, _roleFilterToken))
        {
            return false;
        }

        var snapshot = MeterSnapshotProvider.GetPlayerListImagineRoleSkills(
            characterId,
            entityUuid);
        _entityUuid = snapshot.EntityUuid;
        _professionId = professionId;
        _skillSourceToken = skillSourceToken;
        _roleFilterToken = roleFilterToken;
        _imagineSkills = snapshot.ImagineSkills;
        _roleSkills = snapshot.RoleSkills;
        RebuildTrackedSkillIds();
        _isLoadoutInitialized = true;
        return true;
    }

    private bool ResetLoadout()
    {
        var changed = _isLoadoutInitialized
            || _imagineSkills.Count != 0
            || _roleSkills.Count != 0;
        _entityUuid = 0;
        _professionId = 0;
        _skillSourceToken = null;
        _roleFilterToken = null;
        _imagineSkills = Array.Empty<PlayerCooldownSkillSnapshot?>();
        _roleSkills = Array.Empty<PlayerCooldownSkillSnapshot?>();
        _trackedSkillIds.Clear();
        _isLoadoutInitialized = false;
        return changed;
    }

    private void RebuildTrackedSkillIds()
    {
        _trackedSkillIds.Clear();
        foreach (var skill in _imagineSkills)
        {
            if (skill is { SkillId: > 0 })
            {
                _trackedSkillIds.Add(skill.SkillId);
            }
        }

        foreach (var skill in _roleSkills)
        {
            if (skill is { SkillId: > 0 })
            {
                _trackedSkillIds.Add(skill.SkillId);
            }
        }
    }
}
