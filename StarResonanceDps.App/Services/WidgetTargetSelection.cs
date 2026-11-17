// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.App.Services;

/// <summary>
/// メイン画面のカードで、複数開けるウィジェット(<see cref="WidgetConfigDefaults.SupportsOpenTargets"/>)の窓の相手を選んで開く・閉じる。
///
/// <para>
/// 項目は名前の選択リストを開いたときにだけ作る(閉じている間は作り直さない)。並べるものは種別で決まる:
/// 人の窓は名簿の全員(プレイヤーリストと同じく、自分 → パーティの番号順 → そのほかの名前順)、バフリスト・デバフリストはそれに続けてエンティティリストの敵、
/// 与ダメ・ヒールなどの詳細とグラフはメーターの行(メーターの並び)、被ダメ詳細は被ダメログに出る人(被ダメの多い順)、バフ・デバフカードはカードの設定のショートカットのオンの行(自分のバフ・デバフ、設定の並び)と、
/// バフリスト(デバフはデバフリスト)のカードで選んでいる相手のバフ(デバフ)、チャットはチャンネルとプライベートトークの相手、
/// プラグインのウィジェットはプラグインが返す相手の一覧。
/// 開いている窓の相手と選んでいる項目は、名簿などから外れていても末尾に足す(選んで閉じられるように)。
/// 行の文字は、その項目で開く窓のタイトル(<see cref="WidgetHeaderText"/>)。
/// </para>
///
/// <para>
/// カードの名前の欄も、選んでいる項目で開く窓のタイトルと同じ文字。起動ボタンは、選んでいる項目の窓が開いているかを映す。
/// 並べるもの(と開いている窓)が無いときは、カードは名前だけの表示に戻り、選択リストも開かない。起動ボタンは既定を開く。
/// </para>
///
/// <para>
/// 人の窓とメーターの種類は、選んでいる項目が空(相手を選んでいない項目)のときだけ、一覧の先頭を入れる(<see cref="ApplyFirstCandidate"/>)。
/// 入れた後は自動では替えない。自分は特別に扱わない(人の窓は名簿の並びで自分が先頭になる)。窓はいつも相手を1人持つ(自分の行でも相手の UID で開く)。
/// 並べるものが無い間の既定は相手を選んでいない項目で、起動ボタンは相手を持たない空の窓を開く。空の窓は一覧にプレイヤーが出たらその人の窓になり、
/// その人の窓がもう開いていれば閉じる(<see cref="EnterAwaitingPlayerWindow"/>。カードはその人を選ぶ)。
/// ほかのウィジェット(プレイヤーリストの行など)から窓を開いたときも、選択リストで選んだのと同じくその窓の相手を選ぶ(<see cref="SelectOpenedTarget"/>)。
/// </para>
///
/// <para>
/// バフ・デバフカードの既定はショートカットの1つ目で、オンの行が1つも無ければ「指定なし」(<see cref="WidgetTargetOption.CreateUnspecifiedBuffCard"/>)。
/// 指定なしの欄の文字はウィジェット名だけ。起動ボタンは、待っている窓が開いていなければ、自分のバフリストの1行目のカードか最初に付いたものを待つ窓を開き
/// (<see cref="WidgetWindowManager.OpenUnspecifiedBuffCard"/>)、開いていれば閉じる。開くカードが決まったら、選択をそのカードに置き換える(<see cref="ResolveUnspecified"/>)。
/// 待っている窓が開いていない指定なしは並べるものが1つも無いときだけの項目で、並べるものが出たらその1つ目で上書きし(<see cref="Refresh"/>)、
/// 並べるものが無くなれば既定として戻る。
/// </para>
///
/// <para>
/// プラグインのウィジェットの既定は相手を選んでいない項目(<see cref="WidgetTargetOption.CreateUnselectedPlugin"/>)で、起動ボタンは本体が出す空の窓を開く・閉じる。
/// 手で選ぶまで、プラグインが既定に指定した相手、無ければ一覧の1番目を入れる(<see cref="ApplyPluginOptions"/>)。本体は相手が何かを知らないので、自分を選ぶかはプラグインが指定で決める。
/// プラグインのコードを呼ぶのは、リストを開いたときと、手で選んでおらず既定の相手も選べていない間・空の窓がある間の当て直しだけ。
/// カードを足す処理の中では呼ばない(プラグインが例外で止まると、足している途中のカードと定義の一覧を外すことになる)。
/// </para>
/// </summary>
public sealed class WidgetTargetSelection
{
    /// <summary>チャットの選択リストに並べるチャンネル。並びはゲームのチャンネルの並び。先頭がカードの既定。</summary>
    private static readonly ChitChatChannelType[] ChatChannels =
    [
        ChitChatChannelType.ChannelWorld,
        ChitChatChannelType.ChannelUnion,
        ChitChatChannelType.ChannelTeam,
        ChitChatChannelType.ChannelScene,
        ChitChatChannelType.ChannelNewbie
    ];

    /// <summary>チャットのカードの既定のチャンネル(ワールド)。カードから初めて起動したときもこのチャンネルの窓を開く。</summary>
    public static ChitChatChannelType DefaultChatChannel => ChatChannels[0];

    private readonly WidgetWindowManager _windowManager;
    private readonly Func<WidgetKind, WidgetListItemViewModel?> _findWidget;

    /// <summary>プラグインの相手の一覧を選択に当てている途中。選択の知らせで戻ってきた当て直しではプラグインのコードを呼ばない。</summary>
    private bool _isApplyingPluginOptions;

