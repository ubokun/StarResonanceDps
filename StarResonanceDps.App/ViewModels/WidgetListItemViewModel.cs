// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
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

public partial class WidgetListItemViewModel : ViewModelBase, IWidgetDataStatusSource
{
    private readonly ObservableCollection<PlayerListEntry> _playerListEntries = [];
    private readonly ObservableCollection<EntityListEntry> _entityListEntries = [];

    /// <summary>プレイヤーリストの人ごとの技の枠の値。一覧から外れた人はその同期で、全員はマップ移動で手放す。</summary>
    private readonly Dictionary<long, PlayerSkillDisplayState> _playerSkillStates = [];
    private readonly Func<long, PlayerSkillDisplayState> _getPlayerSkillState;

    /// <summary>右クリックのメニューを開いた行(閉じていれば null)と、開いた時の相手。</summary>
    private PlayerListEntry? _playerMenuRow;
    private long _playerMenuCharacterId;
    private EntityListEntry? _entityMenuRow;
    private long _entityMenuUuid;
    private WidgetThemeConfig _theme = WidgetConfigDefaults.CreateTheme();
    private MeterWidgetSettingsConfig _meter = WidgetConfigDefaults.CreateMeterSettings(WidgetKind.PlayerList);
    private MetricGraphWidgetSettingsConfig _metricGraph = WidgetConfigDefaults.CreateMetricGraphSettings(WidgetKind.DpsGraph);
    private BuffCardWidgetSettingsConfig _buffCard = WidgetConfigDefaults.CreateBuffCardSettings();
    private TimelineWidgetSettingsConfig _timeline = WidgetConfigDefaults.CreateTimelineSettings();
    private PlayerInfoWidgetSettingsConfig _playerInfo = WidgetConfigDefaults.CreatePlayerInfoSettings();
    private BuffListWidgetSettingsConfig _buffList = WidgetConfigDefaults.CreateBuffListSettings(WidgetKind.BuffList);
    private ElementColorWidgetSettingsConfig _elementColor = WidgetConfigDefaults.CreateElementColorSettings(WidgetKind.DamageSkillDetail);
    private SkillDetailWidgetSettingsConfig _skillDetail = WidgetConfigDefaults.CreateSkillDetailSettings();
    private EquipmentWidgetSettingsConfig _equipment = WidgetConfigDefaults.CreateEquipmentSettings();
    private PlayerStatusWidgetSettingsConfig _playerStatus = WidgetConfigDefaults.CreatePlayerStatusSettings();
    private List<int> _playerStatusRowOrder = WidgetConfigDefaults.NormalizePlayerStatusRowOrder(null);
    private BuffDebuffGroupWidgetSettingsConfig _buffDebuffGroup = WidgetConfigDefaults.CreateBuffDebuffGroupSettings();
    private ReviveListWidgetSettingsConfig _reviveList = WidgetConfigDefaults.CreateReviveListSettings();
    private DamageTakenLogWidgetSettingsConfig _damageTakenLog = WidgetConfigDefaults.CreateDamageTakenLogSettings();
    private EntityInfoWidgetSettingsConfig _entityInfo = WidgetConfigDefaults.CreateEntityInfoSettings();
    private ChatWidgetSettingsConfig _chat = WidgetConfigDefaults.CreateChatSettings();
    private PluginWidgetSettingsConfig _pluginSettings = new();

    /// <summary>エンティティリストの行を組む設定。メーターの設定(<c>_meter</c>)が変わったときに作り直す。</summary>
    private EntityRowSettings _entityRowSettings;

    /// <summary>エンティティリストのヘイトの枠の数。狙っている相手の1つと、「ヘイト値」の一番多い人数。</summary>
    private static readonly int EntityListHateSlotCount = WidgetConfigDefaults.HateRankDisplayCounts.Max() + 1;

    /// <summary>
    /// 設定の窓が開いている間だけの、ステータス詳細の行の並び(保存前のプレビュー)。null は窓が開いていない。
    /// ある間はウィジェットもこの並びで出し、ウィジェットでの並べ替えもこの並びを変えるだけにする(設定の窓の「保存」で確定、「破棄」で戻る)。
    /// </summary>
    private List<int>? _playerStatusRowOrderPreview;

    /// <summary>設定の窓が開いているか(<see cref="BeginSettingsPreview"/> / <see cref="EndSettingsPreview"/>)。ウィジェットでの並べ替えを保存するかプレビューにするかを決める。</summary>
    private bool _isSettingsPreviewing;
    private IReadOnlyList<PlayerRosterEntry> _playerRoster = Array.Empty<PlayerRosterEntry>();

    /// <summary>プレイヤーリストに出している行(絞り込みの後)。状態の印の計算に使う。</summary>
    private IReadOnlyList<PlayerRosterEntry> _shownPlayerRoster = Array.Empty<PlayerRosterEntry>();
    private EntityHistorySnapshot _entityHistory = EntityHistorySnapshot.Empty;
    private IReadOnlyList<NearbyEntityEntry> _nearbyEntities = Array.Empty<NearbyEntityEntry>();

    /// <summary>エンティティリストのヘイトの枠のプレイヤーの見た目を名簿から引く(<see cref="UpdateEntityListRoster"/> で名簿を受ける)。</summary>
    private readonly EntityHatePlayerResolver _entityHatePlayers = new();
    private string _mapSceneName = string.Empty;
    private uint _mapChannel;
    private long _playerListMapGeneration = -1;
    private long _entityListMapGeneration = -1;
    private DungeonScoreSnapshot _dungeonScore = DungeonScoreSnapshot.Empty;

    public WidgetKind Kind { get; init; }

    /// <summary>プラグインのウィジェットのカードなら、その定義(<see cref="WidgetKind.Plugin"/> のときだけ)。</summary>
    public PluginWidgetRegistration? PluginWidget { get; init; }

    /// <summary>保存の鍵。本体のウィジェットは種別、プラグインのウィジェットは「プラグインの鍵/ウィジェットの ID」。</summary>
    public WidgetKey Key => PluginWidget?.Key ?? Kind;

    /// <summary>窓にフッターの行があるか(設定の窓の「ピン留め中フッターを隠す」を出すか)。</summary>
    public bool HasFooter => PluginWidget?.Definition.HasFooter ?? WidgetConfigDefaults.HasFooter(Kind);

    public int OriginalIndex { get; init; }

    public string DisplayNameResourceKey { get; init; } = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private WidgetState _state;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isPinned;

    /// <summary>クリック透過。窓側(<c>WidgetWindow</c>)がこの値を見て、マウス入力を裏の窓へ通す。</summary>
    [ObservableProperty]
    private bool _isClickThrough;

    [ObservableProperty]
    private ThemeColorPalette _themePalette = ThemeColorPalette.Create(Color.FromRgb(0x0B, 0x16, 0x24));

    /// <summary>ピン留め中にヘッダーを隠すか。ウィンドウ側が読む。</summary>
    [ObservableProperty]
    private bool _hideHeaderWhenInactive = true;

    /// <summary>ピン留め中にフッターを隠すか。ウィンドウ側が読む。</summary>
    [ObservableProperty]
    private bool _hideFooterWhenInactive;

    [ObservableProperty]
    private WidgetWindowThemePalette _widgetWindowPalette = WidgetWindowThemePalette.Create(Color.FromRgb(0x0B, 0x16, 0x24), 50);

    [ObservableProperty]
    private string? _backgroundImagePath;

    [ObservableProperty]
    private bool _hasBackgroundImage;

    [ObservableProperty]
    private string _mapName = string.Empty;

    [ObservableProperty]
    private string _footerCombatLabel = string.Empty;

    [ObservableProperty]
    private string _footerCombatValueText = string.Empty;

    [ObservableProperty]
    private string _footerOverallLabel = string.Empty;

    [ObservableProperty]
    private string _footerOverallValueText = string.Empty;

    [ObservableProperty]
    private int _openPlayerWindowCount;

    /// <summary>
    /// メイン窓のカードの円の色。開いている窓のうち一番悪い印で、開いている窓が無ければ null(停止の灰色)。決めるのは窓を束ねる側(<see cref="WidgetWindowManager"/>)。
    /// </summary>
    [ObservableProperty]
    private WidgetDataStatusLevel? _cardDataStatusLevel;

    /// <summary>名前の選択リストの項目(複数開けるウィジェットだけ)。リストを開いたときに作り直す(<see cref="WidgetTargetSelection"/>)。</summary>
    [ObservableProperty]
    private IReadOnlyList<WidgetTargetOption> _targetOptions = Array.Empty<WidgetTargetOption>();

    /// <summary>
    /// 名前の選択リストで選んでいる項目(複数開けるウィジェットだけ)。起動ボタンはこの項目の窓を開く・閉じる。保存しない。
    /// 選択リストの選択とは別に持つ(リストは選んだら選択を戻すので、リストに出ていない項目を選んだままでも消えない)。
    /// </summary>
    [ObservableProperty]
    private WidgetTargetOption? _selectedTargetOption;

    /// <summary>名前の欄の文字。選んでいる項目で開く窓のタイトルと同じ(<see cref="WidgetHeaderText"/>)。決めるのは <see cref="WidgetTargetSelection"/>。</summary>
    [ObservableProperty]
    private string _targetSelectorText = string.Empty;

    /// <summary>選んでいる項目の窓が開いているか。決めるのは <see cref="WidgetTargetSelection"/>。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsStopIcon))]
    private bool _isSelectedTargetOpen;

    /// <summary>
    /// 選択リストに並べるもの(か開いている窓)があるか。決めるのは <see cref="WidgetTargetSelection"/>。
    /// 無ければ名前の欄はウィジェット名だけの表示に戻り、選択リストは出さない。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTargetSelector))]
    private bool _hasTargetOptions;

    /// <summary>
    /// メイン画面のウィジェットの検索欄の文字に当たるか。偽ならカードを隠す(カードは作り直さない)。決めるのはメイン画面(<see cref="MainViewModel"/>)で、
    /// 比べるのは名前の欄に出している文字(<see cref="ShownName"/>)。
    /// </summary>
    [ObservableProperty]
    private bool _isSearchMatch = true;

    public ReadOnlyObservableCollection<PlayerListEntry> PlayerListEntries { get; }

    public ReadOnlyObservableCollection<EntityListEntry> EntityListEntries { get; }

    public bool IsPlayerList => Kind == WidgetKind.PlayerList;

    public bool IsEntityList => Kind == WidgetKind.EntityList;

