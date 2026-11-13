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
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// プレイヤーのバフ・デバフを1件だけ大きく出すカード。
///
/// <para>
/// <b>失効したら空になるのが正しい挙動。</b>同じ人に同じバフが付き直せば
/// <c>base:{BaseId}</c> のキーが一致するのでそのまま戻る。
/// </para>
///
/// <para>
/// バフもまとまりも指定せずに開いた窓(メイン画面のカードの「指定なし」)は、最初に付いたものを待つ(<see cref="IsWaiting"/>)。
/// タイトルはウィジェット名だけで、その人のバフリストの1行目(無ければデバフリストの1行目)が出たらそのバフに入り、
/// 以後は普通のカードとして動く(次に来たものへは移らない)。そのバフのカードがもう別の窓で開いていれば、この窓は閉じる
/// (決めるのは窓の管理、<see cref="FirstBuffRowFound"/>)。
/// </para>
///
/// <para>
/// バフ・デバフ効果切れは、追うバフがカードの設定の通知対象に入っているときだけ出す(1秒ごとの更新で今の通知対象を読む)。
/// </para>
/// </summary>
public sealed partial class BuffDebuffCardWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable, IBuffDebuffCardMenuViewModel
{
    /// <summary>バフかデバフか。待っている窓が入るまでは <see cref="PlayerBuffListKind.Buff"/>。</summary>
    private PlayerBuffListKind _kind;

    /// <summary>
    /// 追う対象のバフキー。まとまり(<see cref="_group"/>)を追うときは使わない。待っている窓が入るときにだけ書き換わる。
    /// </summary>
    private string? _requestedBuffKey;

    /// <summary>
    /// 追うまとまり。<see cref="BuffGroup.None"/> なら個別のバフを追う。
    /// まとまりは対象が居なくても名乗れるので、タイトルが空になることがない。待っている窓が入るときにだけ書き換わる。
    /// </summary>
    private BuffGroup _group;

    private readonly DispatcherTimer _refreshTimer;

    /// <summary>待っている窓が入る相手(その人のバフリストの1行目、無ければデバフリストの1行目)を探す経路。</summary>
    private readonly Func<long, (PlayerBuffListKind Kind, PlayerBuffSnapshot Snapshot)?>? _findFirstBuffRow;

    /// <summary>右クリックの「グループに追加」の行き先。</summary>
    private readonly IBuffDebuffGroupEntryAdder? _groupEntryAdder;

    /// <summary>右クリックの「バフ・デバフカードのショートカットに追加」の行き先。</summary>
    private readonly IBuffCardShortcutAdder? _shortcutAdder;

    /// <summary>通知(バフ・デバフ効果切れ・薬剤・料理効果切れ)の判定。カードを開いている間だけ借りる(同じバフを見ているグループと共有)。</summary>
    private readonly BuffNotificationWatcher _notificationWatcher = new();

    private PlayerRosterEntry? _player;
    private string _scaleKey = string.Empty;

    /// <summary>
    /// 最後に分かったバフ名。<b>失効してもタイトルは維持する</b>ので持っておく。
    /// 一度も分かっていないうち(料理バフの自動選択で対象が無い等)は空。
    /// </summary>
    private string _lastKnownBuffName = string.Empty;

    /// <summary>
    /// 最後に出していた実体の付与者(プレイヤーでなければ null)と、その対象。失効した後の付与者プレイヤー名に使う。
    /// 対象が替わったら前の対象の付与者は使わない。
    /// </summary>
    private CombatEventLogParty? _lastKnownCaster;
    private long _lastKnownCasterCharacterId;

    private bool _isDisposed;

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