    public WidgetTargetSelection(
        WidgetWindowManager windowManager,
        Func<WidgetKind, WidgetListItemViewModel?> findWidget)
    {
        _windowManager = windowManager;
        _findWidget = findWidget;
    }

    /// <summary>
    /// 選んでいる項目の既定。バフ・デバフカードはショートカットの1つ目(無ければ「指定なし」)、チャットはワールド、
    /// プラグインのウィジェットと人の窓・メーターの種類は相手を選んでいない項目(並べるものがあれば当て直しで一覧の先頭が入る)。
    /// </summary>
    public WidgetTargetOption CreateDefault(WidgetListItemViewModel widget)
    {
        return widget.Kind switch
        {
            WidgetKind.BuffDebuffCard => CreateShortcutOptions(widget).FirstOrDefault()
                ?? WidgetTargetOption.CreateUnspecifiedBuffCard(),
            WidgetKind.Chat => WidgetTargetOption.CreateChat(DefaultChatChannel, 0),
            WidgetKind.Plugin => WidgetTargetOption.CreateUnselectedPlugin(),
            _ => WidgetTargetOption.CreateUnselectedPlayer()
        };
    }

    /// <summary>
    /// ログアウトで起動時の状態へ戻した。手で選んでいないプラグインの相手は前のキャラのものかもしれないので、既定から選び直す。
    /// </summary>
    public void ResetPluginSelections(IEnumerable<WidgetListItemViewModel> widgets)
    {
        foreach (var widget in widgets)
        {
            if (widget.PluginWidget is not null && widget.SupportsTargetSelection && !widget.IsTargetChosenByUser)
            {
                widget.SelectedTargetOption = CreateDefault(widget);
                Refresh(widget);
            }
        }
    }

    /// <summary>
    /// 名前の選択リストの項目を作り直す。並べるものが1つも無ければ(前の当て直しの後に消えた)、カードを当て直して名前だけの表示に戻す
    /// (開いたリストは画面のコードが閉じる)。
    /// </summary>
    public void BuildOptions(WidgetListItemViewModel widget)
    {
        if (!widget.SupportsTargetSelection)
        {
            return;
        }

        var options = widget.PluginWidget is not null
            ? ReadPluginOptions(widget)
            : CreateListedOptions(widget);
        if (options.Count > 0)
        {
            AppendSelectedTarget(widget, options);
        }

        widget.TargetOptions = [.. options.Select(option => CreateRow(widget, option))];
        if (options.Count == 0)
        {
            Refresh(widget);
        }
    }

    /// <summary>リストの行。文字はその項目で開く窓のタイトル(<see cref="FormatSelectorText"/>)、円の色はその窓の状態の印(開いていなければ null)。</summary>
    private WidgetTargetOption CreateRow(WidgetListItemViewModel widget, WidgetTargetOption option)
    {
        var (person, buff) = Resolve(widget, option);
        return option.WithPresentation(
            FormatSelectorText(widget, option),
            _windowManager.FindTargetWindow(widget, person, buff)?.DataStatus.Level);
    }

    /// <summary>並べるもの(候補と、候補に無い開いている窓の相手)。選んでいる項目は足さない。</summary>
    private List<WidgetTargetOption> CreateListedOptions(WidgetListItemViewModel widget)
    {
        var options = CreateCandidates(widget);
        AppendOpenTargets(widget, options);
        return options;
    }

    /// <summary>
    /// 選んでいる項目の窓を開くか閉じる(起動ボタン)。バフ・デバフカードの「指定なし」は、待っている窓が開いていなければ開くカードを決める。
    /// 人の窓・メーターの種類の相手を選んでいない項目は、相手を持たない空の窓を開く・閉じる。
    /// </summary>
    public void Toggle(WidgetListItemViewModel widget)
    {
        if (!widget.SupportsTargetSelection)
        {
            return;
        }

        var selected = GetSelected(widget);
        var (person, buff) = Resolve(widget, selected);
        if (_windowManager.FindTargetWindow(widget, person, buff) is not null)
        {
            _windowManager.CloseTarget(widget, person, buff);
        }
        else if (selected.IsUnspecifiedBuffCard)
        {
            _windowManager.OpenUnspecifiedBuffCard(
                widget,
                ResolvePlayerIdentity(WidgetTargetOption.CreateSelf(AppState.PlayerUID)).UserId);
        }
        else
        {
            _windowManager.OpenTarget(widget, person, buff);
        }

        Refresh(widget);
    }

    /// <summary>
    /// カードの名前の欄と起動ボタンを、選んでいる項目と開いている窓で当て直す。値が同じなら変えない。
    /// 選択リストの元のうちバフの行とメーターの行には変わった合図が無いので、メイン画面が全部のカードについて1秒ごとにここを呼ぶ。
    /// </summary>
    public void Refresh(WidgetListItemViewModel widget)
    {
        if (!widget.SupportsTargetSelection)
        {
            return;
        }

        if (widget.PluginWidget is not null)
        {
            RefreshPluginSelection(widget);
        }
        else
        {
            RefreshHostSelection(widget);
        }

        var selected = GetSelected(widget);
        widget.TargetSelectorText = FormatSelectorText(widget, selected);

        var (person, buff) = Resolve(widget, selected);
        widget.IsSelectedTargetOpen = _windowManager.FindTargetWindow(widget, person, buff) is not null;
    }

