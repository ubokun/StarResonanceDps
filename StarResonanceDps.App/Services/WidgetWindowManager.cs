// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
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

    public void RegisterPlayerWindowWidget(WidgetListItemViewModel widget)
    {
        if (IsPlayerWindowWidget(widget.Kind))
        {
            TrackPlayerWidget(widget);
        }
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
            OpenTarget(widget, self, card);
            UnspecifiedBuffCardResolved?.Invoke(widget, card);
            return;
        }

        // 待つ窓は相手もバフも指定しない窓(1つだけ)。もう開いていれば OpenTarget が前に出す。
        OpenTarget(widget, self, WidgetTargetOption.CreateUnspecifiedBuffCard());
    }

    /// <summary>
    /// 「指定なし」で開くカードが決まった(引数はウィジェットとそのカードの項目)。起動ボタンで1行目のカードを開いたか前に出したとき、
    /// 待っていた窓がバフに入ったか、そのカードがもう開いていて待っていた窓を閉じたときに上がる。メイン画面のカードが選択を置き換える。
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
        if (FindTargetWindow(session.Widget, WidgetTargetOption.CreateSelf(AppState.PlayerUID), option) is not null)
        {
            session.Window.Close();
        }
        else
        {
            card.Enter(option.BuffListKind, snapshot.CardKey, option.BuffGroup);
        }

        UnspecifiedBuffCardResolved?.Invoke(session.Widget, option);
    }

    public void OpenPlayerWindow(WidgetKind kind, long characterId)
    {
        if (_trackedPlayerWidgets.TryGetValue(kind, out var playerWidget))
        {
            OpenPlayerWindow(playerWidget, characterId);
        }
    }

    /// <summary>エンティティリスト(とエンティティ情報の枠)から、その個体のバフリスト・デバフリスト・エンティティ情報を開く。もう開いていれば前に出す。</summary>
    public void OpenEntityWindow(WidgetKind kind, EntityTargetInfo entity)
    {
        if (kind is not (WidgetKind.BuffList or WidgetKind.DebuffList or WidgetKind.EntityInfo)
            || !_trackedPlayerWidgets.TryGetValue(kind, out var widget))
        {
            return;
        }

        TrackPlayerWidget(widget);

        var existingWindow = _openEntityWindows
            .FirstOrDefault(session =>
                ReferenceEquals(session.Widget, widget)
                && RepresentsEntity(session.ViewModel, entity));

        if (existingWindow is not null)
        {
            existingWindow.ViewModel.TryApplyEntity(entity);
            existingWindow.Window.SetHeaderText(existingWindow.ViewModel.HeaderText);
            RestoreAndActivate(existingWindow.Window);
            return;
        }

        CreateEntityTargetWindow(widget, new EntityWindowTarget(entity));
    }

    /// <summary>
    /// 複数開けるウィジェットの窓が開いた・閉じたときと、開いている窓のタイトルが変わったとき。
    /// メイン画面のカードが、選んでいる項目の欄(窓のタイトル)と起動ボタンを当て直す。
    /// </summary>
    public event Action<WidgetListItemViewModel>? TargetWindowsChanged;

    /// <summary>
    /// 相手(人・敵)と、バフ・デバフカードならバフを指定して、開いている窓を探す。開いていなければ null。
    /// 人は、自分なら自分を映す窓(相手の指定なしで開いた窓を含む)、それ以外は UID で探す。
    /// 敵は実体で探し、捕まえていない窓(復元直後)は種類と種別IDでも探す。敵を選んでいない項目は敵を待つ窓を探す。
    /// チャットはチャンネル(プライベートトークは相手)で、プラグインの相手は相手の ID で探す。
    /// </summary>
    public WidgetWindow? FindTargetWindow(
        WidgetListItemViewModel widget,
        WidgetTargetOption person,
        WidgetTargetOption? buff)
    {
        if (person.Kind == WidgetTargetOptionKind.Plugin)
        {
            return _openPluginWindows
                .FirstOrDefault(session =>
                    ReferenceEquals(session.Widget, widget)
                    && string.Equals(session.TargetId, person.PluginTargetId, StringComparison.OrdinalIgnoreCase))
                ?.Window;
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
            return _openEntityWindows
                .FirstOrDefault(session =>
                    ReferenceEquals(session.Widget, widget)
                    && RepresentsEntityOption(session.ViewModel, person)
                    && (buff is null
                        || (session.ViewModel is EntityBuffDebuffCardWidgetViewModel card
                            && card.RepresentsBuff(buff.BuffListKind, buff.BuffKey, buff.BuffGroup))))
                ?.Window;
        }

        return _openPlayerWindows
            .FirstOrDefault(session =>
                ReferenceEquals(session.Widget, widget)
                && (person.IsSelf
                    ? session.ViewModel.RepresentsSelf()
                    : session.ViewModel.RepresentsPlayer(person.CharacterId))
                && (buff is null
                    || (session.ViewModel is BuffDebuffCardWidgetViewModel card
                        && card.RepresentsBuff(buff.BuffListKind, buff.BuffKey, buff.BuffGroup))))
            ?.Window;
    }

    /// <summary>
    /// 相手(と、バフ・デバフカードならバフ)の窓を開く。もう開いていれば前に出す。
    /// 自分は相手の指定なしで開く(カードの起動ボタンで開いていた既定の窓と同じ)。
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

        TrackPlayerWidget(widget);

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
                : person.IsUnselectedEntity
                    ? EntityWindowTarget.CreateWaiting()
                    : new EntityWindowTarget(person.UnacquiredEntityId, person.UnacquiredEntityType, person.UnacquiredEntityName);

            if (buff is null)
            {
                CreateEntityTargetWindow(widget, target);
            }
            else
            {
                CreateEntityBuffCardWindow(widget, target, buff.BuffListKind, buff.BuffKey, buff.BuffGroup);
            }

            return;
        }

        long? requestedCharacterId = person.IsSelf ? null : person.CharacterId;
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
    /// 開いている窓の相手(と、バフ・デバフカードならバフ)。メイン画面のカードの選択リストに、名簿などから外れた相手も並べるのに使う。
    /// 項目は相手を指す値だけを持ち、表示の文字は持たない。実体を捕まえていない敵の窓は種類と種別IDで指す。
    /// 敵を待つ窓は敵を選んでいない項目、最初に付いたものを待つカードは「指定なし」、プラグインの空の窓は相手を選んでいない項目で指す。
    /// </summary>
    public IReadOnlyList<(WidgetTargetOption Person, WidgetTargetOption? Buff)> GetOpenTargets(WidgetListItemViewModel widget)
    {
        var targets = new List<(WidgetTargetOption Person, WidgetTargetOption? Buff)>();

        foreach (var session in _openPlayerWindows)
        {
            if (!ReferenceEquals(session.Widget, widget))
            {
                continue;
            }

            var viewModel = session.ViewModel;
            var person = viewModel.RequestedCharacterId is { } characterId
                ? new WidgetTargetOption { Kind = WidgetTargetOptionKind.Player, CharacterId = characterId }
                : WidgetTargetOption.CreateSelf(viewModel.LastKnownPlayerUid);
            var buff = viewModel switch
            {
                // 最初に付いたものを待っている窓は「指定なし」。
                BuffDebuffCardWidgetViewModel { IsWaiting: true } => WidgetTargetOption.CreateUnspecifiedBuffCard(),
                BuffDebuffCardWidgetViewModel card => new WidgetTargetOption
                {
                    Kind = WidgetTargetOptionKind.Buff,
                    IsSelf = person.IsSelf && card.Group != BuffGroup.None,
                    BuffListKind = card.BuffListKind,
                    BuffKey = card.RequestedBuffKey,
                    BuffGroup = card.Group,
                    BuffName = card.LastKnownBuffName ?? string.Empty,
                    BuffPerson = person
                },
                _ => null
            };
            targets.Add((person, buff));
        }

        foreach (var session in _openEntityWindows)
        {
            if (!ReferenceEquals(session.Widget, widget))
            {
                continue;
            }

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
            targets.Add((person, buff));
        }

        foreach (var session in _openChatWindows)
        {
            if (ReferenceEquals(session.Widget, widget))
            {
                targets.Add((WidgetTargetOption.CreateChat(session.ViewModel.Channel, session.ViewModel.PartnerId), null));
            }
        }

        foreach (var session in _openPluginWindows)
        {
            if (ReferenceEquals(session.Widget, widget))
            {
                targets.Add((
                    new WidgetTargetOption
                    {
                        Kind = WidgetTargetOptionKind.Plugin,
                        PluginTargetId = session.TargetId,
                        PluginTargetNames = session.TargetNames
                    },
                    null));
            }
        }

        return targets;
    }

    /// <summary>敵の窓がその実体を映すか。捕まえていない窓(復元直後)は、種類と種別IDが一致すれば映す。</summary>
    private static bool RepresentsEntity(IEntityWidgetWindowViewModel viewModel, EntityTargetInfo entity)
    {
        return viewModel.RepresentsEntity(entity.EntityUuid)
            || (!viewModel.IsEntityAcquired
                && viewModel.EntityId == entity.EntityId
                && viewModel.EntityType == entity.EntityType);
    }

    /// <summary>敵の窓がその項目の相手か。実体を持たない項目は、敵を選んでいなければ敵を待つ窓、それ以外は種類と種別IDが一致する窓。</summary>
    private static bool RepresentsEntityOption(IEntityWidgetWindowViewModel viewModel, WidgetTargetOption option)
    {
        if (option.Entity is { } entity)
        {
            return RepresentsEntity(viewModel, entity);
        }

        return option.IsUnselectedEntity
            ? viewModel.IsWaitingForEntity
            : viewModel.EntityId == option.UnacquiredEntityId && viewModel.EntityType == option.UnacquiredEntityType;
    }

    public void ApplyWidgetState(WidgetListItemViewModel widget)
    {
        if (widget.PluginWidget is { IsMultiWindow: true })
        {
            ApplyPluginMultiWidgetState(widget);
            return;
        }

        if (IsPlayerWindowWidget(widget.Kind))
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

    public void ApplyWidgetPinState(WidgetListItemViewModel widget, bool bringToFront)
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
        if (!IsPlayerWindowWidget(playerWidget.Kind))
        {
            return;
        }

        TrackPlayerWidget(playerWidget);

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

        var existingWindow = _openPlayerWindows
            .FirstOrDefault(session =>
                ReferenceEquals(session.Widget, playerWidget)
                && session.ViewModel.RepresentsPlayer(characterId));

        if (existingWindow is not null)
        {
            RestoreAndActivate(existingWindow.Window);
            return;
        }

        CreatePlayerWindow(playerWidget, characterId);
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

        TrackPlayerWidget(widget);

        var existingWindow = _openPlayerWindows
            .FirstOrDefault(session =>
                ReferenceEquals(session.Widget, widget)
                && session.ViewModel is BuffDebuffCardWidgetViewModel card
                && card.RepresentsPlayer(characterId)
                && card.RepresentsBuff(kind, buffKey, group));

        if (existingWindow is not null)
        {
            RestoreAndActivate(existingWindow.Window);
            return;
        }

        CreatePlayerWindow(widget, characterId, kind, buffKey, buffGroup: group, savedBuffName: savedBuffName);
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

        TrackPlayerWidget(widget);

        var group = CombatDataCatalog.GetBuffGroup(baseId);

        var existingWindow = _openEntityWindows
            .FirstOrDefault(session =>
                ReferenceEquals(session.Widget, widget)
                && session.ViewModel is EntityBuffDebuffCardWidgetViewModel card
                && card.RepresentsBuff(kind, buffKey, group)
                && RepresentsEntity(card, entity));

        if (existingWindow is not null)
        {
            existingWindow.ViewModel.TryApplyEntity(entity);
            existingWindow.Window.SetHeaderText(existingWindow.ViewModel.HeaderText);
            RestoreAndActivate(existingWindow.Window);
            return;
        }

        CreateEntityBuffCardWindow(widget, new EntityWindowTarget(entity), kind, buffKey, group);
    }

    /// <summary>
    /// 保存しておいた対象で窓を1枚開き直す。
    ///
    /// <para>
    /// 実体の窓は<b>未捕獲の状態</b>で開く。実体IDは再起動で消えるため、
    /// 種類と種別が一致する個体がAOIに現れた時点で
    /// <see cref="UpdateEntityWindowPresentations"/> が捕まえる。
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
                return;
            }

            var chatChannel = (Zproto.ChitChatChannelType)chatChannelValue;
            var partnerId = chatChannel == Zproto.ChitChatChannelType.ChannelPrivate ? target.CharacterId ?? 0 : 0;
            if (chatChannel == Zproto.ChitChatChannelType.ChannelPrivate && partnerId == 0)
            {
                return;
            }

            CreateChatWindow(widget, chatChannel, partnerId, target.Name, target.Window);
            return;
        }

        // エンティティ情報にプレイヤーの窓は無い。種別IDの無い保存は敵を待つ空の窓。
        if (widget.Kind == WidgetKind.EntityInfo && !target.IsEntity)
        {
            CreateEntityTargetWindow(widget, EntityWindowTarget.CreateWaiting(), target.Window);
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
            CreatePlayerWindow(
                widget,
                target.CharacterId,
                buffListKind,
                target.BuffKey,
                target.Name,
                buffGroup,
                target.ResolvedCharacterId,
                target.BuffName,
                target.Window);
            return;
        }

        var entityType = target.EntityType is { } typeValue && Enum.IsDefined(typeof(Zproto.EEntityType), typeValue)
            ? (Zproto.EEntityType)typeValue
            : (Zproto.EEntityType?)null;
        var entityTarget = new EntityWindowTarget(target.EntityId, entityType, target.Name);

        if (widget.Kind != WidgetKind.BuffDebuffCard)
        {
            CreateEntityTargetWindow(widget, entityTarget, target.Window);
            return;
        }

        if (buffGroup == BuffGroup.None && string.IsNullOrWhiteSpace(target.BuffKey))
        {
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
    /// </summary>
    private void SaveOpenTargets(WidgetListItemViewModel widget)
    {
        if (_isManagerClosing || _isClosingKeepingState || !widget.SupportsTargetSelection)
        {
            return;
        }

        var targets = new List<WidgetOpenTargetConfig>();

        foreach (var session in _openPlayerWindows)
        {
            if (!ReferenceEquals(session.Widget, widget))
            {
                continue;
            }

            var card = session.ViewModel as BuffDebuffCardWidgetViewModel;
            targets.Add(new WidgetOpenTargetConfig
            {
                CharacterId = session.ViewModel.RequestedCharacterId,
                Name = session.ViewModel.LastKnownPlayerName,
                ResolvedCharacterId = session.ViewModel.LastKnownPlayerUid == 0
                    ? null
                    : session.ViewModel.LastKnownPlayerUid,
                BuffListKind = card is null ? null : (int)card.BuffListKind,
                BuffKey = card?.RequestedBuffKey,
                BuffName = card?.LastKnownBuffName,
                BuffGroup = card is null ? null : (int)card.Group,
                Window = session.Window.GetCurrentBounds()
            });
        }

        foreach (var session in _openEntityWindows)
        {
            if (!ReferenceEquals(session.Widget, widget))
            {
                continue;
            }

            // 種別ID 0(未知の敵)の窓は保存しない。復元は種類と種別IDで個体を捕まえるので、
            // 0 のまま戻すと次の起動で別の未知の個体を捕まえる。敵を待つ窓は種別IDが無いまま保存する。
            if (session.ViewModel.EntityId == 0 && !session.ViewModel.IsWaitingForEntity)
            {
                continue;
            }

            var card = session.ViewModel as EntityBuffDebuffCardWidgetViewModel;
            targets.Add(new WidgetOpenTargetConfig
            {
                EntityId = session.ViewModel.EntityId,
                EntityType = session.ViewModel.EntityType is { } entityType ? (int)entityType : null,
                Name = session.ViewModel.TargetName,
                BuffListKind = card is null ? null : (int)card.BuffListKind,
                BuffKey = card?.RequestedBuffKey,
                BuffName = card?.LastKnownBuffName,
                BuffGroup = card is null ? null : (int)card.Group,
                Window = session.Window.GetCurrentBounds()
            });
        }

        foreach (var session in _openChatWindows)
        {
            if (!ReferenceEquals(session.Widget, widget))
            {
                continue;
            }

            var isPrivate = session.ViewModel.Channel == Zproto.ChitChatChannelType.ChannelPrivate;
            targets.Add(new WidgetOpenTargetConfig
            {
                ChatChannel = (int)session.ViewModel.Channel,
                CharacterId = isPrivate ? session.ViewModel.PartnerId : null,
                Name = isPrivate ? session.ViewModel.LastKnownPartnerName : null,
                Window = session.Window.GetCurrentBounds()
            });
        }

        foreach (var session in _openPluginWindows)
        {
            if (ReferenceEquals(session.Widget, widget))
            {
                targets.Add(new WidgetOpenTargetConfig
                {
                    PluginTargetId = session.TargetId,
                    Name = session.TargetName,
                    Window = session.Window.GetCurrentBounds()
                });
            }
        }

        WidgetStateManager.Instance.SaveWidgetOpenTargets(widget.Key, targets);
    }

    private void ApplyPlayerWindowWidgetState(WidgetListItemViewModel widget)
    {
        TrackPlayerWidget(widget);

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

            if (savedTargets is { Count: > 0 })
            {
                foreach (var target in savedTargets)
                {
                    RestoreTargetWindow(widget, target);
                }

                return;
            }

            // 対象の記録が無い。ここはウィジェットカードから初めて起動したときの経路。
            // エンティティ情報は個体を選んで開くもので、相手の指定なしの既定の窓が無い。敵を待つ窓を開く。
            if (widget.Kind == WidgetKind.EntityInfo)
            {
                CreateEntityTargetWindow(widget, EntityWindowTarget.CreateWaiting());
                return;
            }

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
                CreatePluginTargetWindow(
                    widget,
                    string.IsNullOrWhiteSpace(target.PluginTargetId) ? null : target.PluginTargetId,
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
        return _openPluginWindows.Any(session => ReferenceEquals(session.Widget, widget) && session.TargetId is null);
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
            || _openPluginWindows.FirstOrDefault(session => ReferenceEquals(session.Widget, widget) && session.TargetId is null) is not { } empty)
        {
            return;
        }

        var target = targets.FirstOrDefault(candidate => candidate.IsDefault) ?? targets[0];
        if (_openPluginWindows.Any(session =>
                ReferenceEquals(session.Widget, widget)
                && string.Equals(session.TargetId, target.Id, StringComparison.OrdinalIgnoreCase)))
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
                candidate => string.Equals(candidate.Id, session.TargetId, StringComparison.OrdinalIgnoreCase));
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
        if (widget.Kind is WidgetKind.UserBuildLibrary or WidgetKind.CombatExtensionTimeline or WidgetKind.HatePs or WidgetKind.PartyDps or WidgetKind.EnemyCatalog)
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
        window.Show();
    }

    private void CreatePlayerWindow(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerBuffListKind? requestedBuffListKind = null,
        string? requestedBuffKey = null,
        string? savedPlayerName = null,
        BuffGroup buffGroup = BuffGroup.None,
        long? savedPlayerUid = null,
        string? savedBuffName = null,
        WidgetWindowConfig? savedWindowBounds = null)
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
            savedPlayerUid ?? requestedCharacterId ?? 0);

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

        _openPlayerWindows.Add(new PlayerWidgetWindowSession(playerWidget, playerWindowViewModel, window));
        UpdatePlayerWindowCount(playerWidget);
        UpdateCardDataStatus(playerWidget);
        SaveOpenTargets(playerWidget);

        window.Show();

        if (playerWidget.State != WidgetState.Running)
        {
            playerWidget.State = WidgetState.Running;
        }
    }

    /// <summary>個体1つを映す窓(バフリスト・デバフリスト・エンティティ情報)を開く。バフ・デバフカードは <see cref="CreateEntityBuffCardWindow"/>。</summary>
    private void CreateEntityTargetWindow(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        WidgetWindowConfig? savedWindowBounds = null)
    {
        if (widget.Kind == WidgetKind.EntityInfo)
        {
            CreateEntityInfoWindow(widget, target, savedWindowBounds);
            return;
        }

        CreateEntityBuffWindow(widget, target, savedWindowBounds);
    }

    private void CreateEntityInfoWindow(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        WidgetWindowConfig? savedWindowBounds)
    {
        var viewModel = new EntityInfoWidgetViewModel(
            widget,
            target,
            PlayerRosterPresentationStore.Instance.Current.Entries,
            NearbyEntityStore.Instance.Current.Entries,
            OpenEntityWindow,
            OpenBuffCardForEntity);

        CreateEntityWindow(
            widget,
            viewModel,
            new EntityInfoWidgetView
            {
                DataContext = viewModel
            },
            headerActions: null,
            headerChromeActions: null,
            savedWindowBounds);
    }

    private void CreateEntityBuffWindow(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        WidgetWindowConfig? savedWindowBounds = null)
    {
        var kind = widget.Kind == WidgetKind.BuffList
            ? PlayerBuffListKind.Buff
            : PlayerBuffListKind.Debuff;
        var viewModel = new EntityBuffListWidgetViewModel(widget, target, kind, OpenBuffCardForEntity);

        CreateEntityWindow(
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

    private void CreateEntityBuffCardWindow(
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

        CreateEntityWindow(
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

    private void CreateEntityWindow(
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

        _openEntityWindows.Add(new EntityWindowSession(widget, viewModel, window));
        UpdatePlayerWindowCount(widget);
        UpdateCardDataStatus(widget);
        SaveOpenTargets(widget);

        window.Show();

        if (widget.State != WidgetState.Running)
        {
            widget.State = WidgetState.Running;
        }
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

        var viewModel = new ChatWidgetViewModel(widget, channel, partnerId, savedPartnerName);
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

        _openChatWindows.Add(new ChatWindowSession(widget, viewModel, window));
        UpdatePlayerWindowCount(widget);
        UpdateCardDataStatus(widget);
        SaveOpenTargets(widget);

        window.Show();

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

        UpdatePlayerWindowCount(widget);
        UpdateCardDataStatus(widget);
        SaveOpenTargets(widget);

        window.Show();

        if (widget.State != WidgetState.Running)
        {
            widget.State = WidgetState.Running;
        }
    }

    private static PlayerWidgetWindowViewModel CreatePlayerWindowViewModel(
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
                initialPlayer),
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
            WidgetKind.UserBuildLibrary or WidgetKind.CombatExtensionTimeline or WidgetKind.HatePs or WidgetKind.PartyDps or WidgetKind.EnemyCatalog =>
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
            _ => throw new InvalidOperationException($"Widget {widget.Kind} has no single-window content.")
        };
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
        foreach (var targetWindow in _openPlayerWindows
                     .Where(session => ReferenceEquals(session.Widget, playerWidget))
                     .Select(session => session.Window)
                     .Concat(_openEntityWindows
                         .Where(session => ReferenceEquals(session.Widget, playerWidget))
                         .Select(session => session.Window))
                     .Concat(_openChatWindows
                         .Where(session => ReferenceEquals(session.Widget, playerWidget))
                         .Select(session => session.Window))
                     .Concat(_openPluginWindows
                         .Where(session => ReferenceEquals(session.Widget, playerWidget))
                         .Select(session => session.Window))
                     .ToArray())
        {
            targetWindow.Close();
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
    /// エンティティ情報の窓には周りの実体の値(<paramref name="nearbyEntities"/>)を渡す。
    /// </summary>
    public void UpdateEntityWindowPresentations(
        IReadOnlyList<EntityListEntry> entities,
        IReadOnlyList<NearbyEntityEntry> nearbyEntities)
    {
        ApplyEntityTargets(entities);

        // 捕まえた直後の窓にも、同じ一覧で値を当てる。
        foreach (var entityWindow in _openEntityWindows.ToArray())
        {
            if (entityWindow.ViewModel is EntityInfoWidgetViewModel entityInfo)
            {
                entityInfo.UpdateNearbyEntities(nearbyEntities);
            }
        }
    }

    /// <summary>
    /// 実体の窓に、エンティティリストの行で個体を捕まえ直させ、捕まえている個体の値(名前・レベル)を当て直させる。
    /// 行の値が変わったとき(言語や表示設定の変更を含む)に呼ぶ。
    /// </summary>
    public void ApplyEntityTargets(IReadOnlyList<EntityListEntry> entities)
    {
        var entitiesByUuid = entities.ToDictionary(entity => entity.EntityUuid);

        foreach (var entityWindow in _openEntityWindows.ToArray())
        {
            var viewModel = entityWindow.ViewModel;
            EntityListEntry? entity;

            if (viewModel.IsWaitingForEntity)
            {
                continue;
            }

            if (viewModel.IsEntityAcquired)
            {
                entitiesByUuid.TryGetValue(viewModel.EntityUuid, out entity);
            }
            else
            {
                // 設定から復元した直後の窓。種類と種別が一致する個体を1体だけ捕まえる。
                entity = entities.FirstOrDefault(
                    candidate => candidate.EntityId == viewModel.EntityId
                        && candidate.EntityType == viewModel.EntityType);
            }

            if (entity is not null)
            {
                viewModel.TryApplyEntity(entity.Target);
                entityWindow.Window.SetHeaderText(viewModel.HeaderText);
            }
        }

        // 復元した窓が個体を捕まえた後で、敵を待つ窓を当てる(同じ個体の窓を2枚にしない)。
        if (entities.Count > 0)
        {
            ApplyFirstEntityToWaitingWindows(entities[0].Target);
        }
    }

    /// <summary>
    /// エンティティリストに敵が出た。敵を待つ窓に1番目の敵を映させる。その敵の窓がもう開いていれば、敵を待つ窓を閉じる。
    /// </summary>
    private void ApplyFirstEntityToWaitingWindows(EntityTargetInfo first)
    {
        foreach (var waiting in _openEntityWindows.Where(session => session.ViewModel.IsWaitingForEntity).ToArray())
        {
            if (_openEntityWindows.Any(other =>
                    !ReferenceEquals(other, waiting)
                    && ReferenceEquals(other.Widget, waiting.Widget)
                    && RepresentsEntity(other.ViewModel, first)))
            {
                waiting.Window.Close();
                continue;
            }

            waiting.ViewModel.TryApplyEntity(first);
            waiting.Window.SetHeaderText(waiting.ViewModel.HeaderText);
            SaveOpenTargets(waiting.Widget);
        }
    }

    /// <summary>名簿が変わった。エンティティ情報の窓のヘイトの枠(名前・クラス・特化)を引き直させる。</summary>
    public void UpdateEntityInfoRosters(IReadOnlyList<PlayerRosterEntry> roster)
    {
        foreach (var entityWindow in _openEntityWindows.ToArray())
        {
            if (entityWindow.ViewModel is EntityInfoWidgetViewModel entityInfo)
            {
                entityInfo.UpdateRoster(roster);
            }
        }
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
        return _openPlayerWindows.Count(session => ReferenceEquals(session.Widget, widget))
            + _openEntityWindows.Count(session => ReferenceEquals(session.Widget, widget))
            + _openChatWindows.Count(session => ReferenceEquals(session.Widget, widget))
            + _openPluginWindows.Count(session => ReferenceEquals(session.Widget, widget));
    }

    private bool HasOpenTargetWindows(WidgetListItemViewModel widget)
    {
        return CountOpenTargetWindows(widget) > 0;
    }

    private static bool IsPlayerWindowWidget(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerInfo
            or WidgetKind.PlayerStatus
            or WidgetKind.BuffDebuffGroup
            or WidgetKind.PlayerEquipment
            or WidgetKind.PlayerTalent
            or WidgetKind.PlayerSeasonTalent
            or WidgetKind.PlayerModule
            or WidgetKind.BuffList
            or WidgetKind.DebuffList
            or WidgetKind.BuffDebuffCard
            or WidgetKind.DamageSkillDetail
            or WidgetKind.DamageSummary
            or WidgetKind.DpsGraph
            or WidgetKind.HealingSkillDetail
            or WidgetKind.HealingSummary
            or WidgetKind.HpsGraph
            or WidgetKind.HateSkillDetail
            or WidgetKind.HateGraph
            or WidgetKind.BreakSkillDetail
            or WidgetKind.BreakGraph
            or WidgetKind.DamageTakenSummary
            or WidgetKind.EntityInfo
            or WidgetKind.Chat;
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
                     .Concat(_openPlayerWindows.Select(session => session.Window))
                     .Concat(_openEntityWindows.Select(session => session.Window))
                     .Concat(_openChatWindows.Select(session => session.Window))
                     .Concat(_openPluginWindows.Select(session => session.Window))
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

        var playerWindow = _openPlayerWindows.FirstOrDefault(
            session => ReferenceEquals(session.Window, window));

        if (playerWindow is not null)
        {
            playerWindow.ViewModel.PropertyChanged -= PlayerWindowViewModel_PropertyChanged;
            playerWindow.ViewModel.SavedTargetInfoResolved -= PlayerWindowViewModel_SavedTargetInfoResolved;
            if (playerWindow.ViewModel is BuffDebuffCardWidgetViewModel card)
            {
                card.FirstBuffRowFound -= BuffCardViewModel_FirstBuffRowFound;
            }

            if (playerWindow.ViewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _openPlayerWindows.Remove(playerWindow);
            _playerWindowsAwaitingRoster.Remove(playerWindow);
            UpdatePlayerWindowCount(playerWindow.Widget);
            UpdateCardDataStatus(playerWindow.Widget);
            SaveOpenTargets(playerWindow.Widget);

            if (!_isManagerClosing
                && playerWindow.Widget.State == WidgetState.Running
                && !HasOpenTargetWindows(playerWindow.Widget))
            {
                playerWindow.Widget.State = WidgetState.Stopped;
            }

            return;
        }

        var entityWindow = _openEntityWindows.FirstOrDefault(
            session => ReferenceEquals(session.Window, window));

        if (entityWindow is not null)
        {
            entityWindow.ViewModel.PropertyChanged -= EntityWindowViewModel_PropertyChanged;
            if (entityWindow.ViewModel is EntityBuffDebuffCardWidgetViewModel entityCard)
            {
                entityCard.SavedTargetInfoResolved -= EntityBuffCardViewModel_SavedTargetInfoResolved;
            }

            entityWindow.ViewModel.Dispose();
            _openEntityWindows.Remove(entityWindow);
            UpdatePlayerWindowCount(entityWindow.Widget);
            UpdateCardDataStatus(entityWindow.Widget);
            SaveOpenTargets(entityWindow.Widget);

            if (!_isManagerClosing
                && entityWindow.Widget.State == WidgetState.Running
                && !HasOpenTargetWindows(entityWindow.Widget))
            {
                entityWindow.Widget.State = WidgetState.Stopped;
            }

            return;
        }

        var chatWindow = _openChatWindows.FirstOrDefault(
            session => ReferenceEquals(session.Window, window));

        if (chatWindow is not null)
        {
            chatWindow.ViewModel.PropertyChanged -= ChatWindowViewModel_PropertyChanged;
            chatWindow.ViewModel.Dispose();
            _openChatWindows.Remove(chatWindow);
            UpdatePlayerWindowCount(chatWindow.Widget);
            UpdateCardDataStatus(chatWindow.Widget);
            SaveOpenTargets(chatWindow.Widget);

            if (!_isManagerClosing
                && chatWindow.Widget.State == WidgetState.Running
                && !HasOpenTargetWindows(chatWindow.Widget))
            {
                chatWindow.Widget.State = WidgetState.Stopped;
            }

            return;
        }

        var pluginWindow = _openPluginWindows.FirstOrDefault(
            session => ReferenceEquals(session.Window, window));

        if (pluginWindow is not null)
        {
            _openPluginWindows.Remove(pluginWindow);
            UpdatePlayerWindowCount(pluginWindow.Widget);
            UpdateCardDataStatus(pluginWindow.Widget);
            SaveOpenTargets(pluginWindow.Widget);

            if (!_isManagerClosing
                && !_isClosingKeepingState
                && pluginWindow.Widget.State == WidgetState.Running
                && !HasOpenTargetWindows(pluginWindow.Widget))
            {
                pluginWindow.Widget.State = WidgetState.Stopped;
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

        UpdateCardDataStatus(window.Widget);

        if (!_isManagerClosing && !_isClosingKeepingState && window.Widget.State == WidgetState.Running)
        {
            window.Widget.State = WidgetState.Stopped;
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

    /// <summary>そのウィジェットの開いている窓(1枚だけの窓・プレイヤーの窓・実体の窓・チャットの窓・プラグインの相手の窓)。</summary>
    private IEnumerable<WidgetWindow> EnumerateOpenWindows(WidgetListItemViewModel widget)
    {
        if (_openSingleWindows.TryGetValue(widget, out var singleWindow))
        {
            yield return singleWindow;
        }

        foreach (var session in _openPlayerWindows)
        {
            if (ReferenceEquals(session.Widget, widget))
            {
                yield return session.Window;
            }
        }

        foreach (var session in _openEntityWindows)
        {
            if (ReferenceEquals(session.Widget, widget))
            {
                yield return session.Window;
            }
        }

        foreach (var session in _openChatWindows)
        {
            if (ReferenceEquals(session.Widget, widget))
            {
                yield return session.Window;
            }
        }

        foreach (var session in _openPluginWindows)
        {
            if (ReferenceEquals(session.Widget, widget))
            {
                yield return session.Window;
            }
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
            foreach (var window in _openSingleWindows.Values.ToArray())
            {
                window.RefreshDataStatus();
            }

            foreach (var session in _openPlayerWindows.ToArray())
            {
                session.ViewModel.RefreshBuildContext();
                session.Window.SetHeaderText(session.ViewModel.HeaderText);
                session.Window.RefreshDataStatus();
            }

            foreach (var session in _openEntityWindows.ToArray())
            {
                session.Window.RefreshDataStatus();
            }

            foreach (var session in _openChatWindows.ToArray())
            {
                session.Window.RefreshDataStatus();
            }

            foreach (var session in _openPluginWindows.ToArray())
            {
                session.Window.RefreshDataStatus();
            }
        });
    }

    private sealed record PlayerWindowRoster(
        PlayerRosterSnapshot Roster,
        IReadOnlyDictionary<long, PlayerRosterEntry> PlayersByCharacterId,
        PlayerRosterEntry? SelfPlayer);

    private sealed class PlayerWidgetWindowSession
    {
        public PlayerWidgetWindowSession(
            WidgetListItemViewModel widget,
            PlayerWidgetWindowViewModel viewModel,
            WidgetWindow window)
        {
            Widget = widget;
            ViewModel = viewModel;
            Window = window;
        }

        public WidgetListItemViewModel Widget { get; }

        public PlayerWidgetWindowViewModel ViewModel { get; }

        public WidgetWindow Window { get; }
    }

    private sealed class EntityWindowSession
    {
        public EntityWindowSession(
            WidgetListItemViewModel widget,
            IEntityWidgetWindowViewModel viewModel,
            WidgetWindow window)
        {
            Widget = widget;
            ViewModel = viewModel;
            Window = window;
        }

        public WidgetListItemViewModel Widget { get; }

        public IEntityWidgetWindowViewModel ViewModel { get; }

        public WidgetWindow Window { get; }
    }

    private sealed class ChatWindowSession
    {
        public ChatWindowSession(
            WidgetListItemViewModel widget,
            ChatWidgetViewModel viewModel,
            WidgetWindow window)
        {
            Widget = widget;
            ViewModel = viewModel;
            Window = window;
        }

        public WidgetListItemViewModel Widget { get; }

        public ChatWidgetViewModel ViewModel { get; }

        public WidgetWindow Window { get; }
    }

    /// <summary>プラグインの複数起動のウィジェットの窓1枚。中身はプラグインが持ち、後始末は窓が中身の DataContext に行う。</summary>
    private sealed class PluginTargetWindowSession
    {
        public PluginTargetWindowSession(
            WidgetListItemViewModel widget,
            string? targetId,
            IReadOnlyDictionary<string, string> targetNames,
            WidgetWindow window,
            PluginWidgetWindowViewModel viewModel,
            IPluginWidgetElementHost bodyHost,
            IPluginWidgetElementHost? footerHost)
        {
            Widget = widget;
            TargetId = targetId;
            TargetNames = targetNames;
            Window = window;
            ViewModel = viewModel;
            BodyHost = bodyHost;
            FooterHost = footerHost;
        }

        public WidgetListItemViewModel Widget { get; }

        /// <summary>相手の ID。null は空の窓で、相手が出たら入る。</summary>
        public string? TargetId { get; set; }

        public PluginWidgetWindowViewModel ViewModel { get; }

        public IPluginWidgetElementHost BodyHost { get; }

        public IPluginWidgetElementHost? FooterHost { get; }

        /// <summary>相手の名前(言語ごと)。一覧に居る間は一覧の値、一覧から消えた後は最後に分かっていた値、復元の直後は保存しておいた値。</summary>
        public IReadOnlyDictionary<string, string> TargetNames { get; set; }

        public WidgetWindow Window { get; }

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
