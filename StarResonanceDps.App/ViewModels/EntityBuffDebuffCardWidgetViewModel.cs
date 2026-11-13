// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Threading;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Notifications;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// モンスターのバフ・デバフを1件だけ大きく出すカード。
///
/// <para>
/// 倍率の保存キーには <see cref="EntityListEntry.EntityId"/>(種別ID)を使う。
/// <c>EntityUuid</c> は実体ごとの値なので、再起動をまたぐと二度と当たらない。
/// </para>
///
/// <para>
/// バフ・デバフ効果切れは、追うバフがカードの設定の通知対象(プレイヤーのカードと共通)に入っているときだけ出す。
/// 右クリックのメニューは通知対象の項目だけ(グループとショートカットは自分のバフの設定なので出さない)。
/// </para>
/// </summary>
public sealed partial class EntityBuffDebuffCardWidgetViewModel
    : ViewModelBase, IEntityWidgetWindowViewModel, IDisposable, IBuffDebuffCardMenuViewModel
{
    private readonly WidgetListItemViewModel _widget;
    private readonly EntityWindowTarget _target;
    private readonly PlayerBuffListKind _kind;
    private readonly string? _requestedBuffKey;

    /// <summary>追うまとまり。<see cref="BuffGroup.None"/> なら個別のバフを追う。</summary>
    private readonly BuffGroup _group;
    private readonly DispatcherTimer _refreshTimer;

    /// <summary>通知(バフ・デバフ効果切れ・薬剤・料理効果切れ)の判定。カードを開いている間だけ借りる。</summary>
    private readonly BuffNotificationWatcher _notificationWatcher = new();

    private string _scaleKey = string.Empty;

    /// <summary>最後に分かったバフ名。失効してもタイトルは維持する。</summary>
    private string _lastKnownBuffName = string.Empty;

    /// <summary>
    /// 最後に出していた実体の付与者(プレイヤーでなければ null)と、その対象の実体。失効した後の付与者プレイヤー名に使う。
    /// 対象が替わったら前の対象の付与者は使わない。
    /// </summary>
    private CombatEventLogParty? _lastKnownCaster;
    private long _lastKnownCasterEntityUuid;

    private bool _isDisposed;

    [ObservableProperty]
    private string _headerText = string.Empty;

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _durationText = string.Empty;

    [ObservableProperty]
    private string _layerText = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    /// <summary>絵が Icons.xaml の絵(装備効果)なら型抜き(<see cref="BuffDebuffCardContent.IconMask"/>)。</summary>
    [ObservableProperty]
    private ImageBrush? _iconMask;

    /// <summary>絵がモジュール効果の絵か(<see cref="BuffDebuffCardContent.IsModuleEffectIcon"/>)。絵の大きさを切り替える。</summary>
    [ObservableProperty]
    private bool _isModuleEffectIcon;

    /// <summary>絵が因子の絵か(<see cref="BuffDebuffCardContent.IsFactorIcon"/>)。絵の位置を切り替える。</summary>
    [ObservableProperty]
    private bool _isFactorIcon;

    /// <summary>絵のパスはあるのにファイルが見つからないか(<see cref="BuffDebuffCardContent.ShowsUnknownIcon"/>)。はてなを出す。</summary>
    [ObservableProperty]
    private bool _showsUnknownIcon;

    [ObservableProperty]
    private bool _hasBuff;

    /// <summary>名前の行を出すか。<b>失効しても名前だけは残す</b>ので、<see cref="HasBuff"/> とは別。</summary>
    [ObservableProperty]
    private bool _hasDisplayText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Scale))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand))]
    [NotifyCanExecuteChangedFor(nameof(ZoomOutCommand))]
    private int _scalePercent = WidgetConfigDefaults.DefaultBuffCardScale;

    public EntityBuffDebuffCardWidgetViewModel(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        PlayerBuffListKind kind,
        string? requestedBuffKey,
        BuffGroup group)
    {
        _widget = widget;
        _target = target;
        _kind = kind;
        _group = group;
        _requestedBuffKey = group == BuffGroup.None ? requestedBuffKey : null;
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _target.Changed += Target_Changed;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        RefreshPresentation();
        Refresh();
        RefreshDataStatus();
        _refreshTimer.Start();
    }

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

    /// <summary>出現時の一覧の受信と周りから外れたことは対象の変化を伴わないので、1秒ごとの読み直しでも見る。</summary>
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

    public long EntityId => _target.EntityId;

    public Zproto.EEntityType? EntityType => _target.EntityType;

    public long EntityUuid => _target.EntityUuid;

    public bool IsEntityAcquired => _target.IsAcquired;

    public bool IsWaitingForEntity => _target.IsWaitingForEntity;

    public EntityTargetInfo? Entity => _target.Entity;

    public string TargetName => _target.Name;

    public PlayerBuffListKind BuffListKind => _kind;

    public string? RequestedBuffKey => _requestedBuffKey;

    public BuffGroup Group => _group;

    /// <summary>最後に分かったバフ名。保存して、次の起動でタイトルに使う。</summary>
    public string? LastKnownBuffName => _lastKnownBuffName;

    /// <summary>
    /// バフ名が空から確定に変わったときに上がる。対象一覧を書き直す契機。
    /// プレイヤー用カードの <c>SavedTargetInfoResolved</c> と同じ役目。
    /// </summary>
    public event EventHandler? SavedTargetInfoResolved;

    /// <summary>
    /// 設定から復元したときに、バフを観測する前のタイトルを埋める。
    /// <b>実際に観測した値は上書きしない。</b>
    /// </summary>
    public void SeedLastKnownBuffName(string? buffName)
    {
        if (!string.IsNullOrWhiteSpace(_lastKnownBuffName)
            || string.IsNullOrWhiteSpace(buffName))
        {
            return;
        }

        _lastKnownBuffName = buffName;

        // 種を入れただけではタイトルに出ない。窓を作る前にここで組み直す。
        RefreshHeaderText();
    }

    public double Scale => ScalePercent / 100d;

    /// <summary>右クリックのメニューの色を引くウィジェット。</summary>
    public WidgetListItemViewModel Widget => _widget;

    public string AddToGroupMenuText => LocalizationManager.Instance.GetString("BuffList_Menu_AddToGroup");

    /// <summary>モンスターのバフはグループに足さないので、項目を出さない。</summary>
    public bool CanAddToGroup() => false;

    public string AddToShortcutsMenuText => LocalizationManager.Instance.GetString("BuffList_Menu_AddToBuffCardShortcuts");

    /// <summary>モンスターのバフはショートカットに足さないので、項目を出さない。</summary>
    public bool CanAddToShortcuts() => false;

    /// <summary>右クリックのメニューの項目「このバフを通知対象にする」「〜しない」(デバフのカードは「このデバフを」)。</summary>
    public string NotificationTargetMenuText => BuffDebuffCardContent.GetNotificationTargetMenuText(
        _kind == PlayerBuffListKind.Debuff,
        IsNotificationTarget());

    public bool CanToggleNotificationTarget()
    {
        return BuffDebuffCardContent.CreateEntryKey(_group, _requestedBuffKey) is not null;
    }

    public void RefreshMenuText()
    {
        OnPropertyChanged(nameof(NotificationTargetMenuText));
    }

    public bool RepresentsEntity(long entityUuid)
    {
        return _target.Represents(entityUuid);
    }

    public bool RepresentsBuff(PlayerBuffListKind kind, string? buffKey, BuffGroup group)
    {
        if (_kind != kind || _group != group)
        {
            return false;
        }

        // まとまりを追う窓は、個別のバフキーが違っても同じ窓。
        return group != BuffGroup.None
            || string.Equals(_requestedBuffKey, buffKey, StringComparison.Ordinal);
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
        Refresh();
        RefreshHeaderText();
    }

    /// <summary>
    /// タイトルの前半を「バフカード(名前)」「デバフカード(名前)」にする。
    /// バフ名が一度も分かっていないときだけウィジェット名のまま。
    /// </summary>
    private void RefreshHeaderText()
    {
        var prefix = BuffDebuffCardContent.CreateHeaderPrefix(
            _widget.DisplayName,
            _kind,
            _group,
            _lastKnownBuffName);

        HeaderText = WidgetHeaderText.Join(prefix, _target.Name);
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
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        _notificationWatcher.Dispose();
    }

    /// <summary>項目を出さないので呼ばれない(<see cref="CanAddToGroup"/>)。取り決めを満たすためだけにある。</summary>
    [RelayCommand]
    private void AddToGroup()
    {
    }

    /// <summary>項目を出さないので呼ばれない(<see cref="CanAddToShortcuts"/>)。取り決めを満たすためだけにある。</summary>
    [RelayCommand]
    private void AddToShortcuts()
    {
    }

    /// <summary>追うバフをカードの設定の通知対象に入れる・外す(今の状態の逆。決まりは <see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)。</summary>
    [RelayCommand]
    private void ToggleNotificationTarget()
    {
        if (BuffDebuffCardContent.CreateEntryKey(_group, _requestedBuffKey) is { } key)
        {
            _widget.SetBuffCardNotificationTargetEnabledFromWidget(key, !_widget.IsBuffCardNotificationTarget(key));
            RefreshMenuText();
        }
    }

    /// <summary>追うバフがカードの設定の通知対象(設定の窓のプレビューを含む今の値)に入っているか。</summary>
    private bool IsNotificationTarget()
    {
        return BuffDebuffCardContent.CreateEntryKey(_group, _requestedBuffKey) is { } key
            && _widget.IsBuffCardNotificationTarget(key);
    }

    [RelayCommand(CanExecute = nameof(CanZoomIn))]
    private void ZoomIn()
    {
        ApplyScale(ScalePercent + WidgetConfigDefaults.BuffCardScaleStep);
    }

    [RelayCommand(CanExecute = nameof(CanZoomOut))]
    private void ZoomOut()
    {
        ApplyScale(ScalePercent - WidgetConfigDefaults.BuffCardScaleStep);
    }

    private bool CanZoomIn() => ScalePercent < WidgetConfigDefaults.MaxBuffCardScale;

    private bool CanZoomOut() => ScalePercent > WidgetConfigDefaults.MinBuffCardScale;

    private void ApplyScale(int scalePercent)
    {
        var normalized = WidgetConfigDefaults.ClampBuffCardScale(scalePercent);
        if (normalized == ScalePercent)
        {
            return;
        }

        ScalePercent = normalized;
        _widget.SaveBuffCardScale(_scaleKey, normalized);
    }

    private void Target_Changed(object? sender, EventArgs e)
    {
        RefreshPresentation();
        RefreshDataStatus();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
        RefreshDataStatus();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        // タイトルの「バフ/デバフ」もカルチャ依存なので、中身だけでなく組み直す。
        RefreshPresentation();
        RefreshMenuText();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        // カードは番号ごとに1枚で、出すのは残り時間の一番長い実体。一覧の「同種バフの表示」は効かない。
        var snapshots = MeterSnapshotProvider.GetEntityBuffs(_target.EntityUuid, _kind, groupSameBuffs: true);
        var snapshot = _group != BuffGroup.None
            ? BuffDebuffCardContent.FindByGroup(snapshots, _group)
            : BuffDebuffCardContent.FindByKey(snapshots, _requestedBuffKey ?? string.Empty);

        // 控えるのは内部ID注記を付けない名前。snapshot.Name は注記込みのことがあり、
        // それを保存すると ID 表示を切ったあともタイトルに残る。
        var rememberedName = snapshot is null
            ? string.Empty
            : BuffDebuffCardContent.GetCardBuffName(snapshot);
        if (!string.IsNullOrWhiteSpace(rememberedName))
        {
            var wasUnknown = string.IsNullOrWhiteSpace(_lastKnownBuffName);
            _lastKnownBuffName = rememberedName;
            RefreshHeaderText();

            if (wasUnknown)
            {
                SavedTargetInfoResolved?.Invoke(this, EventArgs.Empty);
            }
        }

        _notificationWatcher.Observe(
            BuffNotificationTarget.ForEntity(_target.EntityUuid),
            BuffNotificationRegistry.CreateBuffKey(_group, _requestedBuffKey ?? string.Empty),
            IsNotificationTarget(),
            snapshot,
            _target.Name);

        if (snapshot is null)
        {
            // 失効。アイコンと残り時間は消すが、名前の行は残す。
            ApplyContent(
                BuffDebuffCardContent.CreateNameOnly(
                    _lastKnownBuffName,
                    _target.Name,
                    _target.Level,
                    _widget.BuffInfoFormatString,
                    _lastKnownCasterEntityUuid == _target.EntityUuid ? _lastKnownCaster : null),
                hasBuff: false);
            return;
        }

        _lastKnownCaster = snapshot.Caster;
        _lastKnownCasterEntityUuid = _target.EntityUuid;

        ApplyContent(
            BuffDebuffCardContent.Create(
                snapshot,
                _target.Name,
                _target.Level,
                _widget.BuffInfoFormatString),
            hasBuff: true);
    }

    /// <summary>
    /// <paramref name="hasBuff"/> は<b>バフが見つかったかどうか</b>で、アイコンが引けたかではない。
    /// </summary>
    private void ApplyContent(BuffDebuffCardContent content, bool hasBuff)
    {
        DisplayText = content.DisplayText;
        DurationText = content.DurationText;
        LayerText = content.LayerText;
        IconPath = content.IconPath;
        IconMask = content.IconMask;
        IsModuleEffectIcon = content.IsModuleEffectIcon;
        IsFactorIcon = content.IsFactorIcon;
        ShowsUnknownIcon = content.ShowsUnknownIcon;
        HasBuff = hasBuff;
        HasDisplayText = !string.IsNullOrEmpty(content.DisplayText);

        SynchronizeScaleKey(
            BuffDebuffCardContent.CreateScaleKey(_target.EntityId, _group, _requestedBuffKey));
    }

    private void SynchronizeScaleKey(string scaleKey)
    {
        if (string.IsNullOrEmpty(scaleKey)
            || string.Equals(_scaleKey, scaleKey, StringComparison.Ordinal))
        {
            return;
        }

        _scaleKey = scaleKey;
        ScalePercent = _widget.GetBuffCardScale(scaleKey);
    }
}