    /// <summary>
    /// 本体のウィジェットの選択を当て直す。並べるものが無いときは名前だけの表示になるので、起動ボタンが既定を開くよう、選択を既定に戻す。
    /// </summary>
    private void RefreshHostSelection(WidgetListItemViewModel widget)
    {
        // バフ・デバフカードは並べるもの(候補・開いている窓)を作って数える。既定の項目の上書きにも使う。
        var listed = widget.Kind == WidgetKind.BuffDebuffCard ? CreateListedOptions(widget) : null;
        var hasOptions = listed is not null ? listed.Count > 0 : HasOptions(widget);
        widget.HasTargetOptions = hasOptions;
        if (!hasOptions)
        {
            widget.IsTargetChosenByUser = false;
            var defaultOption = CreateDefault(widget);
            if (widget.SelectedTargetOption is { } current && !current.IsSameTarget(defaultOption))
            {
                widget.SelectedTargetOption = defaultOption;
            }

            return;
        }

        if (listed is not null)
        {
            ReplacePlaceholder(widget, listed[0]);
            return;
        }

        if (UsesFirstCandidateDefault(widget.Kind))
        {
            EnterAwaitingPlayerWindow(widget);
            if (!widget.IsTargetChosenByUser && GetSelected(widget).IsUnselectedPlayer)
            {
                ApplyFirstCandidate(widget);
            }
        }
    }

    /// <summary>
    /// 相手を持たない空の窓を、一覧で先頭のプレイヤー(<see cref="FindFirstCandidatePlayer"/>)の窓にする
    /// (<see cref="WidgetWindowManager.EnterAwaitingPlayerWindow"/>。その人の窓がもう開いていれば、空の窓を閉じる。どちらもカードはその人を選ぶ)。
    /// 一覧を作るのは、空の窓があって候補のプレイヤーがいるときだけ(いない間は一覧を作らずに数えて終わる)。
    /// </summary>
    private void EnterAwaitingPlayerWindow(WidgetListItemViewModel widget)
    {
        if (!_windowManager.HasAwaitingPlayerWindow(widget) || !HasPlayerCandidates(widget))
        {
            return;
        }

        if (FindFirstCandidatePlayer(widget) is { } first)
        {
            _windowManager.EnterAwaitingPlayerWindow(widget, first.CharacterId);
        }
    }

    /// <summary>
    /// 一覧で先頭のプレイヤー(UID の分かる人)。空の窓が入る人。バフリスト・デバフリストの一覧は人の後に敵が続くので、敵は飛ばす。
    /// 一覧を作るのは空の窓があるときだけ。
    /// </summary>
    private WidgetTargetOption? FindFirstCandidatePlayer(WidgetListItemViewModel widget)
    {
        return CreateCandidates(widget).FirstOrDefault(option =>
            option.Kind == WidgetTargetOptionKind.Player && option.CharacterId != 0);
    }

    /// <summary>
    /// バフ・デバフカードの「指定なし」は、窓が開いていなければ、並べるものの1つ目で上書きする。
    /// 窓が開いていれば残す(選んで閉じられるように)。
    /// </summary>
    private void ReplacePlaceholder(WidgetListItemViewModel widget, WidgetTargetOption first)
    {
        if (widget.SelectedTargetOption is not { } placeholder
            || !placeholder.IsUnspecifiedBuffCard
            || placeholder.IsSameTarget(first))
        {
            return;
        }

        var (person, buff) = Resolve(widget, placeholder);
        if (_windowManager.FindTargetWindow(widget, person, buff) is null)
        {
            widget.SelectedTargetOption = first;
        }
    }

    /// <summary>空の選択を一覧の先頭で埋める種別か(人の窓とメーターの種類)。</summary>
    private static bool UsesFirstCandidateDefault(WidgetKind kind)
    {
        return kind is not (WidgetKind.BuffDebuffCard or WidgetKind.Chat);
    }

    /// <summary>
    /// 選んでいる項目が空(相手を選んでいない項目)の人の窓・メーターの種類のカードに、並べるもの(候補と開いている窓)の先頭を入れる。
    /// 一覧を作るのは空から埋めるときの1回だけ(当て直しは1秒ごとに全部のカードで走るので、埋まった後は作らない)。
    /// 空の窓の項目(相手を選んでいない項目)は入れない(今の選択と同じで、入れても空のまま次の当て直しでまた一覧を作るため)。
    /// </summary>
    private void ApplyFirstCandidate(WidgetListItemViewModel widget)
    {
        if (CreateListedOptions(widget).FirstOrDefault(option => !option.IsUnselectedPlayer) is { } first
            && !GetSelected(widget).IsSameTarget(first))
        {
            widget.SelectedTargetOption = first;
        }
    }

    /// <summary>
    /// プラグインのウィジェットの選択を当て直す。プラグインのコードを呼ぶのは、手で選んでおらず既定の相手も選べていない間と、空の窓がある間だけ。
    /// </summary>
    private void RefreshPluginSelection(WidgetListItemViewModel widget)
    {
        if (_isApplyingPluginOptions)
        {
            return;
        }

        var selected = GetSelected(widget);
        var isSettled = (widget.IsTargetChosenByUser && !selected.IsUnselectedPlugin) || selected.IsDefaultPluginTarget;
        if (isSettled && !_windowManager.HasEmptyPluginWindow(widget))
        {
            return;
        }

        ReadPluginOptions(widget);
    }

