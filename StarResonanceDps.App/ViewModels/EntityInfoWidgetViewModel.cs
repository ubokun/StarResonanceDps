// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// エンティティ情報。エンティティリストの実体1つの、1行目(エンティティリストの1行と同じ作りで、得点は出さない)・ヘイトの行・バフとデバフの枠。
///
/// <para>
/// 1行目とヘイトは、周りの実体の一覧(<see cref="UpdateNearbyEntities"/>)と名簿(<see cref="UpdateRoster"/>)から、窓ごとに自分の行(<see cref="Row"/>)を
/// このウィジェットの設定で組む(名前の書式・ヘイトの人数などがエンティティリストと別なので、エンティティリストの行は使わない)。
/// 窓が追う個体は <see cref="EntityWindowTarget"/> が決める(エンティティのバフ一覧と同じ)。
/// 個体が周りの実体の一覧に居ない(倒された・周りから外れた)間は、エンティティリストと同じく何も出さない(バフ・デバフも)。
/// 同じ個体が一覧に戻れば、また出す。ログアウトで個体を放したら中身を空にする。
/// 何も出さない間は中身の高さが 0 になり、窓は高さ自動の決まりどおり今の高さで待つ。
/// </para>
///
/// <para>
/// バフ・デバフはエンティティのバフ一覧と同じく1秒ごとに読む(名前の書式・優先表示リスト・非表示リスト・同種バフの表示はこのウィジェットの設定)。枠は値だけ入れ替える。
/// 詠唱の文字とゲージの 0.1 秒ごとの当て直しは画面が受け持つ(エンティティリストと同じ)。
/// </para>
/// </summary>
public sealed partial class EntityInfoWidgetViewModel : ViewModelBase, IEntityWidgetWindowViewModel
{
    private readonly WidgetListItemViewModel _widget;
    private readonly EntityWindowTarget _target;

    /// <summary>枠からバフリスト・デバフリストを開く経路。</summary>
    private readonly Action<WidgetKind, EntityTargetInfo> _openList;

    /// <summary>枠からバフ・デバフカードを開く経路。</summary>
    private readonly Action<EntityWindowTarget, PlayerBuffListKind, string, int> _openCard;

    private readonly EntityHatePlayerResolver _hatePlayers = new();
    private readonly ObservableCollection<PlayerBuffEntry> _buffs = [];
    private readonly ObservableCollection<PlayerBuffEntry> _debuffs = [];
    private readonly List<PlayerBuffEntry> _orderedEntries = [];
    private readonly DispatcherTimer _refreshTimer;
    private EntityInfoWidgetSettingsConfig _settings;
    private EntityRowSettings _rowSettings;

    /// <summary>
    /// 窓が追う個体の、最後に受けた周りの実体の値。名簿や設定が変わったときに行を組み直すのに使う。
    /// 個体が周りの実体の一覧に居ない間は null(1行目・ヘイト・バフ・デバフを出さない)。
    /// </summary>
    private NearbyEntityEntry? _lastEntity;

    private bool _isDisposed;

    /// <summary>1行目とヘイトの行。窓が追う個体の値をまだ受けていなければ null(何も出さない)。</summary>
    [ObservableProperty]
    private EntityListEntry? _row;

    [ObservableProperty]
    private string _headerText = string.Empty;

    /// <summary>右クリックのメニューを開いた枠。メニューの文言(バフかデバフか、優先表示の項目は今の状態も)はこの枠で決める。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListMenuText))]
    [NotifyPropertyChangedFor(nameof(CardMenuText))]
    [NotifyPropertyChangedFor(nameof(PriorityMenuText))]
    [NotifyPropertyChangedFor(nameof(HideMenuText))]
    private PlayerBuffEntry? _menuEntry;

