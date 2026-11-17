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
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// ターゲット情報(内部名はエンティティ情報。保存のキー <c>EntityInfo</c> を 4.0.0 から変えないため)。1枚だけ開く窓で、
/// 敵1体の、1行目(エンティティリストの1行と同じ作りで、得点は出さない)・ヘイトの行・バフとデバフの枠を出す。
///
/// <para>
/// 映す敵は、既定では自分のターゲット(自分が最後に技を向けた敵、<see cref="SelfTargetStore"/>。その敵が周りから消えるとターゲットも消える)。
/// エンティティリストの行・マップの敵の印から選んだ敵(<see cref="ShowEntity"/>)を映している間は、ヘッダーに「ターゲットに戻る」を出す
/// (<see cref="IsShowingChosenEntity"/>、<see cref="ReturnToTargetCommand"/>)。選んだ敵に自分が技を向けたら、ターゲットへ戻った扱いにする。
/// 選んだ敵が周りの実体の一覧から消えたら(倒された・周りから外れた)、選んだ敵を外してターゲットに戻る。
/// 選んだ敵は保存せず、ログアウトでも外す。
/// </para>
///
/// <para>
/// 1行目とヘイトは、周りの実体の一覧(<see cref="UpdateNearbyEntities"/>)と名簿(<see cref="UpdateRoster"/>)から、窓の行(<see cref="Row"/>)を
/// このウィジェットの設定で組む(名前の書式・ヘイトの人数などがエンティティリストと別なので、エンティティリストの行は使わない)。
/// 映す敵が無い間と、映す敵が周りの実体の一覧に居ない間は、エンティティリストと同じく何も出さない(バフ・デバフも)。
/// 何も出さない間は中身の高さが 0 になり、窓は高さ自動の決まりどおり今の高さで待つ。
/// </para>
///
/// <para>
/// 自分のターゲットの知らせはパケットのスレッドで来るので、画面のスレッドへ回して読み直す。
/// バフ・デバフはエンティティのバフ一覧と同じく1秒ごとに読む(名前の書式・優先表示リスト・非表示リスト・同種バフの表示はこのウィジェットの設定)。枠は値だけ入れ替える。
/// 詠唱の文字とゲージの 0.1 秒ごとの当て直しは画面が受け持つ(エンティティリストと同じ)。
/// </para>
/// </summary>
public sealed partial class EntityInfoWidgetViewModel : ViewModelBase, IDisposable, IWidgetDataStatusSource, IWidgetWindowHiddenAware
{
    private readonly WidgetListItemViewModel _widget;
    private readonly Dispatcher _dispatcher;

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

    /// <summary>最後に受けた周りの実体の一覧。映す敵が替わったときに、すぐその敵の値を引くのに使う。</summary>
    private IReadOnlyList<NearbyEntityEntry> _nearbyEntities;

    /// <summary>自分のターゲットの、最後に読んだ値(<see cref="SelfTargetStore.TargetUuid"/>)。無ければ 0。</summary>
    private long _targetUuid;

    /// <summary>エンティティリスト・マップから選んで映している敵の実体の UUID。自分のターゲットを映している間は 0。</summary>
    private long _chosenEntityUuid;

    /// <summary>
    /// 映す敵の、最後に受けた周りの実体の値。名簿や設定が変わったときに行を組み直すのに使う。
    /// 映す敵が無い間と、周りの実体の一覧に居ない間は null(1行目・ヘイト・バフ・デバフを出さない)。
    /// </summary>
    private NearbyEntityEntry? _lastEntity;

    private bool _isDisposed;

    /// <summary>1行目とヘイトの行。映す敵の値をまだ受けていなければ null(何も出さない)。</summary>
    [ObservableProperty]
    private EntityListEntry? _row;

    /// <summary>選んだ敵を映しているか。ヘッダーの「ターゲットに戻る」はこの間だけ出す。</summary>
    [ObservableProperty]
    private bool _isShowingChosenEntity;

    /// <summary>右クリックのメニューを開いた枠。メニューの文言(バフかデバフか、優先表示の項目は今の状態も)はこの枠で決める。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListMenuText))]
    [NotifyPropertyChangedFor(nameof(CardMenuText))]
    [NotifyPropertyChangedFor(nameof(PriorityMenuText))]
    [NotifyPropertyChangedFor(nameof(HideMenuText))]
    private PlayerBuffEntry? _menuEntry;