    /// <summary>
    /// プラグインの相手の一覧を読んで選択に当て、並べるものを返す。読んでいる間は、知らせで戻ってきた当て直しではプラグインのコードを呼ばない
    /// (読むたびに名前が変わるプラグインでも、知らせと読み直しが繰り返さない)。
    /// </summary>
    private List<WidgetTargetOption> ReadPluginOptions(WidgetListItemViewModel widget)
    {
        _isApplyingPluginOptions = true;
        try
        {
            var options = CreateListedOptions(widget);
            ApplyPluginOptions(widget, options);
            return options;
        }
        finally
        {
            _isApplyingPluginOptions = false;
        }
    }

    /// <summary>
    /// 読んだ相手の一覧(と開いている窓)を選択に当てる。空なら名前だけの表示に戻して相手を選んでいない項目へ戻す。
    /// 手で選ぶまでは、既定に指定された相手か、相手を選んでいなければ1番目を入れる。
    /// </summary>
    private void ApplyPluginOptions(WidgetListItemViewModel widget, IReadOnlyList<WidgetTargetOption> listed)
    {
        widget.HasTargetOptions = listed.Count > 0;
        var selected = GetSelected(widget);
        if (listed.Count == 0)
        {
            widget.IsTargetChosenByUser = false;
            if (!selected.IsUnselectedPlugin)
            {
                widget.SelectedTargetOption = CreateDefault(widget);
            }

            return;
        }

        // 相手を選んでいない項目は、手で選んでも選択が決まったことにならない(空の窓に相手が入ったら、その相手へ替える)。
        if (widget.IsTargetChosenByUser && !selected.IsUnselectedPlugin)
        {
            return;
        }

        var preferred = listed.FirstOrDefault(option => option.IsDefaultPluginTarget)
            ?? (selected.IsUnselectedPlugin ? listed[0] : null);
        if (preferred is not null
            && (!selected.IsSameTarget(preferred) || selected.IsDefaultPluginTarget != preferred.IsDefaultPluginTarget))
        {
            widget.IsTargetChosenByUser = false;
            widget.SelectedTargetOption = preferred;
        }
    }

    /// <summary>
    /// 「指定なし」で開くカードが決まった(<see cref="WidgetWindowManager.UnspecifiedBuffCardResolved"/>)。今の選択が指定なしなら、
    /// そのカードの項目に置き換える(欄はその窓のタイトル、起動ボタンはその窓を映す)。ほかの項目を選んでいれば置き換えない。
    /// </summary>
    public void ResolveUnspecified(WidgetListItemViewModel widget, WidgetTargetOption card)
    {
        if (widget.SelectedTargetOption?.IsUnspecifiedBuffCard == true)
        {
            widget.SelectedTargetOption = card;
        }
    }

    /// <summary>
    /// カード以外(ほかのウィジェットの行・メニュー)から窓を開いたか前に出した(<see cref="WidgetWindowManager.TargetOpenedFromWidget"/>)。
    /// 選択リストで選んだときと同じく、その窓の項目を手で選んだ扱いで選ぶ(既定の決め方では替えない)。
    /// </summary>
    public void SelectOpenedTarget(WidgetListItemViewModel widget, WidgetTargetOption option)
    {
        if (!widget.SupportsTargetSelection)
        {
            return;
        }

        widget.SelectTargetOption(option);
        Refresh(widget);
    }

    /// <summary>
    /// まだ捕まえていなかった敵の窓が敵を捕まえた(<see cref="WidgetWindowManager.EntityWindowAcquired"/>)。捕まえる前の項目を選んでいれば、
    /// 捕まえた後の項目に置き換える(残すと、同じ窓を指す行が2つ並ぶ)。手で選んだかどうかは変えない。
    /// </summary>
    public void ReplaceAcquiredTarget(WidgetListItemViewModel widget, WidgetTargetOption before, WidgetTargetOption after)
    {
        if (!widget.SupportsTargetSelection)
        {
            return;
        }

        if (widget.SelectedTargetOption is { } selected && selected.IsSameTarget(before))
        {
            widget.SelectedTargetOption = after;
        }

        Refresh(widget);
    }

    public void RefreshAll(IEnumerable<WidgetListItemViewModel> widgets)
    {
        foreach (var widget in widgets)
        {
            Refresh(widget);
        }
    }

    private WidgetTargetOption GetSelected(WidgetListItemViewModel widget)
    {
        return widget.SelectedTargetOption ??= CreateDefault(widget);
    }

    /// <summary>
    /// 並べるもの(候補と、相手を持つ開いている窓)があるか。リストを作らずに、件数や1人でもいるかだけを見る。
    /// 空の窓は数えない(相手を選んでいない既定の項目と同じもので、起動ボタンで開け閉めできる。数えると、空の窓しか無い間も
    /// 空の欄と空の窓を埋める処理が毎秒走り、一覧を作っては何も入れずに終わる)。
    /// バフ・デバフカードはここを使わない(<see cref="Refresh"/> が並べるものを作って数える)。
    /// </summary>
    private bool HasOptions(WidgetListItemViewModel widget)
    {
        if (_windowManager.HasFilledTargetWindows(widget))
        {
            return true;
        }

        return widget.Kind switch
        {
            WidgetKind.Chat => true,
            WidgetKind.BuffList or WidgetKind.DebuffList
                => HasPlayerCandidates(widget) || _findWidget(WidgetKind.EntityList)?.EntityListEntries.Count > 0,
            _ => HasPlayerCandidates(widget)
        };
    }