    public EntityInfoWidgetViewModel(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        IReadOnlyList<PlayerRosterEntry> roster,
        IReadOnlyList<NearbyEntityEntry> nearbyEntities,
        Action<WidgetKind, EntityTargetInfo> openList,
        Action<EntityWindowTarget, PlayerBuffListKind, string, int> openCard)
    {
        _widget = widget;
        _target = target;
        _openList = openList;
        _openCard = openCard;
        _settings = widget.GetEntityInfoSettingsSnapshot();
        _rowSettings = EntityRowSettings.FromEntityInfo(_settings);
        _hatePlayers.SetRoster(roster);
        Buffs = new ReadOnlyObservableCollection<PlayerBuffEntry>(_buffs);
        Debuffs = new ReadOnlyObservableCollection<PlayerBuffEntry>(_debuffs);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _target.Changed += Target_Changed;
        _widget.EntityInfoSettingsChanged += Widget_EntityInfoSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        RefreshPresentation();
        UpdateNearbyEntities(nearbyEntities);
        RefreshBuffs();
        RefreshDataStatus();
        _refreshTimer.Start();
    }

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

    public long EntityId => _target.EntityId;

    public Zproto.EEntityType? EntityType => _target.EntityType;

    public long EntityUuid => _target.EntityUuid;

    public bool IsEntityAcquired => _target.IsAcquired;

    public bool IsWaitingForEntity => _target.IsWaitingForEntity;

    public EntityTargetInfo? Entity => _target.Entity;

    public string TargetName => _target.Name;

    /// <summary>メニューと TIPS の色を引くウィジェット。</summary>
    public WidgetListItemViewModel Widget => _widget;

    public ReadOnlyObservableCollection<PlayerBuffEntry> Buffs { get; }

    public ReadOnlyObservableCollection<PlayerBuffEntry> Debuffs { get; }

    /// <summary>メニューの1つ目の項目。バフの枠なら「バフリスト」、デバフの枠なら「デバフリスト」。</summary>
    public string ListMenuText => LocalizationManager.Instance.GetString(
        MenuEntry?.IsDebuff == true
            ? "Widget_DebuffList"
            : "Widget_BuffList");

    /// <summary>メニューの2つ目の項目。バフの枠なら「バフカード」、デバフの枠なら「デバフカード」。</summary>
    public string CardMenuText => LocalizationManager.Instance.GetString(
        MenuEntry?.IsDebuff == true
            ? "BuffList_Menu_DebuffCard"
            : "BuffList_Menu_BuffCard");

    /// <summary>
    /// メニューの3つ目の項目「このバフを優先表示」「このバフの優先表示をやめる」(デバフの枠は「このデバフを」「このデバフの」)。
    /// 開いた枠の今の優先表示の状態で決まる。
    /// </summary>
    public string PriorityMenuText => BuffDebuffCardContent.GetPriorityMenuText(
        MenuEntry?.IsDebuff == true,
        MenuEntry is { } entry && _widget.IsEntityInfoPriorityBuff(entry.BaseId));

    /// <summary>メニューを開く直前に、優先表示の項目の文言を今の状態にする(同じ枠を開き直すと <see cref="MenuEntry"/> が変わらないため)。</summary>
    public void RefreshMenuText()
    {
        OnPropertyChanged(nameof(PriorityMenuText));
    }

    /// <summary>メニューの4つ目の項目。バフの枠なら「このバフを非表示」、デバフの枠なら「このデバフを非表示」。</summary>
    public string HideMenuText => LocalizationManager.Instance.GetString(
        MenuEntry?.IsDebuff == true
            ? "BuffList_Menu_HideDebuff"
            : "BuffList_Menu_HideBuff");

