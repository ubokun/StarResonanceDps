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
/// 被ダメログ。表示中の回(ライブか履歴、タイムラインと同じ)のプレイヤーの被弾を1回1行で、一番上が一番新しい順に並べる(1枚だけ開く窓)。
/// 設定「最新の被ダメのみ表示」がオンなら1人1行で、その人の出す被弾のうち一番新しいものだけを、最後に被弾した人が上の順に並べる。
/// ヘッダーのボタンでも切り替わる(<see cref="ToggleLatestHitOnlyCommand"/>)。
///
/// <para>
/// 被弾は <see cref="MeterSnapshotProvider.GetDamageTakenLog"/> で回の記録の控えを 0.25秒ごとに前回の続きから読む(タイムラインと同じ読み方)。
/// 窓が持つのは出している行と人ごとの一番新しい被弾だけで、回の記録の写しは持たない。
/// 設定「フィルター設定」(自傷・フレンドリーファイア以外なら加害者がプレイヤーの被弾を出さない)と名簿で、出す行を決める。
/// 名前・クラス・特化は名簿(蘇生リストと同じ)で、名簿に居ない人の被弾は出さない(名簿は回に被弾の記録がある人を載せるので、出ないのは名簿が届くまでの間だけ)。
/// </para>
///
/// <para>
/// 行の作りはメーターと同じ。左は時刻(タイムラインの行と同じ戦闘の経過)・クラスアイコン・名前、
/// 右は設定「スキル名」の書式(エンティティ名・スキル名・タイプ、決め方は<see cref="DamageTakenLogHitInfoFormatter"/>)・被ダメ値・属性アイコン。
/// クラスアイコンは設定「アイコンカラー」の色で染め、ほかの文字とアイコンは白で、自分の強調表示と「(戦闘不能)」だけ色が付く(画面の側)。
/// 下敷きのバーは右から伸び、色はその被弾の属性の色(設定「属性カラー」)、長さは一覧の中で被ダメ値が一番大きい被弾を100%にする。
/// </para>
///
/// <para>
/// 行は作り直さず値だけ入れ替える。名前とクラスは人ごとに1回だけ作り、名簿が変わったらその人の行にだけ当て直す。
/// 出す行が変わる(フィルターか「最新の被ダメのみ表示」を変えた、名簿に人が増えた・減った)ときだけ、一覧を組み直す
/// (「最新の被ダメのみ表示」は人ごとの一番新しい被弾から、全部出すときは回の記録を先頭から読み直す)。
/// 「最新の被ダメのみ表示」で同じ人が被弾したら、その人の行の被弾を入れ替えて一番上へ動かす。
/// フッターの左は戦闘の時計の経過(メーターのタイマーと同じ)。行の左クリックと右クリックのメニュー(1項目)は、その人の被ダメ詳細を開く。
/// </para>
/// </summary>
public sealed partial class DamageTakenLogWidgetViewModel : ViewModelBase, IDisposable, IWidgetDataStatusSource
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private readonly WidgetListItemViewModel _widget;
    private readonly ConfigManager _configManager = ConfigManager.Instance;

    /// <summary>プレイヤーの窓を開く(種類と人の番号)。窓の管理の <c>OpenPlayerWindow</c>。</summary>
    private readonly Action<WidgetKind, long> _requestPlayerWindow;

    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<DamageTakenLogEntry> _entries = [];

    /// <summary>
    /// 読んだ被弾に出てくる人と、その人の一番新しい被弾。名簿に居るかが変わった人がいれば一覧を組み直し、「最新の被ダメのみ表示」の行はここから作る。
    /// 人の数で頭打ち。回が替わったら捨てる。
    /// </summary>
    private readonly Dictionary<long, LatestHits> _latestHitsByCharacterId = [];

    private readonly Dictionary<long, PlayerRosterEntry> _rosterRowsByCharacterId = [];

    /// <summary>人ごとの名前とクラス。名簿・設定・言語が変わったら作り直す。</summary>
    private readonly Dictionary<long, PersonDisplay> _personDisplays = [];

    /// <summary>「最新の被ダメのみ表示」の人ごとの行。全部出すときは空。</summary>
    private readonly Dictionary<long, DamageTakenLogEntry> _latestEntriesByCharacterId = [];

    private readonly Dictionary<string, SolidColorBrush> _classBrushes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImageBrush> _professionIconMasks = new(StringComparer.Ordinal);
    private readonly Dictionary<Zproto.EDamageProperty, Brush> _barBrushes = [];

    private DamageTakenLogWidgetSettingsConfig _settings;
    private ElementColorWidgetSettingsConfig _elementColorSettings;
    private Encounter? _encounter;

    /// <summary>次に読む記録の位置(<see cref="DamageTakenLogSnapshot.NextIndex"/>)。</summary>
    private int _nextRecordIndex;

    /// <summary>行の時刻を作ったときの戦闘の時計の起点。変わったら全部の行の時刻を作り直す。</summary>
    private DateTime? _combatStartUtc;

    /// <summary>今のバーの長さの基準(出している行の中で一番大きい被ダメ値)。</summary>
    private long _maxHitValue;

    /// <summary>フッターの左。戦闘の時計の経過(メーターのタイマーと同じ書式)。</summary>
    [ObservableProperty]
    private string _elapsedText = string.Empty;

    /// <summary>ヘッダーのボタンの文字。押すとどうなるかを出す(メーターの「計測」/「計測停止」と同じ決まり)。</summary>
    [ObservableProperty]
    private string _latestHitOnlyActionText = string.Empty;

    public DamageTakenLogWidgetViewModel(
        WidgetListItemViewModel widget,
        PlayerRosterSnapshot roster,
        Action<WidgetKind, long> requestPlayerWindow)
    {
        _widget = widget;
        _requestPlayerWindow = requestPlayerWindow;
        _settings = widget.GetDamageTakenLogSettingsSnapshot();
        _elementColorSettings = widget.GetElementColorSettingsSnapshot();
        LatestHitOnlyActionText = CreateLatestHitOnlyActionText();
        Entries = new ReadOnlyObservableCollection<DamageTakenLogEntry>(_entries);
        SetRosterRows(roster);

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshInterval
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _widget.DamageTakenLogSettingsChanged += Widget_DamageTakenLogSettingsChanged;
        _widget.ElementColorSettingsChanged += Widget_ElementColorSettingsChanged;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        Refresh();
        _refreshTimer.Start();
    }

    /// <summary>行。上が新しく下が古い。</summary>
    public ReadOnlyObservableCollection<DamageTakenLogEntry> Entries { get; }

    /// <summary>このウィジェットのカード。右クリックのメニューと行の TIPS が窓の色(<c>WidgetWindowPalette</c>)を引く。</summary>
    public WidgetListItemViewModel Widget => _widget;

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

    /// <summary>状態の印はタイムラインと同じ決まり(同じ被弾の記録を出すため)。</summary>
    public void RefreshDataStatus()
    {
        var status = WidgetDataStatusRules.ForTimeline(
            GameReceptionStateStore.Current,
            EncounterHistoryProvider.SelectedEncounterId is not null,
            EncounterHistoryProvider.HasLoadFailed);
        if (status == DataStatus)
        {
            return;
        }

        DataStatus = status;
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 名簿が変わった(画面のスレッドで窓の管理から呼ばれる)。被弾に出てくる人が名簿に居るかが変わったら一覧を組み直し、
    /// そうでなければ名前とクラスが変わった人の行にだけ当て直す。
    /// </summary>
    public void UpdateRoster(PlayerRosterSnapshot roster)
    {
        var previousRows = new Dictionary<long, PlayerRosterEntry>(_rosterRowsByCharacterId);
        SetRosterRows(roster);

        if (_latestHitsByCharacterId.Keys.Any(id => previousRows.ContainsKey(id) != _rosterRowsByCharacterId.ContainsKey(id)))
        {
            RebuildRows();
            return;
        }

        RefreshPersonDisplays(_latestHitsByCharacterId.Keys.Where(id => !Equals(
            previousRows.GetValueOrDefault(id),
            _rosterRowsByCharacterId.GetValueOrDefault(id))));
    }

    private void SetRosterRows(PlayerRosterSnapshot roster)
    {
        _rosterRowsByCharacterId.Clear();
        foreach (var row in roster.Entries)
        {
            _rosterRowsByCharacterId.TryAdd(row.CharacterId, row);
        }
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _widget.DamageTakenLogSettingsChanged -= Widget_DamageTakenLogSettingsChanged;
        _widget.ElementColorSettingsChanged -= Widget_ElementColorSettingsChanged;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    [RelayCommand]
    private void RequestDamageTakenSummary(DamageTakenLogEntry? player)
    {
        if (player is not null)
        {
            _requestPlayerWindow(WidgetKind.DamageTakenSummary, player.CharacterId);
        }
    }

    /// <summary>ヘッダーのボタン。「最新の被ダメのみ表示」を切り替えて、その項目だけを保存する(設定の窓が開いていればそちらの表示も合わせる)。</summary>
    [RelayCommand]
    private void ToggleLatestHitOnly()
    {
        _widget.SetDamageTakenLogShowsLatestHitOnlyFromWidget(!_settings.ShowsLatestHitOnly);
    }

    /// <summary>
    /// 表示設定とアイコンカラー(保存かプレビュー、ヘッダーのボタン)が変わった。フィルターか「最新の被ダメのみ表示」が変われば一覧を組み直し、
    /// そうでなければ名前とクラス・右の文字を当て直す(書式の入力の1文字ごとに来るので、行は作り直さない)。
    /// </summary>
    private void Widget_DamageTakenLogSettingsChanged(object? sender, EventArgs e)
    {
        var previousFilterIndex = _settings.AttackerFilterIndex;
        var previousShowsLatestHitOnly = _settings.ShowsLatestHitOnly;
        _settings = _widget.GetDamageTakenLogSettingsSnapshot();
        _classBrushes.Clear();
        _personDisplays.Clear();
        LatestHitOnlyActionText = CreateLatestHitOnlyActionText();

        if (_settings.AttackerFilterIndex != previousFilterIndex
            || _settings.ShowsLatestHitOnly != previousShowsLatestHitOnly)
        {
            RebuildRows();
            return;
        }

        RefreshPersonDisplays(_latestHitsByCharacterId.Keys);
        RefreshHitTexts();
    }

    /// <summary>属性カラー(保存かプレビュー)が変わった。バーの色のブラシを作り直す。</summary>
    private void Widget_ElementColorSettingsChanged(object? sender, EventArgs e)
    {
        _elementColorSettings = _widget.GetElementColorSettingsSnapshot();
        _barBrushes.Clear();
        foreach (var entry in _entries)
        {
            entry.BarBrush = entry.Hit.DamageElement is { } element ? GetBarBrush(element) : null;
        }
    }

    /// <summary>全体設定(内部IDの表示・名前の無い技の出し方・プレイヤー名)は行の文字に入るので作り直す。</summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        _personDisplays.Clear();
        RefreshPersonDisplays(_latestHitsByCharacterId.Keys);
        RefreshHitTexts();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        LatestHitOnlyActionText = CreateLatestHitOnlyActionText();
        _personDisplays.Clear();
        RefreshPersonDisplays(_latestHitsByCharacterId.Keys);
        RefreshHitTexts();
    }

    private void Refresh()
    {
        var snapshot = MeterSnapshotProvider.GetDamageTakenLog(_encounter, _nextRecordIndex);
        if (!ReferenceEquals(snapshot.Encounter, _encounter))
        {
            // 回が替わった(Core は先頭から読み直している)。持っている被弾と行を捨てる。
            _encounter = snapshot.Encounter;
            _latestHitsByCharacterId.Clear();
            ClearEntries();
        }

        _nextRecordIndex = snapshot.NextIndex;

        var clock = _encounter?.ReadCombatClock();
        var combatStartUtc = clock?.StartUtc;
        if (combatStartUtc != _combatStartUtc)
        {
            _combatStartUtc = combatStartUtc;
            foreach (var entry in _entries)
            {
                entry.TimeText = FormatTime(entry.Hit);
            }
        }

        List<DamageTakenLogEntry>? addedEntries = null;
        var latestEntriesChanged = false;
        foreach (var hit in snapshot.Hits)
        {
            RememberLatestHit(hit);
            if (!IsShown(hit))
            {
                continue;
            }

            if (_settings.ShowsLatestHitOnly)
            {
                ShowLatestHit(hit);
                latestEntriesChanged = true;
                continue;
            }

            (addedEntries ??= []).Add(CreateEntry(hit));
        }

        if (latestEntriesChanged)
        {
            // 入れ替えた被弾が前の最大より小さいと最大が下がるので、全部の行から決め直す(1人1行なので行は多くない)。
            UpdateAllBarRatios();
        }
        else if (addedEntries is not null)
        {
            InsertNewestAtTop(addedEntries);
            UpdateBarRatios(addedEntries);
        }

        ElapsedText = MeterWidgetViewModel.FormatDuration(clock?.Elapsed ?? TimeSpan.Zero);
        RefreshDataStatus();
    }

    /// <summary>その人の一番新しい被弾を覚える。初めて出てきた人なら名前とクラスを作る。</summary>
    private void RememberLatestHit(DamageTakenLogHit hit)
    {
        var isNewPerson = !_latestHitsByCharacterId.TryGetValue(hit.CharacterId, out var latest);
        _latestHitsByCharacterId[hit.CharacterId] = new LatestHits(hit, hit.Attacker.IsPlayer ? latest.FromNonPlayer : hit);
        if (isNewPerson)
        {
            RefreshPersonDisplays([hit.CharacterId]);
        }
    }

    /// <summary>古い順の行を、一番新しいものが一番上になるように入れる。一覧が空なら後ろへ足す(先頭へ1件ずつ入れると件数の2乗になる)。</summary>
    private void InsertNewestAtTop(List<DamageTakenLogEntry> entries)
    {
        if (_entries.Count == 0)
        {
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                _entries.Add(entries[index]);
            }

            return;
        }

        foreach (var entry in entries)
        {
            _entries.Insert(0, entry);
        }
    }

    /// <summary>一覧を組み直す(出す行が変わったとき)。名前とクラスは先に名簿から作り直す。</summary>
    private void RebuildRows()
    {
        ClearEntries();
        RefreshPersonDisplays(_latestHitsByCharacterId.Keys);
        if (_settings.ShowsLatestHitOnly)
        {
            AddLatestHitRows();
        }
        else
        {
            AddAllHitRows();
        }

        UpdateBarRatios(_entries);
    }

    /// <summary>「最新の被ダメのみ表示」の行を、最後に被弾した人が上の順で足す。</summary>
    private void AddLatestHitRows()
    {
        var excludesPlayerAttackers = _settings.AttackerFilterIndex == WidgetConfigDefaults.NoSelfOrFriendlyFireDamageTakenLogAttackerFilterIndex;
        var hits = new List<DamageTakenLogHit>(_latestHitsByCharacterId.Count);
        foreach (var latest in _latestHitsByCharacterId.Values)
        {
            if ((excludesPlayerAttackers ? latest.FromNonPlayer : latest.Any) is { } hit && IsShown(hit))
            {
                hits.Add(hit);
            }
        }

        hits.Sort((left, right) => right.Sequence.CompareTo(left.Sequence));
        foreach (var hit in hits)
        {
            var entry = CreateEntry(hit);
            _latestEntriesByCharacterId.Add(hit.CharacterId, entry);
            _entries.Add(entry);
        }
    }

    /// <summary>
    /// 被弾を全部、新しい順で足す。回の記録を先頭から読み直すので、続きの位置と人ごとの一番新しい被弾も今の記録に合わせる。
    /// 読んだときに回が替わっていれば何も足さない(次の更新が回の替わりとして読み直す)。
    /// </summary>
    private void AddAllHitRows()
    {
        var snapshot = MeterSnapshotProvider.GetDamageTakenLog(_encounter, 0);
        if (!ReferenceEquals(snapshot.Encounter, _encounter))
        {
            return;
        }

        _nextRecordIndex = snapshot.NextIndex;
        foreach (var hit in snapshot.Hits)
        {
            RememberLatestHit(hit);
        }

        for (var index = snapshot.Hits.Count - 1; index >= 0; index--)
        {
            var hit = snapshot.Hits[index];
            if (IsShown(hit))
            {
                _entries.Add(CreateEntry(hit));
            }
        }
    }

    /// <summary>
    /// 「最新の被ダメのみ表示」の1件。その人の行があれば被弾を入れ替えて一番上へ動かし(行は作り直さない)、無ければ一番上に足す。
    /// </summary>
    private void ShowLatestHit(DamageTakenLogHit hit)
    {
        if (!_latestEntriesByCharacterId.TryGetValue(hit.CharacterId, out var entry))
        {
            entry = CreateEntry(hit);
            _latestEntriesByCharacterId.Add(hit.CharacterId, entry);
            _entries.Insert(0, entry);
            return;
        }

        entry.Hit = hit;
        ApplyHit(entry);
        var index = _entries.IndexOf(entry);
        if (index > 0)
        {
            _entries.Move(index, 0);
        }
    }

    /// <summary>
    /// 行を全部外す。開いているメニューは閉じる(開いたままだと消えた行のメニューが残る)。
    /// </summary>
    private void ClearEntries()
    {
        foreach (var entry in _entries)
        {
            entry.IsPlayerSelectionMenuOpen = false;
        }

        _entries.Clear();
        _latestEntriesByCharacterId.Clear();
        _maxHitValue = 0;
    }

    /// <summary>
    /// 出す被弾か。名前とクラスが分かっている人(<see cref="RefreshPersonDisplays"/> で作った人)の被弾で、
    /// フィルターが自傷・フレンドリーファイア以外なら加害者がプレイヤーでないもの。
    /// </summary>
    private bool IsShown(DamageTakenLogHit hit)
    {
        return _personDisplays.ContainsKey(hit.CharacterId)
            && !(_settings.AttackerFilterIndex == WidgetConfigDefaults.NoSelfOrFriendlyFireDamageTakenLogAttackerFilterIndex
                && hit.Attacker.IsPlayer);
    }

    private DamageTakenLogEntry CreateEntry(DamageTakenLogHit hit)
    {
        var entry = new DamageTakenLogEntry(hit);
        ApplyHit(entry);
        if (_personDisplays.TryGetValue(hit.CharacterId, out var person))
        {
            ApplyPerson(entry, person);
        }

        return entry;
    }

    /// <summary>行の被弾から決まるもの(時刻・被ダメ値・設定と言語で変わる文字・属性のアイコンとバーの色)。</summary>
    private void ApplyHit(DamageTakenLogEntry entry)
    {
        var hit = entry.Hit;
        entry.TimeText = FormatTime(hit);
        entry.ValueText = hit.Value.ToString(CultureInfo.InvariantCulture);
        ApplyHitTexts(entry);
        if (hit.DamageElement is { } element)
        {
            entry.HasElement = true;
            entry.ElementIconMask = ElementIconMasks.Get(element);
            entry.BarBrush = GetBarBrush(element);
        }
        else
        {
            entry.HasElement = false;
            entry.ElementIconMask = null;
            entry.BarBrush = null;
        }
    }

    /// <summary>設定・言語で変わる行の文字(右の書式・「(戦闘不能)」・TIPS の属性名)。</summary>
    private void ApplyHitTexts(DamageTakenLogEntry entry)
    {
        var hit = entry.Hit;
        entry.HitInfoText = DamageTakenLogHitInfoFormatter.Format(_settings.HitInfoFormatString, hit);
        entry.IsDefeatShown = hit.IsLethal && _settings.ShowsDefeatCause;
        entry.ElementToolTipText = hit.DamageElement is { } element
            ? LocalizationManager.Instance.GetString($"DamageProperty_{element}")
            : string.Empty;
    }

    private void RefreshHitTexts()
    {
        foreach (var entry in _entries)
        {
            ApplyHitTexts(entry);
        }
    }

    /// <summary>
    /// 人ごとの名前とクラスを名簿から作り直し、変わった人の行にだけ当て直す。名簿に居ない人は分かっている値(<see cref="PlayerDirectory"/>)で作り、
    /// 何も分からない人は捨てる(その人の行は出さない)。
    /// </summary>
    private void RefreshPersonDisplays(IEnumerable<long> characterIds)
    {
        var nameDisplayMode = (PlayerNameDisplayMode)_configManager.CurrentSettings.PlayerNameDisplayModeIndex;
        HashSet<long>? changedCharacterIds = null;
        foreach (var characterId in characterIds)
        {
            var row = _rosterRowsByCharacterId.GetValueOrDefault(characterId)
                ?? PlayerRosterPresentationStore.Instance.GetPlayer(characterId);
            if (row is null)
            {
                _personDisplays.Remove(characterId);
                continue;
            }

            var person = CreatePersonDisplay(row, nameDisplayMode);
            if (_personDisplays.TryGetValue(characterId, out var current) && current == person)
            {
                continue;
            }

            _personDisplays[characterId] = person;
            (changedCharacterIds ??= []).Add(characterId);
        }

        if (changedCharacterIds is null)
        {
            return;
        }

        foreach (var entry in _entries)
        {
            if (changedCharacterIds.Contains(entry.CharacterId))
            {
                ApplyPerson(entry, _personDisplays[entry.CharacterId]);
            }
        }
    }

    private PersonDisplay CreatePersonDisplay(PlayerRosterEntry row, PlayerNameDisplayMode nameDisplayMode)
    {
        var professionKey = PlayerProfession.GetKey(row.ProfessionId, row.ClassSpec);
        return new PersonDisplay(
            PlayerInfoFormatFormatter.Format(row, _settings.PlayerInfoFormatString, nameDisplayMode),
            GetProfessionIconMask(professionKey),
            GetClassBrush(professionKey),
            row.IsSelf && _settings.SelfDisplayModeIndex == WidgetConfigDefaults.DefaultSelfDisplayModeIndex);
    }

    private static void ApplyPerson(DamageTakenLogEntry entry, PersonDisplay person)
    {
        entry.DisplayName = person.DisplayName;
        entry.ClassIconMask = person.ClassIconMask;
        entry.ClassBrush = person.ClassBrush;
        entry.IsSelfHighlighted = person.IsSelfHighlighted;
    }

    /// <summary>
    /// バーの長さ。出している行の中で被ダメ値が一番大きい被弾を 1 にする。
    /// 足した行で最大が変わったら全部の行を、変わらなければ足した行だけを当て直す。
    /// </summary>
    private void UpdateBarRatios(IReadOnlyList<DamageTakenLogEntry> addedEntries)
    {
        var maxValue = _maxHitValue;
        foreach (var entry in addedEntries)
        {
            maxValue = Math.Max(maxValue, entry.Hit.Value);
        }

        var targets = maxValue != _maxHitValue ? (IReadOnlyList<DamageTakenLogEntry>)_entries : addedEntries;
        _maxHitValue = maxValue;
        foreach (var entry in targets)
        {
            entry.BarRatio = maxValue > 0
                ? Math.Clamp(entry.Hit.Value / (double)maxValue, 0d, 1d)
                : 0d;
        }
    }

    /// <summary>バーの長さを全部の行から決め直す。「最新の被ダメのみ表示」で行の被弾を入れ替えたとき(最大が下がることがある)に使う。</summary>
    private void UpdateAllBarRatios()
    {
        _maxHitValue = 0;
        UpdateBarRatios(_entries);
    }

    private string CreateLatestHitOnlyActionText() => LocalizationManager.Instance.GetString(
        _settings.ShowsLatestHitOnly ? "DamageTakenLog_ShowAllHits" : "DamageTakenLog_ShowLatestHitOnly");

    /// <summary>
    /// 行の時刻。タイムラインの行と同じく戦闘の経過(<see cref="Encounter.ToCombatOffset"/>、自動一時停止で止まっていた区間を除く)で、
    /// 起点の前は負。起点がまだ無い間は空欄。
    /// </summary>
    private string FormatTime(DamageTakenLogHit hit)
    {
        return _combatStartUtc is not null && _encounter?.ToCombatOffset(hit.Timestamp) is { } offset
            ? MeterWidgetViewModel.FormatDuration(offset)
            : string.Empty;
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
            brush = new SolidColorBrush(SelectClassColor(professionKey));
            brush.Freeze();
            _classBrushes[professionKey] = brush;
        }

        return brush;
    }

    /// <summary>アイコンカラーで選んでいる色(引き方はプレイヤーリストのクラスカラーと同じ)。設定は正規化済みで、全部の鍵を持つ。</summary>
    private Color SelectClassColor(string professionKey)
    {
        var fallback = Color.FromRgb(0xA8, 0xA8, 0xA8);
        if (!_settings.ClassColorPalettes.TryGetValue(professionKey, out var palette) || palette.Count == 0)
        {
            return fallback;
        }

        var selectedIndex = _settings.ClassColorIndexes.TryGetValue(professionKey, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        return ColorUtilities.TryParseHex(palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)], out var color)
            ? color
            : fallback;
    }

    /// <summary>バーの塗り。設定が変われば <see cref="_barBrushes"/> ごと捨てて作り直す。</summary>
    private Brush GetBarBrush(Zproto.EDamageProperty element)
    {
        if (!_barBrushes.TryGetValue(element, out var brush))
        {
            brush = ElementBarColors.CreateBarBrush(
                _elementColorSettings,
                ElementBarColors.Resolve(_elementColorSettings, element));
            _barBrushes[element] = brush;
        }

        return brush;
    }

    /// <summary>1人の一番新しい被弾。<paramref name="FromNonPlayer"/> は加害者がプレイヤーでない被弾のうち一番新しいもので、無ければ null。</summary>
    private readonly record struct LatestHits(DamageTakenLogHit Any, DamageTakenLogHit? FromNonPlayer);

    /// <summary>1人ぶんの名前とクラス。ブラシは使い回すので、中身が同じなら等しい。</summary>
    private sealed record PersonDisplay(
        string DisplayName,
        Brush ClassIconMask,
        Brush ClassBrush,
        bool IsSelfHighlighted);
}