    public BuffDebuffCardWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        PlayerBuffListKind kind,
        string? requestedBuffKey,
        BuffGroup group,
        Func<long, (PlayerBuffListKind Kind, PlayerBuffSnapshot Snapshot)?>? findFirstBuffRow = null,
        IBuffDebuffGroupEntryAdder? groupEntryAdder = null,
        IBuffCardShortcutAdder? shortcutAdder = null)
        : base(playerWidget, requestedCharacterId)
    {
        _kind = kind;
        _group = group;
        _findFirstBuffRow = findFirstBuffRow;
        _groupEntryAdder = groupEntryAdder;
        _shortcutAdder = shortcutAdder;
        _requestedBuffKey = group == BuffGroup.None ? requestedBuffKey : null;
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);

        // 基底のコンストラクタが先にタイトルを組むが、そのとき _group も _kind もまだ既定値。
        // フィールドが揃ってから組み直す。
        RefreshHeader();
        Refresh();
        _refreshTimer.Start();
    }

    /// <summary>いま追っているバフのキー。料理バフ自動選択では実際に見つかったものになる。</summary>
    public string ResolvedBuffKey { get; private set; } = string.Empty;

    public PlayerBuffListKind BuffListKind => _kind;

    /// <summary>開いたときに指定されたバフキー(待っていた窓は入ったバフのキー)。まとまりを追う窓と待っている窓では <c>null</c>。</summary>
    public string? RequestedBuffKey => _requestedBuffKey;

    public BuffGroup Group => _group;

    /// <summary>
    /// 最初に付いたものを待っている窓か(バフもまとまりも決まっていない)。メイン画面のカードの「指定なし」の窓。
    /// 入ったら偽に変わり、戻らない。
    /// </summary>
    public bool IsWaiting => _group == BuffGroup.None && _requestedBuffKey is null;

    /// <summary>
    /// 待っている間に、その人のバフリストの1行目(無ければデバフリストの1行目)が出た(引数はこの窓・バフかデバフか・その行)。
    /// 受けた窓の管理が、<see cref="Enter"/> で入れるか、そのカードがもう別の窓で開いていれば窓を閉じる。受け手が無ければ待ったまま。
    /// </summary>
    public event Action<BuffDebuffCardWidgetViewModel, PlayerBuffListKind, PlayerBuffSnapshot>? FirstBuffRowFound;

    /// <summary>右クリックのメニューの色を引くウィジェット。</summary>
    public WidgetListItemViewModel Widget => PlayerWidget;

    /// <summary>右クリックのメニューの項目「グループに追加」。</summary>
    public string AddToGroupMenuText => LocalizationManager.Instance.GetString("BuffList_Menu_AddToGroup");

    /// <summary>右クリックのメニューの項目「バフ・デバフカードのショートカットに追加」。</summary>
    public string AddToShortcutsMenuText => LocalizationManager.Instance.GetString("BuffList_Menu_AddToBuffCardShortcuts");

    /// <summary>右クリックのメニューの項目「このバフを通知対象にする」「〜しない」(デバフのカードは「このデバフを」)。</summary>
    public string NotificationTargetMenuText => BuffDebuffCardContent.GetNotificationTargetMenuText(
        _kind == PlayerBuffListKind.Debuff,
        IsNotificationTarget());

    /// <summary>追うバフを通知対象にできるか。自分・他人のどちらのカードでも、追うバフが決まっていれば真(待っている窓は偽)。</summary>
    public bool CanToggleNotificationTarget()
    {
        return !IsWaiting && ResolveGroupEntryKey() is not null;
    }

    public void RefreshMenuText()
    {
        OnPropertyChanged(nameof(NotificationTargetMenuText));
    }

    /// <summary>
    /// このカードのバフをバフ・デバフグループに足せるか。自分のカードだけで、グループにまだオンで入っていないとき真。
    /// 偽ならメニューに項目を出さない。バフが今付いていなくても、追っているバフ(まとまり)で決まる。待っている窓は偽。
    /// </summary>
    public bool CanAddToGroup()
    {
        return IsSelfTarget
            && _groupEntryAdder is not null
            && ResolveGroupEntryKey() is { } key
            && _groupEntryAdder.CanAdd(key);
    }

    /// <summary>
    /// このカードのバフをバフ・デバフカードのショートカットに足せるか。条件は <see cref="CanAddToGroup"/> と同じで、ショートカットにまだオンで入っていないとき真。
    /// </summary>
    public bool CanAddToShortcuts()
    {
        return IsSelfTarget
            && _shortcutAdder is not null
            && ResolveGroupEntryKey() is { } key
            && _shortcutAdder.CanAddShortcut(key);
    }

    /// <summary>最後に分かったバフ名。保存して、次の起動でタイトルに使う。</summary>
    public string? LastKnownBuffName => _lastKnownBuffName;

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
        RefreshHeader();
    }

    /// <summary>拡大率。<c>ScaleTransform</c> がそのまま使う倍率。</summary>
    public double Scale => ScalePercent / 100d;

    /// <summary>
    /// タイトルの前半を「バフカード(名前)」「デバフカード(名前)」にする。
    /// バフ名が一度も分かっていないときだけウィジェット名のまま。
    /// </summary>
    protected override string HeaderPrefix =>
        BuffDebuffCardContent.CreateHeaderPrefix(
            PlayerWidget.DisplayName,
            _kind,
            _group,
            _lastKnownBuffName);

    /// <summary>待っている間はタイトルをウィジェット名だけにする(「 - 名前」を付けない)。</summary>
    protected override bool ShowsPlayerIdentityInHeader => !IsWaiting;

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

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        _notificationWatcher.Dispose();
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        _player = player;
        Refresh();
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

    [RelayCommand]
    private void AddToGroup()
    {
        if (CanAddToGroup() && ResolveGroupEntryKey() is { } key)
        {
            _groupEntryAdder!.Add(key);
        }
    }

    [RelayCommand]
    private void AddToShortcuts()
    {
        if (CanAddToShortcuts() && ResolveGroupEntryKey() is { } key)
        {
            _shortcutAdder!.AddShortcut(key);
        }
    }

    /// <summary>追うバフをカードの設定の通知対象に入れる・外す(今の状態の逆。決まりは <see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)。</summary>
    [RelayCommand]
    private void ToggleNotificationTarget()
    {
        if (CanToggleNotificationTarget() && ResolveGroupEntryKey() is { } key)
        {
            PlayerWidget.SetBuffCardNotificationTargetEnabledFromWidget(key, !PlayerWidget.IsBuffCardNotificationTarget(key));
            RefreshMenuText();
        }
    }

    /// <summary>追うバフがカードの設定の通知対象(設定の窓のプレビューを含む今の値)に入っているか。</summary>
    private bool IsNotificationTarget()
    {
        return ResolveGroupEntryKey() is { } key && PlayerWidget.IsBuffCardNotificationTarget(key);
    }

    /// <summary>
    /// 待っている窓が、そのバフに入る(窓の管理が <see cref="FirstBuffRowFound"/> を受けて呼ぶ)。料理・薬剤ならまとまりを追う。
    /// 入ったらそのバフのカードとして保存し直す(<see cref="PlayerWidgetWindowViewModel.SavedTargetInfoResolved"/>)。待っていなければ何もしない。
    /// </summary>
    public void Enter(PlayerBuffListKind kind, string cardKey, BuffGroup group)
    {
        if (!IsWaiting)
        {
            return;
        }

        _kind = kind;
        _group = group;
        _requestedBuffKey = group == BuffGroup.None ? cardKey : null;
        RaiseSavedTargetInfoResolved();
    }

    /// <summary>設定の行(グループの行・ショートカット・通知対象)の鍵(<see cref="BuffDebuffCardContent.CreateEntryKey"/>)。</summary>
    private BuffEntryKey? ResolveGroupEntryKey()
    {
        return BuffDebuffCardContent.CreateEntryKey(_group, _requestedBuffKey);
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
        PlayerWidget.SaveBuffCardScale(_scaleKey, normalized);
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
        // 出現時の一覧の受信は名簿の行を変えないことがあるので、読み直すたびに見る。
        RefreshDataStatus();
    }

    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
        => WidgetDataStatusRules.ForPlayerBuffs(
            reception,
            IsSelfTarget,
            player,
            SelectedCharacterId is { } characterId && MeterSnapshotProvider.HasReceivedPlayerAppearBuffList(characterId));

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(AddToGroupMenuText));
        OnPropertyChanged(nameof(AddToShortcutsMenuText));
        RefreshMenuText();
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        if (SelectedCharacterId is not { } characterId)
        {
            _notificationWatcher.Release();
            ApplyContent(BuffDebuffCardContent.Empty, string.Empty, characterId: 0, hasBuff: false);

            // 対象が分からなくても、まとまり(料理・薬剤)の名前は出せる。
            // ここで組み直さないと、直起動のあいだ既定名のまま固定される。
            RefreshHeader();
            return;
        }

        // 待っている窓は、その人のバフリストの1行目(無ければデバフリストの1行目)が出たら知らせる。
        // 入るか、そのカードがもう別の窓で開いていて閉じるかは、窓の管理が決める(1つのカードに窓は1枚)。
        if (IsWaiting && _findFirstBuffRow?.Invoke(characterId) is { } row)
        {
            FirstBuffRowFound?.Invoke(this, row.Kind, row.Snapshot);
            if (_isDisposed)
            {
                return;
            }
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        // カードは番号ごとに1枚で、出すのは残り時間の一番長い実体。一覧の「同種バフの表示」は効かない。
        var snapshots = MeterSnapshotProvider.GetPlayerBuffs(characterId, _kind, groupSameBuffs: true);
        var snapshot = _group != BuffGroup.None
            ? BuffDebuffCardContent.FindByGroup(snapshots, _group)
            : BuffDebuffCardContent.FindByKey(snapshots, _requestedBuffKey ?? string.Empty);

        // タイトルはバフ名を含むので、バフを解決してから組む。
        //
        // 控えるのは内部ID注記を付けない名前。snapshot.Name は注記込みのことがあり、
        // それを保存すると ID 表示を切ったあともタイトルに残る。
        var rememberedName = snapshot is null
            ? string.Empty
            : BuffDebuffCardContent.GetCardBuffName(snapshot);
        if (!string.IsNullOrWhiteSpace(rememberedName))
        {
            var wasUnknown = string.IsNullOrWhiteSpace(_lastKnownBuffName);
            _lastKnownBuffName = rememberedName;

            if (wasUnknown)
            {
                RaiseSavedTargetInfoResolved();
            }
        }

        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId, playerIdentity.IsNpc, playerIdentity.ProfessionId);
        }
        else
        {
            RefreshHeader();
        }

        // 名前の出し方はプレイヤー一覧・メーターと同じ規則へ通す。
        // 名前がまだ取れていなければUID、伏せる設定なら伏せ字、NPC なら職業名。
        var targetName = PlayerInfoFormatFormatter.GetDisplayName(
            _player?.Name ?? playerIdentity?.Name,
            playerIdentity?.UserId ?? characterId,
            _player?.IsSelf ?? (AppState.PlayerUID != 0 && characterId == AppState.PlayerUID),
            _player?.IsNpc ?? playerIdentity?.IsNpc ?? false,
            _player?.ProfessionId ?? playerIdentity?.ProfessionId ?? 0,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);

        // 入るカードを待っている窓は、まだ見るバフが無い。
        if (IsWaiting)
        {
            _notificationWatcher.Release();
        }
        else
        {
            _notificationWatcher.Observe(
                BuffNotificationTarget.ForPlayer(characterId),
                BuffNotificationRegistry.CreateBuffKey(_group, _requestedBuffKey ?? string.Empty),
                IsNotificationTarget(),
                snapshot,
                targetName);
        }

        if (snapshot is null)
        {
            // 失効。アイコンと残り時間は消すが、名前の行は残す。
            ApplyContent(
                BuffDebuffCardContent.CreateNameOnly(
                    _lastKnownBuffName,
                    targetName,
                    _player?.Level ?? 0,
                    PlayerWidget.BuffInfoFormatString,
                    _lastKnownCasterCharacterId == characterId ? _lastKnownCaster : null),
                string.Empty,
                characterId,
                hasBuff: false);
            return;
        }

        _lastKnownCaster = snapshot.Caster;
        _lastKnownCasterCharacterId = characterId;

        var content = BuffDebuffCardContent.Create(
            snapshot,
            targetName,
            _player?.Level ?? 0,
            PlayerWidget.BuffInfoFormatString);

        ApplyContent(content, snapshot.CardKey, characterId, hasBuff: true);
    }

    /// <summary>
    /// <paramref name="hasBuff"/> は<b>バフが見つかったかどうか</b>で、アイコンが引けたかではない。
    /// アイコンだけ欠けている状態を「バフ無し」に丸めると、名前も残り時間も出なくなって
    /// 失効と見分けがつかなくなる。
    /// </summary>
    private void ApplyContent(
        BuffDebuffCardContent content,
        string buffKey,
        long characterId,
        bool hasBuff)
    {
        ResolvedBuffKey = buffKey;
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

        SynchronizeScaleKey(BuffDebuffCardContent.CreateScaleKey(characterId, _group, _requestedBuffKey));
    }

    /// <summary>
    /// 倍率の保存キーが変わったら、その組み合わせの保存値を読み直す。
    /// 対象が分からない間はキーが空になるので、そのときは今の倍率を保つ。
    /// </summary>
    private void SynchronizeScaleKey(string scaleKey)
    {
        if (string.IsNullOrEmpty(scaleKey)
            || string.Equals(_scaleKey, scaleKey, StringComparison.Ordinal))
        {
            return;
        }

        _scaleKey = scaleKey;
        ScalePercent = PlayerWidget.GetBuffCardScale(scaleKey);
    }
}