    /// <summary>出現時の一覧の受信と周りから外れたことは対象の変化を伴わないので、1秒ごとの読み直しでも見る(エンティティのバフ一覧と同じ)。</summary>
    public void RefreshDataStatus()
    {
        var status = _target.CreateDataStatus(GameReceptionStateStore.Current);
        if (status == DataStatus)
        {
            return;
        }

        DataStatus = status;
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool RepresentsEntity(long entityUuid)
    {
        return _target.Represents(entityUuid);
    }

    public bool TryApplyEntity(EntityTargetInfo entity)
    {
        return _target.TryApply(entity);
    }

    public void ReleaseEntity()
    {
        _target.Release();
    }

    public void RefreshPresentation()
    {
        HeaderText = WidgetHeaderText.Join(_widget.DisplayName, _target.Name);
    }

    /// <summary>
    /// 周りの実体の一覧が変わった。窓が追う個体の値で1行目とヘイトを組み直す。値が同じなら組み直さない。
    /// 個体が一覧に居ない(倒された・周りから外れた)間は、1行目・ヘイト・バフ・デバフを出さない(エンティティリストと同じ)。
    /// 同じ個体が一覧に戻れば、また出す。未捕獲の窓は何もしない。
    /// </summary>
    public void UpdateNearbyEntities(IReadOnlyList<NearbyEntityEntry> nearbyEntities)
    {
        if (_isDisposed || !_target.IsAcquired)
        {
            return;
        }

        var entityUuid = _target.EntityUuid;
        foreach (var entity in nearbyEntities)
        {
            if (entity.EntityUuid != entityUuid)
            {
                continue;
            }

            if (entity == _lastEntity)
            {
                return;
            }

            var returned = _lastEntity is null;
            _lastEntity = entity;
            ApplyRow();
            if (returned)
            {
                RefreshBuffs();
            }

            return;
        }

        if (_lastEntity is null)
        {
            return;
        }

        _lastEntity = null;
        Row = null;
        RefreshBuffs();
    }

    /// <summary>名簿が変わった(ヘイトの枠の名前・クラス・特化を引き直す)。</summary>
    public void UpdateRoster(IReadOnlyList<PlayerRosterEntry> roster)
    {
        if (_isDisposed)
        {
            return;
        }

        _hatePlayers.SetRoster(roster);
        ApplyRow();
    }

    /// <summary>窓を開いた・画面が出たとき。今の時刻で詠唱を当て直す(全体秒数が過ぎていれば外す)。</summary>
    public void ReapplyCast()
    {
        Row?.ReapplyCast(DateTime.UtcNow);
    }

    /// <summary>詠唱の文字とゲージを今の時刻で当て直す(画面の 0.1 秒ごとの更新)。</summary>
    public void RefreshCast()
    {
        Row?.RefreshCast(DateTime.UtcNow);
    }

    /// <summary>枠の左クリックとメニューの1つ目。バフの枠ならその個体のバフリスト、デバフの枠ならデバフリストを開く。</summary>
    [RelayCommand]
    private void OpenList(PlayerBuffEntry? entry)
    {
        if (entry is null || _target.Entity is not { } entity)
        {
            return;
        }

        _openList(entry.IsDebuff ? WidgetKind.DebuffList : WidgetKind.BuffList, entity);
    }

    [RelayCommand]
    private void OpenCard(PlayerBuffEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _openCard(_target, entry.IsDebuff ? PlayerBuffListKind.Debuff : PlayerBuffListKind.Buff, entry.CardKey, entry.BaseId);
    }

    [RelayCommand]
    private void TogglePriority(PlayerBuffEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _widget.SetEntityInfoPriorityBuffEnabledFromWidget(
            entry.BaseId,
            !_widget.IsEntityInfoPriorityBuff(entry.BaseId));
    }

    [RelayCommand]
    private void HideBuff(PlayerBuffEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _widget.HideBuffInEntityInfo(entry.BaseId);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _target.Changed -= Target_Changed;
        _widget.EntityInfoSettingsChanged -= Widget_EntityInfoSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>表示設定(保存かプレビュー)が変わった。1行目・ヘイト・枠を組み直す(優先表示と非表示のバフもここで効く)。</summary>
    private void Widget_EntityInfoSettingsChanged(object? sender, EventArgs e)
    {
        _settings = _widget.GetEntityInfoSettingsSnapshot();
        _rowSettings = EntityRowSettings.FromEntityInfo(_settings);
        ApplyRow();
        RefreshBuffs();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        RefreshBuffs();
        RefreshDataStatus();
    }

    /// <summary>個体を捕まえた・放した・名前が変わった。放したら中身を空にする(次に捕まえた個体の値が来るまで何も出さない)。</summary>
    private void Target_Changed(object? sender, EventArgs e)
    {
        if (!_target.IsAcquired)
        {
            _lastEntity = null;
            Row = null;
        }

        RefreshPresentation();
        RefreshBuffs();
        RefreshDataStatus();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ListMenuText));
        OnPropertyChanged(nameof(CardMenuText));
        OnPropertyChanged(nameof(PriorityMenuText));
        OnPropertyChanged(nameof(HideMenuText));
        RefreshPresentation();
        ApplyRow();
        RefreshBuffs();
    }