    public bool IsPlayerInfo => Kind == WidgetKind.PlayerInfo;

    public bool IsPlayerEquipment => Kind == WidgetKind.PlayerEquipment;

    public bool IsBuffDebuffCard => Kind == WidgetKind.BuffDebuffCard;

    /// <summary>
    /// 相手ごとに窓を開く(か、自分の窓を1枚だけ開く)ウィジェットか。名前は本体の人の窓から来ていて、プラグインの複数起動のウィジェットも入る。
    /// </summary>
    public bool IsPlayerWindowWidget => PluginWidget is { IsMultiWindow: true } || Kind is WidgetKind.PlayerInfo
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

    public bool HasOpenPlayerWindows => IsPlayerWindowWidget && OpenPlayerWindowCount > 0;

    /// <summary>開いている窓の数のバッジを出すか。自分だけを出す1枚の窓(ステータス詳細・バフ・デバフグループ)は出さない。</summary>
    public bool ShowsPlayerWindowCountBadge => IsPlayerWindowWidget && !WidgetConfigDefaults.IsSelfOnlyPlayerWindowKind(Kind);

    /// <summary>名前の欄を、窓の相手を選ぶ選択リストにする種別か(複数開けるウィジェット)。</summary>
    public bool SupportsTargetSelection => PluginWidget is { IsMultiWindow: true } || WidgetConfigDefaults.SupportsOpenTargets(Kind);

    /// <summary>
    /// 選択リストで手で項目を選んだか。選んだ後は既定の決め方(自分・プラグインが指定した相手)で選択を替えない。保存しない。
    /// 並べるものが無くなって既定に戻ったら偽に戻る。
    /// </summary>
    public bool IsTargetChosenByUser { get; set; }

    /// <summary>名前の欄に選択リストを出すか。相手を選ぶ種別で、並べるものがあるときだけ。</summary>
    public bool ShowsTargetSelector => SupportsTargetSelection && HasTargetOptions;

    /// <summary>カードの名前の欄に出している文字。選択リストを出しているときは選んでいる窓のタイトル、そうでなければウィジェット名。</summary>
    public string ShownName => ShowsTargetSelector ? TargetSelectorText : DisplayName;

    /// <summary>起動ボタンを停止の絵にするか。相手を選ぶ種別は選んでいる項目の窓が開いているか、ほかは起動中か。</summary>
    public bool ShowsStopIcon => SupportsTargetSelection ? IsSelectedTargetOpen : State == WidgetState.Running;

    /// <summary>メーター系の今の設定。<b>複製しない。読むだけで、書き換えない。</b>定期更新と名簿の更新が読む。値は代入の時点で正規化してある。</summary>
    public MeterWidgetSettingsConfig MeterSettings => _meter;