    public EntityInfoWidgetViewModel(
        WidgetListItemViewModel widget,
        IReadOnlyList<PlayerRosterEntry> roster,
        IReadOnlyList<NearbyEntityEntry> nearbyEntities,
        Action<WidgetKind, EntityTargetInfo> openList,
        Action<EntityWindowTarget, PlayerBuffListKind, string, int> openCard)
    {
        _widget = widget;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _openList = openList;
        _openCard = openCard;
        _settings = widget.GetEntityInfoSettingsSnapshot();
        _rowSettings = EntityRowSettings.FromEntityInfo(_settings);
        _hatePlayers.SetRoster(roster);
        _nearbyEntities = nearbyEntities;
        _targetUuid = SelfTargetStore.Instance.TargetUuid;
        Buffs = new ReadOnlyObservableCollection<PlayerBuffEntry>(_buffs);
        Debuffs = new ReadOnlyObservableCollection<PlayerBuffEntry>(_debuffs);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        SelfTargetStore.Instance.Changed += SelfTargetStore_Changed;
        _widget.EntityInfoSettingsChanged += Widget_EntityInfoSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        ApplyShownEntity();
        RefreshBuffs();
        _refreshTimer.Start();
    }

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

    /// <summary>映す敵の実体の UUID。選んだ敵があればその敵、無ければ自分のターゲット。どちらも無ければ 0。</summary>
    private long ShownEntityUuid => _chosenEntityUuid != 0 ? _chosenEntityUuid : _targetUuid;

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

