// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.ViewModels.Plugins;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;
using StarResonanceDps.App.Views.Widgets;

namespace StarResonanceDps.App.Services;

public sealed class WidgetWindowManager : IBuffDebuffGroupEntryAdder, IBuffCardShortcutAdder
{
    private const double PlayerWindowCascadeOffset = 20d;

    /// <summary>1枚だけ開く窓。鍵はカード(本体の種別は1枚ずつ、プラグインのウィジェットは同じ種別でカードが分かれる)。</summary>
    private readonly Dictionary<WidgetListItemViewModel, WidgetWindow> _openSingleWindows = new();
    private readonly List<PlayerWidgetWindowSession> _openPlayerWindows = [];
    private readonly List<EntityWindowSession> _openEntityWindows = [];
    private readonly List<ChatWindowSession> _openChatWindows = [];

    /// <summary>プラグインの複数起動のウィジェットの窓。窓を閉じたら外す。</summary>
    private readonly List<PluginTargetWindowSession> _openPluginWindows = [];

    private readonly Dictionary<WidgetKind, WidgetListItemViewModel> _trackedPlayerWidgets = new();

    /// <summary>窓のタイトルを当て直すために知らせを受けている、プラグインの複数起動のウィジェット。止まったら外す。</summary>
    private readonly HashSet<WidgetListItemViewModel> _trackedPluginWidgets = [];

    /// <summary>顔ぶれを当てる処理を積んだまま、まだ当てていないプレイヤーの窓(<see cref="UpdatePlayerWindowPresentations"/>)。</summary>
    private readonly HashSet<PlayerWidgetWindowSession> _playerWindowsAwaitingRoster = [];

    /// <summary>窓へ当てる最新の顔ぶれ。</summary>
    private PlayerWindowRoster? _latestPlayerWindowRoster;

    /// <summary>開いている蘇生リストの窓の中身。顔ぶれを当てる先(<see cref="UpdateListWidgetRosters"/>)。閉じていれば null。</summary>
    private ReviveListWidgetViewModel? _reviveListViewModel;

    /// <summary>開いている被ダメログの窓の中身。顔ぶれを当てる先(<see cref="UpdateListWidgetRosters"/>)。閉じていれば null。</summary>
    private DamageTakenLogWidgetViewModel? _damageTakenLogViewModel;

    /// <summary>
    /// マップのカード。プレイヤー情報の現在地(<see cref="ShowPlayerOnMap"/>)とチャットの座標のリンク
    /// (<see cref="ShowSharedPositionOnMap"/>)からマップを開くのに使う。
    /// </summary>
    private WidgetListItemViewModel? _mapWidget;

    /// <summary>開いているマップの窓の中身。閉じていれば null。</summary>
    private MapWidgetViewModel? _mapViewModel;

    /// <summary>ターゲット情報(内部名はエンティティ情報)のカード。エンティティリストとマップから敵を選んで開くのに使う(<see cref="ShowEntityInTargetInfo"/>)。</summary>
    private WidgetListItemViewModel? _entityInfoWidget;

    /// <summary>開いているターゲット情報の窓の中身。周りの実体と名簿を当てる先。閉じていれば null。</summary>
    private EntityInfoWidgetViewModel? _entityInfoViewModel;

    private Window? _managerWindow;
    private bool _isManagerClosing;

    /// <summary>保存した状態(起動中)を変えずに窓を閉じている(<see cref="CloseKeepingState"/>)。</summary>
    private bool _isClosingKeepingState;

    private WidgetWindowManager()
    {
        MessageManager.ResetToStartupCompleted += MessageManager_ResetToStartupCompleted;
        GameReceptionStateStore.Changed += RefreshAllDataStatusLater;
        EncounterHistoryProvider.SelectionChanged += RefreshAllDataStatusLater;
        EncounterHistoryProvider.LoadFailedChanged += RefreshAllDataStatusLater;
    }