    /// <summary>候補のプレイヤーが1人でもいるか(空の窓が入る人がいるか)。リストを作らずに数える。</summary>
    private bool HasPlayerCandidates(WidgetListItemViewModel widget)
    {
        return widget.Kind switch
        {
            WidgetKind.DamageSkillDetail or WidgetKind.DamageSummary or WidgetKind.DpsGraph
                => MeterSnapshotProvider.HasPlayers(MeterSnapshotKind.Damage, GetPartyDisplayMode(WidgetKind.DpsMeter)),
            WidgetKind.HealingSkillDetail or WidgetKind.HealingSummary or WidgetKind.HpsGraph
                => MeterSnapshotProvider.HasPlayers(MeterSnapshotKind.Healing, GetPartyDisplayMode(WidgetKind.HpsMeter)),
            WidgetKind.HateSkillDetail or WidgetKind.HateGraph
                => MeterSnapshotProvider.HasPlayers(MeterSnapshotKind.Hate, GetPartyDisplayMode(WidgetKind.HateMeter)),
            WidgetKind.BreakSkillDetail or WidgetKind.BreakGraph
                => MeterSnapshotProvider.HasPlayers(MeterSnapshotKind.Break, GetPartyDisplayMode(WidgetKind.BreakMeter)),
            WidgetKind.DamageTakenSummary => MeterSnapshotProvider.HasDamageTakenPlayers(),
            WidgetKind.Chat => false,
            _ => PlayerRosterPresentationStore.Instance.Current.Entries.Count > 0
        };
    }

    private List<WidgetTargetOption> CreateCandidates(WidgetListItemViewModel widget)
    {
        return widget.Kind switch
        {
            WidgetKind.BuffDebuffCard => CreateBuffCardOptions(widget),
            WidgetKind.DamageSkillDetail or WidgetKind.DamageSummary or WidgetKind.DpsGraph
                => CreateMeterOptions(MeterSnapshotKind.Damage, WidgetKind.DpsMeter),
            WidgetKind.HealingSkillDetail or WidgetKind.HealingSummary or WidgetKind.HpsGraph
                => CreateMeterOptions(MeterSnapshotKind.Healing, WidgetKind.HpsMeter),
            WidgetKind.HateSkillDetail or WidgetKind.HateGraph => CreateMeterOptions(MeterSnapshotKind.Hate, WidgetKind.HateMeter),
            WidgetKind.BreakSkillDetail or WidgetKind.BreakGraph => CreateMeterOptions(MeterSnapshotKind.Break, WidgetKind.BreakMeter),
            WidgetKind.DamageTakenSummary => CreateDamageTakenOptions(),
            WidgetKind.Chat => CreateChatOptions(),
            WidgetKind.Plugin => CreatePluginOptions(widget),
            _ => CreateRosterOptions(includesEntities: widget.Kind is WidgetKind.BuffList or WidgetKind.DebuffList)
        };
    }