    /// <summary>
    /// 状態の印。映す敵の決まりはエンティティのバフ一覧と同じ(<see cref="WidgetDataStatusRules.ForEntityBuffs"/>)。映す敵がまだ無い間は欠けている情報が無い。
    /// 出現時の一覧の受信と周りから外れたことは映す敵の変化を伴わないので、1秒ごとの読み直しでも見る。
    /// </summary>
    public void RefreshDataStatus()
    {
        var entityUuid = ShownEntityUuid;
        var status = entityUuid == 0
            ? WidgetDataStatus.Complete
            : WidgetDataStatusRules.ForEntityBuffs(
                GameReceptionStateStore.Current,
                isAcquired: true,
                MeterSnapshotProvider.HasReceivedEntityAppearBuffList(entityUuid),
                NearbyEntityStore.Instance.Contains(entityUuid),
                _lastEntity?.EntityId == 0);
        if (status == DataStatus)
        {
            return;
        }

        DataStatus = status;
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// エンティティリストの行・マップの敵の印から選んだ敵を映す。自分のターゲットと同じ敵なら、ターゲットを映している扱いにする。
    /// </summary>
    public void ShowEntity(EntityTargetInfo entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (_isDisposed)
        {
            return;
        }

        _chosenEntityUuid = entity.EntityUuid == _targetUuid ? 0 : entity.EntityUuid;
        ApplyShownEntity();
    }

    /// <summary>ログアウトで起動時の状態へ戻した。選んだ敵を外し、自分のターゲット(消えている)を読み直す。</summary>
    public void ResetToStartup()
    {
        if (_isDisposed)
        {
            return;
        }

        _chosenEntityUuid = 0;
        _targetUuid = SelfTargetStore.Instance.TargetUuid;
        ApplyShownEntity();
    }

    /// <summary>
    /// 周りの実体の一覧が変わった。映す敵の値で1行目とヘイトを組み直す。値が同じなら組み直さない。
    /// 映す敵が一覧に居ない間は、1行目・ヘイト・バフ・デバフを出さない(エンティティリストと同じ)。
    /// 選んだ敵が一覧から消えたら、選んだ敵を外して自分のターゲットに戻る。
    /// </summary>
    public void UpdateNearbyEntities(IReadOnlyList<NearbyEntityEntry> nearbyEntities)
    {
        if (_isDisposed)
        {
            return;
        }

        _nearbyEntities = nearbyEntities;
        ApplyNearbyEntity();
    }

    /// <summary>ヘッダーの「ターゲットに戻る」。選んだ敵を外し、自分のターゲットを映す。</summary>
    [RelayCommand]
    private void ReturnToTarget()
    {
        _chosenEntityUuid = 0;
        ApplyShownEntity();
    }

    /// <summary>映す敵が替わった(か、替わったかもしれない)。ボタンの出し入れと、映す敵の値・状態の印を当て直す。</summary>
    private void ApplyShownEntity()
    {
        IsShowingChosenEntity = _chosenEntityUuid != 0;
        ApplyNearbyEntity();
        RefreshDataStatus();
    }

    /// <summary>
    /// 最後に受けた周りの実体の一覧から、映す敵の値を引いて1行目とヘイトを組む。値が同じなら組み直さない。
    /// 選んだ敵が一覧に居なければ(倒された・周りから外れた)、選んだ敵を外して自分のターゲットに戻る。
    /// </summary>
    private void ApplyNearbyEntity()
    {
        var found = FindNearbyEntity(ShownEntityUuid);
        var hasReleasedChosenEntity = false;
        if (found is null && _chosenEntityUuid != 0)
        {
            _chosenEntityUuid = 0;
            IsShowingChosenEntity = false;
            hasReleasedChosenEntity = true;
            found = FindNearbyEntity(ShownEntityUuid);
        }

        ApplyFoundEntity(found);
        if (hasReleasedChosenEntity)
        {
            RefreshDataStatus();
        }
    }

    private NearbyEntityEntry? FindNearbyEntity(long entityUuid)
    {
        if (entityUuid == 0)
        {
            return null;
        }

        foreach (var entity in _nearbyEntities)
        {
            if (entity.EntityUuid == entityUuid)
            {
                return entity;
            }
        }

        return null;
    }

    /// <summary>映す敵の値(一覧に居なければ null)を当てる。映す敵が替わった・一覧に現れた・一覧から外れたときは、バフ・デバフも読み直す。</summary>
    private void ApplyFoundEntity(NearbyEntityEntry? found)
    {
        if (found is null)
        {
            if (_lastEntity is null)
            {
                return;
            }

            _lastEntity = null;
            Row = null;
            RefreshBuffs();
            return;
        }

        if (found == _lastEntity)
        {
            return;
        }

        var isOtherEntity = _lastEntity?.EntityUuid != found.EntityUuid;
        _lastEntity = found;
        ApplyRow();
        if (isOtherEntity)
        {
            RefreshBuffs();
        }
    }

    /// <summary>自分のターゲットが変わった(パケットのスレッド)。画面のスレッドで読み直す。</summary>
    private void SelfTargetStore_Changed(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RefreshTarget));
    }

    /// <summary>自分のターゲットを読み直す。選んだ敵に自分が技を向けたら、ターゲットへ戻った扱いにする。</summary>
    private void RefreshTarget()
    {
        if (_isDisposed)
        {
            return;
        }

        var targetUuid = SelfTargetStore.Instance.TargetUuid;
        if (targetUuid == _targetUuid)
        {
            return;
        }

        _targetUuid = targetUuid;
        if (_chosenEntityUuid != 0 && _chosenEntityUuid == targetUuid)
        {
            _chosenEntityUuid = 0;
        }

        ApplyShownEntity();
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

    /// <summary>枠の左クリックとメニューの1つ目。バフの枠なら映している敵のバフリスト、デバフの枠ならデバフリストを開く。</summary>
    [RelayCommand]
    private void OpenList(PlayerBuffEntry? entry)
    {
        if (entry is null || Row?.Target is not { } entity)
        {
            return;
        }

        _openList(entry.IsDebuff ? WidgetKind.DebuffList : WidgetKind.BuffList, entity);
    }

    /// <summary>メニューの2つ目。映している敵のバフカード・デバフカードを開く(カードはその敵を追い続ける)。</summary>
    [RelayCommand]
    private void OpenCard(PlayerBuffEntry? entry)
    {
        if (entry is null || Row?.Target is not { } entity)
        {
            return;
        }

        _openCard(
            new EntityWindowTarget(entity),
            entry.IsDebuff ? PlayerBuffListKind.Debuff : PlayerBuffListKind.Buff,
            entry.CardKey,
            entry.BaseId);
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
        SelfTargetStore.Instance.Changed -= SelfTargetStore_Changed;
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

    /// <summary>窓が隠れたら定期の読み直しを止め、見えたら1回読み直してから再開する(<see cref="IWidgetWindowHiddenAware"/>)。</summary>
    public void SetWindowHidden(bool isHidden)
    {
        if (isHidden)
        {
            _refreshTimer.Stop();
            return;
        }

        RefreshTimer_Tick(this, EventArgs.Empty);
        _refreshTimer.Start();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        RefreshBuffs();
        RefreshDataStatus();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ListMenuText));
        OnPropertyChanged(nameof(CardMenuText));
        OnPropertyChanged(nameof(PriorityMenuText));
        OnPropertyChanged(nameof(HideMenuText));
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
        // 映す敵が周りの一覧に居ない間は出さない(倒された後もバフの控えが残ることがある)。
        IReadOnlyList<PlayerBuffSnapshot> snapshots = _lastEntity is not { } entity
            ? []
            : BuffListSnapshotFilter.Apply(
                _settings,
                MeterSnapshotProvider.GetEntityBuffs(entity.EntityUuid, kind, groupsSameBuffs));
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
            // 対象者名とレベルはエンティティのバフ一覧と同じく映している敵のもの。
            var name = BuffListRowName.Format(
                _settings.BuffInfoFormatString,
                _settings.CasterNameDisplayModeIndex,
                snapshot,
                Row?.Target.Name ?? string.Empty,
                Row?.Target.Level ?? 0);
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