    /// <summary>最後に受けた値で1行目とヘイトを組む。同じ個体の行は作り直さず値だけ入れ替える。</summary>
    private void ApplyRow()
    {
        if (_isDisposed || _lastEntity is not { } entity)
        {
            return;
        }

        // プレイヤー名の見せ方はエンティティリストと同じく、組むたびに全体設定から読む。
        _hatePlayers.Prepare(
            _rowSettings,
            (PlayerNameDisplayMode)ConfigManager.Instance.CurrentSettings.PlayerNameDisplayModeIndex);

        if (Row is { } row && row.EntityUuid == entity.EntityUuid)
        {
            row.Update(entity, _rowSettings, _hatePlayers, showsScore: false);
            return;
        }

        Row = EntityListEntry.Create(entity, _rowSettings, _hatePlayers, showsScore: false, fixedHateSlotCount: null);
    }

    private void RefreshBuffs()
    {
        if (_isDisposed)
        {
            return;
        }

        var groupsSameBuffs = _settings.SameBuffGroupingIndex == WidgetConfigDefaults.EntityInfoSameBuffGroupedIndex;
        SynchronizeEntries(_buffs, PlayerBuffListKind.Buff, groupsSameBuffs);
        SynchronizeEntries(_debuffs, PlayerBuffListKind.Debuff, groupsSameBuffs);
    }

    /// <summary>枠を当て直す(エンティティのバフ一覧の行と同じ扱い。消える枠はメニューも閉じる)。</summary>
    private void SynchronizeEntries(
        ObservableCollection<PlayerBuffEntry> entries,
        PlayerBuffListKind kind,
        bool groupsSameBuffs)
    {
        // 個体が周りの一覧に居ない間は出さない(倒された後もバフの控えが残ることがある)。
        IReadOnlyList<PlayerBuffSnapshot> snapshots = _lastEntity is null
            ? []
            : BuffListSnapshotFilter.Apply(
                _settings,
                MeterSnapshotProvider.GetEntityBuffs(EntityUuid, kind, groupsSameBuffs));
        var snapshotKeys = snapshots
            .Select(snapshot => snapshot.Key)
            .ToHashSet(StringComparer.Ordinal);

        for (var index = entries.Count - 1; index >= 0; index--)
        {
            if (snapshotKeys.Contains(entries[index].Key))
            {
                continue;
            }

            entries[index].IsMenuOpen = false;
            entries.RemoveAt(index);
        }

        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            // 対象者名とレベルはエンティティのバフ一覧と同じく窓の対象のもの。
            var name = BuffListRowName.Format(
                _settings.BuffInfoFormatString,
                _settings.CasterNameDisplayModeIndex,
                snapshot,
                _target.Name,
                _target.Level);
            var existingIndex = FindEntryIndex(entries, snapshot.Key);
            if (existingIndex < 0)
            {
                _orderedEntries.Add(new PlayerBuffEntry(snapshot, name)
                {
                    IsDebuff = kind == PlayerBuffListKind.Debuff
                });
                continue;
            }

            var entry = entries[existingIndex];
            entry.Update(snapshot, name);
            _orderedEntries.Add(entry);
        }

        CollectionOrder.Apply(entries, _orderedEntries);
        _orderedEntries.Clear();
    }

    private static int FindEntryIndex(ObservableCollection<PlayerBuffEntry> entries, string key)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Key, key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
