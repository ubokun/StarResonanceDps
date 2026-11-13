// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
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
/// 蘇生リスト。名簿の戦闘不能の人を、蘇生不可デバフの残り時間の小さい順に並べる(1枚だけ開く窓、いつもライブ)。
///
/// <para>
/// 戦闘不能は、値が届いている(灰色でない)・記録から来た行でない・名簿の <see cref="PlayerRosterEntry.IsIncapacitated"/> が真の行
/// (周りにいる人は最大HPが分かっていて HP が 0、周りにいないパーティの人はパーティの同期の状態が戦闘不能)。
/// </para>
///
/// <para>
/// 右の文字と並びは <see cref="MeterSnapshotProvider.GetReviveBlockState"/> で決める。残り秒の分かる行は残り秒(並びは切れる時刻の早い順)、
/// 付いていない行は「蘇生可能」で最初、分からない行は「??秒」で最後。残りが 0 になった行も「蘇生可能」と出す。
/// 同じ鍵どうしは今の並びを動かさない。
/// </para>
///
/// <para>
/// 行の形とゲージはデバフ一覧と同じ。ゲージは残り秒を出している行だけで、「??秒」(付いているかどうかも分からない人を含む)と「蘇生可能」の行には出さない。
/// 長さは設定「ゲージの長さ」で、自動は蘇生不可デバフの効果時間の総量。色は設定「デバフカラー」。
/// </para>
///
/// <para>
/// 名簿は変わるたびに窓の管理から受け(<see cref="UpdateRoster"/>)、蘇生不可の残りは変化の合図が無いので1秒ごとにも当て直す。
/// 行は作り直さず値だけ入れ替え、マップが変わったら作り直す。
/// </para>
///
/// <para>
/// 行の左クリックと右クリックのメニューはプレイヤーリストと同じ。開く窓は窓の管理へ頼む(<see cref="_requestPlayerWindow"/>、メーターと同じ形)。
/// </para>
/// </summary>
public sealed partial class ReviveListWidgetViewModel : ViewModelBase, IDisposable, IWidgetDataStatusSource
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    /// <summary>残り秒が分からないときに書式の数字の位置へ差し込む文字(バフリストの経過不明と同じ)。</summary>
    private const string UnknownSecondsText = "??";

    private readonly WidgetListItemViewModel _widget;

    /// <summary>プレイヤーの窓を開く(種類と人の番号)。窓の管理の <c>OpenPlayerWindow</c>。</summary>
    private readonly Action<WidgetKind, long> _requestPlayerWindow;

    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<ReviveListEntry> _entries = [];
    private readonly Dictionary<long, ReviveListEntry> _entriesByCharacterId = [];
    private readonly Dictionary<string, SolidColorBrush> _classBrushes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImageBrush> _professionIconMasks = new(StringComparer.Ordinal);
    private ReviveListWidgetSettingsConfig _settings;
    private SolidColorBrush _revivableBrush;
    private PlayerRosterSnapshot _roster;
    private long _mapGeneration;

    /// <summary>出している行の元の名簿の行。状態の印の計算に使う。</summary>
    private PlayerRosterEntry[] _shownRows = [];

    /// <summary>他の人の行に、蘇生不可が分からない行があるか。状態の印の計算に使う。</summary>
    private bool _hasOtherReviveBlockUnknown;

    /// <summary>ゲージの塗り(設定「デバフカラー」)。設定が変わったときだけ作り直す。</summary>
    [ObservableProperty]
    private Brush _gaugeBrush;

    public ReviveListWidgetViewModel(
        WidgetListItemViewModel widget,
        PlayerRosterSnapshot roster,
        Action<WidgetKind, long> requestPlayerWindow)
    {
        _widget = widget;
        _requestPlayerWindow = requestPlayerWindow;
        _settings = widget.GetReviveListSettingsSnapshot();
        _revivableBrush = CreateRevivableBrush();
        _gaugeBrush = BuffGaugeBrush.Create(_settings.GaugeColors!);
        _roster = roster;
        _mapGeneration = roster.MapGeneration;
        Entries = new ReadOnlyObservableCollection<ReviveListEntry>(_entries);

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshInterval
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _widget.ReviveListSettingsChanged += Widget_ReviveListSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        Refresh();
        _refreshTimer.Start();
    }

    public ReadOnlyObservableCollection<ReviveListEntry> Entries { get; }

    /// <summary>このウィジェットのカード。右クリックのメニューが窓の色(<c>WidgetWindowPalette</c>)を引く(メーターと同じ)。</summary>
    public WidgetListItemViewModel Widget => _widget;

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

    public void RefreshDataStatus()
    {
        var status = WidgetDataStatusRules.ForReviveList(
            GameReceptionStateStore.Current,
            _shownRows,
            _hasOtherReviveBlockUnknown,
            PlayerInfoFormatFormatter.ShowsSeasonTalent(_settings.PlayerInfoFormatString));
        if (status == DataStatus)
        {
            return;
        }

        DataStatus = status;
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>名簿が変わった(画面のスレッドで窓の管理から呼ばれる)。</summary>
    public void UpdateRoster(PlayerRosterSnapshot roster)
    {
        _roster = roster;
        Refresh();
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _widget.ReviveListSettingsChanged -= Widget_ReviveListSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    [RelayCommand]
    private void RequestPlayerInfo(ReviveListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerInfo, player);
    }

    [RelayCommand]
    private void RequestPlayerStatus(ReviveListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerStatus, player);
    }

    [RelayCommand]
    private void RequestPlayerEquipment(ReviveListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerEquipment, player);
    }

    [RelayCommand]
    private void RequestPlayerTalent(ReviveListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerTalent, player);
    }

    [RelayCommand]
    private void RequestPlayerSeasonTalent(ReviveListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerSeasonTalent, player);
    }

    [RelayCommand]
    private void RequestPlayerModule(ReviveListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerModule, player);
    }

    [RelayCommand]
    private void RequestBuffList(ReviveListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.BuffList, player);
    }

    [RelayCommand]
    private void RequestDebuffList(ReviveListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.DebuffList, player);
    }

    private void RequestPlayerWindow(WidgetKind kind, ReviveListEntry? player)
    {
        if (player is not null)
        {
            _requestPlayerWindow(kind, player.CharacterId);
        }
    }

    /// <summary>設定(保存かプレビュー)が変わった。色のブラシを作り直して当て直す。</summary>
    private void Widget_ReviveListSettingsChanged(object? sender, EventArgs e)
    {
        _settings = _widget.GetReviveListSettingsSnapshot();
        _classBrushes.Clear();
        _revivableBrush = CreateRevivableBrush();
        GaugeBrush = BuffGaugeBrush.Create(_settings.GaugeColors!);
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_roster.MapGeneration != _mapGeneration)
        {
            _mapGeneration = _roster.MapGeneration;
            foreach (var entry in _entries)
            {
                entry.IsPlayerSelectionMenuOpen = false;
            }

            _entries.Clear();
            _entriesByCharacterId.Clear();
        }

        var nameDisplayMode = (PlayerNameDisplayMode)ConfigManager.Instance.CurrentSettings.PlayerNameDisplayModeIndex;
        var rows = _roster.Entries.Where(IsIncapacitated).ToArray();
        var rowCharacterIds = rows.Select(row => row.CharacterId).ToHashSet();

        for (var index = _entries.Count - 1; index >= 0; index--)
        {
            var entry = _entries[index];
            if (rowCharacterIds.Contains(entry.CharacterId))
            {
                continue;
            }

            // 一覧から消える人(蘇生された人など)のメニューは閉じる。開いたままだと消えた人のメニューが残る。
            entry.IsPlayerSelectionMenuOpen = false;
            _entriesByCharacterId.Remove(entry.CharacterId);
            _entries.RemoveAt(index);
        }

        var hasOtherReviveBlockUnknown = false;
        foreach (var row in rows)
        {
            // 新しい行は後ろに足し、並べ直しで同じ鍵の行の後ろに入る。
            if (!_entriesByCharacterId.TryGetValue(row.CharacterId, out var entry))
            {
                entry = new ReviveListEntry(row.CharacterId);
                _entriesByCharacterId.Add(row.CharacterId, entry);
                _entries.Add(entry);
            }

            var state = MeterSnapshotProvider.GetReviveBlockState(row);
            UpdateEntry(entry, row, state, nameDisplayMode);
            hasOtherReviveBlockUnknown |= !row.IsSelf && state.Kind == ReviveBlockStateKind.Unknown;
        }

        SortEntries();
        _shownRows = rows;
        _hasOtherReviveBlockUnknown = hasOtherReviveBlockUnknown;
        RefreshDataStatus();
    }

    /// <summary>
    /// 戦闘不能の行か(決め方は名簿の <see cref="PlayerRosterEntry.IsIncapacitated"/>)。
    /// 灰色の行(控えしか無い)は最後に見た状態でいまの状態ではなく、記録から来た行も同じなので外す。
    /// </summary>
    private static bool IsIncapacitated(PlayerRosterEntry player)
    {
        return player.IsLive
            && !player.IsFromRecord
            && player.IsIncapacitated;
    }

    private void UpdateEntry(
        ReviveListEntry entry,
        PlayerRosterEntry row,
        ReviveBlockState state,
        PlayerNameDisplayMode nameDisplayMode)
    {
        var professionKey = PlayerProfession.GetKey(row.ProfessionId, row.ClassSpec);
        entry.IsSelf = row.IsSelf;
        entry.ClassIconMask = GetProfessionIconMask(professionKey);
        entry.ClassBrush = GetClassBrush(professionKey);
        entry.DisplayName = PlayerInfoFormatFormatter.Format(row, _settings.PlayerInfoFormatString, nameDisplayMode);
        entry.IsSelfHighlighted = row.IsSelf
            && _settings.SelfDisplayModeIndex == WidgetConfigDefaults.DefaultSelfDisplayModeIndex;

        // 残り秒はバフリストと同じく切り上げ。0 になったら「蘇生可能」と出す。
        var roundedSeconds = state.Kind == ReviveBlockStateKind.Blocked
            ? Math.Max(0, (int)Math.Ceiling(state.RemainingSeconds))
            : 0;
        var isRevivable = state.Kind == ReviveBlockStateKind.Revivable
            || (state.Kind == ReviveBlockStateKind.Blocked && roundedSeconds == 0);
        entry.IsRevivable = isRevivable;
        entry.RevivableBrush = _revivableBrush;
        entry.StateText = isRevivable
            ? LocalizationManager.Instance.GetString("ReviveList_Revivable")
            : FormatSeconds(state.Kind == ReviveBlockStateKind.Unknown ? UnknownSecondsText : roundedSeconds);

        if (state.Kind == ReviveBlockStateKind.Blocked && roundedSeconds > 0)
        {
            var lengthSeconds = BuffGaugeBrush.GetLengthSeconds(_settings.GaugeLengthIndex) ?? state.DurationSeconds;
            (entry.BarRatio, entry.BarDecaySeconds) = BuffGaugeBrush.GetBarState(state.RemainingSeconds, lengthSeconds);
        }
        else
        {
            entry.BarRatio = 0d;
            entry.BarDecaySeconds = 0d;
        }
        entry.SortKey = state.Kind switch
        {
            ReviveBlockStateKind.Unknown => DateTime.MaxValue,
            ReviveBlockStateKind.Revivable => DateTime.MinValue,
            _ => state.EndTimeUtc
        };
    }

    /// <summary>
    /// 蘇生不可デバフの切れる早い順に並べ直す。残り秒ではなく切れる時刻で比べる(残り秒は人ごとに読む瞬間が違い、
    /// 切れる時刻のほぼ同じ人どうしで大小が入れ替わる)。OrderBy は安定なので、同じ鍵どうしは今の並びのまま。動く行だけ動かす。
    /// </summary>
    private void SortEntries()
    {
        CollectionOrder.Apply(_entries, _entries.OrderBy(entry => entry.SortKey).ToArray());
    }

    private static string FormatSeconds(object value)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            LocalizationManager.Instance.GetString("Widget_BuffDurationFormat"),
            value);
    }

    /// <summary>クラスアイコンの形。絵は職業ごとに1つなので鍵ごとに使い回す。</summary>
    private ImageBrush GetProfessionIconMask(string professionKey)
    {
        if (!_professionIconMasks.TryGetValue(professionKey, out var mask))
        {
            mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.Profession.{professionKey}"))
            {
                Stretch = Stretch.Uniform
            };
            mask.Freeze();
            _professionIconMasks[professionKey] = mask;
        }

        return mask;
    }

    /// <summary>クラスアイコンの色。設定が変われば <see cref="_classBrushes"/> ごと捨てて引き直す。</summary>
    private SolidColorBrush GetClassBrush(string professionKey)
    {
        if (!_classBrushes.TryGetValue(professionKey, out var brush))
        {
            brush = CreateBrush(SelectColor(
                _settings.ClassColorPalettes,
                _settings.ClassColorIndexes,
                professionKey,
                Color.FromRgb(0xA8, 0xA8, 0xA8)));
            _classBrushes[professionKey] = brush;
        }

        return brush;
    }

    /// <summary>テキストカラー「蘇生可能」で選んでいる色。</summary>
    private SolidColorBrush CreateRevivableBrush()
    {
        return CreateBrush(SelectColor(
            _settings.TextColorPalettes,
            _settings.TextColorIndexes,
            WidgetConfigDefaults.ReviveListRevivableTextColorKey,
            Colors.White));
    }

    /// <summary>色の設定で選んでいる色(引き方はプレイヤーリストのクラスカラーと同じ)。設定は正規化済みで、全部の鍵を持つ。</summary>
    private static Color SelectColor(
        IReadOnlyDictionary<string, List<string>> palettes,
        IReadOnlyDictionary<string, int> indexes,
        string key,
        Color fallback)
    {
        if (!palettes.TryGetValue(key, out var palette) || palette.Count == 0)
        {
            return fallback;
        }

        var selectedIndex = indexes.TryGetValue(key, out var index) ? index : WidgetConfigDefaults.MinClassColorIndex;
        return ColorUtilities.TryParseHex(palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)], out var color)
            ? color
            : fallback;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
