// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerImagineRoleSkillEntry : ObservableObject
{
    public PlayerImagineRoleSkillEntry(bool isImagine = false)
    {
        IsImagine = isImagine;
    }

    public bool IsImagine { get; }

    private bool _usesImagineAsset;

    /// <summary>アイコンがイマジンの絵か(<c>CombatDataCatalog.IsImagineArtIcon</c>)。アイコンの大きさと位置の調整を変える。</summary>
    public bool UsesImagineAsset
    {
        get => _usesImagineAsset;
        private set => SetProperty(ref _usesImagineAsset, value);
    }

    private PlayerBuffEntry? _buffEntry;

    public PlayerBuffEntry? BuffEntry
    {
        get => _buffEntry;
        private set => SetProperty(ref _buffEntry, value);
    }

    private PlayerBuffEntry? _debuffEntry;

    public PlayerBuffEntry? DebuffEntry
    {
        get => _debuffEntry;
        private set => SetProperty(ref _debuffEntry, value);
    }

    [ObservableProperty]
    private int _skillId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsUnknownIcon))]
    private bool _hasSkill;

    /// <summary>
    /// 枠の中身が分かっているか。何も装備していない枠(自分のアクションバーの空枠、一覧が届いた他人の足りない枠)は、分かっている(空)に入る。
    /// 一覧が届くまでは分からない。
    /// </summary>
    [ObservableProperty]
    private bool _isSlotKnown;

    /// <summary>行が映している人の枠の数に入っているか。入っていない枠は場所も空けない(人が替わるたびに枠の部品を作り直さないよう、外さずに隠す)。</summary>
    [ObservableProperty]
    private bool _isSlotInUse = true;

    [ObservableProperty]
    private string _nameDisplayText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsUnknownIcon))]
    private string? _iconPath;

    /// <summary>技があるのに絵のファイルが無い。絵の代わりにクラス不明のアイコン(はてな)を出す。</summary>
    public bool ShowsUnknownIcon => HasSkill && IconPath is null;

    [ObservableProperty]
    private string _chargeCountText = string.Empty;

    [ObservableProperty]
    private bool _hasChargeCount;

    [ObservableProperty]
    private string _cooldownText = string.Empty;

    [ObservableProperty]
    private bool _hasCooldown;

    public void Update(
        PlayerCooldownSkillSnapshot? snapshot,
        SkillCooldownDisplayState cooldownState)
    {
        UpdateSkill(snapshot);
        UpdateCooldown(snapshot, cooldownState);
    }

    public void UpdateSkill(PlayerCooldownSkillSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            SkillId = 0;
            HasSkill = false;
            UsesImagineAsset = false;
            NameDisplayText = string.Empty;
            IconPath = null;
            HasChargeCount = false;
            UpdateEffects(null, null);
            return;
        }

        if (SkillId != snapshot.SkillId)
        {
            UpdateEffects(null, null);
        }

        SkillId = snapshot.SkillId;
        HasSkill = true;
        UsesImagineAsset = CombatDataCatalog.IsImagineArtIcon(snapshot.IconName);
        NameDisplayText = MeterSnapshotProvider.FormatSkillNameWithGrade(snapshot.Name, snapshot.Grade);
        IconPath = CombatIconResolver.ResolveSkillIcon(snapshot.IconName);
        HasChargeCount = snapshot.MaxCharges > 1;
    }

    public void UpdateCooldown(
        PlayerCooldownSkillSnapshot? snapshot,
        SkillCooldownDisplayState cooldownState)
    {
        if (snapshot is null)
        {
            ChargeCountText = string.Empty;
            CooldownText = string.Empty;
            HasCooldown = false;
            return;
        }

        ChargeCountText = HasChargeCount
            ? Math.Clamp(
                    cooldownState.AvailableCharges ?? snapshot.MaxCharges,
                    0,
                    snapshot.MaxCharges)
                .ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        var remainingSeconds = cooldownState.CooldownRemainingSeconds;
        if (remainingSeconds is not > 0)
        {
            CooldownText = string.Empty;
            HasCooldown = false;
            return;
        }

        CooldownText = remainingSeconds.Value
            .ToString("0.0", CultureInfo.InvariantCulture);
        HasCooldown = true;
    }

    public void UpdateEffects(
        PlayerBuffSnapshot? buff,
        PlayerBuffSnapshot? debuff)
    {
        BuffEntry = UpdateEffect(BuffEntry, buff);
        DebuffEntry = UpdateEffect(DebuffEntry, debuff);
    }

    private static PlayerBuffEntry? UpdateEffect(
        PlayerBuffEntry? entry,
        PlayerBuffSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        if (entry is null
            || !string.Equals(entry.Key, snapshot.Key, StringComparison.Ordinal))
        {
            return new PlayerBuffEntry(snapshot);
        }

        entry.Update(snapshot);
        return entry;
    }
}