    public MetricGraphWidgetSettingsConfig GetMetricGraphSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedMetricGraph(Kind, _metricGraph);
    }

    public TimelineWidgetSettingsConfig GetTimelineSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedTimeline(_timeline);
    }

    public PlayerInfoWidgetSettingsConfig GetPlayerInfoSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedPlayerInfo(_playerInfo);
    }

    public BuffListWidgetSettingsConfig GetBuffListSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedBuffList(Kind, _buffList);
    }

    public ElementColorWidgetSettingsConfig GetElementColorSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedElementColor(Kind, _elementColor);
    }

    public SkillDetailWidgetSettingsConfig GetSkillDetailSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedSkillDetail(_skillDetail);
    }

    public EquipmentWidgetSettingsConfig GetEquipmentSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedEquipment(_equipment);
    }

    public PlayerStatusWidgetSettingsConfig GetPlayerStatusSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedPlayerStatus(_playerStatus);
    }

    public BuffDebuffGroupWidgetSettingsConfig GetBuffDebuffGroupSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedBuffDebuffGroup(_buffDebuffGroup);
    }

    public ReviveListWidgetSettingsConfig GetReviveListSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedReviveList(_reviveList);
    }

    public DamageTakenLogWidgetSettingsConfig GetDamageTakenLogSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedDamageTakenLog(_damageTakenLog);
    }

    public EntityInfoWidgetSettingsConfig GetEntityInfoSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedEntityInfo(_entityInfo);
    }

    public ChatWidgetSettingsConfig GetChatSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedChat(_chat);
    }

    /// <summary>バフ・デバフグループに、その鍵の行がオンで入っているか(設定の窓のプレビューを含む今の値)。</summary>
    public bool IsBuffDebuffGroupEntryEnabled(BuffEntryKey key)
    {
        return WidgetConfigDefaults.SupportsBuffDebuffGroupSettings(Kind)
            && _buffDebuffGroup.Entries is { } entries
            && entries.Exists(entry => entry.IsEnabled && entry.GetKey() == key);
    }

    /// <summary>
    /// 右クリックで、バフ・デバフグループの行をオン/オフにする(一覧とカードの「グループに追加」、グループの窓の「グループから削除」)。
    /// 決まりは <see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>。開いているグループの窓へ
    /// <see cref="BuffDebuffGroupSettingsChanged"/> で、保存と設定ウィンドウへ <see cref="BuffDebuffGroupEntryChangedFromWidget"/> で知らせる。
    /// 既にその状態なら何もしない。
    /// </summary>
    public void SetBuffDebuffGroupEntryEnabledFromWidget(BuffEntryKey key, bool isEnabled)
    {
        if (!WidgetConfigDefaults.SupportsBuffDebuffGroupSettings(Kind)
            || !key.IsValid
            || IsBuffDebuffGroupEntryEnabled(key) == isEnabled)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffEntryEnabled(
            _buffDebuffGroup.Entries ??= [],
            key,
            isEnabled,
            WidgetConfigDefaults.DefaultBuffDebuffGroupEntries);
        BuffDebuffGroupSettingsChanged?.Invoke(this, EventArgs.Empty);
        BuffDebuffGroupEntryChangedFromWidget?.Invoke(Kind, key, isEnabled);
    }

    /// <summary>
    /// バフ・デバフカードのショートカットの、オンの行の鍵(設定の並び。設定の窓のプレビューを含む今の値)。
    /// メイン画面のカードの選択リストの先頭の項目と既定の選択に使う。
    /// </summary>
    public IReadOnlyList<BuffEntryKey> GetEnabledBuffCardShortcuts()
    {
        return WidgetConfigDefaults.SupportsBuffCardSettings(Kind) && _buffCard.Shortcuts is { } shortcuts
            ? [.. shortcuts.Where(entry => entry.IsEnabled).Select(entry => entry.GetKey())]
            : [];
    }

    /// <summary>バフ・デバフカードのショートカットに、その鍵の行がオンで入っているか(設定の窓のプレビューを含む今の値)。</summary>
    public bool IsBuffCardShortcutEnabled(BuffEntryKey key)
    {
        return WidgetConfigDefaults.SupportsBuffCardSettings(Kind)
            && _buffCard.Shortcuts is { } shortcuts
            && shortcuts.Exists(entry => entry.IsEnabled && entry.GetKey() == key);
    }

    /// <summary>バフ・デバフカードの通知対象に、その鍵の行がオンで入っているか(設定の窓のプレビューを含む今の値)。</summary>
    public bool IsBuffCardNotificationTarget(BuffEntryKey key)
    {
        return WidgetConfigDefaults.SupportsBuffCardSettings(Kind)
            && _buffCard.NotificationTargets is { } targets
            && targets.Exists(entry => entry.IsEnabled && entry.GetKey() == key);
    }

    /// <summary>
    /// 右クリックで、バフ・デバフカードの通知対象の行をオン/オフにする(カードの「このバフを通知対象にする」「〜しない」)。
    /// 決まりはショートカットと同じ(<see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)。保存と設定ウィンドウへ
    /// <see cref="BuffCardNotificationTargetChangedFromWidget"/> で知らせる。カードは1秒ごとの更新で今の値を読む。既にその状態なら何もしない。
    /// </summary>
    public void SetBuffCardNotificationTargetEnabledFromWidget(BuffEntryKey key, bool isEnabled)
    {
        if (!WidgetConfigDefaults.SupportsBuffCardSettings(Kind)
            || !key.IsValid
            || IsBuffCardNotificationTarget(key) == isEnabled)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffEntryEnabled(
            _buffCard.NotificationTargets ??= [],
            key,
            isEnabled,
            WidgetConfigDefaults.DefaultBuffCardNotificationTargetEntries);
        BuffCardNotificationTargetChangedFromWidget?.Invoke(Kind, key, isEnabled);
    }

    /// <summary>バフ・デバフグループの通知対象に、その鍵の行がオンで入っているか(設定の窓のプレビューを含む今の値)。</summary>
    public bool IsBuffDebuffGroupNotificationTarget(BuffEntryKey key)
    {
        return WidgetConfigDefaults.SupportsBuffDebuffGroupSettings(Kind)
            && _buffDebuffGroup.NotificationTargets is { } targets
            && targets.Exists(entry => entry.IsEnabled && entry.GetKey() == key);
    }

    /// <summary>
    /// 右クリックで、バフ・デバフグループの通知対象の行をオン/オフにする(グループの行の「このバフを通知対象にする」「〜しない」)。
    /// 決まりはグループの行と同じ(<see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)。開いているグループの窓へ
    /// <see cref="BuffDebuffGroupSettingsChanged"/> で、保存と設定ウィンドウへ <see cref="BuffDebuffGroupNotificationTargetChangedFromWidget"/> で知らせる。
    /// 既にその状態なら何もしない。
    /// </summary>
    public void SetBuffDebuffGroupNotificationTargetEnabledFromWidget(BuffEntryKey key, bool isEnabled)
    {
        if (!WidgetConfigDefaults.SupportsBuffDebuffGroupSettings(Kind)
            || !key.IsValid
            || IsBuffDebuffGroupNotificationTarget(key) == isEnabled)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffEntryEnabled(
            _buffDebuffGroup.NotificationTargets ??= [],
            key,
            isEnabled,
            WidgetConfigDefaults.DefaultBuffDebuffGroupNotificationTargetEntries);
        BuffDebuffGroupSettingsChanged?.Invoke(this, EventArgs.Empty);
        BuffDebuffGroupNotificationTargetChangedFromWidget?.Invoke(Kind, key, isEnabled);
    }

    /// <summary>
    /// 右クリックで、バフ・デバフカードのショートカットの行をオンにする(一覧とカードの「バフ・デバフカードのショートカットに追加」)。
    /// 決まりはグループの行と同じ(<see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)。メイン画面のカードへ
    /// <see cref="BuffCardShortcutsChanged"/> で、保存と設定ウィンドウへ <see cref="BuffCardShortcutChangedFromWidget"/> で知らせる。
    /// 既にその状態なら何もしない。
    /// </summary>
    public void SetBuffCardShortcutEnabledFromWidget(BuffEntryKey key, bool isEnabled)
    {
        if (!WidgetConfigDefaults.SupportsBuffCardSettings(Kind)
            || !key.IsValid
            || IsBuffCardShortcutEnabled(key) == isEnabled)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffEntryEnabled(
            _buffCard.Shortcuts ??= [],
            key,
            isEnabled,
            WidgetConfigDefaults.DefaultBuffCardShortcutEntries);
        BuffCardShortcutsChanged?.Invoke(this, EventArgs.Empty);
        BuffCardShortcutChangedFromWidget?.Invoke(Kind, key, isEnabled);
    }

    /// <summary>
    /// バフ・デバフグループの窓で行を並べ替えた。設定の窓が開いている間は今の値(保存前のプレビュー)を変えて設定の窓へ
    /// <see cref="BuffDebuffGroupEntryOrderChangedFromWidget"/> で渡すだけ(保存は設定の窓の「保存」)、
    /// 開いていなければその場で行だけを書き戻す(<b>設定一式は触らない</b>)。ステータス詳細の行の並べ替えと同じ扱い。
    /// </summary>
    public void ChangeBuffDebuffGroupEntryOrderFromWidget(IReadOnlyList<BuffEntryKey> order)
    {
        if (!WidgetConfigDefaults.SupportsBuffDebuffGroupSettings(Kind))
        {
            return;
        }

        _buffDebuffGroup.Entries = WidgetConfigDefaults.OrderBuffDebuffGroupEntries(_buffDebuffGroup.Entries ?? [], order);
        BuffDebuffGroupSettingsChanged?.Invoke(this, EventArgs.Empty);

        if (_isSettingsPreviewing)
        {
            BuffDebuffGroupEntryOrderChangedFromWidget?.Invoke([.. _buffDebuffGroup.Entries.Select(entry => entry.GetKey())]);
            return;
        }

        WidgetStateManager.Instance.SaveBuffDebuffGroupEntries(Kind, _buffDebuffGroup.Entries);
    }

    /// <summary>今の行の並び。設定の窓が開いている間はプレビューの並び。</summary>
    public IReadOnlyList<int> GetPlayerStatusRowOrderSnapshot()
    {
        return [.. _playerStatusRowOrderPreview ?? _playerStatusRowOrder];
    }

    /// <summary>
    /// ステータス詳細の窓で行を並べ替えた。設定の窓が開いている間はプレビューの並びを変えるだけ(保存は設定の窓の「保存」)、
    /// 開いていなければその場で並びを1項目だけ書き戻す(<b>設定一式は触らない</b>)。
    /// </summary>
    public void ChangePlayerStatusRowOrderFromWidget(IReadOnlyList<int> order)
    {
        if (Kind != WidgetKind.PlayerStatus)
        {
            return;
        }

        if (_playerStatusRowOrderPreview is not null)
        {
            ApplyPlayerStatusRowOrderPreview(order);
            return;
        }

        var before = GetPlayerStatusRowOrderSnapshot();
        _playerStatusRowOrder = WidgetConfigDefaults.NormalizePlayerStatusRowOrder(order);
        WidgetStateManager.Instance.SavePlayerStatusRowOrder(Kind, _playerStatusRowOrder);
        RaisePlayerStatusRowOrderChangedIfDifferent(before);
    }

    /// <summary>
    /// 設定の窓を開いた。閉じるまで、ウィジェットでの並べ替えは保存前のプレビューになる
    /// (ステータス詳細は今の保存された並びからプレビューを始める。バフ・デバフグループは今の値がもともとプレビュー)。
    /// </summary>
    public void BeginSettingsPreview()
    {
        _isSettingsPreviewing = true;
        if (Kind == WidgetKind.PlayerStatus)
        {
            _playerStatusRowOrderPreview = [.. _playerStatusRowOrder];
        }
    }

    /// <summary>設定の窓で並べ替えた(またはウィジェットで並べ替えた)保存前の並び。プレビューの間だけ効く。</summary>
    public void ApplyPlayerStatusRowOrderPreview(IReadOnlyList<int> order)
    {
        if (_playerStatusRowOrderPreview is null)
        {
            return;
        }

        var before = GetPlayerStatusRowOrderSnapshot();
        _playerStatusRowOrderPreview = WidgetConfigDefaults.NormalizePlayerStatusRowOrder(order);
        RaisePlayerStatusRowOrderChangedIfDifferent(before);
    }

    /// <summary>設定の窓を閉じた。プレビューを終え、ステータス詳細の保存されていない並びは保存された並びに戻す。</summary>
    public void EndSettingsPreview()
    {
        _isSettingsPreviewing = false;
        if (_playerStatusRowOrderPreview is null)
        {
            return;
        }

        var before = GetPlayerStatusRowOrderSnapshot();
        _playerStatusRowOrderPreview = null;
        RaisePlayerStatusRowOrderChangedIfDifferent(before);
    }

    private void RaisePlayerStatusRowOrderChangedIfDifferent(IReadOnlyList<int> before)
    {
        var after = GetPlayerStatusRowOrderSnapshot();
        if (!before.SequenceEqual(after))
        {
            PlayerStatusRowOrderChanged?.Invoke(after);
        }
    }

    /// <summary>
    /// バフ・デバフ一覧の窓の右クリックで、そのバフを非表示にする。
    /// 開いている一覧の窓へ <see cref="BuffListSettingsChanged"/> で、保存と設定ウィンドウへ
    /// <see cref="BuffListHiddenBuffsChanged"/> で知らせる。既に非表示なら何もしない。
    /// </summary>
    public void HideBuffInList(int baseId)
    {
        if (!WidgetConfigDefaults.SupportsBuffListSettings(Kind)
            || baseId <= 0
            || _buffList.HiddenBuffIds.Contains(baseId))
        {
            return;
        }

        _buffList.HiddenBuffIds.Add(baseId);
        BuffListSettingsChanged?.Invoke(this, EventArgs.Empty);
        BuffListHiddenBuffsChanged?.Invoke(Kind, baseId);
    }

    /// <summary>
    /// エンティティ情報の窓のバフ・デバフの枠の右クリックで、そのバフを非表示にする。
    /// 開いているエンティティ情報の窓へ <see cref="EntityInfoSettingsChanged"/> で、保存と設定ウィンドウへ
    /// <see cref="EntityInfoHiddenBuffsChanged"/> で知らせる。既に非表示なら何もしない(バフ・デバフ一覧と同じ扱い)。
    /// </summary>
    public void HideBuffInEntityInfo(int baseId)
    {
        if (!WidgetConfigDefaults.SupportsEntityInfoSettings(Kind)
            || baseId <= 0
            || _entityInfo.HiddenBuffIds.Contains(baseId))
        {
            return;
        }

        _entityInfo.HiddenBuffIds.Add(baseId);
        EntityInfoSettingsChanged?.Invoke(this, EventArgs.Empty);
        EntityInfoHiddenBuffsChanged?.Invoke(Kind, baseId);
    }

    /// <summary>バフ・デバフ一覧の優先表示リストに、そのバフの行がオンで入っているか(設定の窓のプレビューを含む今の値)。</summary>
    public bool IsBuffListPriorityBuff(int baseId)
    {
        return WidgetConfigDefaults.SupportsBuffListSettings(Kind)
            && _buffList.PriorityBuffs is { } priorityBuffs
            && priorityBuffs.Exists(entry => entry.IsEnabled && entry.GetKey() == BuffEntryKey.ForBuff(baseId));
    }

    /// <summary>
    /// バフ・デバフ一覧の窓の右クリックで、そのバフを優先表示にする・やめる(優先表示リストの行をオン/オフにする)。
    /// 決まりはグループの行と同じ(<see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>。やめると既定の行はオフで残り、足した行は消える)。
    /// 開いている一覧の窓へ <see cref="BuffListSettingsChanged"/> で、保存と設定ウィンドウへ <see cref="BuffListPriorityBuffChangedFromWidget"/> で知らせる。
    /// 既にその状態なら何もしない。
    /// </summary>
    public void SetBuffListPriorityBuffEnabledFromWidget(int baseId, bool isEnabled)
    {
        var key = BuffEntryKey.ForBuff(baseId);
        if (!WidgetConfigDefaults.SupportsBuffListSettings(Kind)
            || !key.IsValid
            || IsBuffListPriorityBuff(baseId) == isEnabled)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffEntryEnabled(
            _buffList.PriorityBuffs ??= [],
            key,
            isEnabled,
            WidgetConfigDefaults.GetBuffListPriorityDefaults(Kind));
        BuffListSettingsChanged?.Invoke(this, EventArgs.Empty);
        BuffListPriorityBuffChangedFromWidget?.Invoke(Kind, key, isEnabled);
    }

    /// <summary>エンティティ情報の優先表示リストに、そのバフの行がオンで入っているか(設定の窓のプレビューを含む今の値)。</summary>
    public bool IsEntityInfoPriorityBuff(int baseId)
    {
        return WidgetConfigDefaults.SupportsEntityInfoSettings(Kind)
            && _entityInfo.PriorityBuffs is { } priorityBuffs
            && priorityBuffs.Exists(entry => entry.IsEnabled && entry.GetKey() == BuffEntryKey.ForBuff(baseId));
    }

    /// <summary>
    /// エンティティ情報の窓のバフ・デバフの枠の右クリックで、そのバフを優先表示にする・やめる。
    /// 開いているエンティティ情報の窓へ <see cref="EntityInfoSettingsChanged"/> で、保存と設定ウィンドウへ
    /// <see cref="EntityInfoPriorityBuffChangedFromWidget"/> で知らせる。既にその状態なら何もしない(バフ・デバフ一覧と同じ扱い)。
    /// </summary>
    public void SetEntityInfoPriorityBuffEnabledFromWidget(int baseId, bool isEnabled)
    {
        var key = BuffEntryKey.ForBuff(baseId);
        if (!WidgetConfigDefaults.SupportsEntityInfoSettings(Kind)
            || !key.IsValid
            || IsEntityInfoPriorityBuff(baseId) == isEnabled)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffEntryEnabled(
            _entityInfo.PriorityBuffs ??= [],
            key,
            isEnabled,
            WidgetConfigDefaults.DefaultEntityInfoPriorityBuffEntries);
        EntityInfoSettingsChanged?.Invoke(this, EventArgs.Empty);
        EntityInfoPriorityBuffChangedFromWidget?.Invoke(Kind, key, isEnabled);
    }

    /// <summary>推移グラフの今の横軸の長さ(設定の窓のプレビューを含む)。</summary>
    public int MetricGraphVisibleSeconds => _metricGraph.VisibleSeconds;

    /// <summary>
    /// 推移グラフの窓の上のホイールで横軸の長さを変える。同じ種類の開いている窓を全部描き直し、
    /// 開いている設定の窓へ <see cref="MetricGraphVisibleSecondsChangedFromWidget"/> で知らせる。
    /// 保存はホイールが止まってから <see cref="SaveMetricGraphVisibleSeconds"/> で行う。
    /// </summary>
    public void SetMetricGraphVisibleSecondsFromWidget(int visibleSeconds)
    {
        if (!WidgetConfigDefaults.SupportsMetricGraphSettings(Kind))
        {
            return;
        }

        var normalized = WidgetConfigDefaults.ClampMetricGraphVisibleSeconds(visibleSeconds);
        if (_metricGraph.VisibleSeconds == normalized)
        {
            return;
        }

        _metricGraph.VisibleSeconds = normalized;
        RaisePlayerWindowPresentationChanged();
        MetricGraphVisibleSecondsChangedFromWidget?.Invoke(normalized);
    }

    /// <summary>
    /// 横軸の長さを1項目だけ書き戻す。<b>設定一式は触らない</b>ので、開いている設定の窓が持つ未保存の値とぶつからない。
    /// </summary>
    public void SaveMetricGraphVisibleSeconds(int visibleSeconds)
    {
        if (!WidgetConfigDefaults.SupportsMetricGraphSettings(Kind))
        {
            return;
        }

        WidgetStateManager.Instance.SaveMetricGraphVisibleSeconds(
            Kind,
            WidgetConfigDefaults.ClampMetricGraphVisibleSeconds(visibleSeconds));
    }

    /// <summary>
    /// 被ダメログのヘッダーのボタンで「最新の被ダメのみ表示」を切り替える。窓へ設定の変化を知らせ、
    /// 開いている設定の窓へ <see cref="DamageTakenLogShowsLatestHitOnlyChangedFromWidget"/> で知らせ、この項目だけを保存する。
    /// </summary>
    public void SetDamageTakenLogShowsLatestHitOnlyFromWidget(bool showsLatestHitOnly)
    {
        if (!WidgetConfigDefaults.SupportsDamageTakenLogSettings(Kind)
            || _damageTakenLog.ShowsLatestHitOnly == showsLatestHitOnly)
        {
            return;
        }

        _damageTakenLog.ShowsLatestHitOnly = showsLatestHitOnly;
        DamageTakenLogSettingsChanged?.Invoke(this, EventArgs.Empty);
        DamageTakenLogShowsLatestHitOnlyChangedFromWidget?.Invoke(showsLatestHitOnly);
        WidgetStateManager.Instance.SaveDamageTakenLogShowsLatestHitOnly(Kind, showsLatestHitOnly);
    }

    /// <summary>カードの表示書式。倍率辞書を丸ごと複製しないよう、これだけ直に返す。</summary>
    public string BuffInfoFormatString =>
        _buffCard.BuffInfoFormatString ?? WidgetConfigDefaults.DefaultBuffInfoFormatString;

    /// <summary>保存済みの倍率。無ければ既定(200%)。</summary>
    public int GetBuffCardScale(string scaleKey)
    {
        return !string.IsNullOrWhiteSpace(scaleKey)
            && _buffCard.Scales.TryGetValue(scaleKey, out var scale)
            ? WidgetConfigDefaults.ClampBuffCardScale(scale)
            : WidgetConfigDefaults.DefaultBuffCardScale;
    }

    /// <summary>
    /// 倍率を1件だけ書き戻す。<b>設定一式は触らない</b>ので、開いている他のカードに影響しない。
    /// </summary>
    public void SaveBuffCardScale(string scaleKey, int scale)
    {
        if (!IsBuffDebuffCard || string.IsNullOrWhiteSpace(scaleKey))
        {
            return;
        }

        var normalized = WidgetConfigDefaults.ClampBuffCardScale(scale);
        _buffCard.Scales[scaleKey] = normalized;
        WidgetStateManager.Instance.SaveBuffCardScale(Kind, scaleKey, normalized);
    }

    public string StateText => State == WidgetState.Running
        ? LocalizationManager.Instance.GetString("Widget_State_Running")
        : LocalizationManager.Instance.GetString("Widget_State_Stopped");

    public bool IsRunning => State == WidgetState.Running;

    public event Action<WidgetKind, long>? PlayerWindowRequested;

    public event Action<WidgetKind, EntityTargetInfo>? EntityWindowRequested;

    /// <summary>エンティティリストの行が映す個体やその値が変わった。実体の窓が当て直す。</summary>
    public event EventHandler? EntityListTargetsChanged;

    /// <summary>右クリックのメニューの相手が一覧から消えたか、開いた行が外れた(マップ移動も)。画面がメニューを閉じる。</summary>
    public event EventHandler? PlayerMenuTargetLeft;

    /// <summary>右クリックのメニューの相手が一覧から消えたか、開いた行が外れた(マップ移動も)。画面がメニューを閉じる。</summary>
    public event EventHandler? EntityMenuTargetLeft;

    /// <summary>名前の選択リストを開いた。項目を作り直す。</summary>
    public event EventHandler? TargetOptionsRequested;

    /// <summary>選んでいる項目が変わった。名前の欄と起動ボタンを当て直す。</summary>
    public event EventHandler? SelectedTargetChanged;

    /// <summary>相手を選ぶ種別で起動ボタンを押した。選んでいる項目の窓を開くか閉じる。</summary>
    public event EventHandler? SelectedTargetToggleRequested;

    public event EventHandler? PlayerWindowPresentationChanged;

    public event EventHandler? MeterSettingsChanged;

    public event EventHandler? TimelineSettingsChanged;

    public event EventHandler? PlayerInfoSettingsChanged;

    public event EventHandler? BuffListSettingsChanged;

    /// <summary>窓の右クリックでバフを1つ非表示にした(引数はウィジェットの種別とバフのID)。設定画面の削除では上げない。</summary>
    public event Action<WidgetKind, int>? BuffListHiddenBuffsChanged;

    /// <summary>窓の右クリックで優先表示リストの行をオン/オフにした(引数はウィジェットの種別・行の鍵・オンか)。設定画面のスイッチでは上げない。</summary>
    public event Action<WidgetKind, BuffEntryKey, bool>? BuffListPriorityBuffChangedFromWidget;

    /// <summary>推移グラフの窓の上のホイールで横軸の長さが変わった(引数は新しい秒数)。設定の窓のスライダーでは上げない。</summary>
    public event Action<int>? MetricGraphVisibleSecondsChangedFromWidget;

    /// <summary>被ダメログのヘッダーのボタンで「最新の被ダメのみ表示」が変わった(引数は新しい値)。設定の窓のスイッチでは上げない。</summary>
    public event Action<bool>? DamageTakenLogShowsLatestHitOnlyChangedFromWidget;

    public event EventHandler? ElementColorSettingsChanged;

    public event EventHandler? SkillDetailSettingsChanged;

    public event EventHandler? EquipmentSettingsChanged;

    public event EventHandler? PlayerStatusSettingsChanged;

    public event EventHandler? BuffDebuffGroupSettingsChanged;

    public event EventHandler? ReviveListSettingsChanged;

    public event EventHandler? DamageTakenLogSettingsChanged;

    public event EventHandler? EntityInfoSettingsChanged;

    public event EventHandler? ChatSettingsChanged;

    /// <summary>プラグインのウィジェットの設定が変わった(保存・保存前のプレビュー・破棄・初期化)。</summary>
    public event EventHandler? PluginSettingsChanged;

    /// <summary>エンティティ情報の窓の右クリックでバフを1つ非表示にした(引数はウィジェットの種別とバフのID)。設定画面のスイッチでは上げない。</summary>
    public event Action<WidgetKind, int>? EntityInfoHiddenBuffsChanged;

    /// <summary>エンティティ情報の窓の右クリックで優先表示リストの行をオン/オフにした(引数はウィジェットの種別・行の鍵・オンか)。設定画面のスイッチでは上げない。</summary>
    public event Action<WidgetKind, BuffEntryKey, bool>? EntityInfoPriorityBuffChangedFromWidget;

    /// <summary>
    /// 右クリックでバフ・デバフグループの行をオン/オフにした(引数はウィジェットの種別・行の鍵・オンか)。設定画面のスイッチでは上げない。
    /// </summary>
    public event Action<WidgetKind, BuffEntryKey, bool>? BuffDebuffGroupEntryChangedFromWidget;

    /// <summary>設定の窓が開いている間に、バフ・デバフグループの窓で行を並べ替えた(引数は新しい行の鍵の並び)。設定の窓の一覧が並べ直すのに使う。</summary>
    public event Action<IReadOnlyList<BuffEntryKey>>? BuffDebuffGroupEntryOrderChangedFromWidget;

    /// <summary>バフ・デバフカードのショートカット(保存かプレビュー)が変わった。メイン画面のカードの選択リストを当て直す。</summary>
    public event EventHandler? BuffCardShortcutsChanged;

    /// <summary>
    /// 右クリックでバフ・デバフカードのショートカットの行をオンにした(引数はウィジェットの種別・行の鍵・オンか)。設定画面のスイッチでは上げない。
    /// </summary>
    public event Action<WidgetKind, BuffEntryKey, bool>? BuffCardShortcutChangedFromWidget;

    /// <summary>
    /// 右クリックでバフ・デバフカードの通知対象の行をオン/オフにした(引数はウィジェットの種別・行の鍵・オンか)。設定画面のスイッチでは上げない。
    /// </summary>
    public event Action<WidgetKind, BuffEntryKey, bool>? BuffCardNotificationTargetChangedFromWidget;

    /// <summary>
    /// 右クリックでバフ・デバフグループの通知対象の行をオン/オフにした(引数はウィジェットの種別・行の鍵・オンか)。設定画面のスイッチでは上げない。
    /// </summary>
    public event Action<WidgetKind, BuffEntryKey, bool>? BuffDebuffGroupNotificationTargetChangedFromWidget;

    /// <summary>
    /// ステータス詳細の行の今の並び(<see cref="GetPlayerStatusRowOrderSnapshot"/>)が変わった(引数は新しい並び)。
    /// ウィジェットの窓と、開いている設定の窓の一覧が並べ直すのに使う。
    /// </summary>
    public event Action<IReadOnlyList<int>>? PlayerStatusRowOrderChanged;

    public WidgetListItemViewModel()
    {
        _getPlayerSkillState = GetPlayerSkillState;
        PlayerListEntries = new ReadOnlyObservableCollection<PlayerListEntry>(_playerListEntries);
        EntityListEntries = new ReadOnlyObservableCollection<EntityListEntry>(_entityListEntries);
        _entityRowSettings = EntityRowSettings.FromEntityList(_meter);
    }

    /// <summary>
    /// マップ名を組み立てる。チャンネルの表記は言語ごとに違う(中国語は「1线」)ので、
    /// <b>素の名前と番号を控えておき、言語が変わったら組み直す。</b>
    /// </summary>
    private void ApplyMapName(string? mapName, uint mapChannel)
    {
        _mapSceneName = mapName ?? string.Empty;
        _mapChannel = mapChannel;
        RenderMapName();
    }

    private void RenderMapName()
    {
        MapName = FormatMapName(_mapSceneName, _mapChannel);
    }

    /// <summary>マップ名とチャンネルの表示。プレイヤーリストの左下・プレイヤー情報の現在地・集計タブの履歴の行が使う。</summary>
    public static string FormatMapName(string mapName, uint mapChannel)
    {
        return mapChannel > 0 && !string.IsNullOrEmpty(mapName)
            ? $"{mapName} {LocalizationManager.Instance.Format("Map_ChannelFormat", mapChannel)}"
            : mapName;
    }

    public void RefreshLocalizedText()
    {
        DisplayName = PluginWidget is null
            ? (Kind is WidgetKind.UserBuildLibrary or WidgetKind.CombatExtensionTimeline or WidgetKind.HatePs or WidgetKind.PartyDps or WidgetKind.EnemyCatalog ? DisplayNameResourceKey : LocalizationManager.Instance.GetString(DisplayNameResourceKey))
            : PluginWidget.GetDisplayName(LocalizationManager.Instance.CurrentCulture);
        RenderMapName();
        OnPropertyChanged(nameof(StateText));
        SynchronizePlayerListEntries(resetEntries: false);
        RefreshPlayerListSkillNames();
        SynchronizeEntityListEntries(resetEntries: false);
        RaisePlayerWindowPresentationChanged();
    }

    /// <summary>
    /// プレイヤーリストの技の枠の名前を引き直す。名前は技の一覧を読んだときに焼き込むので、
    /// 言語と表示設定(内部ID・名前の無い技の内部名)が変わったら一覧を読み直す(<see cref="PlayerListEntry.InvalidateSkillLoadout"/>)。
    /// </summary>
    private void RefreshPlayerListSkillNames()
    {
        if (!IsPlayerList)
        {
            return;
        }

        foreach (var entry in _playerListEntries)
        {
            entry.InvalidateSkillLoadout();
            entry.RefreshSkillDisplay(refreshEffects: false);
        }
    }

    public WidgetConfig CreateWidgetConfig()
    {
        return new WidgetConfig
        {
            IsFavorite = IsFavorite,
            IsPinned = IsPinned,
            IsClickThrough = IsClickThrough,
            State = State,
            Theme = _theme.Clone(),
            Meter = WidgetConfigDefaults.SupportsMeterSettings(Kind)
                ? _meter.Clone()
                : null,
            MetricGraph = WidgetConfigDefaults.SupportsMetricGraphSettings(Kind)
                ? _metricGraph.Clone()
                : null,
            BuffCard = WidgetConfigDefaults.SupportsBuffCardSettings(Kind)
                ? _buffCard.Clone()
                : null,
            Timeline = WidgetConfigDefaults.SupportsTimelineSettings(Kind)
                ? _timeline.Clone()
                : null,
            PlayerInfo = WidgetConfigDefaults.SupportsPlayerInfoSettings(Kind)
                ? _playerInfo.Clone()
                : null,
            BuffList = WidgetConfigDefaults.SupportsBuffListSettings(Kind)
                ? _buffList.Clone()
                : null,
            ElementColor = WidgetConfigDefaults.SupportsElementColorSettings(Kind)
                ? _elementColor.Clone()
                : null,
            SkillDetail = WidgetConfigDefaults.SupportsSkillDetailSettings(Kind)
                ? _skillDetail.Clone()
                : null,
            Equipment = WidgetConfigDefaults.SupportsEquipmentSettings(Kind)
                ? _equipment.Clone()
                : null,
            PlayerStatus = WidgetConfigDefaults.SupportsPlayerStatusSettings(Kind)
                ? _playerStatus.Clone()
                : null,
            PlayerStatusRowOrder = Kind == WidgetKind.PlayerStatus
                ? [.. _playerStatusRowOrder]
                : null,
            BuffDebuffGroup = WidgetConfigDefaults.SupportsBuffDebuffGroupSettings(Kind)
                ? _buffDebuffGroup.Clone()
                : null,
            ReviveList = WidgetConfigDefaults.SupportsReviveListSettings(Kind)
                ? _reviveList.Clone()
                : null,
            DamageTakenLog = WidgetConfigDefaults.SupportsDamageTakenLogSettings(Kind)
                ? _damageTakenLog.Clone()
                : null,
            EntityInfo = WidgetConfigDefaults.SupportsEntityInfoSettings(Kind)
                ? _entityInfo.Clone()
                : null,
            Chat = WidgetConfigDefaults.SupportsChatSettings(Kind)
                ? _chat.Clone()
                : null
        };
    }

    public void ApplyWidgetConfig(WidgetConfig config)
    {
        WidgetConfigDefaults.Normalize(Kind, config);

        IsFavorite = config.IsFavorite;
        IsPinned = config.IsPinned;
        IsClickThrough = config.IsClickThrough;

        if (config.State is { } state)
        {
            State = state;
        }

        ApplyTheme(config.Theme);
        if (WidgetConfigDefaults.SupportsMeterSettings(Kind))
        {
            _meter = WidgetConfigDefaults.CloneNormalizedMeter(Kind, config.Meter);
            _entityRowSettings = EntityRowSettings.FromEntityList(_meter);
        }

        if (WidgetConfigDefaults.SupportsMetricGraphSettings(Kind))
        {
            _metricGraph = WidgetConfigDefaults.CloneNormalizedMetricGraph(Kind, config.MetricGraph);
            RaisePlayerWindowPresentationChanged();
        }

        if (WidgetConfigDefaults.SupportsBuffCardSettings(Kind))
        {
            _buffCard = WidgetConfigDefaults.CloneNormalizedBuffCard(config.BuffCard);
            RaisePlayerWindowPresentationChanged();
            BuffCardShortcutsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (WidgetConfigDefaults.SupportsTimelineSettings(Kind))
        {
            _timeline = WidgetConfigDefaults.CloneNormalizedTimeline(config.Timeline);
            TimelineSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (WidgetConfigDefaults.SupportsPlayerInfoSettings(Kind))
        {
            _playerInfo = WidgetConfigDefaults.CloneNormalizedPlayerInfo(config.PlayerInfo);
            PlayerInfoSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (WidgetConfigDefaults.SupportsBuffListSettings(Kind))
        {
            _buffList = WidgetConfigDefaults.CloneNormalizedBuffList(Kind, config.BuffList);
            BuffListSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (WidgetConfigDefaults.SupportsElementColorSettings(Kind))
        {
            _elementColor = WidgetConfigDefaults.CloneNormalizedElementColor(Kind, config.ElementColor);
            ElementColorSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (WidgetConfigDefaults.SupportsSkillDetailSettings(Kind))
        {
            _skillDetail = WidgetConfigDefaults.CloneNormalizedSkillDetail(config.SkillDetail);
            SkillDetailSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (WidgetConfigDefaults.SupportsEquipmentSettings(Kind))
        {
            _equipment = WidgetConfigDefaults.CloneNormalizedEquipment(config.Equipment);
            EquipmentSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (WidgetConfigDefaults.SupportsPlayerStatusSettings(Kind))
        {
            _playerStatus = WidgetConfigDefaults.CloneNormalizedPlayerStatus(config.PlayerStatus);
            PlayerStatusSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (Kind == WidgetKind.PlayerStatus)
        {
            // 設定の窓の「保存」もここを通る。プレビューの間ならプレビューも保存した並びにそろえる。
            var rowOrderBefore = GetPlayerStatusRowOrderSnapshot();
            _playerStatusRowOrder = WidgetConfigDefaults.NormalizePlayerStatusRowOrder(config.PlayerStatusRowOrder);
            if (_playerStatusRowOrderPreview is not null)
            {
                _playerStatusRowOrderPreview = [.. _playerStatusRowOrder];
            }

            RaisePlayerStatusRowOrderChangedIfDifferent(rowOrderBefore);
        }

        if (WidgetConfigDefaults.SupportsBuffDebuffGroupSettings(Kind))
        {
            _buffDebuffGroup = WidgetConfigDefaults.CloneNormalizedBuffDebuffGroup(config.BuffDebuffGroup);
            BuffDebuffGroupSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (WidgetConfigDefaults.SupportsReviveListSettings(Kind))
        {
            _reviveList = WidgetConfigDefaults.CloneNormalizedReviveList(config.ReviveList);
            ReviveListSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (WidgetConfigDefaults.SupportsDamageTakenLogSettings(Kind))
        {
            _damageTakenLog = WidgetConfigDefaults.CloneNormalizedDamageTakenLog(config.DamageTakenLog);
            DamageTakenLogSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (WidgetConfigDefaults.SupportsEntityInfoSettings(Kind))
        {
            _entityInfo = WidgetConfigDefaults.CloneNormalizedEntityInfo(config.EntityInfo);
            EntityInfoSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (WidgetConfigDefaults.SupportsChatSettings(Kind))
        {
            _chat = WidgetConfigDefaults.CloneNormalizedChat(config.Chat);
            ChatSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (Kind == WidgetKind.Plugin)
        {
            _pluginSettings = WidgetConfigDefaults.CloneNormalizedPlugin(config.Plugin);
            PluginSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        SynchronizePlayerListEntries(resetEntries: false);
        SynchronizeEntityListEntries(resetEntries: false);
        MeterSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyMeterSettingsPreview(MeterWidgetSettingsConfig meter)
    {
        if (!WidgetConfigDefaults.SupportsMeterSettings(Kind))
        {
            return;
        }

        _meter = WidgetConfigDefaults.CloneNormalizedMeter(Kind, meter);
        _entityRowSettings = EntityRowSettings.FromEntityList(_meter);
        SynchronizePlayerListEntries(resetEntries: false);
        SynchronizeEntityListEntries(resetEntries: false);
        MeterSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyMetricGraphSettingsPreview(MetricGraphWidgetSettingsConfig metricGraph)
    {
        if (!WidgetConfigDefaults.SupportsMetricGraphSettings(Kind))
        {
            return;
        }

        _metricGraph = WidgetConfigDefaults.CloneNormalizedMetricGraph(Kind, metricGraph);
        RaisePlayerWindowPresentationChanged();
    }

    public void ApplyBuffCardSettingsPreview(BuffCardWidgetSettingsConfig buffCard)
    {
        if (!WidgetConfigDefaults.SupportsBuffCardSettings(Kind))
        {
            return;
        }

        _buffCard = WidgetConfigDefaults.CloneNormalizedBuffCard(buffCard);
        RaisePlayerWindowPresentationChanged();
        BuffCardShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyTimelineSettingsPreview(TimelineWidgetSettingsConfig timeline)
    {
        if (!WidgetConfigDefaults.SupportsTimelineSettings(Kind))
        {
            return;
        }

        _timeline = WidgetConfigDefaults.CloneNormalizedTimeline(timeline);
        TimelineSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyPlayerInfoSettingsPreview(PlayerInfoWidgetSettingsConfig playerInfo)
    {
        if (!WidgetConfigDefaults.SupportsPlayerInfoSettings(Kind))
        {
            return;
        }

        _playerInfo = WidgetConfigDefaults.CloneNormalizedPlayerInfo(playerInfo);
        PlayerInfoSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyBuffListSettingsPreview(BuffListWidgetSettingsConfig buffList)
    {
        if (!WidgetConfigDefaults.SupportsBuffListSettings(Kind))
        {
            return;
        }

        _buffList = WidgetConfigDefaults.CloneNormalizedBuffList(Kind, buffList);
        BuffListSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyElementColorSettingsPreview(ElementColorWidgetSettingsConfig elementColor)
    {
        if (!WidgetConfigDefaults.SupportsElementColorSettings(Kind))
        {
            return;
        }

        _elementColor = WidgetConfigDefaults.CloneNormalizedElementColor(Kind, elementColor);
        ElementColorSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplySkillDetailSettingsPreview(SkillDetailWidgetSettingsConfig skillDetail)
    {
        if (!WidgetConfigDefaults.SupportsSkillDetailSettings(Kind))
        {
            return;
        }

        _skillDetail = WidgetConfigDefaults.CloneNormalizedSkillDetail(skillDetail);
        SkillDetailSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyEquipmentSettingsPreview(EquipmentWidgetSettingsConfig equipment)
    {
        if (!WidgetConfigDefaults.SupportsEquipmentSettings(Kind))
        {
            return;
        }

        _equipment = WidgetConfigDefaults.CloneNormalizedEquipment(equipment);
        EquipmentSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyPlayerStatusSettingsPreview(PlayerStatusWidgetSettingsConfig playerStatus)
    {
        if (!WidgetConfigDefaults.SupportsPlayerStatusSettings(Kind))
        {
            return;
        }

        _playerStatus = WidgetConfigDefaults.CloneNormalizedPlayerStatus(playerStatus);
        PlayerStatusSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyBuffDebuffGroupSettingsPreview(BuffDebuffGroupWidgetSettingsConfig buffDebuffGroup)
    {
        if (!WidgetConfigDefaults.SupportsBuffDebuffGroupSettings(Kind))
        {
            return;
        }

        _buffDebuffGroup = WidgetConfigDefaults.CloneNormalizedBuffDebuffGroup(buffDebuffGroup);
        BuffDebuffGroupSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyReviveListSettingsPreview(ReviveListWidgetSettingsConfig reviveList)
    {
        if (!WidgetConfigDefaults.SupportsReviveListSettings(Kind))
        {
            return;
        }

        _reviveList = WidgetConfigDefaults.CloneNormalizedReviveList(reviveList);
        ReviveListSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyDamageTakenLogSettingsPreview(DamageTakenLogWidgetSettingsConfig damageTakenLog)
    {
        if (!WidgetConfigDefaults.SupportsDamageTakenLogSettings(Kind))
        {
            return;
        }

        _damageTakenLog = WidgetConfigDefaults.CloneNormalizedDamageTakenLog(damageTakenLog);
        DamageTakenLogSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>プラグインのウィジェットの設定の保存前のプレビュー(設定の窓)。開いている窓の中身へは <see cref="PluginSettingsChanged"/> で届く。</summary>
    public void ApplyPluginSettingsPreview(PluginWidgetSettingsConfig plugin)
    {
        if (Kind != WidgetKind.Plugin)
        {
            return;
        }

        _pluginSettings = WidgetConfigDefaults.CloneNormalizedPlugin(plugin);
        PluginSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>プラグインのウィジェットの今の設定(設定の窓のプレビューを含む)。</summary>
    public PluginWidgetSettingsConfig GetPluginSettingsSnapshot()
    {
        return _pluginSettings.Clone();
    }

    public void ApplyChatSettingsPreview(ChatWidgetSettingsConfig chat)
    {
        if (!WidgetConfigDefaults.SupportsChatSettings(Kind))
        {
            return;
        }

        _chat = WidgetConfigDefaults.CloneNormalizedChat(chat);
        ChatSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyEntityInfoSettingsPreview(EntityInfoWidgetSettingsConfig entityInfo)
    {
        if (!WidgetConfigDefaults.SupportsEntityInfoSettings(Kind))
        {
            return;
        }

        _entityInfo = WidgetConfigDefaults.CloneNormalizedEntityInfo(entityInfo);
        EntityInfoSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyTheme(WidgetThemeConfig theme)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedTheme(theme);

        _theme = normalized.Clone();

        var selectedHex = normalized.WindowColors[
            Math.Clamp(normalized.WindowColorIndex, 0, normalized.WindowColors.Count - 1)];

        if (!ColorUtilities.TryParseHex(selectedHex, out var windowSurface))
        {
            windowSurface = Color.FromRgb(0x0B, 0x16, 0x24);
        }

        var hasBackgroundImage = !string.IsNullOrWhiteSpace(normalized.BackgroundImagePath)
            && File.Exists(normalized.BackgroundImagePath);
        Color? backgroundImageAverageColor = null;
        if (hasBackgroundImage
            && string.Equals(
                normalized.BackgroundImageAverageColorSourcePath,
                normalized.BackgroundImagePath,
                StringComparison.OrdinalIgnoreCase)
            && ColorUtilities.TryParseHex(normalized.BackgroundImageAverageColor, out var parsedAverageColor))
        {
            backgroundImageAverageColor = parsedAverageColor;
        }

        ThemePalette = ThemeColorPalette.Create(windowSurface);
        WidgetWindowPalette = WidgetWindowThemePalette.Create(
            windowSurface,
            normalized.WindowOpacity,
            backgroundImageAverageColor);

        HasBackgroundImage = hasBackgroundImage;
        BackgroundImagePath = hasBackgroundImage
            ? normalized.BackgroundImagePath
            : null;

        HideHeaderWhenInactive = normalized.HideHeaderWhenInactive;
        HideFooterWhenInactive = normalized.HideFooterWhenInactive;
    }

    public void UpdatePlayerRoster(
        IReadOnlyList<PlayerRosterEntry> playerRoster,
        string mapName,
        uint mapChannel,
        long mapGeneration)
    {
        if (!IsPlayerList)
        {
            return;
        }

        ApplyMapName(mapName, mapChannel);
        _playerRoster = playerRoster;

        var resetEntries = _playerListMapGeneration != mapGeneration;
        _playerListMapGeneration = mapGeneration;
        SynchronizePlayerListEntries(resetEntries);
    }

    public void UpdateEntityHistory(EntityHistorySnapshot snapshot)
    {
        _entityHistory=snapshot;
        if(IsEntityList) { if(snapshot.IsHistorical) ApplyMapName(snapshot.SceneName,0); else {var nearby=NearbyEntityStore.Instance.Current;ApplyMapName(nearby.MapName,nearby.MapChannel);} SynchronizeEntityListEntries(false); }
    }

    public void UpdateNearbyEntities(
        IReadOnlyList<NearbyEntityEntry> nearbyEntities,
        string mapName,
        uint mapChannel,
        long mapGeneration)
    {
        if (!IsEntityList)
        {
            return;
        }

        if(!_entityHistory.IsHistorical) ApplyMapName(mapName, mapChannel);
        _nearbyEntities = nearbyEntities;

        var resetEntries = _entityListMapGeneration != mapGeneration;
        _entityListMapGeneration = mapGeneration;
        SynchronizeEntityListEntries(resetEntries);
    }

    /// <summary>ダンジョンの得点(空間調査)が変わった(行の「○○pt」とフッターを当て直す)。</summary>
    public void UpdateDungeonScore(DungeonScoreSnapshot dungeonScore)
    {
        if (!IsEntityList || _dungeonScore == dungeonScore)
        {
            return;
        }

        _dungeonScore = dungeonScore;
        SynchronizeEntityListEntries(resetEntries: false);
    }

    /// <summary>名簿が変わった(エンティティリストのヘイトの枠の名前・クラス・特化を引き直す)。</summary>
    public void UpdateEntityListRoster(IReadOnlyList<PlayerRosterEntry> playerRoster)
    {
        if (!IsEntityList)
        {
            return;
        }

        _entityHatePlayers.SetRoster(playerRoster);
        SynchronizeEntityListEntries(resetEntries: false);
    }

    public void SetOpenPlayerWindowCount(int count)
    {
        OpenPlayerWindowCount = Math.Max(count, 0);
    }

    /// <summary>名前の選択リストを開いたとき(画面から)。項目の作り直しを頼む。</summary>
    public void RequestTargetOptions()
    {
        TargetOptionsRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>名前の選択リストで項目を選んだとき(画面から)。</summary>
    public void SelectTargetOption(WidgetTargetOption option)
    {
        IsTargetChosenByUser = true;
        SelectedTargetOption = option;
    }

    partial void OnSelectedTargetOptionChanged(WidgetTargetOption? value)
    {
        SelectedTargetChanged?.Invoke(this, EventArgs.Empty);
    }

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

    /// <summary>
    /// プレイヤーリスト・エンティティリストの窓の状態の印(この2つは窓の中身もこの項目が持つ)。
    /// ほかの種類の窓の印はそれぞれの中身が持つので、ここで計算することは無い。
    /// </summary>
    public void RefreshDataStatus()
    {
        var reception = GameReceptionStateStore.Current;
        var status = Kind switch
        {
            WidgetKind.PlayerList => WidgetDataStatusRules.ForPlayerList(
                reception,
                EncounterHistoryProvider.HasLoadFailed,
                PartyStateStore.Instance.Current.IsMembershipPending,
                _shownPlayerRoster,
                _meter.ShowSeasonTalent || PlayerInfoFormatFormatter.ShowsSeasonTalent(_meter.PlayerInfoFormatString)),
            WidgetKind.EntityList => WidgetDataStatusRules.ForEntityList(
                reception,
                _entityListEntries.Any(entry => entry.IsTypeUnknown)),
            _ => throw new InvalidOperationException($"The data status of widget {Kind} is held by its window content.")
        };

        if (status == DataStatus)
        {
            return;
        }

        DataStatus = status;
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshPlayerListEntries()
    {
        SynchronizePlayerListEntries(resetEntries: false);
    }

    public void RefreshPlayerListSkillEntries(bool refreshEffects)
    {
        if (!IsPlayerList)
        {
            return;
        }

        foreach (var entry in _playerListEntries)
        {
            entry.RefreshSkillDisplay(refreshEffects);
        }
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }

    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;
    }

    [RelayCommand]
    private void ToggleClickThrough()
    {
        IsClickThrough = !IsClickThrough;
    }

    /// <summary>
    /// 起動ボタン。相手を選ぶ種別(複数開けるウィジェット)は選んでいる項目の窓だけを開く・閉じる(窓を束ねる側へ頼む)。
    /// ほかは起動中と停止を切り替える。
    /// </summary>
    [RelayCommand]
    private void ToggleRunning()
    {
        if (SupportsTargetSelection)
        {
            SelectedTargetToggleRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        State = State == WidgetState.Running
            ? WidgetState.Stopped
            : WidgetState.Running;
    }

    [RelayCommand]
    private void RequestPlayerInfo(PlayerMenuTarget? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerInfo, player);
    }

    [RelayCommand]
    private void RequestPlayerStatus(PlayerMenuTarget? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerStatus, player);
    }

    [RelayCommand]
    private void RequestPlayerEquipment(PlayerMenuTarget? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerEquipment, player);
    }

    [RelayCommand]
    private void RequestPlayerTalent(PlayerMenuTarget? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerTalent, player);
    }

    [RelayCommand]
    private void RequestPlayerSeasonTalent(PlayerMenuTarget? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerSeasonTalent, player);
    }

    [RelayCommand]
    private void RequestPlayerModule(PlayerMenuTarget? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerModule, player);
    }

    [RelayCommand]
    private void RequestBuffList(PlayerMenuTarget? player)
    {
        RequestPlayerWindow(WidgetKind.BuffList, player);
    }

    [RelayCommand]
    private void RequestDebuffList(PlayerMenuTarget? player)
    {
        RequestPlayerWindow(WidgetKind.DebuffList, player);
    }

    [RelayCommand]
    private void RequestEntityInfo(EntityTargetInfo? entity)
    {
        RequestEntityWindow(WidgetKind.EntityInfo, entity);
    }

    [RelayCommand]
    private void RequestEntityBuffList(EntityTargetInfo? entity)
    {
        RequestEntityWindow(WidgetKind.BuffList, entity);
    }

    [RelayCommand]
    private void RequestEntityDebuffList(EntityTargetInfo? entity)
    {
        RequestEntityWindow(WidgetKind.DebuffList, entity);
    }

    partial void OnStateChanged(WidgetState value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(ShowsStopIcon));
    }

    partial void OnOpenPlayerWindowCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasOpenPlayerWindows));
    }

    private void RequestPlayerWindow(WidgetKind kind, PlayerMenuTarget? player)
    {
        if (!IsPlayerList || player is null)
        {
            return;
        }

        PlayerWindowRequested?.Invoke(kind, player.CharacterId);
    }

    private void RequestEntityWindow(WidgetKind kind, EntityTargetInfo? entity)
    {
        if (!IsEntityList || entity is null)
        {
            return;
        }

        EntityWindowRequested?.Invoke(kind, entity);
    }

    /// <summary>
    /// 右クリックのメニューを開いた行を受ける(閉じたら null)。行の強調は開いた時の人についていく。
    /// メニューの相手が一覧から消えたか、開いた行が外れたら <see cref="PlayerMenuTargetLeft"/> で知らせる。
    /// </summary>
    public void SetPlayerMenuTarget(PlayerListEntry? row)
    {
        _playerMenuRow = row;
        _playerMenuCharacterId = row?.CharacterId ?? 0;
        foreach (var entry in _playerListEntries)
        {
            entry.IsPlayerSelectionMenuOpen = IsPlayerMenuTarget(entry.CharacterId);
        }
    }

    /// <summary>右クリックのメニューを開いた行を受ける(閉じたら null)。形は <see cref="SetPlayerMenuTarget"/> と同じ。</summary>
    public void SetEntityMenuTarget(EntityListEntry? row)
    {
        _entityMenuRow = row;
        _entityMenuUuid = row?.EntityUuid ?? 0;
        foreach (var entry in _entityListEntries)
        {
            entry.IsEntitySelectionMenuOpen = IsEntityMenuTarget(entry.EntityUuid);
        }
    }

    private bool IsPlayerMenuTarget(long characterId) => _playerMenuRow is not null && characterId == _playerMenuCharacterId;

    private bool IsEntityMenuTarget(long entityUuid) => _entityMenuRow is not null && entityUuid == _entityMenuUuid;

    private PlayerSkillDisplayState GetPlayerSkillState(long characterId)
    {
        if (!_playerSkillStates.TryGetValue(characterId, out var state))
        {
            state = new PlayerSkillDisplayState(characterId);
            _playerSkillStates.Add(characterId, state);
        }

        return state;
    }

    /// <summary>一覧から外れた人の技の枠の値を手放す。外れた人がいなければ何も作らない。</summary>
    private void ReleaseLeftPlayerSkillStates(IReadOnlyList<PlayerRosterEntry> visibleRoster)
    {
        if (_playerSkillStates.Count <= visibleRoster.Count)
        {
            return;
        }

        var shownCharacterIds = new HashSet<long>(visibleRoster.Count);
        foreach (var player in visibleRoster)
        {
            shownCharacterIds.Add(player.CharacterId);
        }

        foreach (var characterId in _playerSkillStates.Keys.ToArray())
        {
            if (!shownCharacterIds.Contains(characterId))
            {
                _playerSkillStates.Remove(characterId);
            }
        }
    }

    /// <summary>
    /// 行を位置ごとに当て直す(順が変わっても行の部品を作り直さない)。足りなければ末尾に足し、余れば末尾から外す。
    /// マップが替わったら行を作り直す。
    /// </summary>
    private void SynchronizePlayerListEntries(bool resetEntries)
    {
        if (!IsPlayerList)
        {
            return;
        }

        var visibleRoster = GetVisiblePlayerRoster();
        var menuRowRemoved = resetEntries;

        if (resetEntries)
        {
            _playerListEntries.Clear();
            _playerSkillStates.Clear();
        }

        var playerNameDisplayMode = (PlayerNameDisplayMode)ConfigManager.Instance.CurrentSettings.PlayerNameDisplayModeIndex;

        var isMenuTargetShown = false;
        for (var index = 0; index < visibleRoster.Count; index++)
        {
            var player = visibleRoster[index];
            PlayerListEntry entry;
            if (index < _playerListEntries.Count)
            {
                entry = _playerListEntries[index];
                entry.Update(player, _meter, playerNameDisplayMode, _getPlayerSkillState);
            }
            else
            {
                entry = PlayerListEntry.Create(player, _meter, playerNameDisplayMode, _getPlayerSkillState);
                _playerListEntries.Add(entry);
            }

            var isMenuTarget = IsPlayerMenuTarget(player.CharacterId);
            entry.IsPlayerSelectionMenuOpen = isMenuTarget;
            isMenuTargetShown |= isMenuTarget;
        }

        for (var index = _playerListEntries.Count - 1; index >= visibleRoster.Count; index--)
        {
            menuRowRemoved |= ReferenceEquals(_playerListEntries[index], _playerMenuRow);
            _playerListEntries.RemoveAt(index);
        }

        ReleaseLeftPlayerSkillStates(visibleRoster);

        _shownPlayerRoster = visibleRoster;
        RenderPlayerListFooter(visibleRoster);
        RefreshDataStatus();

        if (_playerMenuRow is not null && (menuRowRemoved || !isMenuTargetShown))
        {
            PlayerMenuTargetLeft?.Invoke(this, EventArgs.Empty);
        }
    }

    private IReadOnlyList<PlayerRosterEntry> GetVisiblePlayerRoster()
    {
        var mode = (PartyDisplayMode)_meter.PartyDisplayModeIndex;
        var party = PartyStateStore.Instance.Current;
        var visibleRoster = _playerRoster
            .Where(entry => party.ShouldInclude(entry.CharacterId, entry.IsSelf, mode))
            .ToArray();
        if (mode == PartyDisplayMode.NonPartyMembersOnly)
        {
            return SortNonPartyByName(visibleRoster);
        }

        var entriesByCharacterId = visibleRoster
            .GroupBy(entry => entry.CharacterId)
            .ToDictionary(group => group.Key, group => group.Last());
        var orderedPartyEntries = party.OrderedCharacterIds
            .Where(entriesByCharacterId.ContainsKey)
            .Select(characterId => entriesByCharacterId[characterId])
            .ToArray();
        var partyCharacterIds = party.OrderedCharacterIds.ToHashSet();
        return SortNonPartyByName(orderedPartyEntries
            .Concat(visibleRoster.Where(entry => !partyCharacterIds.Contains(entry.CharacterId)))
            .ToArray());
    }

    /// <summary>
    /// 並び替えが名前順のときに、<b>自分とパーティ以外</b>を名前順にする。
    /// 自分とパーティ(PT番号順)の並びと、PT外の 灰色 → 戦闘中 → その他 の群は守る。
    /// 名前は画面に出しているものと同じ(伏せ字・NPC の職業名込み)。同じ名前どうしは元の並び(発見順)のまま。
    /// </summary>
    private IReadOnlyList<PlayerRosterEntry> SortNonPartyByName(IReadOnlyList<PlayerRosterEntry> roster)
    {
        if (!SortsListByName)
        {
            return roster;
        }

        var nameDisplayMode = (PlayerNameDisplayMode)ConfigManager.Instance.CurrentSettings.PlayerNameDisplayModeIndex;

        return
        [
            .. roster.Where(entry => entry.IsSelf || entry.IsPartyMember),
            .. roster
                .Where(entry => !entry.IsSelf && !entry.IsPartyMember)
                .OrderBy(PlayerRosterStore.GetGroupRank)
                .ThenBy(
                    entry => PlayerInfoFormatFormatter.GetDisplayName(
                        entry.Name,
                        entry.CharacterId,
                        entry.IsSelf,
                        entry.IsNpc,
                        entry.ProfessionId,
                        nameDisplayMode),
                    StringComparer.CurrentCulture)
        ];
    }

    /// <summary>
    /// 行を位置ごとに当て直す(順が変わっても行の部品を作り直さない)。足りなければ末尾に足し、余れば末尾から外す。
    /// マップが替わったら行を作り直す。
    /// </summary>
    private void SynchronizeEntityListEntries(bool resetEntries)
    {
        if (!IsEntityList)
        {
            return;
        }

        var activeEntities = GetVisibleNearbyEntities();
        var deadEntities = _entityHistory.Defeated.Where(x=>IsListed(x)).ToArray();
        var visibleEntities = activeEntities.Concat(deadEntities.Where(x=>!activeEntities.Any(a=>a.EntityUuid==x.EntityUuid))).ToArray();
        var menuRowRemoved = resetEntries;

        if (resetEntries)
        {
            _entityListEntries.Clear();
        }

        _entityHatePlayers.Prepare(
            _entityRowSettings,
            (PlayerNameDisplayMode)ConfigManager.Instance.CurrentSettings.PlayerNameDisplayModeIndex);

        var targetsChanged = false;
        var isMenuTargetShown = false;
        for (var index = 0; index < visibleEntities.Length; index++)
        {
            var entity = visibleEntities[index];
            EntityListEntry entry;
            if (index < _entityListEntries.Count)
            {
                entry = _entityListEntries[index];
                var previousTarget = entry.Target;
                entry.Update(entity, _entityRowSettings, _entityHatePlayers, _dungeonScore.IsScoreRequested);
                targetsChanged |= !ReferenceEquals(previousTarget, entry.Target);
            }
            else
            {
                entry = EntityListEntry.Create(
                    entity,
                    _entityRowSettings,
                    _entityHatePlayers,
                    _dungeonScore.IsScoreRequested,
                    EntityListHateSlotCount);
                _entityListEntries.Add(entry);
                targetsChanged = true;
            }

            entry.IsFirstDefeated = index == activeEntities.Count && deadEntities.Length>0;
            var isMenuTarget = IsEntityMenuTarget(entity.EntityUuid);
            entry.IsEntitySelectionMenuOpen = isMenuTarget;
            isMenuTargetShown |= isMenuTarget;
        }

        for (var index = _entityListEntries.Count - 1; index >= visibleEntities.Length; index--)
        {
            menuRowRemoved |= ReferenceEquals(_entityListEntries[index], _entityMenuRow);
            _entityListEntries.RemoveAt(index);
        }

        RenderEntityListFooter(activeEntities);
        RefreshDataStatus();
        if (targetsChanged)
        {
            EntityListTargetsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (_entityMenuRow is not null && (menuRowRemoved || !isMenuTargetShown))
        {
            EntityMenuTargetLeft?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// フッターの右の2組。いつも出す。
    ///
    /// <para>
    /// 今の場所が得点を求めている間は得点。交戦中は周りにいる敵(一覧の絞り込みの前)のうち、戦闘状態の敵のアイコンの得点の合計
    /// (<see cref="EntityListEntry.GetDisplayedScore"/>)、現在は今までに得た得点。
    /// </para>
    ///
    /// <para>
    /// それ以外(求めているかが分からない途中起動の間も)は個数。付近は一覧に載っている行の数、交戦中はそのうち戦闘状態の行の数
    /// (付近より多くならないよう、一覧に載っている行の中で数える)。
    /// </para>
    /// </summary>
    private void RenderEntityListFooter(IReadOnlyList<NearbyEntityEntry> visibleEntities)
    {
        FooterCombatLabel = $"{LocalizationManager.Instance.GetString("CombatState_Tip")}:";

        if (_dungeonScore.IsScoreRequested)
        {
            FooterCombatValueText = EntityListEntry.FormatScore(
                _nearbyEntities
                    .Where(entry => entry.IsInCombat)
                    .Sum(entry => EntityListEntry.GetDisplayedScore(entry, isScoreRequested: true)));
            FooterOverallLabel = $"{LocalizationManager.Instance.GetString("EntityList_CurrentScore")}:";
            FooterOverallValueText = EntityListEntry.FormatScore(_dungeonScore.TotalScore);
            return;
        }

        FooterCombatValueText = FormatEntityCount(visibleEntities.Count(entry => entry.IsInCombat));
        FooterOverallLabel = $"{LocalizationManager.Instance.GetString("ListFooter_Nearby")}:";
        FooterOverallValueText = FormatEntityCount(visibleEntities.Count);
    }

    private static string FormatEntityCount(int count)
    {
        return LocalizationManager.Instance.Format(
            "EntityList_CountFormat",
            count.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// フッターの右の2組。付近は一覧に載っている行のうち周りにいる人(自分を含む)の数で、灰色の行と周りにいないパーティの人は数えない。
    /// 交戦中はそのうち戦闘状態の人の数(戦闘状態が届くのは自分と周りにいる人だけ)。
    /// </summary>
    private void RenderPlayerListFooter(IReadOnlyList<PlayerRosterEntry> visibleRoster)
    {
        FooterCombatLabel = $"{LocalizationManager.Instance.GetString("CombatState_Tip")}:";
        FooterCombatValueText = FormatPlayerCount(visibleRoster.Count(entry => entry.IsNearby && entry.IsInCombat));
        FooterOverallLabel = $"{LocalizationManager.Instance.GetString("ListFooter_Nearby")}:";
        FooterOverallValueText = FormatPlayerCount(visibleRoster.Count(entry => entry.IsNearby));
    }

    private static string FormatPlayerCount(int count)
    {
        return LocalizationManager.Instance.Format(
            "PlayerList_CountFormat",
            count.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// エンティティリストの画面が出たとき。出ていない間は詠唱ゲージを当て直さないので、今の時刻で当て直す
    /// (全体秒数が過ぎていれば、満タンの表示を挟まずに外す)。
    /// </summary>
    public void ReapplyEntityListCasts()
    {
        if (!IsEntityList)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        foreach (var entry in _entityListEntries)
        {
            entry.ReapplyCast(utcNow);
        }
    }

    /// <summary>
    /// 詠唱ゲージの文字とゲージを今の時刻で当て直す(画面の 0.1 秒ごとの更新)。
    /// 当て直すのはこの更新だけ(行の差し替えのたびには当て直さない。満ちた後の「全体/全体」を出す時間が、差分の当たり方で縮まないように)。
    /// </summary>
    public void RefreshEntityListCasts()
    {
        if (!IsEntityList)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        foreach (var entry in _entityListEntries)
        {
            entry.RefreshCast(utcNow);
        }
    }

    /// <summary>
    /// 一覧に載せる実体。フィルター「オブジェクト以外」は、さらにプレイヤーに見える HP バーを持つ実体だけを残す
    /// (名前はあっても HP バーの見えない実体がいる)。
    /// </summary>
    private IReadOnlyList<NearbyEntityEntry> GetVisibleNearbyEntities()
    {
        var hidesObjects = (EntityDisplayMode)_meter.EntityDisplayModeIndex == EntityDisplayMode.HideObjects;
        var visibleEntities = (_entityHistory.IsHistorical ? _entityHistory.Alive : _nearbyEntities)
            .Where(entry => !(entry.EntityType==Zproto.EEntityType.EntMonster && entry.CurrentHp==0) && IsListed(entry) && (!hidesObjects || entry.HasHpBar))
            .ToArray();

        if (!SortsListByName)
        {
            return visibleEntities;
        }

        // 戦闘状態とボス → 精鋭 → 普通 の群は守り、その中だけを名前順にする。
        // 並べ替えは安定なので、同じ名前どうしは元の並び(発見順)のまま。
        return [.. visibleEntities
            .OrderBy(NearbyEntityStore.GetSortRank)
            .ThenBy(EntityListEntry.ResolveName, StringComparer.CurrentCulture)];
    }

    /// <summary>
    /// 並び替えの設定が名前順か。名前は画面に出しているものを使うので、並べ替えは App 側で行う
    /// (Core で並べると表示言語の切り替えに追従しない)。
    /// </summary>
    private bool SortsListByName =>
        _meter.ListSortModeIndex == WidgetConfigDefaults.NameListSortModeIndex;

    /// <summary>
    /// 一覧に載せるか。
    ///
    /// <para>
    /// <c>AttrId</c> が未着(種別IDが 0)の実体は載せる(名前は「未知の敵」。<see cref="EntityListEntry"/>)。
    /// <c>AttrId</c> は出現の通知でしか届かないので、アプリの起動前から居た実体はずっと種別が分からない。
    /// 種別IDを UUID の通し番号で代えない(個体の番号なので、表を引くと別のモンスターの名前・判定になる)。
    /// </para>
    ///
    /// <para>
    /// プレイヤーに見える HP バーを持つ実体は、名前が無くても載せる(名前は「敵」「味方」。<see cref="EntityListEntry"/>)。
    /// HP バーを持たない実体は、名前があるときだけ載せる。名前は表示と同じ引き方(表示言語、空なら zh-CN)で見る。
    /// </para>
    ///
    /// <para>
    /// <b>表に行が無い実体は載せる</b>(名前は空欄で、内部ID注記だけ)。行が無いのはアプリの表が古いということで、
    /// 消すと表の欠けに気付けない。
    /// </para>
    /// </summary>
    private static bool IsListed(NearbyEntityEntry entry)
    {
        return entry.EntityId == 0
            || entry.HasHpBar
            || !CombatDataCatalog.HasEntityRow(entry.EntityType, entry.EntityId)
            || CombatDataCatalog.HasEntityName(entry.EntityType, entry.EntityId);
    }

    private void RaisePlayerWindowPresentationChanged()
    {
        if (IsPlayerWindowWidget)
        {
            PlayerWindowPresentationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