    /// <summary>プラグインが返す相手の一覧。読むたびに、開いている窓へ新しい名前を当て、空の窓に相手を入れる。</summary>
    private List<WidgetTargetOption> CreatePluginOptions(WidgetListItemViewModel widget)
    {
        var targets = widget.PluginWidget!.ReadTargets();
        _windowManager.ApplyPluginTargets(widget, targets);
        return [.. targets.Select(target => new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Plugin,
            PluginTargetId = target.Id,
            PluginTargetNames = target.Names,
            IsDefaultPluginTarget = target.IsDefault
        })];
    }

    /// <summary>
    /// 項目から、開く窓の相手とバフを決める。バフ・デバフカードの相手は、開いている窓の項目ならその窓の相手、
    /// 自分のカード(<see cref="WidgetTargetOption.IsSelf"/>、ショートカット・「指定なし」とそれが決まった項目)なら自分、
    /// それ以外はバフリスト(デバフはデバフリスト)のカードで今選んでいる項目。
    /// </summary>
    private (WidgetTargetOption Person, WidgetTargetOption? Buff) Resolve(WidgetListItemViewModel widget, WidgetTargetOption option)
    {
        if (widget.Kind != WidgetKind.BuffDebuffCard)
        {
            return (option, null);
        }

        return (ResolveBuffPerson(option), option);
    }

    private WidgetTargetOption ResolveBuffPerson(WidgetTargetOption buff)
    {
        if (buff.BuffPerson is { } openWindowPerson)
        {
            return openWindowPerson;
        }

        if (buff.IsSelf)
        {
            return WidgetTargetOption.CreateSelf(AppState.PlayerUID);
        }

        var listWidget = _findWidget(buff.BuffListKind == PlayerBuffListKind.Debuff ? WidgetKind.DebuffList : WidgetKind.BuffList);
        return listWidget?.SelectedTargetOption ?? WidgetTargetOption.CreateSelf(AppState.PlayerUID);
    }

    /// <summary>
    /// その項目で開く窓のタイトルと同じ文字。カードの名前の欄とリストの行に使う。
    /// 窓が開いていれば窓のタイトルそのもの(再起動の直後は窓が保存しておいた名前で名乗り、名簿にはまだ相手がいない)。
    /// </summary>
    private string FormatSelectorText(WidgetListItemViewModel widget, WidgetTargetOption option)
    {
        // 「指定なし」で開く窓は決まっていない(1行目のカードか、待つ窓)。待つ窓のタイトルと同じくウィジェット名だけ。
        if (option.IsUnspecifiedBuffCard)
        {
            return widget.DisplayName;
        }

        var (person, buff) = Resolve(widget, option);
        if (_windowManager.FindTargetWindow(widget, person, buff) is { } window)
        {
            return window.HeaderText;
        }

        if (option.Kind == WidgetTargetOptionKind.Chat)
        {
            return WidgetHeaderText.ForChat(
                widget.DisplayName,
                option.ChatChannel,
                option.CharacterId,
                ChatMessageStore.GetPrivatePartnerName(option.CharacterId),
                PlayerRosterPresentationStore.Instance.NameDisplayMode);
        }

        if (option.Kind == WidgetTargetOptionKind.Plugin)
        {
            return WidgetHeaderText.Join(
                widget.DisplayName,
                PluginTexts.Resolve(option.PluginTargetNames ?? PluginTexts.Empty, LocalizationManager.Instance.CurrentCulture));
        }

        if (option.Kind == WidgetTargetOptionKind.Buff)
        {
            var prefix = BuffDebuffCardContent.CreateHeaderPrefix(
                widget.DisplayName,
                option.BuffListKind,
                option.BuffGroup,
                option.BuffName);
            return FormatPersonText(prefix, ResolveBuffPerson(option));
        }

        return FormatPersonText(widget.DisplayName, option);
    }

    private static string FormatPersonText(string prefix, WidgetTargetOption person)
    {
        if (person.Kind == WidgetTargetOptionKind.Entity)
        {
            return WidgetHeaderText.Join(prefix, person.Entity?.Name ?? person.UnacquiredEntityName);
        }

        var identity = ResolvePlayerIdentity(person);
        return WidgetHeaderText.ForPlayer(
            prefix,
            identity.Name,
            identity.UserId,
            identity.IsSelf,
            identity.IsNpc,
            identity.ProfessionId,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);
    }

    /// <summary>
    /// 人の素性。窓と同じく、名簿の行、無ければメーターの実体から引く。どちらにも無ければ、自分は何も分からない、
    /// ほかの人は相手の指定の UID だけ(窓のタイトルも相手の指定の UID を出す)。相手を選んでいない項目は何も分からない。
    /// </summary>
    private static PlayerIdentity ResolvePlayerIdentity(WidgetTargetOption person)
    {
        if (person.IsUnselectedPlayer)
        {
            return new PlayerIdentity(null, 0, false, false, 0);
        }

        var roster = PlayerRosterPresentationStore.Instance.Current.Entries;
        if (person.IsSelf)
        {
            if (roster.FirstOrDefault(entry => entry.IsSelf) is { } selfRow)
            {
                return PlayerIdentity.FromRoster(selfRow);
            }

            return MeterSnapshotProvider.GetSelfPlayerIdentity() is { } selfIdentity
                ? PlayerIdentity.FromMeter(selfIdentity, isSelf: true)
                : new PlayerIdentity(null, 0, true, false, 0);
        }

        if (roster.FirstOrDefault(entry => entry.CharacterId == person.CharacterId) is { } row)
        {
            return PlayerIdentity.FromRoster(row);
        }

        var isSelf = AppState.PlayerUID != 0 && person.CharacterId == AppState.PlayerUID;
        return MeterSnapshotProvider.GetPlayerIdentity(person.CharacterId) is { } identity
            ? PlayerIdentity.FromMeter(identity, isSelf)
            : new PlayerIdentity(null, person.CharacterId, isSelf, false, 0);
    }

    /// <summary>
    /// 名簿の全員(プレイヤーリストと同じく、自分 → パーティの番号順 → 他の名前順)。自分の行は名簿が持つ(自分が分かっていれば必ずある)。
    /// バフリスト・デバフリストはエンティティリストの敵を続ける。
    /// </summary>
    private List<WidgetTargetOption> CreateRosterOptions(bool includesEntities)
    {
        var roster = PlayerRosterPresentationStore.Instance.Current.Entries;
        var options = new List<WidgetTargetOption>();

        options.AddRange(roster
            .Where(entry => entry.IsSelf)
            .Select(entry => WidgetTargetOption.CreatePlayer(entry.CharacterId)));

        options.AddRange(roster
            .Where(entry => !entry.IsSelf && entry.IsPartyMember)
            .OrderBy(entry => entry.PartyNumber ?? int.MaxValue)
            .Select(entry => WidgetTargetOption.CreatePlayer(entry.CharacterId)));

        options.AddRange(roster
            .Where(entry => !entry.IsSelf && !entry.IsPartyMember)
            .Select(entry => PlayerIdentity.FromRoster(entry))
            .OrderBy(identity => identity.DisplayName, StringComparer.CurrentCulture)
            .Select(identity => WidgetTargetOption.CreatePlayer(identity.UserId)));

        if (includesEntities)
        {
            options.AddRange(CreateEntityOptions());
        }

        return options;
    }

    /// <summary>チャットのチャンネル(<see cref="ChatChannels"/> の並び)と、プライベートトークの相手(最後のメッセージが新しい順)。</summary>
    private static List<WidgetTargetOption> CreateChatOptions()
    {
        var options = ChatChannels
            .Select(channel => WidgetTargetOption.CreateChat(channel, 0))
            .ToList();
        options.AddRange(ChatMessageStore.GetPrivatePartners()
            .Select(partner => WidgetTargetOption.CreateChat(ChitChatChannelType.ChannelPrivate, partner.CharacterId)));
        return options;
    }

    /// <summary>エンティティリストの敵(エンティティリストの並び)。</summary>
    private List<WidgetTargetOption> CreateEntityOptions()
    {
        return _findWidget(WidgetKind.EntityList) is { } entityList
            ? [.. entityList.EntityListEntries.Select(CreateEntityOption)]
            : [];
    }

    /// <summary>メーターに出ている行(メーターの並びと、メーターのカードの設定のパーティ表示のまま)。</summary>
    private List<WidgetTargetOption> CreateMeterOptions(MeterSnapshotKind kind, WidgetKind meterKind)
    {
        var options = new List<WidgetTargetOption>();
        foreach (var player in MeterSnapshotProvider.GetSnapshot(kind, GetPartyDisplayMode(meterKind)).Players)
        {
            // メーターの行から開くときと同じ相手の指定(MeterPlayerEntry.PlayerId)。
            options.Add(WidgetTargetOption.CreatePlayer(player.UserId != 0 ? player.UserId : player.CharacterId));
        }

        return options;
    }

    /// <summary>被ダメ詳細。被ダメログに出る人(その回で被弾の記録がある人)を被ダメの合計の多い順。</summary>
    private static List<WidgetTargetOption> CreateDamageTakenOptions()
    {
        var options = new List<WidgetTargetOption>();
        foreach (var player in MeterSnapshotProvider.GetDamageTakenPlayers())
        {
            // 被ダメログの行から開くときと同じ相手の指定(DamageTakenLogEntry.CharacterId)。
            options.Add(WidgetTargetOption.CreatePlayer(player.CharacterId));
        }

        return options;
    }

    private PartyDisplayMode GetPartyDisplayMode(WidgetKind meterKind)
    {
        return (PartyDisplayMode)(_findWidget(meterKind)?.MeterSettings.PartyDisplayModeIndex ?? 0);
    }

    /// <summary>
    /// 先頭にショートカットのオンの行(<see cref="CreateShortcutOptions"/>)、
    /// 続けてバフリストのカードで選んでいる相手のバフ、デバフリストのカードで選んでいる相手のデバフ。
    /// </summary>
    private List<WidgetTargetOption> CreateBuffCardOptions(WidgetListItemViewModel widget)
    {
        var options = CreateShortcutOptions(widget);
        AppendBuffRows(options, PlayerBuffListKind.Buff, WidgetKind.BuffList);
        AppendBuffRows(options, PlayerBuffListKind.Debuff, WidgetKind.DebuffList);
        return options;
    }

    /// <summary>
    /// バフ・デバフカードの設定のショートカットのオンの行(設定の並び)から、自分のカードの項目を作る(相手はいつも自分)。
    /// バフかデバフか分からない番号は作らない(<see cref="CreateSelfGroupEntryOption"/>)。
    /// </summary>
    private static List<WidgetTargetOption> CreateShortcutOptions(WidgetListItemViewModel widget)
    {
        var options = new List<WidgetTargetOption>();
        foreach (var key in widget.GetEnabledBuffCardShortcuts())
        {
            if (CreateSelfGroupEntryOption(key) is { } option)
            {
                options.Add(option);
            }
        }

        return options;
    }

    /// <summary>
    /// 行の鍵(ショートカット・バフ・デバフグループの行)から、自分のカードの項目を作る。開き方はグループの行の「バフカード」「デバフカード」と同じ
    /// (<see cref="WidgetWindowManager.OpenBuffCardForGroupEntry"/>)。バフかデバフか分からない番号は作らない。
    /// </summary>
    private static WidgetTargetOption? CreateSelfGroupEntryOption(BuffEntryKey key)
    {
        if (key.IsGroup)
        {
            return WidgetTargetOption.CreateSelfBuffGroup(key.Group);
        }

        if (CombatDataCatalog.GetBuffListKind(key.BuffId) is not { } kind)
        {
            return null;
        }

        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Buff,
            IsSelf = true,
            BuffListKind = kind,
            BuffKey = MeterSnapshotProvider.CreateBuffCardKey(key.BuffId),
            BuffName = CombatDataCatalog.GetBuffDisplayNameWithoutInternalId(key.BuffId, SourceLanding.None)
        };
    }

    /// <summary>同じカード(同じ相手の、同じ種類・まとまり・鍵)を指す項目か。</summary>
    private bool IsSameCard(WidgetTargetOption existing, WidgetTargetOption buff, WidgetTargetOption person)
    {
        return existing.Kind == WidgetTargetOptionKind.Buff
            && existing.BuffListKind == buff.BuffListKind
            && existing.BuffGroup == buff.BuffGroup
            && string.Equals(existing.BuffKey, buff.BuffKey, StringComparison.Ordinal)
            && IsSamePerson(ResolveBuffPerson(existing), person);
    }

    /// <summary>
    /// リストのカードで選んでいる相手のバフ(デバフ)を、そのリストと同じ作り方(そのカードの設定の非表示リスト・同種バフの表示)で並べる。
    /// 1枚のカードはバフの番号ごと(<c>PlayerBuffSnapshot.CardKey</c>)なので、行はカードに出す代表の行だけ。
    /// 先頭のショートカットに同じカード(<see cref="IsSameCard"/>、相手が自分のとき)があれば、そちらに任せて並べない。
    /// </summary>
    private void AppendBuffRows(List<WidgetTargetOption> options, PlayerBuffListKind kind, WidgetKind listKind)
    {
        var listWidget = _findWidget(listKind);
        var person = listWidget?.SelectedTargetOption ?? WidgetTargetOption.CreateSelf(AppState.PlayerUID);
        var settings = listWidget?.GetBuffListSettingsSnapshot() ?? WidgetConfigDefaults.CreateBuffListSettings(listKind);

        IReadOnlyList<PlayerBuffSnapshot> rows;
        if (person.Kind == WidgetTargetOptionKind.Entity)
        {
            if (person.Entity is not { } entity)
            {
                return;
            }

            rows = BuffListSnapshotFilter.Apply(
                settings,
                MeterSnapshotProvider.GetEntityBuffs(
                    entity.EntityUuid,
                    kind,
                    WidgetConfigDefaults.GroupsSameBuffs(settings, isEntityTarget: true)));
        }
        else
        {
            var identity = ResolvePlayerIdentity(person);
            if (identity.UserId == 0)
            {
                return;
            }

            rows = BuffListSnapshotFilter.GetPlayerRows(settings, identity.UserId, kind);
        }

        foreach (var snapshot in rows)
        {
            if (!snapshot.IsCardRepresentative)
            {
                continue;
            }

            // バフリストの行から開くのと同じく、行のバフのまとまりで開く(料理・薬剤はまとまりで、バフのキーを持たない。ショートカットと同じ形)。
            var group = CombatDataCatalog.GetBuffGroup(snapshot.BaseId);
            var row = new WidgetTargetOption
            {
                Kind = WidgetTargetOptionKind.Buff,
                BuffListKind = kind,
                BuffKey = group == BuffGroup.None ? snapshot.CardKey : null,
                BuffGroup = group,
                BuffName = snapshot.NameWithoutInternalId
            };

            if (!options.Any(existing => IsSameCard(existing, row, person)))
            {
                options.Add(row);
            }
        }
    }

    /// <summary>開いている窓の相手で、リストに無いものを末尾に足す(選んで閉じられるように)。バフ・デバフカードの項目は窓の相手を持つ。</summary>
    private void AppendOpenTargets(WidgetListItemViewModel widget, List<WidgetTargetOption> options)
    {
        foreach (var (person, buff) in _windowManager.GetOpenTargets(widget))
        {
            if (widget.Kind == WidgetKind.BuffDebuffCard)
            {
                if (buff is not null && !options.Any(existing => IsSameCard(existing, buff, person)))
                {
                    options.Add(buff);
                }

                continue;
            }

            if (!options.Any(existing => existing.IsSameTarget(person)))
            {
                options.Add(person);
            }
        }
    }

    /// <summary>
    /// 選んでいる項目がリストに無ければ末尾に足す。バフ・デバフカードは同じカード(<see cref="IsSameCard"/>)があれば足さない
    /// (「指定なし」から置き換えた自分のカードと、バフリストのカードで自分を選んでいるときの自分のバフの行は、同じカードを指す)。
    /// </summary>
    private void AppendSelectedTarget(WidgetListItemViewModel widget, List<WidgetTargetOption> options)
    {
        var selected = GetSelected(widget);
        // 相手を選んでいない項目は、並べるものがあるときには残さない(次の当て直しで1つ目に替わる)。
        if (selected.IsUnselectedPlugin)
        {
            return;
        }

        var isListed = widget.Kind == WidgetKind.BuffDebuffCard && !selected.IsUnspecifiedBuffCard
            ? options.Any(option => IsSameCard(option, selected, ResolveBuffPerson(selected)))
            : options.Any(option => option.IsSameTarget(selected));
        if (!isListed)
        {
            options.Add(selected);
        }
    }

    private static bool IsSamePerson(WidgetTargetOption left, WidgetTargetOption right)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        if (left.Kind == WidgetTargetOptionKind.Entity)
        {
            return left.IsSameTarget(right);
        }

        var leftId = left.IsSelf ? AppState.PlayerUID : left.CharacterId;
        var rightId = right.IsSelf ? AppState.PlayerUID : right.CharacterId;
        return (left.IsSelf && right.IsSelf) || (leftId != 0 && leftId == rightId);
    }

    private static WidgetTargetOption CreateEntityOption(EntityListEntry entity)
    {
        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Entity,
            Entity = entity.Target
        };
    }

    /// <summary>人の素性。名前は伏せ字なら null(伏せるかは名前の規則が決める)。</summary>
    private sealed record PlayerIdentity(
        string? Name,
        long UserId,
        bool IsSelf,
        bool IsNpc,
        int ProfessionId)
    {
        /// <summary>名前の規則を通した名前(名前が無ければ UID)。並べ替えに使う。</summary>
        public string DisplayName => PlayerInfoFormatFormatter.GetDisplayName(
            Name,
            UserId,
            IsSelf,
            IsNpc,
            ProfessionId,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);

        public static PlayerIdentity FromRoster(PlayerRosterEntry row)
        {
            return new PlayerIdentity(
                NameOrNull(row.Name),
                row.CharacterId,
                row.IsSelf,
                row.IsNpc,
                row.ProfessionId);
        }

        public static PlayerIdentity FromMeter(MeterPlayerIdentity identity, bool isSelf)
        {
            return new PlayerIdentity(
                NameOrNull(identity.Name),
                identity.UserId,
                isSelf,
                identity.IsNpc,
                identity.ProfessionId);
        }

        private static string? NameOrNull(string? name)
        {
            return string.Equals(name, PlayerRosterPresentationStore.HiddenPlayerName, StringComparison.Ordinal)
                ? null
                : name;
        }
    }
}
