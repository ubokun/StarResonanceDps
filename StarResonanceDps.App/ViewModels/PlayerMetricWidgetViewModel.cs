using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

public enum PlayerMetricDisplayMode
{
    SkillDetail,
    Graph
}

/// <summary>
/// スキル詳細の属性列に出すアイコン。クラスアイコンと同じ染め方で、塗りは白(行の色は下のバーが持つ)。
/// TIPSは行に1つ付けるので、アイコンは持たない。
///
/// <para>
/// 2種類以上の属性を含む行は、枠を中央の縦線で2等分し、
/// <b>量の多い1位を左、2位を右</b>に切り抜いて重ねる。3つ目以降は出さない(TIPSには出る)。
/// </para>
/// </summary>
/// <param name="PrimaryMask">1位の属性アイコンの形。2位があるときは左半分に切り抜く。</param>
/// <param name="SecondaryMask">2位の属性アイコンの形。無ければ <c>null</c>。</param>
public sealed record MetricElementIconSegment(Brush PrimaryMask, Brush? SecondaryMask)
{
    /// <summary>2位があるか。切り抜きと2枚目の出し分けに使う。</summary>
    public bool HasSecondary => SecondaryMask is not null;
}

public sealed class PlayerMetricWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    /// <summary>タイプ(書式の{Type}と行のTIPS)の並び。enumの値の順ではない。</summary>
    private static readonly Zproto.EDamageMode[] DamageModeDisplayOrder =
    [
        Zproto.EDamageMode.DamagePhysical,
        Zproto.EDamageMode.DamageMagical,
        Zproto.EDamageMode.DamageNormal
    ];

    /// <summary>属性・種類の並びと、その TIPS の区切り。記号なので4言語とも同じ。</summary>
    private const string ShareSeparator = "/";

    /// <summary>行の TIPS で、属性の内訳とタイプの内訳を分ける区切り。</summary>
    private const string ToolTipPartSeparator = ", ";

    /// <summary>
    /// ホイールが止まったとみなすまでの待ち。最後に回してからこの時間何も来なければ横軸の長さを保存する。
    /// 窓の位置と大きさの保存(<c>WidgetWindow</c> の保存待ち)と同じ値。
    /// </summary>
    private static readonly TimeSpan WheelSaveDelay = TimeSpan.FromMilliseconds(300);

    private readonly MeterSnapshotKind _kind;
    private readonly PlayerMetricDisplayMode _displayMode;
    private ElementColorWidgetSettingsConfig _elementColorSettings;
    private SkillDetailWidgetSettingsConfig _skillDetailSettings;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<MetricSkillTableEntry> _skillEntries = [];
    private readonly Dictionary<long, MetricSkillTableEntry> _skillEntriesBySkillId = [];
    private readonly List<MetricSkillTableEntry> _orderedSkillEntries = [];
    private IReadOnlyList<MetricGraphPoint> _graphPoints = Array.Empty<MetricGraphPoint>();
    private readonly MetricGraphPointBuffer _graphPointBuffer = new();
    private IReadOnlyList<MetricGraphSkillMarker> _graphSkillMarkers = Array.Empty<MetricGraphSkillMarker>();

    /// <summary>技のアイコンの置き場。後ろに足すだけで使い回し、読み直すときは頭から書く。窓と一緒に解放する。</summary>
    private MetricGraphSkillMarker[] _graphMarkerItems = [];
    private int _graphMarkerCount;
    private long _graphMarkerCharacterId;
    private int _graphMarkerNameRevision;
    private Encounter? _graphMarkerEncounter;
    private object? _graphMarkerGradeSource;
    private int _graphMarkerNextIndex;
    private int _graphVisibleSeconds = WidgetConfigDefaults.DefaultMetricGraphVisibleSeconds;
    private int _graphNumberDisplayFormatIndex;
    private bool _graphShowsSkillLog = true;
    private Brush? _graphLineBrush;
    private readonly DispatcherTimer _wheelSaveTimer;

    /// <summary>ホイールで決めて、まだ保存していない横軸の長さ。保存待ちが無ければ null。</summary>
    private int? _pendingWheelVisibleSeconds;
    private bool _isDisposed;

    public PlayerMetricWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        MeterSnapshotKind kind,
        PlayerMetricDisplayMode displayMode)
        : base(playerWidget, requestedCharacterId)
    {
        _kind = kind;
        _displayMode = displayMode;
        _elementColorSettings = playerWidget.GetElementColorSettingsSnapshot();
        _skillDetailSettings = playerWidget.GetSkillDetailSettingsSnapshot();
        SkillEntries = new ReadOnlyObservableCollection<MetricSkillTableEntry>(_skillEntries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _wheelSaveTimer = new DispatcherTimer
        {
            Interval = WheelSaveDelay
        };
        _wheelSaveTimer.Tick += WheelSaveTimer_Tick;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        playerWidget.ElementColorSettingsChanged += Widget_ElementColorSettingsChanged;
        playerWidget.SkillDetailSettingsChanged += Widget_SkillDetailSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
        Refresh();
        _refreshTimer.Start();
    }

    public bool IsSkillDetail => _displayMode == PlayerMetricDisplayMode.SkillDetail;

    public bool IsGraph => _displayMode == PlayerMetricDisplayMode.Graph;

    /// <summary>
    /// 推移グラフの点。値が変わったときだけ差し替える(差し替えるとグラフが線と配置を全部やり直す)。
    /// </summary>
    public IReadOnlyList<MetricGraphPoint> GraphPoints
    {
        get => _graphPoints;
        private set => SetProperty(ref _graphPoints, value);
    }

    /// <summary>推移グラフの横軸の長さ(秒)。設定「横軸の長さ」(窓の上のホイールでも変わる)。</summary>
    public int GraphVisibleSeconds
    {
        get => _graphVisibleSeconds;
        private set => SetProperty(ref _graphVisibleSeconds, value);
    }

    /// <summary>推移グラフの縦軸の数字の縮め方。全体設定「数値」の番号。</summary>
    public int GraphNumberDisplayFormatIndex
    {
        get => _graphNumberDisplayFormatIndex;
        private set => SetProperty(ref _graphNumberDisplayFormatIndex, value);
    }

    /// <summary>推移グラフの横軸の下に技のアイコンの行を出すか。設定「スキルログを表示」。</summary>
    public bool GraphShowsSkillLog
    {
        get => _graphShowsSkillLog;
        private set => SetProperty(ref _graphShowsSkillLog, value);
    }

    /// <summary>推移グラフの線の色。その人のクラスのグラフカラー。相手が分かるまでは null。</summary>
    public Brush? GraphLineBrush
    {
        get => _graphLineBrush;
        private set => SetProperty(ref _graphLineBrush, value);
    }

    /// <summary>推移グラフの横軸の下に出す、その人の技の開始のアイコン(届いた順)。</summary>
    public IReadOnlyList<MetricGraphSkillMarker> GraphSkillMarkers
    {
        get => _graphSkillMarkers;
        private set => SetProperty(ref _graphSkillMarkers, value);
    }

    public ReadOnlyObservableCollection<MetricSkillTableEntry> SkillEntries { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        // ホイールの保存待ちのまま窓を閉じた(アプリの終了で閉じたときも含む)。その値をここで書く。
        SavePendingWheelVisibleSeconds();
        _wheelSaveTimer.Tick -= WheelSaveTimer_Tick;

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        PlayerWidget.ElementColorSettingsChanged -= Widget_ElementColorSettingsChanged;
        PlayerWidget.SkillDetailSettingsChanged -= Widget_SkillDetailSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>
    /// 推移グラフの窓の上のホイール。<paramref name="notches"/> は奥へ回すと正で、1回ごとに横軸を
    /// <see cref="WidgetConfigDefaults.MetricGraphVisibleSecondsWheelStep"/> 秒短くする(手前へ回すと長くする)。
    /// 同じ種類の窓の描き直しと設定の窓の表示はすぐ変え、保存はホイールが止まってから(<see cref="WheelSaveDelay"/>)行う。
    /// 保存するのはホイールが決めた値(止まるまでに設定の窓のスライダーを動かしても、そのプレビューの値は書かない)。
    /// </summary>
    public void ChangeGraphVisibleSecondsByWheel(int notches)
    {
        if (!IsGraph || notches == 0)
        {
            return;
        }

        var current = PlayerWidget.MetricGraphVisibleSeconds;
        var visibleSeconds = WidgetConfigDefaults.ClampMetricGraphVisibleSeconds(
            current - notches * WidgetConfigDefaults.MetricGraphVisibleSecondsWheelStep);
        if (visibleSeconds == current)
        {
            return;
        }

        PlayerWidget.SetMetricGraphVisibleSecondsFromWidget(visibleSeconds);
        _pendingWheelVisibleSeconds = visibleSeconds;
        _wheelSaveTimer.Stop();
        _wheelSaveTimer.Start();
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        Refresh();
    }

    private void WheelSaveTimer_Tick(object? sender, EventArgs e)
    {
        SavePendingWheelVisibleSeconds();
    }

    private void SavePendingWheelVisibleSeconds()
    {
        _wheelSaveTimer.Stop();
        if (_pendingWheelVisibleSeconds is not { } visibleSeconds)
        {
            return;
        }

        _pendingWheelVisibleSeconds = null;
        PlayerWidget.SaveMetricGraphVisibleSeconds(visibleSeconds);
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
        // 相手の素性・パーティの名簿は名簿の行を変えずに届くことがあるので、読み直すたびに見る。
        RefreshDataStatus();
    }

    /// <summary>技の段階(G○)はスキルログを出している推移グラフだけ、料理と自然回復はヒール側だけが数える。</summary>
    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
        => WidgetDataStatusRules.ForPlayerMetric(
            reception,
            EncounterHistoryProvider.SelectedEncounterId is not null,
            EncounterHistoryProvider.HasLoadFailed,
            IsSelfTarget,
            SelectedCharacterId is { } characterId ? MeterSnapshotProvider.GetPlayerIdentity(characterId) : null,
            PartyStateStore.Instance.Current.IsMembershipPending,
            countsSkillLevels: IsGraph && GraphShowsSkillLog,
            countsHealingSources: _kind == MeterSnapshotKind.Healing);

    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    /// <summary>属性カラーの設定(保存かプレビュー)が変わった。行のバーの色を作り直す。</summary>
    private void Widget_ElementColorSettingsChanged(object? sender, EventArgs e)
    {
        _elementColorSettings = PlayerWidget.GetElementColorSettingsSnapshot();
        Refresh();
    }

    private void Widget_SkillDetailSettingsChanged(object? sender, EventArgs e)
    {
        _skillDetailSettings = PlayerWidget.GetSkillDetailSettingsSnapshot();
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var graphSettings = IsGraph ? PlayerWidget.GetMetricGraphSettingsSnapshot() : null;
        if (graphSettings is not null)
        {
            GraphVisibleSeconds = graphSettings.VisibleSeconds;
            GraphShowsSkillLog = graphSettings.ShowSkillLog;
            GraphNumberDisplayFormatIndex = _configManager.CurrentSettings.NumberDisplayFormatIndex;
        }

        if (SelectedCharacterId is not { } characterId)
        {
            ApplyEmptyData();
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId, playerIdentity.IsNpc, playerIdentity.ProfessionId);
        }

        if (graphSettings is not null)
        {
            if (MeterSnapshotProvider.ReadPlayerGraph(
                    _kind,
                    characterId,
                    graphSettings.AggregationIntervalSeconds,
                    _graphPointBuffer))
            {
                GraphPoints = _graphPointBuffer.CreateView();
            }

            if (graphSettings.ShowSkillLog)
            {
                RefreshGraphSkillMarkers(characterId);
            }
            else if (GraphSkillMarkers.Count != 0)
            {
                // 出さない間は技の開始を取りに行かず、アイコンの部品も作らない。
                ForgetGraphSkillMarkers();
                GraphSkillMarkers = Array.Empty<MetricGraphSkillMarker>();
            }
            if (playerIdentity is not null)
            {
                ApplyGraphLineColor(
                    graphSettings,
                    PlayerProfession.GetKey(playerIdentity.ProfessionId, playerIdentity.ClassSpec));
            }

            ClearSkillEntries();
            return;
        }

        var table = MeterSnapshotProvider.GetPlayerSkillTable(_kind, characterId);
        GraphPoints = Array.Empty<MetricGraphPoint>();
        SynchronizeSkillEntries(table.Entries, _configManager.CurrentSettings.NumberDisplayFormatIndex);
    }

    private void ApplyEmptyData()
    {
        _graphPointBuffer.Clear();
        GraphPoints = Array.Empty<MetricGraphPoint>();
        ForgetGraphSkillMarkers();
        GraphSkillMarkers = Array.Empty<MetricGraphSkillMarker>();
        ClearSkillEntries();
    }

    /// <summary>
    /// 技のアイコンを、前回の続きだけ読んで足す(タイムラインと同じ読み方)。足す分が無ければ何も作らず何も知らせない。
    /// 回・相手・G○ の元の一覧・名前の決め方(言語と表示設定)が替わったら頭から読み直す。読み直しても同じ番目の中身が同じなら、グラフは部品を差し替えない。
    /// アイコンは技の表のアイコンの欄を Skills のフォルダで探し(無ければ null = クラス不明のアイコン)、
    /// 名前が無い技の TIPS は「不明」。G○ の数字があれば、プレイヤーリストの技の枠の TIPS と同じ組み立て(<c>MeterSnapshotProvider.FormatSkillNameWithGrade</c>)で「名前 G○」にする。
    /// 背景の枠とイマジンの絵かは Core が決めた値をそのまま渡す。
    /// </summary>
    private void RefreshGraphSkillMarkers(long characterId)
    {
        var nameRevision = CombatDataCatalog.DisplayNameRevision;
        var isReread = characterId != _graphMarkerCharacterId || nameRevision != _graphMarkerNameRevision;
        if (isReread)
        {
            _graphMarkerCharacterId = characterId;
            _graphMarkerNameRevision = nameRevision;
            ForgetGraphSkillMarkers();
        }

        var slice = MeterSnapshotProvider.GetPlayerSkillActivations(
            characterId,
            _graphMarkerEncounter,
            _graphMarkerGradeSource,
            _graphMarkerNextIndex);
        if (!ReferenceEquals(slice.Encounter, _graphMarkerEncounter)
            || !ReferenceEquals(slice.GradeSource, _graphMarkerGradeSource))
        {
            _graphMarkerEncounter = slice.Encounter;
            _graphMarkerGradeSource = slice.GradeSource;
            ClearGraphMarkerItems();
            isReread = true;
        }

        _graphMarkerNextIndex = slice.NextIndex;
        var added = slice.Added;
        if (!isReread && added.Count == 0)
        {
            return;
        }

        var neededCount = _graphMarkerCount + added.Count;
        if (neededCount > _graphMarkerItems.Length)
        {
            Array.Resize(ref _graphMarkerItems, Math.Max(neededCount, _graphMarkerItems.Length * 2));
        }

        var unknownName = LocalizationManager.Instance.GetString("MetricGraph_UnknownSkill");
        for (var index = 0; index < added.Count; index++)
        {
            var activation = added[index];
            var name = string.IsNullOrWhiteSpace(activation.Name) ? unknownName : activation.Name;
            _graphMarkerItems[_graphMarkerCount++] = new MetricGraphSkillMarker(
                activation.Seconds,
                CombatIconResolver.ResolveSkillIcon(activation.IconName),
                MeterSnapshotProvider.FormatSkillNameWithGrade(name, activation.Grade),
                activation.Frame,
                activation.UsesImagineAsset);
        }

        GraphSkillMarkers = _graphMarkerCount == 0
            ? Array.Empty<MetricGraphSkillMarker>()
            : new ArrayPrefixList<MetricGraphSkillMarker>(_graphMarkerItems, _graphMarkerCount);
    }

    /// <summary>読んだ位置と控えを捨てる。次に読むときは先頭から。</summary>
    private void ForgetGraphSkillMarkers()
    {
        _graphMarkerEncounter = null;
        _graphMarkerGradeSource = null;
        _graphMarkerNextIndex = 0;
        ClearGraphMarkerItems();
    }

    private void ClearGraphMarkerItems()
    {
        Array.Clear(_graphMarkerItems, 0, _graphMarkerCount);
        _graphMarkerCount = 0;
    }

    /// <summary>
    /// 線の色。クラスの鍵の選んでいる色。グラフカラーは不透明度を持たないので不透明で描く。
    /// </summary>
    private void ApplyGraphLineColor(MetricGraphWidgetSettingsConfig settings, string classKey)
    {
        var lineColor = ResolveGraphColor(settings, classKey);
        if (GraphLineBrush is SolidColorBrush current && current.Color == lineColor)
        {
            return;
        }

        var brush = new SolidColorBrush(lineColor);
        brush.Freeze();
        GraphLineBrush = brush;
    }

    /// <summary>
    /// グラフカラーの <paramref name="classKey"/> の選んでいる色を不透明にした色。
    /// 設定はそろえた後なので、鍵の行と色の形は必ずある。
    /// </summary>
    private static Color ResolveGraphColor(MetricGraphWidgetSettingsConfig settings, string classKey)
    {
        var palette = settings.ClassColorPalettes[classKey];
        var hex = palette[Math.Clamp(settings.ClassColorIndexes[classKey], 0, palette.Count - 1)];
        if (!ColorUtilities.TryParseHex(hex, out var color))
        {
            throw new InvalidOperationException($"Graph color is not a valid color (class={classKey}, value={hex}).");
        }

        return Color.FromRgb(color.R, color.G, color.B);
    }

    /// <summary>
    /// 行を作り直さず使い回す(メーターの行と同じ)。毎回作り直すと行の UI 要素ごと作り直され、
    /// バーのアニメーションが更新のたびに 0 から始まってしまう。
    /// </summary>
    private void SynchronizeSkillEntries(
        IReadOnlyList<MetricSkillTableRowSnapshot> entries,
        int numberDisplayFormatIndex)
    {
        var activeSkillIds = entries
            .Select(entry => entry.SkillId)
            .ToHashSet();

        // バーの長さは1位の値で割る(メーターと同じ)。並びは総量の降順なので先頭が1位。
        var topValue = entries.Count == 0 ? 0UL : entries[0].TotalValue;

        for (var index = _skillEntries.Count - 1; index >= 0; index--)
        {
            var skillId = _skillEntries[index].SkillId;
            if (activeSkillIds.Contains(skillId))
            {
                continue;
            }

            _skillEntriesBySkillId.Remove(skillId);
            _skillEntries.RemoveAt(index);
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (!_skillEntriesBySkillId.TryGetValue(entry.SkillId, out var item))
            {
                item = new MetricSkillTableEntry(entry.SkillId);
                _skillEntriesBySkillId.Add(entry.SkillId, item);
            }

            ApplySkillEntry(item, entry, index, numberDisplayFormatIndex, topValue);
            _orderedSkillEntries.Add(item);
        }

        CollectionOrder.Apply(_skillEntries, _orderedSkillEntries);
        _orderedSkillEntries.Clear();
    }

    /// <param name="topValue">一覧の1位の総量。バーの長さはこれに対する比(メーターと同じ)。</param>
    private void ApplySkillEntry(
        MetricSkillTableEntry item,
        MetricSkillTableRowSnapshot entry,
        int index,
        int numberDisplayFormatIndex,
        ulong topValue)
    {
        item.Update(
            // 並びは総量の降順なので、そのまま振るとメーターの順位と同じ形になる。
            (index + 1).ToString(CultureInfo.CurrentCulture),
            CreateElementSegments(entry),
            CreateRowToolTipText(entry),
            SkillInfoFormatFormatter.Format(
                entry.Name,
                CreateElementText(entry),
                CreateDamageModeText(entry),
                entry.HitCount,
                entry.CritRate,
                _skillDetailSettings.SkillInfoFormatString),
            // 値の書式はメーターの行と同じ(MeterPlayerEntry.ValueText)。
            $"{MeterNumberFormatter.Format(entry.TotalValue, numberDisplayFormatIndex)}"
                + $" ({MeterNumberFormatter.Format(entry.ValuePerSecond, numberDisplayFormatIndex)})"
                + $" {entry.Percentage.ToString("F2", CultureInfo.CurrentCulture)}%",
            CreateBarBrush(entry),
            topValue == 0UL ? 0d : Math.Clamp(entry.TotalValue / (double)topValue, 0d, 1d));
    }

    /// <summary>
    /// 行のバーの塗り。属性ごとの色を<b>その行の割合で混ぜ</b>、不透明度を掛ける。
    /// 材料(属性の内訳)が無ければ色を作れないので <c>null</c> を返す(バーを出さない)。
    /// </summary>
    private Brush? CreateBarBrush(MetricSkillTableRowSnapshot entry)
    {
        var parts = new List<(Color Color, double Weight)>();
        foreach (var pair in entry.ValueByElement)
        {
            if (pair.Value == 0UL)
            {
                continue;
            }

            parts.Add((ElementBarColors.Resolve(_elementColorSettings, pair.Key), pair.Value));
        }

        if (!ColorUtilities.TryBlendWeighted(parts, out var blended))
        {
            return null;
        }

        return ElementBarColors.CreateBarBrush(_elementColorSettings, blended);
    }

    /// <summary>
    /// 属性の内訳。量の多い上位2つのアイコンを1つの枠に重ねる(<see cref="MetricElementIconSegment"/>)。<b>割合が0の属性は出さない</b>ので、
    /// 混ざっていない行はアイコン1つになる。割合は行の TIPS(<see cref="CreateRowToolTipText"/>)に出す。
    /// </summary>
    private IReadOnlyList<object> CreateElementSegments(MetricSkillTableRowSnapshot entry)
    {
        // 量の多い順に上位2つ。3つ目以降は枠に入らないので出さない(TIPSには全部出る)。
        var masks = new List<Brush>();
        foreach (var pair in entry.ValueByElement.OrderByDescending(pair => pair.Value))
        {
            if (pair.Value == 0UL || masks.Count >= 2)
            {
                break;
            }

            masks.Add(ElementIconMasks.Get(pair.Key));
        }

        return masks.Count switch
        {
            0 => [],
            1 => [new MetricElementIconSegment(masks[0], null)],
            _ => [new MetricElementIconSegment(masks[0], masks[1])]
        };
    }

    /// <summary>
    /// 行の TIPS。「火属性33.33%/光属性66.67%, 物理60.00%/無分類40.00%」の形で、
    /// 属性の内訳とタイプの内訳を「, 」でつなぐ。割合が 0 のものは出さない。
    /// </summary>
    private static string CreateRowToolTipText(MetricSkillTableRowSnapshot entry)
    {
        var elements = CreateElementShareText(entry);
        var modes = string.Join(
            ShareSeparator,
            EnumerateDamageModeParts(entry).Select(part => part.Label + FormatShare(part.Value, entry.TotalValue)));

        if (elements.Length == 0)
        {
            return modes;
        }

        return modes.Length == 0 ? elements : elements + ToolTipPartSeparator + modes;
    }

    /// <summary>属性の内訳(割合つき)。「火属性33.33%/光属性66.67%」。</summary>
    private static string CreateElementShareText(MetricSkillTableRowSnapshot entry)
    {
        var localization = LocalizationManager.Instance;
        var parts = new List<string>();
        foreach (var pair in entry.ValueByElement)
        {
            if (pair.Value == 0UL)
            {
                continue;
            }

            parts.Add(localization.GetString($"DamageProperty_{pair.Key}") + FormatShare(pair.Value, entry.TotalValue));
        }

        return string.Join(ShareSeparator, parts);
    }

    /// <summary>書式の {Element} に入れる属性名。2種類以上あれば「/」でつなぐ(割合は付けない)。</summary>
    private static string CreateElementText(MetricSkillTableRowSnapshot entry)
    {
        var localization = LocalizationManager.Instance;
        var parts = new List<string>();
        foreach (var pair in entry.ValueByElement)
        {
            if (pair.Value == 0UL)
            {
                continue;
            }

            parts.Add(localization.GetString($"DamageProperty_{pair.Key}"));
        }

        return string.Join(ShareSeparator, parts);
    }

    /// <summary>
    /// 物理・魔法の内訳。ラベルだけを「/」で並べる。<b>並びは 物理 → 魔法 → 無分類</b> で、割合が 0 のものは出さない。
    /// 「無分類」は <c>DamageNormal</c>(物理でも魔法でもない)。
    /// 割合は行の TIPS(<see cref="CreateRowToolTipText"/>)に出す。
    /// </summary>
    private static string CreateDamageModeText(MetricSkillTableRowSnapshot entry)
    {
        return string.Join(ShareSeparator, EnumerateDamageModeParts(entry).Select(part => part.Label));
    }

    /// <summary>タイプ(書式の{Type}と行のTIPS)に出す分だけを、決めた並びで返す。</summary>
    private static IEnumerable<(string Label, ulong Value)> EnumerateDamageModeParts(MetricSkillTableRowSnapshot entry)
    {
        var localization = LocalizationManager.Instance;
        var parts = new List<(string Label, ulong Value)>();
        foreach (var mode in DamageModeDisplayOrder)
        {
            var value = 0UL;
            foreach (var pair in entry.ValueByMode)
            {
                if (pair.Key == mode)
                {
                    value = pair.Value;
                    break;
                }
            }

            if (value == 0UL)
            {
                continue;
            }

            var label = mode switch
            {
                Zproto.EDamageMode.DamagePhysical => localization.GetString("Metric_DamageMode_Physical"),
                Zproto.EDamageMode.DamageMagical => localization.GetString("Metric_DamageMode_Magical"),
                _ => localization.GetString("Metric_DamageMode_Normal")
            };

            parts.Add((label, value));
        }

        return parts;
    }

    private static string FormatShare(ulong value, ulong total)
    {
        return ((double)value / total * 100d).ToString("F2", CultureInfo.CurrentCulture) + "%";
    }

    private void ClearSkillEntries()
    {
        _skillEntriesBySkillId.Clear();

        if (_skillEntries.Count > 0)
        {
            _skillEntries.Clear();
        }
    }
}