    /// <summary>
    /// ログアウトで Core を起動時の状態へ戻した。実体の窓を未捕獲に戻し、起動時の復元と同じく種類と種別IDで捕まえ直させる。
    /// 未知の敵(種別ID 0)の窓は捕まえ直せないのでそのままにする。通知はパケット処理のスレッドで来るので、UI のスレッドへ渡す。
    /// </summary>
    private void MessageManager_ResetToStartupCompleted()
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            ReleaseEntityWindows();
            ResetPlayerWindowsToStartup();
            _entityInfoViewModel?.ResetToStartup();
        });
    }

    /// <summary>
    /// プレイヤーの窓を起動直後の状態へ戻す。空の一覧の通知が先に UI のスレッドへ積まれているので、
    /// 窓が一覧の外れで残した最後の値を、その後で捨てる。
    /// 窓へ当てる処理はそれより低い優先度で積まれているので、先にここで当て切ってから戻す。
    /// </summary>
    private void ResetPlayerWindowsToStartup()
    {
        ApplyAwaitingRosterToPlayerWindows();
        foreach (var session in _openPlayerWindows.ToArray())
        {
            session.ViewModel.ResetToStartup();
        }
    }

    private void ReleaseEntityWindows()
    {
        foreach (var session in _openEntityWindows)
        {
            if (!session.ViewModel.IsEntityAcquired || session.ViewModel.EntityId == 0)
            {
                continue;
            }

            session.ViewModel.ReleaseEntity();
            session.Window.SetHeaderText(session.ViewModel.HeaderText);
        }
    }

    public static WidgetWindowManager Instance { get; } = new();

    /// <summary>
    /// カードを作ったときに呼ぶ。人の窓の種別のカードと、マップのカード(現在地から開くため)と、
    /// ターゲット情報のカード(エンティティリストとマップから敵を選んで開くため)を控える。
    /// </summary>
    public void RegisterWidget(WidgetListItemViewModel widget)
    {
        if (WidgetConfigDefaults.IsPlayerWindowKind(widget.Kind))
        {
            TrackPlayerWidget(widget);
        }

        if (widget.Kind == WidgetKind.Map)
        {
            _mapWidget = widget;
        }

        if (widget.Kind == WidgetKind.EntityInfo)
        {
            _entityInfoWidget = widget;
        }
    }

    /// <summary>
    /// エンティティリストの行・マップの敵の印から敵を選んだ。ターゲット情報の窓を開くか前に出し、その敵を映す(<see cref="EntityInfoWidgetViewModel.ShowEntity"/>)。
    /// 起動していないカードは起動する(窓はその場で開く)。
    /// </summary>
    public void ShowEntityInTargetInfo(EntityTargetInfo entity)
    {
        if (_entityInfoWidget is null)
        {
            return;
        }

        if (_entityInfoWidget.State != WidgetState.Running)
        {
            _entityInfoWidget.State = WidgetState.Running;
        }
        else if (_openSingleWindows.TryGetValue(_entityInfoWidget, out var window))
        {
            RestoreAndActivate(window);
        }
        else
        {
            Open(_entityInfoWidget);
        }

        _entityInfoViewModel?.ShowEntity(entity);
    }

    /// <summary>
    /// プレイヤー情報の現在地を押した。マップの窓を開くか前に出し、その人を中心に出す(<see cref="MapWidgetViewModel.ShowPlayer"/>)。
    /// 起動していないカードは起動する(窓はその場で開く)。
    /// </summary>
    public void ShowPlayerOnMap(long characterId)
    {
        if (TryOpenMap())
        {
            _mapViewModel?.ShowPlayer(characterId);
        }
    }

    /// <summary>
    /// チャットの座標のリンクを押した。マップの窓を開くか前に出し、その場所を中心に出して印を置く
    /// (<see cref="MapWidgetViewModel.ShowSharedPosition"/>)。
    /// </summary>
    public void ShowSharedPositionOnMap(MapSharedPosition position)
    {
        if (TryOpenMap())
        {
            _mapViewModel?.ShowSharedPosition(position);
        }
    }

    /// <summary>マップの窓を開くか前に出す。起動していないカードは起動する(窓はその場で開く)。カードが無ければ偽。</summary>
    private bool TryOpenMap()
    {
        if (_mapWidget is null)
        {
            return false;
        }

        if (_mapWidget.State != WidgetState.Running)
        {
            _mapWidget.State = WidgetState.Running;
        }
        else if (_openSingleWindows.TryGetValue(_mapWidget, out var window))
        {
            RestoreAndActivate(window);
        }
        else
        {
            Open(_mapWidget);
        }

        return true;
    }

    public bool CanAdd(BuffEntryKey key)
    {
        return key.IsValid
            && _trackedPlayerWidgets.TryGetValue(WidgetKind.BuffDebuffGroup, out var widget)
            && !widget.IsBuffDebuffGroupEntryEnabled(key);
    }

    public void Add(BuffEntryKey key)
    {
        if (_trackedPlayerWidgets.TryGetValue(WidgetKind.BuffDebuffGroup, out var widget))
        {
            widget.SetBuffDebuffGroupEntryEnabledFromWidget(key, isEnabled: true);
        }
    }

    public bool CanAddShortcut(BuffEntryKey key)
    {
        return key.IsValid
            && _trackedPlayerWidgets.TryGetValue(WidgetKind.BuffDebuffCard, out var widget)
            && !widget.IsBuffCardShortcutEnabled(key);
    }

    public void AddShortcut(BuffEntryKey key)
    {
        if (_trackedPlayerWidgets.TryGetValue(WidgetKind.BuffDebuffCard, out var widget))
        {
            widget.SetBuffCardShortcutEnabledFromWidget(key, isEnabled: true);
        }
    }

    /// <summary>
    /// その人のバフリストの1行目のバフ(バフが無ければデバフリストの1行目のデバフ)。行はそれぞれの一覧の窓と同じ作り方
    /// (<see cref="BuffListSnapshotFilter.GetPlayerRows"/>、そのカードの設定)で、カードの鍵ごとの代表の行だけを見る。
    /// バフ・デバフカードの「指定なし」が開く相手と、待っている窓が入る相手。どちらにも無ければ null。
    /// </summary>
    public (PlayerBuffListKind Kind, PlayerBuffSnapshot Snapshot)? FindFirstBuffRow(long characterId)
    {
        foreach (var (kind, listKind) in new[]
        {
            (PlayerBuffListKind.Buff, WidgetKind.BuffList),
            (PlayerBuffListKind.Debuff, WidgetKind.DebuffList)
        })
        {
            var settings = _trackedPlayerWidgets.TryGetValue(listKind, out var listWidget)
                ? listWidget.GetBuffListSettingsSnapshot()
                : WidgetConfigDefaults.CreateBuffListSettings(listKind);
            var first = BuffListSnapshotFilter
                .GetPlayerRows(settings, characterId, kind)
                .FirstOrDefault(snapshot => snapshot.IsCardRepresentative);
            if (first is not null)
            {
                return (kind, first);
            }
        }

        return null;
    }

    /// <summary>
    /// バフ・デバフカードの「指定なし」の起動ボタン。自分のバフリストの1行目(無ければデバフリストの1行目)のカードを開き、もう開いていれば前に出す
    /// (<see cref="FindFirstBuffRow"/>、開き方はリストの行から開くのと同じく行のバフのまとまりで)。開くカードが決まったら
    /// <see cref="UnspecifiedBuffCardResolved"/> で知らせる。どちらにも行が無ければ、最初に付いたものを待つ窓を開く(もう開いていれば前に出す)。
    /// </summary>
    /// <param name="selfCharacterId">自分の UID。まだ分からなければ 0(行は探さず、待つ窓にする)。</param>
    public void OpenUnspecifiedBuffCard(WidgetListItemViewModel widget, long selfCharacterId)
    {
        var self = WidgetTargetOption.CreateSelf(selfCharacterId);
        if (selfCharacterId != 0 && FindFirstBuffRow(selfCharacterId) is { } row)
        {
            var card = CreateSelfBuffCardOption(row.Kind, row.Snapshot);
            // 知らせは開く前(開いたときの当て直しが「指定なし」をほかの項目に置き換える前に、決まったカードへ替える)。
            UnspecifiedBuffCardResolved?.Invoke(widget, card);
            OpenTarget(widget, self, card);
            return;
        }

        // 待つ窓は相手もバフも指定しない窓(1つだけ)。もう開いていれば OpenTarget が前に出す。
        OpenTarget(widget, self, WidgetTargetOption.CreateUnspecifiedBuffCard());
    }

    /// <summary>
    /// 「指定なし」で開くカードが決まった(引数はウィジェットとそのカードの項目)。起動ボタンで1行目のカードを開く(前に出す)とき、
    /// 待っていた窓がバフに入るとき、そのカードがもう開いていて待っていた窓を閉じるときに、窓を開け閉めする前に上がる。メイン画面のカードが選択を置き換える。
    /// </summary>
    public event Action<WidgetListItemViewModel, WidgetTargetOption>? UnspecifiedBuffCardResolved;

    /// <summary>自分のバフリスト(デバフリスト)の行のカードの項目。開き方はリストの行から開くのと同じく行のバフのまとまりで。</summary>
    private static WidgetTargetOption CreateSelfBuffCardOption(PlayerBuffListKind kind, PlayerBuffSnapshot snapshot)
    {
        return WidgetTargetOption.CreateSelfBuffCard(
            kind,
            snapshot.CardKey,
            CombatDataCatalog.GetBuffGroup(snapshot.BaseId),
            snapshot.NameWithoutInternalId);
    }

    /// <summary>
    /// 待っている窓に1行目が出た。そのカードがもう別の窓で開いていれば待っている窓を閉じ(もう開いている窓は前に出さない。
    /// ゲーム中に勝手に前面へ出るとゲームの操作を奪うため)、開いていなければ待っている窓をそのバフに入れる。どちらも
    /// <see cref="UnspecifiedBuffCardResolved"/> で知らせる。
    /// </summary>
    private void BuffCardViewModel_FirstBuffRowFound(
        BuffDebuffCardWidgetViewModel card,
        PlayerBuffListKind kind,
        PlayerBuffSnapshot snapshot)
    {
        var session = _openPlayerWindows.FirstOrDefault(
            candidate => ReferenceEquals(candidate.ViewModel, card));
        if (session is null || !card.IsWaiting)
        {
            return;
        }

        var option = CreateSelfBuffCardOption(kind, snapshot);
        var isAlreadyOpen = FindTargetWindow(session.Widget, WidgetTargetOption.CreateSelf(AppState.PlayerUID), option) is not null;

        // 知らせは閉じる・入れる前(閉じたときの当て直しが「指定なし」をほかの項目に置き換える前に、決まったカードへ替える)。
        UnspecifiedBuffCardResolved?.Invoke(session.Widget, option);

        if (isAlreadyOpen)
        {
            session.Window.Close();
        }
        else
        {
            card.Enter(option.BuffListKind, snapshot.CardKey, option.BuffGroup);
        }
    }

    public void OpenPlayerWindow(WidgetKind kind, long characterId)
    {
        if (_trackedPlayerWidgets.TryGetValue(kind, out var playerWidget))
        {
            OpenPlayerWindow(playerWidget, characterId);
        }
    }

    /// <summary>
    /// エンティティリスト(とターゲット情報の枠)から、その個体のバフリスト・デバフリストを開く。もう開いていれば前に出す。
    /// ターゲット情報は1枚だけの窓で、その敵を映す(<see cref="ShowEntityInTargetInfo"/>)。
    /// </summary>
    public void OpenEntityWindow(WidgetKind kind, EntityTargetInfo entity)
    {
        if (kind == WidgetKind.EntityInfo)
        {
            ShowEntityInTargetInfo(entity);
            return;
        }

        if (kind is not (WidgetKind.BuffList or WidgetKind.DebuffList)
            || !_trackedPlayerWidgets.TryGetValue(kind, out var widget))
        {
            return;
        }

        var existingWindow = FindEntityWindow(widget, entity, buff: null);

        if (existingWindow is not null)
        {
            existingWindow.ViewModel.TryApplyEntity(entity);
            existingWindow.Window.SetHeaderText(existingWindow.ViewModel.HeaderText);
            RestoreAndActivate(existingWindow.Window);
            RaiseTargetOpenedFromWidget(widget, CreateOpenTarget(existingWindow));
            return;
        }

        var createdSession = CreateEntityBuffWindow(widget, new EntityWindowTarget(entity));
        RaiseTargetOpenedFromWidget(widget, CreateOpenTarget(createdSession));
    }

    /// <summary>
    /// 複数開けるウィジェットの窓が開いた・閉じたときと、開いている窓のタイトルが変わったとき。
    /// メイン画面のカードが、選んでいる項目の欄(窓のタイトル)と起動ボタンを当て直す。
    /// </summary>
    public event Action<WidgetListItemViewModel>? TargetWindowsChanged;

    /// <summary>
    /// 相手(人・敵)と、バフ・デバフカードならバフを指定して、開いている窓を探す。開いていなければ null。
    /// 人は、自分(バフ・デバフカードの自分のカード)なら自分を映す窓(相手の指定なしで開いた窓を含む)、相手を選んでいない項目なら相手を持たない空の窓、
    /// それ以外は UID で探す。
    /// 敵は、その1体を映している窓、無ければその1体を捕まえてよい捕まえていない窓(<see cref="FindEntityWindow"/>)を探す。
    /// 捕まえていない窓の項目は、種類と種別IDが同じ捕まえていない窓を探す。
    /// チャットはチャンネル(プライベートトークは相手)で、プラグインの相手は相手の ID で探す。
    /// </summary>
    public WidgetWindow? FindTargetWindow(
        WidgetListItemViewModel widget,
        WidgetTargetOption person,
        WidgetTargetOption? buff)
    {
        if (person.Kind == WidgetTargetOptionKind.Plugin)
        {
            return FindPluginWindow(widget, person.PluginTargetId)?.Window;
        }

        if (person.Kind == WidgetTargetOptionKind.Chat)
        {
            return _openChatWindows
                .FirstOrDefault(session =>
                    ReferenceEquals(session.Widget, widget)
                    && session.ViewModel.Represents(person.ChatChannel, person.CharacterId))
                ?.Window;
        }

        if (person.Kind == WidgetTargetOptionKind.Entity)
        {
            if (person.Entity is { } entity)
            {
                return FindEntityWindow(widget, entity, buff)?.Window;
            }

            // 捕まえていない窓の項目は、まだ捕まえていない窓だけに当てる(捕まえた窓は捕まえた敵の項目で指す)。
            return _openEntityWindows
                .FirstOrDefault(session =>
                    ReferenceEquals(session.Widget, widget)
                    && !session.ViewModel.IsEntityAcquired
                    && session.ViewModel.EntityId == person.UnacquiredEntityId
                    && session.ViewModel.EntityType == person.UnacquiredEntityType
                    && RepresentsEntityBuff(session.ViewModel, buff))
                ?.Window;
        }

        return FindPlayerWindow(widget, person, buff)?.Window;
    }

    /// <summary>
    /// 人の窓を探す。自分(バフ・デバフカードの自分のカード)なら自分を映す窓、相手を選んでいない項目なら相手を持たない空の窓、
    /// それ以外は UID で探す。バフ・デバフカードは、そのバフのカードだけを探す(<paramref name="buff"/>)。
    /// </summary>
    private PlayerWidgetWindowSession? FindPlayerWindow(
        WidgetListItemViewModel widget,
        WidgetTargetOption person,
        WidgetTargetOption? buff)
    {
        return _openPlayerWindows
            .FirstOrDefault(session =>
                ReferenceEquals(session.Widget, widget)
                && (person.IsSelf
                    ? session.ViewModel.RepresentsSelf()
                    : person.IsUnselectedPlayer
                        ? buff is null && session.ViewModel.IsAwaitingPlayer
                        : session.ViewModel.RepresentsPlayer(person.CharacterId))
                && (buff is null
                    || (session.ViewModel is BuffDebuffCardWidgetViewModel card
                        && card.RepresentsBuff(buff.BuffListKind, buff.BuffKey, buff.BuffGroup))));
    }

    /// <summary>
    /// 相手(と、バフ・デバフカードならバフ)の窓を開く。もう開いていれば前に出す。
    /// 人は相手の UID で開き、相手を選んでいない項目なら相手を持たない空の窓を開く。バフ・デバフカードの自分のカードは相手の指定なしで開く。
    /// </summary>
    public void OpenTarget(
        WidgetListItemViewModel widget,
        WidgetTargetOption person,
        WidgetTargetOption? buff)
    {
        if (person.Kind == WidgetTargetOptionKind.Plugin)
        {
            if (FindTargetWindow(widget, person, buff) is { } existingPluginWindow)
            {
                RestoreAndActivate(existingPluginWindow);
            }
            else
            {
                CreatePluginTargetWindow(widget, person.PluginTargetId, person.PluginTargetNames ?? PluginTexts.Empty);
            }

            return;
        }

        if (FindTargetWindow(widget, person, buff) is { } existingWindow)
        {
            RestoreAndActivate(existingWindow);
            return;
        }

        if (person.Kind == WidgetTargetOptionKind.Chat)
        {
            CreateChatWindow(widget, person.ChatChannel, person.CharacterId, savedPartnerName: null);
            return;
        }

        if (person.Kind == WidgetTargetOptionKind.Entity)
        {
            var target = person.Entity is { } entity
                ? new EntityWindowTarget(entity)
                : new EntityWindowTarget(person.UnacquiredEntityId, person.UnacquiredEntityType, person.UnacquiredEntityName);

            if (buff is null)
            {
                CreateEntityBuffWindow(widget, target);
            }
            else
            {
                CreateEntityBuffCardWindow(widget, target, buff.BuffListKind, buff.BuffKey, buff.BuffGroup);
            }

            return;
        }

        long? requestedCharacterId = person.IsSelf || person.IsUnselectedPlayer ? null : person.CharacterId;
        if (buff is null)
        {
            CreatePlayerWindow(widget, requestedCharacterId);
        }
        else
        {
            CreatePlayerWindow(widget, requestedCharacterId, buff.BuffListKind, buff.BuffKey, buffGroup: buff.BuffGroup);
        }
    }

    /// <summary>相手(と、バフ・デバフカードならバフ)の窓を閉じる。開いていなければ何もしない。</summary>
    public void CloseTarget(
        WidgetListItemViewModel widget,
        WidgetTargetOption person,
        WidgetTargetOption? buff)
    {
        FindTargetWindow(widget, person, buff)?.Close();
    }

    /// <summary>
    /// そのウィジェットに、相手を持たない空の窓(<see cref="PlayerWidgetWindowViewModel.IsAwaitingPlayer"/>)が開いているか。
    /// </summary>
    public bool HasAwaitingPlayerWindow(WidgetListItemViewModel widget)
    {
        return FindAwaitingPlayerWindow(widget) is not null;
    }

    /// <summary>
    /// 空の窓のある一覧にプレイヤーが出た(引数は一覧で先頭のプレイヤーの UID)。空の窓をその人の窓にする。
    /// その人の窓がもう別の窓で開いていれば、空の窓を閉じる(もう開いている窓は前に出さない。ゲーム中に勝手に前面へ出ると
    /// ゲームの操作を奪うため)。どちらもその人の窓の項目で <see cref="TargetOpenedFromWidget"/> を上げ、カードがその人を選ぶ。
    /// バフ・デバフカードの待っている窓(<see cref="BuffCardViewModel_FirstBuffRowFound"/>)と同じ形。
    /// </summary>
    public void EnterAwaitingPlayerWindow(WidgetListItemViewModel widget, long characterId)
    {
        if (characterId == 0 || FindAwaitingPlayerWindow(widget) is not { } session)
        {
            return;
        }

        var existing = _openPlayerWindows.FirstOrDefault(other =>
            !ReferenceEquals(other, session)
            && ReferenceEquals(other.Widget, widget)
            && other.ViewModel.RepresentsPlayer(characterId));
        if (existing is not null)
        {
            // 閉じてから知らせる(先に知らせると、知らせの当て直しがまだ開いている空の窓でここへ戻ってくる)。
            session.Window.Close();
            RaiseTargetOpenedFromWidget(widget, CreateOpenTarget(existing));
            return;
        }

        session.ViewModel.EnterPlayer(characterId);
        if (_latestPlayerWindowRoster is { } latest)
        {
            session.ViewModel.UpdatePlayerFromRoster(latest.PlayersByCharacterId, latest.SelfPlayer);
        }

        session.Window.SetHeaderText(session.ViewModel.HeaderText);
        RaiseTargetOpenedFromWidget(widget, CreateOpenTarget(session));
    }

    private PlayerWidgetWindowSession? FindAwaitingPlayerWindow(WidgetListItemViewModel widget)
    {
        return _openPlayerWindows.FirstOrDefault(session =>
            ReferenceEquals(session.Widget, widget) && session.ViewModel.IsAwaitingPlayer);
    }

    /// <summary>
    /// 開いている窓の相手(と、バフ・デバフカードならバフ)。メイン画面のカードの選択リストに、名簿などから外れた相手も並べるのに使う。
    /// 項目は相手を指す値だけを持ち、表示の文字は持たない。実体を捕まえていない敵の窓は種類と種別IDで指す。
    /// 人の空の窓とプラグインの空の窓は相手を選んでいない項目、最初に付いたものを待つカードは「指定なし」で指す。
    /// </summary>
    public IReadOnlyList<(WidgetTargetOption Person, WidgetTargetOption? Buff)> GetOpenTargets(WidgetListItemViewModel widget)
    {
        var targets = new List<(WidgetTargetOption Person, WidgetTargetOption? Buff)>();
        foreach (var session in EnumerateTargetSessions(widget))
        {
            targets.Add(CreateOpenTarget(session));
        }

        return targets;
    }

    /// <summary>開いている窓1枚の相手(と、バフ・デバフカードならバフ)。種類ごとの作り方へ振り分ける。</summary>
    private static (WidgetTargetOption Person, WidgetTargetOption? Buff) CreateOpenTarget(TargetWindowSession session)
    {
        return session switch
        {
            PlayerWidgetWindowSession player => CreateOpenTarget(player),
            EntityWindowSession entity => CreateOpenTarget(entity),
            ChatWindowSession chat => (WidgetTargetOption.CreateChat(chat.ViewModel.Channel, chat.ViewModel.PartnerId), null),
            PluginTargetWindowSession plugin => (
                new WidgetTargetOption
                {
                    Kind = WidgetTargetOptionKind.Plugin,
                    PluginTargetId = plugin.TargetId,
                    PluginTargetNames = plugin.TargetNames
                },
                null),
            _ => throw new ArgumentOutOfRangeException(nameof(session), session, null)
        };
    }

    /// <summary>
    /// 開いている複数開けるウィジェットの窓(人・敵・チャット・プラグイン、この並び)。<paramref name="widget"/> を渡すと、そのウィジェットの窓だけ。
    /// 回している間に窓を開け閉めする所は、先に写し(<c>ToArray</c>)を作る。
    /// </summary>
    private IEnumerable<TargetWindowSession> EnumerateTargetSessions(WidgetListItemViewModel? widget = null)
    {
        foreach (var session in _openPlayerWindows)
        {
            if (widget is null || ReferenceEquals(session.Widget, widget))
            {
                yield return session;
            }
        }

        foreach (var session in _openEntityWindows)
        {
            if (widget is null || ReferenceEquals(session.Widget, widget))
            {
                yield return session;
            }
        }

        foreach (var session in _openChatWindows)
        {
            if (widget is null || ReferenceEquals(session.Widget, widget))
            {
                yield return session;
            }
        }

        foreach (var session in _openPluginWindows)
        {
            if (widget is null || ReferenceEquals(session.Widget, widget))
            {
                yield return session;
            }
        }
    }

    /// <summary>
    /// 開いている人の窓1枚の相手(と、バフ・デバフカードならバフ)。選択リストに並べる項目と同じ値。
    /// 人は相手の UID の項目で、相手を持たない空の窓は相手を選んでいない項目。バフ・デバフカードの相手の指定なしの窓は自分(自分のカード)。
    /// </summary>
    private static (WidgetTargetOption Person, WidgetTargetOption? Buff) CreateOpenTarget(PlayerWidgetWindowSession session)
    {
        var viewModel = session.ViewModel;
        if (viewModel is not BuffDebuffCardWidgetViewModel card)
        {
            return (viewModel.RequestedCharacterId is { } requestedCharacterId
                ? WidgetTargetOption.CreatePlayer(requestedCharacterId)
                : WidgetTargetOption.CreateUnselectedPlayer(), null);
        }

        var person = viewModel.RequestedCharacterId is { } characterId
            ? WidgetTargetOption.CreatePlayer(characterId)
            : WidgetTargetOption.CreateSelf(viewModel.LastKnownPlayerUid);

        // 最初に付いたものを待っている窓は「指定なし」。
        var buff = card.IsWaiting
            ? WidgetTargetOption.CreateUnspecifiedBuffCard()
            : new WidgetTargetOption
            {
                Kind = WidgetTargetOptionKind.Buff,
                IsSelf = person.IsSelf,
                BuffListKind = card.BuffListKind,
                BuffKey = card.RequestedBuffKey,
                BuffGroup = card.Group,
                BuffName = card.LastKnownBuffName ?? string.Empty,
                BuffPerson = person
            };
        return (person, buff);
    }

    /// <summary>開いている敵の窓1枚の相手(と、バフ・デバフカードならバフ)。選択リストに並べる項目と同じ値。</summary>
    private static (WidgetTargetOption Person, WidgetTargetOption? Buff) CreateOpenTarget(EntityWindowSession session)
    {
        var viewModel = session.ViewModel;
        var person = viewModel.Entity is { } entity
            ? new WidgetTargetOption { Kind = WidgetTargetOptionKind.Entity, Entity = entity }
            : new WidgetTargetOption
            {
                Kind = WidgetTargetOptionKind.Entity,
                UnacquiredEntityId = viewModel.EntityId,
                UnacquiredEntityType = viewModel.EntityType,
                UnacquiredEntityName = viewModel.TargetName
            };
        var buff = viewModel is EntityBuffDebuffCardWidgetViewModel card
            ? new WidgetTargetOption
            {
                Kind = WidgetTargetOptionKind.Buff,
                BuffListKind = card.BuffListKind,
                BuffKey = card.RequestedBuffKey,
                BuffGroup = card.Group,
                BuffName = card.LastKnownBuffName ?? string.Empty,
                BuffPerson = person
            }
            : null;
        return (person, buff);
    }

    /// <summary>
    /// カード以外(ほかのウィジェットの行・メニュー)から、複数開けるウィジェットの窓を開いたか、開いている窓を前に出した
    /// (引数はウィジェットと、その窓の選択リストの項目)。空の窓がその人の窓になったときと、その人の窓がもう開いていて
    /// 空の窓を閉じたとき(<see cref="EnterAwaitingPlayerWindow"/>)も上げる。メイン画面のカードがその項目を選ぶ。
    /// </summary>
    public event Action<WidgetListItemViewModel, WidgetTargetOption>? TargetOpenedFromWidget;

    /// <summary>その窓の項目で <see cref="TargetOpenedFromWidget"/> を上げる。バフ・デバフカードはカードの項目、ほかは相手の項目。</summary>
    private void RaiseTargetOpenedFromWidget(
        WidgetListItemViewModel widget,
        (WidgetTargetOption Person, WidgetTargetOption? Buff) target)
    {
        var option = widget.Kind == WidgetKind.BuffDebuffCard ? target.Buff : target.Person;
        if (option is not null)
        {
            TargetOpenedFromWidget?.Invoke(widget, option);
        }
    }

    /// <summary>
    /// 敵の1体を映す窓。その1体を映している窓を先に探し、無ければ、その1体を捕まえてよい、まだ捕まえていない窓(<see cref="CanCaptureEntity"/>)。
    /// バフ・デバフカードは、そのバフのカードだけを探す(<paramref name="buff"/>)。
    /// </summary>
    private EntityWindowSession? FindEntityWindow(
        WidgetListItemViewModel widget,
        EntityTargetInfo entity,
        WidgetTargetOption? buff)
    {
        return _openEntityWindows.FirstOrDefault(session =>
                ReferenceEquals(session.Widget, widget)
                && session.ViewModel.RepresentsEntity(entity.EntityUuid)
                && RepresentsEntityBuff(session.ViewModel, buff))
            ?? _openEntityWindows.FirstOrDefault(session =>
                ReferenceEquals(session.Widget, widget)
                && RepresentsEntityBuff(session.ViewModel, buff)
                && CanCaptureEntity(session, entity));
    }

    /// <summary>
    /// まだ捕まえていない敵の窓が、その1体を捕まえてよいか。種類と種別IDが合い、同じウィジェットのほかの窓
    /// (バフ・デバフカードは同じバフのカード)がその1体を映していないこと。
    /// </summary>
    private bool CanCaptureEntity(EntityWindowSession session, EntityTargetInfo entity)
    {
        return session.ViewModel.CanCaptureEntity(entity)
            && !_openEntityWindows.Any(other =>
                !ReferenceEquals(other, session)
                && ReferenceEquals(other.Widget, session.Widget)
                && other.ViewModel.RepresentsEntity(entity.EntityUuid)
                && IsSameEntityWindowBuff(other.ViewModel, session.ViewModel));
    }

    /// <summary>敵の窓がそのバフの窓か。バフを指定しなければ、どの窓も当たる。</summary>
    private static bool RepresentsEntityBuff(IEntityWidgetWindowViewModel viewModel, WidgetTargetOption? buff)
    {
        return buff is null
            || (viewModel is EntityBuffDebuffCardWidgetViewModel card
                && card.RepresentsBuff(buff.BuffListKind, buff.BuffKey, buff.BuffGroup));
    }

    /// <summary>敵の窓2枚が同じバフの窓か。バフリスト・デバフリストの窓どうしは同じ、バフ・デバフカードは同じバフのカードどうし。</summary>
    private static bool IsSameEntityWindowBuff(IEntityWidgetWindowViewModel left, IEntityWidgetWindowViewModel right)
    {
        return (left, right) switch
        {
            (EntityBuffDebuffCardWidgetViewModel leftCard, EntityBuffDebuffCardWidgetViewModel rightCard)
                => leftCard.RepresentsBuff(rightCard.BuffListKind, rightCard.RequestedBuffKey, rightCard.Group),
            (EntityBuffDebuffCardWidgetViewModel, _) or (_, EntityBuffDebuffCardWidgetViewModel) => false,
            _ => true
        };
    }

    public void ApplyWidgetState(WidgetListItemViewModel widget)
    {
        if (widget.PluginWidget is { IsMultiWindow: true })
        {
            ApplyPluginMultiWidgetState(widget);
            return;
        }

        if (WidgetConfigDefaults.IsPlayerWindowKind(widget.Kind))
        {
            ApplyPlayerWindowWidgetState(widget);
            return;
        }

        if (widget.State == WidgetState.Running)
        {
            Open(widget);
            return;
        }

        Close(widget);
    }

    /// <summary>
    /// プラグインのウィジェットの窓を、保存した状態(起動中と開いていた相手)を変えずに全部閉じる。プラグインを止めてカードを外すときに使い、次の起動で開き直す。
    /// </summary>
    public void CloseKeepingState(WidgetListItemViewModel widget)
    {
        if (_trackedPluginWidgets.Remove(widget))
        {
            widget.PlayerWindowPresentationChanged -= PlayerWidget_PresentationChanged;
        }

        _isClosingKeepingState = true;
        try
        {
            foreach (var window in EnumerateOpenWindows(widget).ToArray())
            {
                window.Close();
            }
        }
        finally
        {
            _isClosingKeepingState = false;
        }
    }

    public void ApplyWidgetPinState(WidgetListItemViewModel widget)
    {
        // ピン留め中はアクティブにしない。WS_EX_NOACTIVATE が防ぐのは
        // クリックによるアクティブ化で、ここで Activate() を呼ぶと
        // こちらからフォーカスを奪ってしまう。
        foreach (var window in EnumerateOpenWindows(widget).ToArray())
        {
            window.ApplyPinState(widget.IsPinned);
        }
    }

    public void OpenPlayerWindow(WidgetListItemViewModel playerWidget, long characterId)
    {
        if (!WidgetConfigDefaults.IsPlayerWindowKind(playerWidget.Kind))
        {
            return;
        }

        if (WidgetConfigDefaults.IsSelfOnlyPlayerWindowKind(playerWidget.Kind))
        {
            var openSelfWindow = _openPlayerWindows
                .FirstOrDefault(session => ReferenceEquals(session.Widget, playerWidget));

            if (openSelfWindow is not null)
            {
                RestoreAndActivate(openSelfWindow.Window);
                return;
            }

            CreatePlayerWindow(playerWidget, requestedCharacterId: null);
            return;
        }

        var existingWindow = FindPlayerWindow(playerWidget, WidgetTargetOption.CreatePlayer(characterId), buff: null);

        if (existingWindow is not null)
        {
            RestoreAndActivate(existingWindow.Window);
            RaiseTargetOpenedFromWidget(playerWidget, CreateOpenTarget(existingWindow));
            return;
        }

        var createdSession = CreatePlayerWindow(playerWidget, characterId);
        RaiseTargetOpenedFromWidget(playerWidget, CreateOpenTarget(createdSession));
    }

    /// <summary>
    /// バフ/デバフ一覧の行から、そのバフだけを出すカードを開く。
    ///
    /// <para>
    /// 1ウィンドウ = 1プレイヤー × 1バフ。すでに同じ組み合わせが開いていれば前面に出すだけ。
    /// </para>
    /// </summary>
    public void OpenBuffCardForPlayer(
        long characterId,
        PlayerBuffListKind kind,
        string buffKey,
        int baseId)
    {
        if (string.IsNullOrWhiteSpace(buffKey))
        {
            return;
        }

        // 料理・薬剤は食べ直すたびに別のIDへ入れ替わる。まとまりとして追う。
        OpenBuffCardForPlayer(characterId, kind, buffKey, CombatDataCatalog.GetBuffGroup(baseId), savedBuffName: null);
    }

    /// <summary>
    /// バフ・デバフグループの行から、その行のカードを開く。バフが付いていない行からも開けるので、
    /// カードが覚える名前(まとまりならまとまりの名前、バフなら内部IDの注記なしの名前)を種に入れる。
    /// </summary>
    public void OpenBuffCardForGroupEntry(long characterId, PlayerBuffListKind kind, BuffEntryKey key)
    {
        if (!key.IsValid)
        {
            return;
        }

        var buffName = key.IsGroup
            ? BuffDebuffCardContent.GetGroupName(key.Group)
            : CombatDataCatalog.GetBuffDisplayNameWithoutInternalId(key.BuffId, SourceLanding.None);
        OpenBuffCardForPlayer(
            characterId,
            kind,
            key.IsGroup ? null : MeterSnapshotProvider.CreateBuffCardKey(key.BuffId),
            key.Group,
            buffName);
    }

    /// <param name="buffKey">追うバフのカードの鍵。まとまり(<paramref name="group"/>)を追うカードは使わないので null でよい。</param>
    /// <param name="savedBuffName">新しく開くカードに入れる名前の種(<see cref="BuffDebuffCardWidgetViewModel.SeedLastKnownBuffName"/>)。</param>
    private void OpenBuffCardForPlayer(
        long characterId,
        PlayerBuffListKind kind,
        string? buffKey,
        BuffGroup group,
        string? savedBuffName)
    {
        if (!_trackedPlayerWidgets.TryGetValue(WidgetKind.BuffDebuffCard, out var widget))
        {
            return;
        }

        var existingWindow = FindPlayerWindow(widget, WidgetTargetOption.CreatePlayer(characterId), WidgetTargetOption.CreateBuff(kind, buffKey, group));

        if (existingWindow is not null)
        {
            RestoreAndActivate(existingWindow.Window);
            RaiseTargetOpenedFromWidget(widget, CreateOpenTarget(existingWindow));
            return;
        }

        var createdSession = CreatePlayerWindow(widget, characterId, kind, buffKey, buffGroup: group, savedBuffName: savedBuffName);
        RaiseTargetOpenedFromWidget(widget, CreateOpenTarget(createdSession));
    }

    /// <summary>モンスターのバフ/デバフ一覧の行から開く版。1ウィンドウ = 1体 × 1バフ。</summary>
    public void OpenBuffCardForEntity(
        EntityWindowTarget source,
        PlayerBuffListKind kind,
        string buffKey,
        int baseId)
    {
        if (string.IsNullOrWhiteSpace(buffKey)
            || source.Entity is not { } entity
            || !_trackedPlayerWidgets.TryGetValue(WidgetKind.BuffDebuffCard, out var widget))
        {
            return;
        }

        var group = CombatDataCatalog.GetBuffGroup(baseId);

        var existingWindow = FindEntityWindow(widget, entity, WidgetTargetOption.CreateBuff(kind, buffKey, group));

        if (existingWindow is not null)
        {
            existingWindow.ViewModel.TryApplyEntity(entity);
            existingWindow.Window.SetHeaderText(existingWindow.ViewModel.HeaderText);
            RestoreAndActivate(existingWindow.Window);
            RaiseTargetOpenedFromWidget(widget, CreateOpenTarget(existingWindow));
            return;
        }

        var createdSession = CreateEntityBuffCardWindow(widget, new EntityWindowTarget(entity), kind, buffKey, group);
        RaiseTargetOpenedFromWidget(widget, CreateOpenTarget(createdSession));
    }

    /// <summary>
    /// 保存しておいた対象で窓を1枚開き直す。
    ///
    /// <para>
    /// 実体の窓は<b>未捕獲の状態</b>で開く。実体IDは再起動で消えるため、
    /// 種類と種別が一致する個体(同じウィジェットのほかの窓が映していないもの)がAOIに現れた時点で
    /// <see cref="UpdateEntityWindowPresentations"/> が捕まえる。
    /// </para>
    ///
    /// <para>
    /// 同じ相手の窓がもう開いていれば開かない(人・チャット。敵の窓は同じ種類の窓が別々の個体を捕まえるので除く)。
    /// 前の版のファイルには、自分の窓(相手の指定なし、最後の相手 X)と X の窓が両方入っていることがあり、どちらも X の窓として開くため。
    /// 値が壊れていて戻せない項目は、ログに書いて飛ばす。
    /// </para>
    /// </summary>
    private void RestoreTargetWindow(WidgetListItemViewModel widget, WidgetOpenTargetConfig target)
    {
        if (widget.Kind == WidgetKind.Chat)
        {
            if (target.ChatChannel is not { } chatChannelValue
                || !Enum.IsDefined(typeof(Zproto.ChitChatChannelType), chatChannelValue)
                || !ChatMessageStore.IsKeptChannel((Zproto.ChitChatChannelType)chatChannelValue))
            {
                Log.Warning(
                    "Skipped a saved {WidgetKey} window: the chat channel {ChatChannel} cannot be restored",
                    widget.Key.DocumentKey,
                    target.ChatChannel);
                return;
            }

            var chatChannel = (Zproto.ChitChatChannelType)chatChannelValue;
            var partnerId = chatChannel == Zproto.ChitChatChannelType.ChannelPrivate ? target.CharacterId ?? 0 : 0;
            if (chatChannel == Zproto.ChitChatChannelType.ChannelPrivate && partnerId == 0)
            {
                Log.Warning(
                    "Skipped a saved {WidgetKey} window: the private chat has no partner",
                    widget.Key.DocumentKey);
                return;
            }

            if (FindTargetWindow(widget, WidgetTargetOption.CreateChat(chatChannel, partnerId), buff: null) is not null)
            {
                return;
            }

            CreateChatWindow(widget, chatChannel, partnerId, target.Name, target.Window);
            return;
        }

        var buffListKind = target.BuffListKind is { } value
            && Enum.IsDefined(typeof(PlayerBuffListKind), value)
                ? (PlayerBuffListKind)value
                : (PlayerBuffListKind?)null;

        var buffGroup = target.BuffGroup is { } groupValue
            && Enum.IsDefined(typeof(BuffGroup), groupValue)
                ? (BuffGroup)groupValue
                : BuffGroup.None;

        if (!target.IsEntity)
        {
            // 相手の指定なしで保存した人の窓(前の版の自分の窓)は、最後に映した相手の窓として開く。最後の相手が無ければ空の窓。
            // バフ・デバフカードの相手の指定なしは自分のカードなので、そのまま。
            var requestedCharacterId = target.CharacterId is null && widget.Kind != WidgetKind.BuffDebuffCard
                ? target.ResolvedCharacterId
                : target.CharacterId;
            var person = requestedCharacterId is { } characterId
                ? WidgetTargetOption.CreatePlayer(characterId)
                : widget.Kind == WidgetKind.BuffDebuffCard
                    ? WidgetTargetOption.CreateSelf(AppState.PlayerUID)
                    : WidgetTargetOption.CreateUnselectedPlayer();
            var buff = widget.Kind == WidgetKind.BuffDebuffCard
                ? WidgetTargetOption.CreateBuff(buffListKind ?? PlayerBuffListKind.Buff, target.BuffKey, buffGroup)
                : null;
            if (FindTargetWindow(widget, person, buff) is not null)
            {
                return;
            }

            CreatePlayerWindow(
                widget,
                requestedCharacterId,
                buffListKind,
                target.BuffKey,
                target.Name,
                buffGroup,
                target.ResolvedCharacterId,
                target.BuffName,
                target.Window,
                target.IsNpc == true,
                target.ProfessionId ?? 0);
            return;
        }

        var entityType = target.EntityType is { } typeValue && Enum.IsDefined(typeof(Zproto.EEntityType), typeValue)
            ? (Zproto.EEntityType)typeValue
            : (Zproto.EEntityType?)null;
        var entityTarget = new EntityWindowTarget(target.EntityId, entityType, target.Name);

        if (widget.Kind != WidgetKind.BuffDebuffCard)
        {
            CreateEntityBuffWindow(widget, entityTarget, target.Window);
            return;
        }

        if (buffGroup == BuffGroup.None && string.IsNullOrWhiteSpace(target.BuffKey))
        {
            Log.Warning(
                "Skipped a saved {WidgetKey} window: the enemy buff card has no buff",
                widget.Key.DocumentKey);
            return;
        }

        CreateEntityBuffCardWindow(
            widget,
            entityTarget,
            buffListKind ?? PlayerBuffListKind.Buff,
            target.BuffKey,
            buffGroup,
            target.BuffName,
            target.Window);
    }

    /// <summary>
    /// 開いている窓の対象一覧を書き出す。窓が増減したときだけ呼ぶ。
    ///
    /// <para>
    /// アプリ終了時とプラグインを止めたときは呼ばない。どちらも全部の窓を閉じるので、
    /// <b>そこで保存すると次回に復元するものが消える。</b>
    /// </para>
    ///
    /// <para>
    /// 窓が無ければ記録なし(<c>null</c>)。窓はあるのにどれも保存できない(種別ID 0 の未知の敵の窓だけ)ときは空の一覧で、
    /// 次の起動で既定の窓を開かない(<see cref="ApplyPlayerWindowWidgetState"/>)。
    /// </para>
    /// </summary>
    private void SaveOpenTargets(WidgetListItemViewModel widget)
    {
        if (_isManagerClosing || _isClosingKeepingState || !widget.SupportsTargetSelection)
        {
            return;
        }

        var targets = new List<WidgetOpenTargetConfig>();
        foreach (var session in EnumerateTargetSessions(widget))
        {
            if (CreateSavedTarget(session) is { } target)
            {
                targets.Add(target);
            }
        }

        WidgetStateManager.Instance.SaveWidgetOpenTargets(
            widget.Key,
            targets.Count == 0 && !HasOpenTargetWindows(widget) ? null : targets);
    }

    /// <summary>窓1枚の保存する値。保存できない窓(種別ID 0 の未知の敵の窓)は null。</summary>
    private static WidgetOpenTargetConfig? CreateSavedTarget(TargetWindowSession session)
    {
        switch (session)
        {
            case PlayerWidgetWindowSession player:
            {
                var viewModel = player.ViewModel;
                var card = viewModel as BuffDebuffCardWidgetViewModel;
                return new WidgetOpenTargetConfig
                {
                    CharacterId = viewModel.RequestedCharacterId,
                    Name = viewModel.LastKnownPlayerName,
                    ResolvedCharacterId = viewModel.LastKnownPlayerUid == 0
                        ? null
                        : viewModel.LastKnownPlayerUid,
                    IsNpc = viewModel.LastKnownIsNpc ? true : null,
                    ProfessionId = viewModel.LastKnownProfessionId == 0
                        ? null
                        : viewModel.LastKnownProfessionId,
                    BuffListKind = card is null ? null : (int)card.BuffListKind,
                    BuffKey = card?.RequestedBuffKey,
                    BuffName = card?.LastKnownBuffName,
                    BuffGroup = card is null ? null : (int)card.Group,
                    Window = player.Window.GetCurrentBounds()
                };
            }

            case EntityWindowSession entity:
            {
                var viewModel = entity.ViewModel;

                // 種別ID 0(未知の敵)の窓は保存しない。復元は種類と種別IDで個体を捕まえるので、
                // 0 のまま戻すと次の起動で別の未知の個体を捕まえる。
                if (viewModel.EntityId == 0)
                {
                    return null;
                }

                var card = viewModel as EntityBuffDebuffCardWidgetViewModel;
                return new WidgetOpenTargetConfig
                {
                    EntityId = viewModel.EntityId,
                    EntityType = viewModel.EntityType is { } entityType ? (int)entityType : null,
                    Name = viewModel.TargetName,
                    BuffListKind = card is null ? null : (int)card.BuffListKind,
                    BuffKey = card?.RequestedBuffKey,
                    BuffName = card?.LastKnownBuffName,
                    BuffGroup = card is null ? null : (int)card.Group,
                    Window = entity.Window.GetCurrentBounds()
                };
            }

            case ChatWindowSession chat:
            {
                var isPrivate = chat.ViewModel.Channel == Zproto.ChitChatChannelType.ChannelPrivate;
                return new WidgetOpenTargetConfig
                {
                    ChatChannel = (int)chat.ViewModel.Channel,
                    CharacterId = isPrivate ? chat.ViewModel.PartnerId : null,
                    Name = isPrivate ? chat.ViewModel.LastKnownPartnerName : null,
                    Window = chat.Window.GetCurrentBounds()
                };
            }

            case PluginTargetWindowSession plugin:
                return new WidgetOpenTargetConfig
                {
                    PluginTargetId = plugin.TargetId,
                    Name = plugin.TargetName,
                    Window = plugin.Window.GetCurrentBounds()
                };

            default:
                throw new ArgumentOutOfRangeException(nameof(session), session, null);
        }
    }

    private void ApplyPlayerWindowWidgetState(WidgetListItemViewModel widget)
    {
        if (widget.State == WidgetState.Running)
        {
            if (HasOpenTargetWindows(widget))
            {
                UpdatePlayerWindowCount(widget);
                return;
            }

            var savedTargets = WidgetStateManager.Instance
                .GetWidgetSnapshot(widget.Kind)
                .OpenTargets;

            if (savedTargets is not null)
            {
                foreach (var target in savedTargets)
                {
                    RestoreTargetWindow(widget, target);
                }

                // 戻せる窓が無かった(保存できない窓だけを開いていた、または全部戻せなかった)。既定の窓は開かずに止め、記録も消す
                // (残すと「すべてを起動」などで起動しても毎回ここで止まる。記録が無ければ下の記録の無い経路になる)。
                if (!HasOpenTargetWindows(widget))
                {
                    // 空の一覧は保存できない窓だけを開いていた形(設計どおり)。項目があったのに1枚も戻せなかったときだけ書く。
                    if (savedTargets.Count > 0)
                    {
                        Log.Warning(
                            "Stopped {WidgetKey}: none of the {Count} saved windows could be restored; cleared the saved windows",
                            widget.Key.DocumentKey,
                            savedTargets.Count);
                    }

                    WidgetStateManager.Instance.SaveWidgetOpenTargets(widget.Key, null);
                    widget.State = WidgetState.Stopped;
                }

                return;
            }

            // 対象の記録が無い。お気に入りの一括起動などで、記録の無いまま起動中になったときの経路
            // (カードの起動ボタンは選んでいる項目の窓を開く別の経路で、ここを通らない)。
            // チャットはカードの既定と同じチャンネル。
            if (widget.Kind == WidgetKind.Chat)
            {
                CreateChatWindow(widget, WidgetTargetSelection.DefaultChatChannel, 0, savedPartnerName: null);
                return;
            }

            if (widget.Kind == WidgetKind.BuffDebuffCard)
            {
                // 自分の料理と薬剤を1枚ずつ。どちらも対象が居なくても
                // 「バフカード(料理)」「バフカード(薬剤)」と名乗れるので、空でも何の枠か分かる。
                CreatePlayerWindow(
                    widget,
                    requestedCharacterId: null,
                    buffGroup: BuffGroup.Cuisine);
                CreatePlayerWindow(
                    widget,
                    requestedCharacterId: null,
                    buffGroup: BuffGroup.Potion);
                return;
            }

            // 人の窓とメーターの種類は相手を持たない空の窓(一覧にプレイヤーが出たらその人の窓になる)、1枚だけの窓は自分の窓。
            CreatePlayerWindow(widget, requestedCharacterId: null);
            return;
        }

        ClosePlayerWindows(widget);
    }

    /// <summary>
    /// プラグインの複数起動のウィジェットの起動中と停止を窓へ当てる。起動中で窓が無ければ、保存した相手の窓を開き直す(今の一覧に無い相手と空の窓も開く)。
    /// 保存が無ければカードで選んでいる相手を開き、相手も選んでいなければ空の窓を開く。
    /// </summary>
    private void ApplyPluginMultiWidgetState(WidgetListItemViewModel widget)
    {
        if (widget.State != WidgetState.Running)
        {
            ClosePlayerWindows(widget);
            return;
        }

        if (HasOpenTargetWindows(widget))
        {
            UpdatePlayerWindowCount(widget);
            return;
        }

        var savedTargets = WidgetStateManager.Instance
            .GetWidgetSnapshot(widget.Key)
            .OpenTargets;
        if (savedTargets is { Count: > 0 })
        {
            foreach (var target in savedTargets)
            {
                // 同じ相手(空の窓どうしも)の窓は2枚開かない。
                var targetId = string.IsNullOrWhiteSpace(target.PluginTargetId) ? null : target.PluginTargetId;
                if (FindPluginWindow(widget, targetId) is not null)
                {
                    continue;
                }

                CreatePluginTargetWindow(
                    widget,
                    targetId,
                    CreateSavedTargetNames(target.Name),
                    target.Window);
            }

            return;
        }

        var selected = widget.SelectedTargetOption;
        CreatePluginTargetWindow(widget, selected?.PluginTargetId, selected?.PluginTargetNames ?? PluginTexts.Empty);
    }

    /// <summary>保存しておいた相手の名前。今の言語の文字として持つ(どの言語でもこの名前に落ちる)。名前が無ければ空。</summary>
    private static IReadOnlyDictionary<string, string> CreateSavedTargetNames(string? name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? PluginTexts.Empty
            : new Dictionary<string, string> { [LocalizationManager.Instance.CurrentCulture.Name] = name };
    }

    /// <summary>プラグインの空の窓が開いているか。開いている間は、相手が出るのを見るために一覧を読み続ける。</summary>
    public bool HasEmptyPluginWindow(WidgetListItemViewModel widget)
    {
        return FindPluginWindow(widget, targetId: null) is not null;
    }

    /// <summary>プラグインの窓を相手の ID で探す。<c>null</c> は空の窓(相手が出たら中身を入れる窓)。</summary>
    private PluginTargetWindowSession? FindPluginWindow(WidgetListItemViewModel widget, string? targetId)
    {
        return _openPluginWindows.FirstOrDefault(session =>
            ReferenceEquals(session.Widget, widget)
            && WidgetTargetOption.IsSamePluginTargetId(session.TargetId, targetId));
    }

    /// <summary>読んだ相手の一覧を、開いている窓へ当てる(名前と、空の窓の相手)。</summary>
    public void ApplyPluginTargets(WidgetListItemViewModel widget, IReadOnlyList<PluginSdk.PluginWidgetTarget> targets)
    {
        UpdatePluginTargetNames(widget, targets);
        FillEmptyPluginWindow(widget, targets);
    }

    /// <summary>
    /// 一覧に相手が出た。空の窓に既定の相手(プラグインの指定、無ければ1番目)の中身を入れる。その相手の窓がもう開いていれば、空の窓を閉じる。
    /// </summary>
    private void FillEmptyPluginWindow(WidgetListItemViewModel widget, IReadOnlyList<PluginSdk.PluginWidgetTarget> targets)
    {
        if (targets.Count == 0
            || FindPluginWindow(widget, targetId: null) is not { } empty)
        {
            return;
        }

        var target = targets.FirstOrDefault(candidate => candidate.IsDefault) ?? targets[0];
        if (FindPluginWindow(widget, target.Id) is not null)
        {
            empty.Window.Close();
            return;
        }

        // 中身を作れないのはプラグインの例外のとき。受け止めがプラグインを止め、この窓も閉じる。
        if (!widget.PluginWidget!.TrySetWindowContent(empty.ViewModel, widget, target.Id))
        {
            widget.State = WidgetState.Stopped;
            WidgetStateManager.Instance.SaveWidgetState(widget.Key, WidgetState.Stopped);
            return;
        }

        var viewModel = empty.ViewModel;
        empty.BodyHost.SetElement(viewModel.Body!);
        if (viewModel.Footer is { } footer)
        {
            empty.FooterHost?.SetElement(footer);
        }

        if (viewModel.HeaderButtons.Count > 0)
        {
            empty.Window.SetHeaderActions(
                new PluginWidgetHeaderLabelsView { DataContext = viewModel },
                new PluginWidgetHeaderActionsView { DataContext = viewModel });
        }

        empty.TargetId = target.Id;
        empty.TargetNames = target.Names;
        empty.Window.SetHeaderText(empty.HeaderText);
        SaveOpenTargets(widget);
        TargetWindowsChanged?.Invoke(widget);
    }

    /// <summary>
    /// 読んだ相手の一覧の名前を、開いている窓へ当てる。一覧に無い相手の窓は最後に分かっていた名前のまま。
    /// 今の言語の名前が変わった窓のタイトルを当て直し、次の起動のタイトルに使う名前を保存する。
    /// </summary>
    private void UpdatePluginTargetNames(WidgetListItemViewModel widget, IReadOnlyList<PluginSdk.PluginWidgetTarget> targets)
    {
        var hasChangedName = false;
        foreach (var session in _openPluginWindows)
        {
            if (!ReferenceEquals(session.Widget, widget) || session.TargetId is null)
            {
                continue;
            }

            var target = targets.FirstOrDefault(
                candidate => WidgetTargetOption.IsSamePluginTargetId(candidate.Id, session.TargetId));
            if (target is null)
            {
                continue;
            }

            var previousName = session.TargetName;
            session.TargetNames = target.Names;
            if (!string.Equals(previousName, session.TargetName, StringComparison.Ordinal))
            {
                session.Window.SetHeaderText(session.HeaderText);
                hasChangedName = true;
            }
        }

        if (hasChangedName)
        {
            SaveOpenTargets(widget);
            TargetWindowsChanged?.Invoke(widget);
        }
    }

    public void OpenExtension(WidgetListItemViewModel widget)
    {
        widget.State = WidgetState.Running;
        Open(widget);
        ExtensionWidgetHost.ApplyTarget(widget.Kind);
    }

    private void Open(WidgetListItemViewModel widget)
    {
        // 止めたプラグインのカードは、一覧から外れるまでの間に順番が回ることがある。保存した状態は変えず、次の起動で開く。
        if (widget.PluginWidget is { IsStopped: true })
        {
            return;
        }

        if (_openSingleWindows.TryGetValue(widget, out var existingWindow))
        {
            existingWindow.ApplyPinState(widget.IsPinned);
            RestoreAndActivate(existingWindow);
            return;
        }

        var owner = Application.Current?.MainWindow;
        TrackManagerWindow(owner);

        var savedBounds = WidgetStateManager.Instance.GetWidgetSnapshot(widget.Key).Window;
        if (widget.Kind is WidgetKind.UserBuildLibrary or WidgetKind.CombatExtensionTimeline or WidgetKind.PartyDps or WidgetKind.EnemyCatalog)
        {
            savedBounds.Width ??= 1100;
            savedBounds.Height ??= 720;
        }
        if (widget.PluginWidget is { } pluginWidget)
        {
            // プラグインのウィジェットの既定の大きさは定義にある(保存の正規化では分からない)。
            savedBounds.Width ??= pluginWidget.Definition.DefaultWidth;
            savedBounds.Height ??= pluginWidget.Definition.DefaultHeight;
        }

        // プラグインのウィジェットは中身を作れないことがある(プラグインの例外。受け止めがプラグインを止め、カードも外れる)。
        // カードの知らせはもう解除されているので、次の起動で同じ例外を繰り返さないよう保存を直接直す。
        if (CreateWidgetWindowComposition(widget) is not { } composition)
        {
            widget.State = WidgetState.Stopped;
            WidgetStateManager.Instance.SaveWidgetState(widget.Key, WidgetState.Stopped);
            return;
        }

        var window = new WidgetWindow(
            widget,
            composition.Content,
            composition.DataStatusSource,
            savedBounds,
            owner,
            headerChromeActions: composition.HeaderChromeActions,
            headerActions: composition.HeaderActions,
            footerContent: composition.FooterContent);
        window.Closed += WidgetWindow_Closed;
        window.DataStatusChanged += WidgetWindow_DataStatusChanged;

        _openSingleWindows.Add(widget, window);
        UpdateCardDataStatus(widget);
        window.ShowRespectingForeground();
    }

    private PlayerWidgetWindowSession CreatePlayerWindow(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerBuffListKind? requestedBuffListKind = null,
        string? requestedBuffKey = null,
        string? savedPlayerName = null,
        BuffGroup buffGroup = BuffGroup.None,
        long? savedPlayerUid = null,
        string? savedBuffName = null,
        WidgetWindowConfig? savedWindowBounds = null,
        bool savedIsNpc = false,
        int savedProfessionId = 0)
    {
        var owner = Application.Current?.MainWindow;
        TrackManagerWindow(owner);

        var roster = PlayerRosterPresentationStore.Instance.Current.Entries;
        var initialPlayer = ResolvePlayer(requestedCharacterId, roster);
        var playerWindowViewModel = CreatePlayerWindowViewModel(
            playerWidget,
            requestedCharacterId,
            initialPlayer,
            requestedBuffListKind,
            requestedBuffKey,
            buffGroup);
        // 種に使うのは観測したUID。自分の窓は requestedCharacterId が null(＝「自分」という指定)
        // なので、それを条件にすると一度も種が入らない。
        playerWindowViewModel.SeedLastKnownPlayer(
            savedPlayerName,
            savedPlayerUid ?? requestedCharacterId ?? 0,
            savedIsNpc,
            savedProfessionId);

        if (playerWindowViewModel is BuffDebuffCardWidgetViewModel seededCard)
        {
            seededCard.SeedLastKnownBuffName(savedBuffName);
        }

        var content = CreatePlayerWindowContent(playerWindowViewModel);
        var headerActions = CreatePlayerWindowHeaderActions(playerWindowViewModel);
        var savedBounds = savedWindowBounds
            ?? WidgetStateManager.Instance.GetWidgetSnapshot(playerWidget.Kind).Window;
        var window = new WidgetWindow(
            playerWidget,
            content,
            playerWindowViewModel,
            savedBounds,
            owner,
            playerWindowViewModel.HeaderText,
            headerChromeActions: headerActions.HeaderChromeActions,
            headerActions: headerActions.HeaderActions);
        playerWindowViewModel.PropertyChanged += PlayerWindowViewModel_PropertyChanged;
        playerWindowViewModel.SavedTargetInfoResolved += PlayerWindowViewModel_SavedTargetInfoResolved;
        if (playerWindowViewModel is BuffDebuffCardWidgetViewModel card)
        {
            card.FirstBuffRowFound += BuffCardViewModel_FirstBuffRowFound;
        }

        window.Closed += WidgetWindow_Closed;
        window.DataStatusChanged += WidgetWindow_DataStatusChanged;
        // 開き先を持たない種別(常に自分1枚のステータス詳細・バフ・デバフグループ)は、種別ごとの保存だけを使う
        // (SaveOpenTargets はその種別では何もせず返る)。
        window.SaveWindowBoundsOverride = WidgetConfigDefaults.SupportsOpenTargets(playerWidget.Kind)
            ? () => SaveOpenTargets(playerWidget)
            : null;

        // 復元した位置があるならカスケードで動かさない。
        if (savedWindowBounds is null)
        {
            ApplyPlayerWindowCascade(window, CountOpenTargetWindows(playerWidget));
        }

        // 一覧へ足すのは Show の前(Show の中で上がる知らせが窓を引く)。カードへの知らせ(UpdatePlayerWindowCount)は Show の後
        // (知らせの当て直しが窓を閉じることがあり、見せる前に閉じた窓は Show できない)。保存は知らせより先(当て直しの中で窓が閉じたときの保存を上書きしない)。
        var session = new PlayerWidgetWindowSession(playerWidget, playerWindowViewModel, window);
        _openPlayerWindows.Add(session);

        window.ShowRespectingForeground();

        SaveOpenTargets(playerWidget);
        UpdateCardDataStatus(playerWidget);
        UpdatePlayerWindowCount(playerWidget);

        if (playerWidget.State != WidgetState.Running)
        {
            playerWidget.State = WidgetState.Running;
        }

        return session;
    }

    /// <summary>個体1つを映す窓(バフリスト・デバフリスト)を開く。バフ・デバフカードは <see cref="CreateEntityBuffCardWindow"/>。</summary>
    private EntityWindowSession CreateEntityBuffWindow(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        WidgetWindowConfig? savedWindowBounds = null)
    {
        var kind = widget.Kind == WidgetKind.BuffList
            ? PlayerBuffListKind.Buff
            : PlayerBuffListKind.Debuff;
        var viewModel = new EntityBuffListWidgetViewModel(widget, target, kind, OpenBuffCardForEntity);

        return CreateEntityWindow(
            widget,
            viewModel,
            new PlayerBuffListWidgetView
            {
                DataContext = viewModel
            },
            headerActions: null,
            headerChromeActions: null,
            savedWindowBounds);
    }

    private EntityWindowSession CreateEntityBuffCardWindow(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        PlayerBuffListKind kind,
        string? buffKey,
        BuffGroup group,
        string? savedBuffName = null,
        WidgetWindowConfig? savedWindowBounds = null)
    {
        var viewModel = new EntityBuffDebuffCardWidgetViewModel(widget, target, kind, buffKey, group);
        viewModel.SeedLastKnownBuffName(savedBuffName);
        viewModel.SavedTargetInfoResolved += EntityBuffCardViewModel_SavedTargetInfoResolved;

        return CreateEntityWindow(
            widget,
            viewModel,
            new BuffDebuffCardWidgetView
            {
                DataContext = viewModel
            },
            new BuffDebuffCardHeaderActionsView
            {
                DataContext = viewModel
            },
            new BuffDebuffCardHeaderLabelsView
            {
                DataContext = viewModel
            },
            savedWindowBounds);
    }

    private EntityWindowSession CreateEntityWindow(
        WidgetListItemViewModel widget,
        IEntityWidgetWindowViewModel viewModel,
        FrameworkElement content,
        FrameworkElement? headerActions,
        FrameworkElement? headerChromeActions,
        WidgetWindowConfig? savedWindowBounds = null)
    {
        var owner = Application.Current?.MainWindow;
        TrackManagerWindow(owner);

        var savedBounds = savedWindowBounds
            ?? WidgetStateManager.Instance.GetWidgetSnapshot(widget.Kind).Window;
        var window = new WidgetWindow(
            widget,
            content,
            viewModel,
            savedBounds,
            owner,
            viewModel.HeaderText,
            headerChromeActions: headerChromeActions,
            headerActions: headerActions);
        viewModel.PropertyChanged += EntityWindowViewModel_PropertyChanged;
        window.Closed += WidgetWindow_Closed;
        window.DataStatusChanged += WidgetWindow_DataStatusChanged;
        window.SaveWindowBoundsOverride = () => SaveOpenTargets(widget);

        if (savedWindowBounds is null)
        {
            ApplyPlayerWindowCascade(window, CountOpenTargetWindows(widget));
        }

        // 知らせと保存の順は CreatePlayerWindow と同じ(知らせは Show の後)。
        var session = new EntityWindowSession(widget, viewModel, window);
        _openEntityWindows.Add(session);

        window.ShowRespectingForeground();

        SaveOpenTargets(widget);
        UpdateCardDataStatus(widget);
        UpdatePlayerWindowCount(widget);

        if (widget.State != WidgetState.Running)
        {
            widget.State = WidgetState.Running;
        }

        return session;
    }

    /// <summary>チャットの窓(チャンネル1つか、プライベートトークの相手1人)を開く。</summary>
    /// <param name="partnerId">プライベートトークの相手の UID。ほかのチャンネルは 0。</param>
    /// <param name="savedPartnerName">復元のときに保存しておいた相手の名前。</param>
    private void CreateChatWindow(
        WidgetListItemViewModel widget,
        Zproto.ChitChatChannelType channel,
        long partnerId,
        string? savedPartnerName,
        WidgetWindowConfig? savedWindowBounds = null)
    {
        var owner = Application.Current?.MainWindow;
        TrackManagerWindow(owner);

        var viewModel = new ChatWidgetViewModel(widget, channel, partnerId, savedPartnerName, ShowSharedPositionOnMap);
        var savedBounds = savedWindowBounds
            ?? WidgetStateManager.Instance.GetWidgetSnapshot(widget.Kind).Window;
        var window = new WidgetWindow(
            widget,
            new ChatWidgetView
            {
                DataContext = viewModel
            },
            viewModel,
            savedBounds,
            owner,
            viewModel.HeaderText);
        viewModel.PropertyChanged += ChatWindowViewModel_PropertyChanged;
        window.Closed += WidgetWindow_Closed;
        window.DataStatusChanged += WidgetWindow_DataStatusChanged;
        window.SaveWindowBoundsOverride = () => SaveOpenTargets(widget);

        if (savedWindowBounds is null)
        {
            ApplyPlayerWindowCascade(window, CountOpenTargetWindows(widget));
        }

        // 知らせと保存の順は CreatePlayerWindow と同じ(知らせは Show の後)。
        _openChatWindows.Add(new ChatWindowSession(widget, viewModel, window));

        window.ShowRespectingForeground();

        SaveOpenTargets(widget);
        UpdateCardDataStatus(widget);
        UpdatePlayerWindowCount(widget);

        if (widget.State != WidgetState.Running)
        {
            widget.State = WidgetState.Running;
        }
    }

    /// <summary>プラグインの複数起動のウィジェットの窓を1枚開く。プラグインが止まっていれば開かない。</summary>
    /// <param name="targetId">相手の ID。null は空の窓(本体が出し、プラグインは呼ばない。相手が出たら中身を入れる)。</param>
    /// <param name="targetNames">相手の名前(タイトルに使う)。</param>
    private void CreatePluginTargetWindow(
        WidgetListItemViewModel widget,
        string? targetId,
        IReadOnlyDictionary<string, string> targetNames,
        WidgetWindowConfig? savedWindowBounds = null)
    {
        if (widget.PluginWidget is not { IsStopped: false } pluginWidget)
        {
            return;
        }

        var owner = Application.Current?.MainWindow;
        TrackManagerWindow(owner);

        // 中身を作れないのはプラグインの例外のとき。受け止めがプラグインを止め、カードも外れる。
        // カードの知らせはもう解除されているので、次の起動で同じ例外を繰り返さないよう保存を直接直す。
        var createdComposition = targetId is null
            ? CreateEmptyPluginWidgetComposition(pluginWidget)
            : CreatePluginWidgetComposition(widget, targetId);
        if (createdComposition is not { } composition)
        {
            widget.State = WidgetState.Stopped;
            WidgetStateManager.Instance.SaveWidgetState(widget.Key, WidgetState.Stopped);
            return;
        }

        var savedBounds = savedWindowBounds ?? WidgetStateManager.Instance.GetWidgetSnapshot(widget.Key).Window;
        savedBounds.Width ??= pluginWidget.Definition.DefaultWidth;
        savedBounds.Height ??= pluginWidget.Definition.DefaultHeight;

        var window = new WidgetWindow(
            widget,
            composition.Content,
            composition.DataStatusSource,
            savedBounds,
            owner,
            PluginTargetWindowSession.FormatHeaderText(widget, targetNames),
            headerChromeActions: composition.HeaderChromeActions,
            headerActions: composition.HeaderActions,
            footerContent: composition.FooterContent);
        window.Closed += WidgetWindow_Closed;
        window.DataStatusChanged += WidgetWindow_DataStatusChanged;
        window.SaveWindowBoundsOverride = () => SaveOpenTargets(widget);

        if (savedWindowBounds is null)
        {
            ApplyPlayerWindowCascade(window, CountOpenTargetWindows(widget));
        }

        _openPluginWindows.Add(new PluginTargetWindowSession(
            widget,
            targetId,
            targetNames,
            window,
            (PluginWidgetWindowViewModel)composition.DataStatusSource,
            (IPluginWidgetElementHost)composition.Content,
            composition.FooterContent as IPluginWidgetElementHost));
        if (_trackedPluginWidgets.Add(widget))
        {
            widget.PlayerWindowPresentationChanged += PlayerWidget_PresentationChanged;
        }

        // 知らせと保存の順は CreatePlayerWindow と同じ(知らせは Show の後)。
        window.ShowRespectingForeground();

        SaveOpenTargets(widget);
        UpdateCardDataStatus(widget);
        UpdatePlayerWindowCount(widget);

        if (widget.State != WidgetState.Running)
        {
            widget.State = WidgetState.Running;
        }
    }

    private PlayerWidgetWindowViewModel CreatePlayerWindowViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        PlayerBuffListKind? requestedBuffListKind = null,
        string? requestedBuffKey = null,
        BuffGroup buffGroup = BuffGroup.None)
    {
        return playerWidget.Kind switch
        {
            WidgetKind.PlayerInfo => new PlayerInfoWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                ShowPlayerOnMap),
            WidgetKind.PlayerStatus => new PlayerStatusWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer),
            WidgetKind.PlayerEquipment => new PlayerEquipmentWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer),
            WidgetKind.PlayerTalent => new PlayerTalentTreeWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer),
            // 上にノード効果(ノード・上級)、下に因子効果。
            WidgetKind.PlayerSeasonTalent => new PlayerRootBuffWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                [RootBuffKind.SeasonTalentNode, RootBuffKind.SeasonTalentAdvanced, RootBuffKind.Factor]),
            WidgetKind.PlayerModule => new PlayerRootBuffWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                [RootBuffKind.ModuleEffect]),
            WidgetKind.BuffList => new PlayerBuffListWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                PlayerBuffListKind.Buff,
                Instance,
                Instance,
                Instance.OpenBuffCardForPlayer),
            WidgetKind.DebuffList => new PlayerBuffListWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                PlayerBuffListKind.Debuff,
                Instance,
                Instance,
                Instance.OpenBuffCardForPlayer),
            // バフもまとまりも指定が無ければ、最初に付いたものを待つ窓(「指定なし」)。
            WidgetKind.BuffDebuffCard => new BuffDebuffCardWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                requestedBuffListKind ?? PlayerBuffListKind.Buff,
                requestedBuffKey,
                buffGroup,
                Instance.FindFirstBuffRow,
                Instance,
                Instance),
            // 自分だけを出す1枚の窓(相手の指定は持たない)。
            WidgetKind.BuffDebuffGroup => new BuffDebuffGroupWidgetViewModel(
                playerWidget,
                initialPlayer,
                Instance.OpenBuffCardForGroupEntry),
            WidgetKind.DamageSkillDetail => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Damage,
                PlayerMetricDisplayMode.SkillDetail),
            WidgetKind.DamageSummary => new PlayerMetricSummaryWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                PlayerMetricSummaryKind.Damage),
            WidgetKind.DpsGraph => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Damage,
                PlayerMetricDisplayMode.Graph),
            WidgetKind.HealingSkillDetail => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Healing,
                PlayerMetricDisplayMode.SkillDetail),
            WidgetKind.HateSkillDetail => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Hate,
                PlayerMetricDisplayMode.SkillDetail),
            WidgetKind.BreakSkillDetail => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Break,
                PlayerMetricDisplayMode.SkillDetail),
            WidgetKind.HateGraph => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Hate,
                PlayerMetricDisplayMode.Graph),
            WidgetKind.BreakGraph => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Break,
                PlayerMetricDisplayMode.Graph),
            WidgetKind.HealingSummary => new PlayerMetricSummaryWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                PlayerMetricSummaryKind.Healing),
            WidgetKind.HpsGraph => new PlayerMetricWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                MeterSnapshotKind.Healing,
                PlayerMetricDisplayMode.Graph),
            WidgetKind.DamageTakenSummary => new PlayerMetricSummaryWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer,
                PlayerMetricSummaryKind.DamageTaken),
            WidgetKind.BuffUptime => new BuffUptimeWidgetViewModel(
                playerWidget,
                requestedCharacterId,
                initialPlayer),
            _ => throw new ArgumentOutOfRangeException(nameof(playerWidget))
        };
    }

    private static FrameworkElement CreatePlayerWindowContent(PlayerWidgetWindowViewModel playerWindowViewModel)
    {
        return playerWindowViewModel switch
        {
            PlayerInfoWidgetViewModel infoViewModel => new PlayerInfoWidgetView
            {
                DataContext = infoViewModel
            },
            PlayerStatusWidgetViewModel statusViewModel => new PlayerStatusWidgetView
            {
                DataContext = statusViewModel
            },
            PlayerEquipmentWidgetViewModel equipmentViewModel => new PlayerEquipmentWidgetView
            {
                DataContext = equipmentViewModel
            },
            PlayerTalentTreeWidgetViewModel talentTreeViewModel => new PlayerTalentTreeWidgetView
            {
                DataContext = talentTreeViewModel
            },
            PlayerRootBuffWidgetViewModel rootBuffViewModel => new PlayerRootBuffWidgetView
            {
                DataContext = rootBuffViewModel
            },
            PlayerBuffListWidgetViewModel buffListViewModel => new PlayerBuffListWidgetView
            {
                DataContext = buffListViewModel
            },
            BuffDebuffCardWidgetViewModel buffCardViewModel => new BuffDebuffCardWidgetView
            {
                DataContext = buffCardViewModel
            },
            BuffDebuffGroupWidgetViewModel buffDebuffGroupViewModel => new BuffDebuffGroupWidgetView
            {
                DataContext = buffDebuffGroupViewModel
            },
            PlayerMetricWidgetViewModel { IsSkillDetail: true } metricViewModel => new SkillDetailWidgetView
            {
                DataContext = metricViewModel
            },
            PlayerMetricSummaryWidgetViewModel summaryViewModel => new PlayerMetricSummaryWidgetView
            {
                DataContext = summaryViewModel
            },
            PlayerMetricWidgetViewModel metricViewModel => new PlayerMetricGraphWidgetView
            {
                DataContext = metricViewModel
            },
            BuffUptimeWidgetViewModel buffUptimeViewModel => new BuffUptimeWidgetView
            {
                DataContext = buffUptimeViewModel
            },
            _ => throw new ArgumentOutOfRangeException(nameof(playerWindowViewModel))
        };
    }

    /// <summary>
    /// プレイヤー用ウィンドウのヘッダーボタン。
    ///
    /// <para>
    /// ヘッダーの文字ボタンは<b>2層で1組</b>。押せるだけの層(<c>HeaderActions</c>)と、
    /// 見える文字を描く枠側の層(<c>HeaderChromeActions</c>)を両方渡さないと文字が出ない。
    /// </para>
    /// </summary>
    private static (FrameworkElement? HeaderActions, FrameworkElement? HeaderChromeActions)
        CreatePlayerWindowHeaderActions(PlayerWidgetWindowViewModel playerWindowViewModel)
    {
        if (playerWindowViewModel is not BuffDebuffCardWidgetViewModel)
        {
            return (null, null);
        }

        return (
            new BuffDebuffCardHeaderActionsView
            {
                DataContext = playerWindowViewModel
            },
            new BuffDebuffCardHeaderLabelsView
            {
                DataContext = playerWindowViewModel
            });
    }

    /// <summary>
    /// 1枚だけ開く種類の窓の中身。プレイヤーリストとエンティティリストは窓の中身もウィジェットの項目が持つ。
    /// プラグインのウィジェットの中身を作れなかったときだけ null。
    /// </summary>
    private static WidgetWindowComposition CreateExtensionComposition(WidgetListItemViewModel widget)
    {
        var body = new ExtensionWidgetHost(widget.Kind);
        return new WidgetWindowComposition(body, body, null, null, null);
    }

    private WidgetWindowComposition? CreateWidgetWindowComposition(WidgetListItemViewModel widget)
    {
        return widget.Kind switch
        {
            WidgetKind.UserBuildLibrary or WidgetKind.CombatExtensionTimeline or WidgetKind.PartyDps or WidgetKind.EnemyCatalog =>
                CreateExtensionComposition(widget),
            WidgetKind.Plugin => CreatePluginWidgetComposition(widget, targetId: null),
            WidgetKind.PlayerList => new WidgetWindowComposition(
                new PlayerListWidgetView(),
                widget,
                null,
                null,
                new ListWidgetFooterView()),
            WidgetKind.EntityList => new WidgetWindowComposition(
                new EntityListWidgetView(),
                widget,
                null,
                null,
                new ListWidgetFooterView()),
            WidgetKind.DpsMeter => CreateMeterWidgetComposition(widget, MeterSnapshotKind.Damage),
            WidgetKind.HpsMeter => CreateMeterWidgetComposition(widget, MeterSnapshotKind.Healing),
            WidgetKind.HateMeter => CreateMeterWidgetComposition(widget, MeterSnapshotKind.Hate),
            WidgetKind.BreakMeter => CreateMeterWidgetComposition(widget, MeterSnapshotKind.Break),
            WidgetKind.Timeline => CreateTimelineComposition(widget),
            WidgetKind.ReviveList => CreateReviveListComposition(widget),
            WidgetKind.DamageTakenLog => CreateDamageTakenLogComposition(widget),
            WidgetKind.Map => CreateMapComposition(widget),
            WidgetKind.EntityInfo => CreateEntityInfoComposition(widget),
            _ => throw new InvalidOperationException($"Widget {widget.Kind} has no single-window content.")
        };
    }

    /// <summary>
    /// ターゲット情報(内部名はエンティティ情報)。タイトルはほかの1枚だけ開く窓と同じくウィジェット名だけで、敵の名前は1行目に出す。
    /// ヘッダーの「ターゲットに戻る」はマップの「自分のいるマップに戻る」と同じく2層で1組。
    /// </summary>
    private WidgetWindowComposition CreateEntityInfoComposition(WidgetListItemViewModel widget)
    {
        var viewModel = new EntityInfoWidgetViewModel(
            widget,
            PlayerRosterPresentationStore.Instance.Current.Entries,
            NearbyEntityStore.Instance.Current.Entries,
            OpenEntityWindow,
            OpenBuffCardForEntity);
        _entityInfoViewModel = viewModel;
        return new WidgetWindowComposition(
            new EntityInfoWidgetView
            {
                DataContext = viewModel
            },
            viewModel,
            new EntityInfoWidgetHeaderLabelsView
            {
                DataContext = viewModel
            },
            new EntityInfoWidgetHeaderActionsView
            {
                DataContext = viewModel
            },
            null);
    }

    /// <summary>
    /// マップ。タイトルはほかの1枚だけ開く窓と同じくウィジェット名だけで、マップの名前とチャンネルはフッターの左に出す。
    /// ヘッダーの「自分のいるマップに戻る」は被ダメログの文字ボタンと同じく2層で1組。
    /// </summary>
    private WidgetWindowComposition CreateMapComposition(WidgetListItemViewModel widget)
    {
        var viewModel = new MapWidgetViewModel(widget, OpenPlayerWindow, ShowEntityInTargetInfo);
        _mapViewModel = viewModel;
        return new WidgetWindowComposition(
            new MapWidgetView
            {
                DataContext = viewModel
            },
            viewModel,
            new MapWidgetHeaderLabelsView
            {
                DataContext = viewModel
            },
            new MapWidgetHeaderActionsView
            {
                DataContext = viewModel
            },
            new ListWidgetFooterView
            {
                DataContext = viewModel
            });
    }

    /// <summary>
    /// プラグインのウィジェットの中身。本体と本体のスクロールは定義で決め、ヘッダーのボタンはメーターと同じく2層で1組(ボタンが無ければ渡さない)。
    /// 窓は中身の DataContext(<see cref="PluginWidgetWindowViewModel"/>)を閉じたときに後始末する。
    /// </summary>
    /// <param name="targetId">相手ごとに窓を開くウィジェットの相手の ID。窓が1枚だけのウィジェットは null。</param>
    private static WidgetWindowComposition? CreatePluginWidgetComposition(WidgetListItemViewModel widget, string? targetId)
    {
        if (widget.PluginWidget!.CreateWindowViewModel(widget, targetId) is not { } viewModel)
        {
            return null;
        }

        FrameworkElement content = viewModel.ScrollsVertically
            ? new PluginWidgetScrollBodyView(viewModel.Body)
            : new PluginWidgetBodyView(viewModel.Body);
        content.DataContext = viewModel;
        var hasHeaderButtons = viewModel.HeaderButtons.Count > 0;
        return new WidgetWindowComposition(
            content,
            viewModel,
            hasHeaderButtons ? new PluginWidgetHeaderLabelsView { DataContext = viewModel } : null,
            hasHeaderButtons ? new PluginWidgetHeaderActionsView { DataContext = viewModel } : null,
            viewModel.Footer is null ? null : new PluginWidgetBodyView(viewModel.Footer));
    }

    /// <summary>
    /// プラグインの空の窓の中身。本体とフッターの置き場は定義で決まるので最初から作り、プラグインの中身だけを後から入れる
    /// (<see cref="FillEmptyPluginWindow"/>)。ヘッダーのボタンは中身が入るまで無い。
    /// </summary>
    private static WidgetWindowComposition CreateEmptyPluginWidgetComposition(PluginWidgetRegistration pluginWidget)
    {
        var viewModel = pluginWidget.CreateEmptyWindowViewModel();
        FrameworkElement content = viewModel.ScrollsVertically
            ? new PluginWidgetScrollBodyView(null)
            : new PluginWidgetBodyView(null);
        content.DataContext = viewModel;
        return new WidgetWindowComposition(
            content,
            viewModel,
            null,
            null,
            pluginWidget.Definition.HasFooter ? new PluginWidgetBodyView(null) : null);
    }

    private WidgetWindowComposition CreateReviveListComposition(WidgetListItemViewModel widget)
    {
        var viewModel = new ReviveListWidgetViewModel(widget, PlayerRosterPresentationStore.Instance.Current, OpenPlayerWindow);
        _reviveListViewModel = viewModel;
        return new WidgetWindowComposition(
            new ReviveListWidgetView
            {
                DataContext = viewModel
            },
            viewModel,
            null,
            null,
            null);
    }

    private WidgetWindowComposition CreateDamageTakenLogComposition(WidgetListItemViewModel widget)
    {
        var viewModel = new DamageTakenLogWidgetViewModel(widget, PlayerRosterPresentationStore.Instance.Current, OpenPlayerWindow);
        _damageTakenLogViewModel = viewModel;

        // ヘッダーの文字ボタン(「最新の被ダメのみ表示」の切り替え)はメーターと同じく2層で1組。
        return new WidgetWindowComposition(
            new DamageTakenLogWidgetView
            {
                DataContext = viewModel
            },
            viewModel,
            new DamageTakenLogWidgetHeaderLabelsView
            {
                DataContext = viewModel
            },
            new DamageTakenLogWidgetHeaderActionsView
            {
                DataContext = viewModel
            },
            new DamageTakenLogWidgetFooterView
            {
                DataContext = viewModel
            });
    }

    /// <summary>顔ぶれを蘇生リストと被ダメログの窓へ当てる(画面のスレッドで呼ぶ)。開いていない窓には何もしない。</summary>
    public void UpdateListWidgetRosters(PlayerRosterSnapshot roster)
    {
        _reviveListViewModel?.UpdateRoster(roster);
        _damageTakenLogViewModel?.UpdateRoster(roster);
    }

    private static WidgetWindowComposition CreateTimelineComposition(WidgetListItemViewModel widget)
    {
        var viewModel = new TimelineWidgetViewModel(widget);
        return new WidgetWindowComposition(
            new TimelineWidgetView
            {
                DataContext = viewModel
            },
            viewModel,
            null,
            null,
            null);
    }

    private WidgetWindowComposition CreateMeterWidgetComposition(
        WidgetListItemViewModel widget,
        MeterSnapshotKind kind)
    {
        var viewModel = new MeterWidgetViewModel(widget, kind, OpenPlayerWindow);

        // ヘッダーの文字ボタンは2層で1組。**片方だけ渡すと文字の無いボタンか、押せない文字が残る。**
        // Labels が枠側で文字を描き(IsHitTestVisible=False)、Actions が中身を透明にして当たり判定だけ持つ。
        return new WidgetWindowComposition(
            new MeterWidgetView
            {
                DataContext = viewModel
            },
            viewModel,
            new MeterWidgetHeaderLabelsView
            {
                DataContext = viewModel
            },
            new MeterWidgetHeaderActionsView
            {
                DataContext = viewModel
            },
            new MeterWidgetFooterView
            {
                DataContext = viewModel
            });
    }

    private void Close(WidgetListItemViewModel widget)
    {
        if (!_openSingleWindows.TryGetValue(widget, out var window))
        {
            return;
        }

        window.Close();
    }

    private void ClosePlayerWindows(WidgetListItemViewModel playerWidget)
    {
        foreach (var session in EnumerateTargetSessions(playerWidget).ToArray())
        {
            session.Window.Close();
        }

        UpdatePlayerWindowCount(playerWidget);
    }

    private void TrackPlayerWidget(WidgetListItemViewModel playerWidget)
    {
        if (_trackedPlayerWidgets.TryGetValue(playerWidget.Kind, out var trackedWidget)
            && ReferenceEquals(trackedWidget, playerWidget))
        {
            return;
        }

        if (trackedWidget is not null)
        {
            trackedWidget.PlayerWindowPresentationChanged -= PlayerWidget_PresentationChanged;
        }

        _trackedPlayerWidgets[playerWidget.Kind] = playerWidget;
        playerWidget.PlayerWindowPresentationChanged += PlayerWidget_PresentationChanged;
    }

    /// <summary>その相手の名簿の外の値(名刺照会の値・チャットの発言者の値)が変わった。開いているプレイヤーの窓へ渡す。</summary>
    public void UpdatePlayerWindowPlayerData(long characterId)
    {
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            playerWindow.ViewModel.NotifyPlayerDataChanged(characterId);
            playerWindow.Window.SetHeaderText(playerWindow.ViewModel.HeaderText);
        }
    }

    /// <summary>名刺の値の控えが全部消えた。開いているプレイヤーの窓すべてへ渡す(一覧に居ない相手を出している窓も含む)。</summary>
    public void ClearPlayerWindowSocialData()
    {
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            playerWindow.ViewModel.NotifySocialDataCleared();
        }
    }

    /// <summary>自分の装備の値が変わった。装備詳細の窓に知らせる(自分を出している窓だけが作り直す)。</summary>
    public void UpdatePlayerWindowSelfEquipment()
    {
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            if (playerWindow.ViewModel is PlayerEquipmentWidgetViewModel equipment)
            {
                equipment.NotifySelfEquipmentChanged();
            }
        }
    }

    /// <summary>自分のモジュールが変わった。モジュール情報の窓に知らせる(自分を出している窓だけ作り直す)。</summary>
    public void UpdatePlayerWindowSelfModules()
    {
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            if (playerWindow.ViewModel is PlayerRootBuffWidgetViewModel rootBuffs)
            {
                rootBuffs.NotifySelfModulesChanged();
            }
        }
    }

    /// <summary>自分の心相晶の育成データが変わった。潜在心相晶情報の窓に知らせる(自分を出している窓だけ作り直す)。</summary>
    public void UpdatePlayerWindowSelfSeasonCultivate()
    {
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            if (playerWindow.ViewModel is PlayerRootBuffWidgetViewModel rootBuffs)
            {
                rootBuffs.NotifySelfSeasonCultivateChanged();
            }
        }
    }

    /// <summary>
    /// 顔ぶれをプレイヤーの窓へ当てる。<b>窓ごとに別の処理(Background)として積む。</b>
    /// 相手が変わった窓はその場で中身を作り直すので、1つの処理で全部の窓に当てると、全部の窓の作り直しの間は入力も描画も止まる。
    /// 積んだ窓に当たる前に次の顔ぶれが来たら、その窓は最新の顔ぶれで1回だけ当てる。閉じた窓は飛ばす。
    /// </summary>
    public void UpdatePlayerWindowPresentations(PlayerRosterSnapshot roster)
    {
        _latestPlayerWindowRoster = new PlayerWindowRoster(
            roster,
            roster.Entries
                .Where(player => player.CharacterId != 0)
                .ToDictionary(player => player.CharacterId),
            roster.Entries.FirstOrDefault(player => player.IsSelf));

        var dispatcher = Application.Current?.Dispatcher;
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            if (!_playerWindowsAwaitingRoster.Add(playerWindow))
            {
                continue;
            }

            if (dispatcher is null)
            {
                ApplyRosterToPlayerWindow(playerWindow);
                continue;
            }

            dispatcher.BeginInvoke(DispatcherPriority.Background, () => ApplyRosterToPlayerWindow(playerWindow));
        }
    }

    /// <summary>積んである顔ぶれを窓へ当てる。積んでいない窓・閉じた窓には何もしない。</summary>
    private void ApplyRosterToPlayerWindow(PlayerWidgetWindowSession playerWindow)
    {
        if (!_playerWindowsAwaitingRoster.Remove(playerWindow)
            || _latestPlayerWindowRoster is not { } latest
            || !_openPlayerWindows.Contains(playerWindow))
        {
            return;
        }

        playerWindow.ViewModel.UpdateRosterContext(latest.Roster.MapName, latest.Roster.MapChannel, latest.Roster.SeasonId);
        playerWindow.ViewModel.UpdatePlayerFromRoster(latest.PlayersByCharacterId, latest.SelfPlayer);
        playerWindow.Window.SetHeaderText(playerWindow.ViewModel.HeaderText);
    }

    /// <summary>積んである顔ぶれを、待っている窓全部へその場で当てる。</summary>
    private void ApplyAwaitingRosterToPlayerWindows()
    {
        foreach (var playerWindow in _openPlayerWindows.ToArray())
        {
            ApplyRosterToPlayerWindow(playerWindow);
        }
    }

    /// <summary>
    /// 周りの実体が変わった。実体の窓に、エンティティリストの行で個体を捕まえ直させ(<paramref name="entities"/>)、
    /// ターゲット情報の窓には周りの実体の値(<paramref name="nearbyEntities"/>)を渡す。
    /// </summary>
    public void UpdateEntityWindowPresentations(
        IReadOnlyList<EntityListEntry> entities,
        IReadOnlyList<NearbyEntityEntry> nearbyEntities)
    {
        ApplyEntityTargets(entities);
        _entityInfoViewModel?.UpdateNearbyEntities(nearbyEntities);
    }

    /// <summary>
    /// 実体の窓に、エンティティリストの行で個体を捕まえ直させ、捕まえている個体の値(名前・レベル)を当て直させる。
    /// 行の値が変わったとき(言語や表示設定の変更を含む)に呼ぶ。
    /// まだ捕まえていない窓が捕まえたら <see cref="EntityWindowAcquired"/> を上げる。
    /// </summary>
    public void ApplyEntityTargets(IReadOnlyList<EntityListEntry> entities)
    {
        var entitiesByUuid = entities.ToDictionary(entity => entity.EntityUuid);

        foreach (var entityWindow in _openEntityWindows.ToArray())
        {
            var viewModel = entityWindow.ViewModel;

            if (viewModel.IsEntityAcquired)
            {
                if (entitiesByUuid.TryGetValue(viewModel.EntityUuid, out var held))
                {
                    viewModel.TryApplyEntity(held.Target);
                    entityWindow.Window.SetHeaderText(viewModel.HeaderText);
                }

                continue;
            }

            // 設定から復元した直後かログアウトの後の窓。捕まえてよい個体(種類と種別が一致し、同じウィジェットのほかの窓が映していない)を1体だけ捕まえる。
            // 前の窓が捕まえた個体も避けるので、同じ種類の窓が2枚あれば別の1体になる。
            var candidate = entities.FirstOrDefault(entity => CanCaptureEntity(entityWindow, entity.Target));
            if (candidate is null)
            {
                continue;
            }

            var before = CreateOpenTarget(entityWindow);
            viewModel.TryApplyEntity(candidate.Target);
            entityWindow.Window.SetHeaderText(viewModel.HeaderText);
            RaiseEntityWindowAcquired(entityWindow.Widget, before, CreateOpenTarget(entityWindow));
        }
    }

    /// <summary>
    /// まだ捕まえていなかった敵の窓が、エンティティリストの1体を捕まえた(引数はウィジェット、捕まえる前の項目、捕まえた後の項目)。
    /// 項目はバフ・デバフカードならカードの項目、ほかは相手の項目。メイン画面のカードが、捕まえる前の項目を選んでいれば捕まえた後の項目に置き換える。
    /// </summary>
    public event Action<WidgetListItemViewModel, WidgetTargetOption, WidgetTargetOption>? EntityWindowAcquired;

    private void RaiseEntityWindowAcquired(
        WidgetListItemViewModel widget,
        (WidgetTargetOption Person, WidgetTargetOption? Buff) before,
        (WidgetTargetOption Person, WidgetTargetOption? Buff) after)
    {
        var isCard = widget.Kind == WidgetKind.BuffDebuffCard;
        if ((isCard ? before.Buff : before.Person) is { } beforeOption
            && (isCard ? after.Buff : after.Person) is { } afterOption)
        {
            EntityWindowAcquired?.Invoke(widget, beforeOption, afterOption);
        }
    }

    /// <summary>名簿が変わった。ターゲット情報の窓のヘイトの枠(名前・クラス・特化)を引き直させる。</summary>
    public void UpdateEntityInfoRosters(IReadOnlyList<PlayerRosterEntry> roster)
    {
        _entityInfoViewModel?.UpdateRoster(roster);
    }

    private void PlayerWindowViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlayerWidgetWindowViewModel.HeaderText)
            || sender is not PlayerWidgetWindowViewModel playerWindowViewModel)
        {
            return;
        }

        var session = _openPlayerWindows.FirstOrDefault(
            candidate => ReferenceEquals(candidate.ViewModel, playerWindowViewModel));
        if (session is null)
        {
            return;
        }

        session.Window.SetHeaderText(playerWindowViewModel.HeaderText);

        // メイン画面のカードの欄は、選んでいる項目の窓のタイトルそのもの。
        TargetWindowsChanged?.Invoke(session.Widget);
    }

    /// <summary>
    /// 相手の素性が確定したら対象一覧を書き直す。
    /// 窓の開閉だけで保存していると、開いた直後はまだ名前が空なので
    /// <b>保存に名前が入らず、次の起動でタイトルを復元できない</b>。
    /// </summary>
    private void PlayerWindowViewModel_SavedTargetInfoResolved(object? sender, EventArgs e)
    {
        if (sender is not PlayerWidgetWindowViewModel playerWindowViewModel)
        {
            return;
        }

        var session = _openPlayerWindows.FirstOrDefault(
            candidate => ReferenceEquals(candidate.ViewModel, playerWindowViewModel));

        if (session is not null)
        {
            SaveOpenTargets(session.Widget);
        }
    }

    private void EntityBuffCardViewModel_SavedTargetInfoResolved(object? sender, EventArgs e)
    {
        var session = _openEntityWindows.FirstOrDefault(
            candidate => ReferenceEquals(candidate.ViewModel, sender));

        if (session is not null)
        {
            SaveOpenTargets(session.Widget);
        }
    }

    private void EntityWindowViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IEntityWidgetWindowViewModel.HeaderText)
            || sender is not IEntityWidgetWindowViewModel viewModel)
        {
            return;
        }

        var session = _openEntityWindows.FirstOrDefault(
            candidate => ReferenceEquals(candidate.ViewModel, viewModel));
        if (session is null)
        {
            return;
        }

        session.Window.SetHeaderText(viewModel.HeaderText);

        // メイン画面のカードの欄は、選んでいる項目の窓のタイトルそのもの。
        TargetWindowsChanged?.Invoke(session.Widget);
    }

    /// <summary>
    /// チャットの窓のタイトルが変わった(相手の名前が分かった・名前の伏せ字・言語)。窓のタイトルとカードの欄を当て直し、
    /// 次の起動のタイトルに使う相手の名前を保存する。
    /// </summary>
    private void ChatWindowViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ChatWidgetViewModel.HeaderText) || sender is not ChatWidgetViewModel viewModel)
        {
            return;
        }

        var session = _openChatWindows.FirstOrDefault(candidate => ReferenceEquals(candidate.ViewModel, viewModel));
        if (session is null)
        {
            return;
        }

        session.Window.SetHeaderText(viewModel.HeaderText);
        SaveOpenTargets(session.Widget);
        TargetWindowsChanged?.Invoke(session.Widget);
    }

    private void PlayerWidget_PresentationChanged(object? sender, EventArgs e)
    {
        if (sender is not WidgetListItemViewModel playerWidget)
        {
            return;
        }

        foreach (var playerWindow in _openPlayerWindows
                     .Where(session => ReferenceEquals(session.Widget, playerWidget))
                     .ToArray())
        {
            playerWindow.ViewModel.RefreshPlayerPresentation();
            playerWindow.Window.SetHeaderText(playerWindow.ViewModel.HeaderText);
        }

        foreach (var entityWindow in _openEntityWindows
                     .Where(session => ReferenceEquals(session.Widget, playerWidget))
                     .ToArray())
        {
            entityWindow.ViewModel.RefreshPresentation();
            entityWindow.Window.SetHeaderText(entityWindow.ViewModel.HeaderText);
        }

        foreach (var pluginWindow in _openPluginWindows
                     .Where(session => ReferenceEquals(session.Widget, playerWidget))
                     .ToArray())
        {
            pluginWindow.Window.SetHeaderText(pluginWindow.HeaderText);
        }
    }

    private static PlayerRosterEntry? ResolvePlayer(
        long? requestedCharacterId,
        IReadOnlyList<PlayerRosterEntry> roster)
    {
        return requestedCharacterId is { } characterId
            ? roster.FirstOrDefault(player => player.CharacterId == characterId)
            : roster.FirstOrDefault(player => player.IsSelf);
    }

    private void UpdatePlayerWindowCount(WidgetListItemViewModel playerWidget)
    {
        playerWidget.SetOpenPlayerWindowCount(CountOpenTargetWindows(playerWidget));
        TargetWindowsChanged?.Invoke(playerWidget);
    }

    private int CountOpenTargetWindows(WidgetListItemViewModel widget)
    {
        return EnumerateTargetSessions(widget).Count();
    }

    private bool HasOpenTargetWindows(WidgetListItemViewModel widget)
    {
        return EnumerateTargetSessions(widget).Any();
    }

    /// <summary>
    /// そのウィジェットで、相手を持つ窓が1枚でも開いているか(一覧を作らずに数える)。カードの選択リストに並べるものがあるかに使う。
    /// 人の空の窓とプラグインの空の窓は数えない(相手を選んでいない既定の項目と同じもので、起動ボタンで開け閉めできる)。
    /// </summary>
    public bool HasFilledTargetWindows(WidgetListItemViewModel widget)
    {
        return EnumerateTargetSessions(widget).Any(session => session switch
        {
            // バフ・デバフカードの相手の指定なしは自分のカード(空の窓ではない)。
            PlayerWidgetWindowSession player => player.ViewModel is BuffDebuffCardWidgetViewModel || !player.ViewModel.IsAwaitingPlayer,
            PluginTargetWindowSession plugin => plugin.TargetId is not null,
            _ => true
        });
    }


    private static void RestoreAndActivate(WidgetWindow window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        Activate(window);
    }

    private static void Activate(WidgetWindow window)
    {
        window.Activate();
    }

    private static void ApplyPlayerWindowCascade(WidgetWindow window, int cascadeIndex)
    {
        if (cascadeIndex <= 0)
        {
            return;
        }

        var offset = PlayerWindowCascadeOffset * cascadeIndex;
        window.Left += offset;
        window.Top += offset;
    }

    private void TrackManagerWindow(Window? managerWindow)
    {
        if (managerWindow is null || ReferenceEquals(_managerWindow, managerWindow))
        {
            return;
        }

        if (_managerWindow is not null)
        {
            _managerWindow.Closing -= ManagerWindow_Closing;
        }

        _managerWindow = managerWindow;
        _managerWindow.Closing += ManagerWindow_Closing;
    }

    private void ManagerWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (e.Cancel)
        {
            return;
        }

        _isManagerClosing = true;

        foreach (var window in _openSingleWindows.Values
                     .Concat(EnumerateTargetSessions().Select(session => session.Window))
                     .ToArray())
        {
            window.Close();
        }
    }

    private void WidgetWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not WidgetWindow window)
        {
            return;
        }

        window.Closed -= WidgetWindow_Closed;
        window.DataStatusChanged -= WidgetWindow_DataStatusChanged;

        var targetSession = EnumerateTargetSessions().FirstOrDefault(
            session => ReferenceEquals(session.Window, window));

        if (targetSession is not null)
        {
            DetachTargetSession(targetSession);
            var widget = targetSession.Widget;
            UpdatePlayerWindowCount(widget);
            UpdateCardDataStatus(widget);
            SaveOpenTargets(widget);

            if (!_isManagerClosing
                && !_isClosingKeepingState
                && widget.State == WidgetState.Running
                && !HasOpenTargetWindows(widget))
            {
                widget.State = WidgetState.Stopped;
            }

            return;
        }

        _openSingleWindows.Remove(window.Widget);
        if (window.Widget.Kind == WidgetKind.ReviveList)
        {
            _reviveListViewModel = null;
        }

        if (window.Widget.Kind == WidgetKind.DamageTakenLog)
        {
            _damageTakenLogViewModel = null;
        }

        if (window.Widget.Kind == WidgetKind.Map)
        {
            _mapViewModel = null;
        }

        if (window.Widget.Kind == WidgetKind.EntityInfo)
        {
            _entityInfoViewModel = null;
        }

        UpdateCardDataStatus(window.Widget);

        if (!_isManagerClosing && !_isClosingKeepingState && window.Widget.State == WidgetState.Running)
        {
            window.Widget.State = WidgetState.Stopped;
        }
    }

    /// <summary>
    /// 相手ごとの窓を閉じたときの、種類ごとの後始末(購読の解除・Dispose・一覧から外す)。
    /// </summary>
    private void DetachTargetSession(TargetWindowSession session)
    {
        switch (session)
        {
            case PlayerWidgetWindowSession player:
                player.ViewModel.PropertyChanged -= PlayerWindowViewModel_PropertyChanged;
                player.ViewModel.SavedTargetInfoResolved -= PlayerWindowViewModel_SavedTargetInfoResolved;
                if (player.ViewModel is BuffDebuffCardWidgetViewModel card)
                {
                    card.FirstBuffRowFound -= BuffCardViewModel_FirstBuffRowFound;
                }

                if (player.ViewModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _openPlayerWindows.Remove(player);
                _playerWindowsAwaitingRoster.Remove(player);
                break;
            case EntityWindowSession entity:
                entity.ViewModel.PropertyChanged -= EntityWindowViewModel_PropertyChanged;
                if (entity.ViewModel is EntityBuffDebuffCardWidgetViewModel entityCard)
                {
                    entityCard.SavedTargetInfoResolved -= EntityBuffCardViewModel_SavedTargetInfoResolved;
                }

                entity.ViewModel.Dispose();
                _openEntityWindows.Remove(entity);
                break;
            case ChatWindowSession chat:
                chat.ViewModel.PropertyChanged -= ChatWindowViewModel_PropertyChanged;
                chat.ViewModel.Dispose();
                _openChatWindows.Remove(chat);
                break;
            case PluginTargetWindowSession plugin:
                _openPluginWindows.Remove(plugin);
                break;
        }
    }

    private void WidgetWindow_DataStatusChanged(object? sender, EventArgs e)
    {
        if (sender is WidgetWindow window)
        {
            UpdateCardDataStatus(window.Widget);
        }
    }

    /// <summary>
    /// メイン窓のカードの円。そのウィジェットの開いている窓のうち一番悪い状態の印(赤 → 黄 → 緑)、開いている窓が無ければ null(停止の灰色)。
    /// </summary>
    private void UpdateCardDataStatus(WidgetListItemViewModel widget)
    {
        WidgetDataStatusLevel? worst = null;
        foreach (var window in EnumerateOpenWindows(widget))
        {
            var level = window.DataStatus.Level;
            if (worst is null || level > worst)
            {
                worst = level;
            }
        }

        widget.CardDataStatusLevel = worst;
    }

    /// <summary>そのウィジェットの開いている窓(1枚だけの窓と、複数開けるウィジェットの窓)。</summary>
    private IEnumerable<WidgetWindow> EnumerateOpenWindows(WidgetListItemViewModel widget)
    {
        if (_openSingleWindows.TryGetValue(widget, out var singleWindow))
        {
            yield return singleWindow;
        }

        foreach (var session in EnumerateTargetSessions(widget))
        {
            yield return session.Window;
        }
    }

    /// <summary>
    /// 受信の状態(<see cref="GameReceptionStateStore"/>)か、履歴の選択か読み込みの失敗(<see cref="EncounterHistoryProvider"/>)が変わった。
    /// どちらもパケット処理のスレッドなどから来るので画面のスレッドへ渡し、開いている窓全部の中身に状態の印を計算し直させる
    /// (変わった窓だけがカードの円をまとめ直す)。
    /// </summary>
    private void RefreshAllDataStatusLater()
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            foreach (var window in _openSingleWindows.Values
                         .Concat(EnumerateTargetSessions().Select(session => session.Window))
                         .ToArray())
            {
                window.RefreshDataStatus();
            }

            foreach (var session in _openPlayerWindows.ToArray())
            {
                session.ViewModel.RefreshBuildContext();
                session.Window.SetHeaderText(session.ViewModel.HeaderText);
            }
        });
    }

    private sealed record PlayerWindowRoster(
        PlayerRosterSnapshot Roster,
        IReadOnlyDictionary<long, PlayerRosterEntry> PlayersByCharacterId,
        PlayerRosterEntry? SelfPlayer);

    /// <summary>
    /// 複数開けるウィジェットの窓1枚の記録(人・敵・チャット・プラグイン)の共通の土台。4つの一覧をまとめて回すのに使う
    /// (<see cref="EnumerateTargetSessions"/>)。種類ごとの処理は、それぞれの一覧と型で行う。
    /// </summary>
    private abstract class TargetWindowSession
    {
        protected TargetWindowSession(WidgetListItemViewModel widget, WidgetWindow window)
        {
            Widget = widget;
            Window = window;
        }

        public WidgetListItemViewModel Widget { get; }

        public WidgetWindow Window { get; }
    }

    private sealed class PlayerWidgetWindowSession : TargetWindowSession
    {
        public PlayerWidgetWindowSession(
            WidgetListItemViewModel widget,
            PlayerWidgetWindowViewModel viewModel,
            WidgetWindow window)
            : base(widget, window)
        {
            ViewModel = viewModel;
        }

        public PlayerWidgetWindowViewModel ViewModel { get; }
    }

    private sealed class EntityWindowSession : TargetWindowSession
    {
        public EntityWindowSession(
            WidgetListItemViewModel widget,
            IEntityWidgetWindowViewModel viewModel,
            WidgetWindow window)
            : base(widget, window)
        {
            ViewModel = viewModel;
        }

        public IEntityWidgetWindowViewModel ViewModel { get; }
    }

    private sealed class ChatWindowSession : TargetWindowSession
    {
        public ChatWindowSession(
            WidgetListItemViewModel widget,
            ChatWidgetViewModel viewModel,
            WidgetWindow window)
            : base(widget, window)
        {
            ViewModel = viewModel;
        }

        public ChatWidgetViewModel ViewModel { get; }
    }

    /// <summary>プラグインの複数起動のウィジェットの窓1枚。中身はプラグインが持ち、後始末は窓が中身の DataContext に行う。</summary>
    private sealed class PluginTargetWindowSession : TargetWindowSession
    {
        public PluginTargetWindowSession(
            WidgetListItemViewModel widget,
            string? targetId,
            IReadOnlyDictionary<string, string> targetNames,
            WidgetWindow window,
            PluginWidgetWindowViewModel viewModel,
            IPluginWidgetElementHost bodyHost,
            IPluginWidgetElementHost? footerHost)
            : base(widget, window)
        {
            TargetId = targetId;
            TargetNames = targetNames;
            ViewModel = viewModel;
            BodyHost = bodyHost;
            FooterHost = footerHost;
        }

        /// <summary>相手の ID。null は空の窓で、相手が出たら入る。</summary>
        public string? TargetId { get; set; }

        public PluginWidgetWindowViewModel ViewModel { get; }

        public IPluginWidgetElementHost BodyHost { get; }

        public IPluginWidgetElementHost? FooterHost { get; }

        /// <summary>相手の名前(言語ごと)。一覧に居る間は一覧の値、一覧から消えた後は最後に分かっていた値、復元の直後は保存しておいた値。</summary>
        public IReadOnlyDictionary<string, string> TargetNames { get; set; }

        public string? TargetName => PluginTexts.Resolve(TargetNames, LocalizationManager.Instance.CurrentCulture);

        public string HeaderText => FormatHeaderText(Widget, TargetNames);

        public static string FormatHeaderText(WidgetListItemViewModel widget, IReadOnlyDictionary<string, string> targetNames)
        {
            return WidgetHeaderText.Join(widget.DisplayName, PluginTexts.Resolve(targetNames, LocalizationManager.Instance.CurrentCulture));
        }
    }

    /// <param name="DataStatusSource">タイトルの左の円に映す状態の印の持ち主(窓の中身)。</param>
    private sealed record WidgetWindowComposition(
        FrameworkElement Content,
        IWidgetDataStatusSource DataStatusSource,
        FrameworkElement? HeaderChromeActions,
        FrameworkElement? HeaderActions,
        FrameworkElement? FooterContent);

}
