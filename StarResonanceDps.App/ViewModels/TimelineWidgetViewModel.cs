// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using StarResonanceDps.App.Behaviors;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 画面に出すタイムラインの1行。時刻を見せるのは、その時刻のジャンルごとの最初の行だけ(<see cref="IsTimeShown"/>、決めるのは <see cref="TimelineLayout"/>)。
/// <see cref="Segments"/> は文字列と色つきの文字(<see cref="TextRunSegment"/>)と、プレイヤーのクラスアイコン(<see cref="TimelineClassIconSegment"/>)の並び。
/// </summary>
/// <param name="TimeText">
/// どの行も自分の経過時刻(戦闘の時計の起点から。起点の前は負)を持つ。時刻を見せない行は場所だけ取って描かない。時刻の列の幅は画面に作られている行だけで揃うので、
/// 空にすると、時刻を見せる行が画面に無いとき列が縮んで本文が左へずれる。空にするのは起点がまだ無い間だけ(立ったら全部の行を作り直す)。
/// </param>
/// <param name="Widget">選んだ文字の地の色が引く窓のパレットの持ち主。</param>
public sealed record TimelineEntry(
    string TimeText,
    bool IsTimeShown,
    IReadOnlyList<object> Segments,
    WidgetListItemViewModel Widget);

/// <summary>相手がプレイヤーの行で、名前の前に出すクラスアイコン。色はタイムラインのアイコンカラー、TIPS は特化名。</summary>
/// <param name="IconMask">職業アイコンの形。クラス色の塗りをこの形で抜く。</param>
/// <param name="Widget">TIPS の色を取る窓のパレットの持ち主。</param>
public sealed record TimelineClassIconSegment(Brush IconMask, Brush ClassBrush, string SpecName, WidgetListItemViewModel Widget);

/// <summary>
/// タイムライン。表示中のエンカウンターで、ボス大技の予告、警告の技の詠唱と、プレイヤーへのダメージ以外の敵の行動
/// (敵とプレイヤーの回復、敵へのダメージ、敵・自分・プレイヤーへのバフとデバフ)を出す。行動が詠唱から起きたなら、その詠唱の行も出す。
/// プレイヤーの被弾は被ダメログが出す。
///
/// <code>
/// 00:22:01  [敵]が[技]を構えている
/// 00:22:03  [敵]が[技]を唱えている
/// 00:22:05  [敵]の[予告技]
/// 00:22:07  [敵]の[技]
///           [敵]のHPを12345回復
///           [敵]に12345ダメージ
///           [敵]に[バフ](30秒)
/// </code>
///
/// <para>
/// 効果は、届いた瞬間ごとに技の行を1行出し、その下に同じ瞬間(同じ行動した敵・同じ発生源・同じ到着時刻)の効果だけを並べる。
/// 技の行の下で同じ相手への回復・ダメージは1行に畳み、同じ相手への同じバフも1行に畳む。同じ時刻の中は 敵の行動 → 効果 の順で、
/// 時刻はジャンルごとの最初の行に出す。詠唱の行を詠唱の時刻へ差し込むのも並べ方の側(<see cref="TimelineLayout"/>)。
/// </para>
///
/// <para>
/// <b>履歴表示の対象。</b> エンカウンターの決め方はメーターと同じで、履歴を開けばその戦闘のログになる。
/// ライブ中は <see cref="RefreshInterval"/> ごとに増えた分だけ追記する。
/// </para>
///
/// <para>
/// 取得した行(<see cref="_lines"/>)は全部持ち、表示の設定は画面に出すときに当てる。
/// 設定を変えれば、持っている行から組み直す。
/// </para>
///
/// <para>
/// 画面で選んだ文字は、右クリックの「コピー」で文字にする(<see cref="CreateCopyText"/>)。
/// </para>
/// </summary>
public sealed class TimelineWidgetViewModel : ViewModelBase, IDisposable, IWidgetDataStatusSource, ITextRowsCopySource
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    // 書式の中の差し込む位置の印。整形した文字列をここで区切って色つきの文字を差し込む。
    private const string TargetNameMarker = "\x01";
    private const string AttackerNameMarker = "\x03";
    private const string SourceNameMarker = "\x04";
    private const string DamagePartMarker = "\x05";
    private const string BuffNameMarker = "\x06";

    private readonly WidgetListItemViewModel _widget;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly DispatcherTimer _refreshTimer;
    private readonly List<TimelineLine> _lines = [];
    private readonly ObservableCollection<TimelineEntry> _entries = [];

    // 画面の行の並び(技の行・畳み)と、_entries の各行を作った元の行。元の行が変わった位置だけ作り直す。
    private readonly TimelineLayout _layout = new();
    private readonly List<TimelineRow> _entryRows = [];
    private readonly Dictionary<string, SolidColorBrush> _textBrushes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SolidColorBrush> _classBrushes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImageBrush> _professionIconMasks = new(StringComparer.Ordinal);

    private Encounter? _encounter;
    private int _nextIndex;

    // 行の時刻の起点(戦闘の時計の起点)。同じエンカウンターでも後から立つ。
    private DateTime? _combatStartUtc;
    private TimelineWidgetSettingsConfig _settings;

    public TimelineWidgetViewModel(WidgetListItemViewModel widget)
    {
        _widget = widget;
        _settings = widget.GetTimelineSettingsSnapshot();
        Entries = new ReadOnlyObservableCollection<TimelineEntry>(_entries);

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshInterval
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _widget.TimelineSettingsChanged += Widget_TimelineSettingsChanged;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        Refresh();
        _refreshTimer.Start();
    }

    public ReadOnlyObservableCollection<TimelineEntry> Entries { get; }

    /// <summary>このウィジェット。右クリックのメニューの色と不透明度は、この窓のパレットから取る。</summary>
    public WidgetListItemViewModel Widget => _widget;

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

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

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _widget.TimelineSettingsChanged -= Widget_TimelineSettingsChanged;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>
    /// 選んだ文字のコピーの文字。行の間は改行。範囲の行と位置の数え方は画面の選択(<see cref="TextRowsSelectionBehavior"/>)と同じで、
    /// 行の文字は <see cref="GetRowText"/>。
    /// </summary>
    public string CreateCopyText(TextRowsSelection selection)
    {
        var text = new StringBuilder();
        var lastRow = Math.Min(selection.EndRow, _entries.Count - 1);
        for (var row = Math.Max(selection.StartRow, 0); row <= lastRow; row++)
        {
            var rowText = GetRowText(_entries[row]);
            var start = row == selection.StartRow ? Math.Clamp(selection.StartOffset, 0, rowText.Length) : 0;
            var end = row == selection.EndRow ? Math.Clamp(selection.EndOffset, start, rowText.Length) : rowText.Length;
            if (row > selection.StartRow)
            {
                text.AppendLine();
            }

            text.Append(rowText, start, end - start);
        }

        return text.ToString();
    }

    /// <summary>
    /// 行の文字。時刻を出している行は時刻と空白1つの後に本文、ほかの行は時刻と同じ幅の空白と空白1つの後に本文(画面と同じ字下げ)。
    /// 戦闘の時計が立つ前(時刻が空)は本文だけ。本文は画面の文字そのまま。
    /// </summary>
    private static string GetRowText(TimelineEntry entry)
    {
        var text = new StringBuilder();
        if (entry.TimeText.Length > 0)
        {
            text.Append(entry.IsTimeShown ? entry.TimeText : new string(' ', entry.TimeText.Length)).Append(' ');
        }

        foreach (var segment in entry.Segments)
        {
            switch (segment)
            {
                case string part:
                    text.Append(part);
                    break;
                case TextRunSegment run:
                    text.Append(run.Text);
                    break;
            }
        }

        return text.ToString();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    /// <summary>
    /// 表示設定(保存かプレビュー)が変わった。敵の回復の表示が変われば並ぶ行が変わるので、持っている行から組み直す。
    /// テキストカラーは行の中身だけなので、並びはそのままで各行を作り直す
    /// (色見本を触るたびに全行を並べ直さない)。
    /// </summary>
    private void Widget_TimelineSettingsChanged(object? sender, EventArgs e)
    {
        var previous = _settings;
        _settings = _widget.GetTimelineSettingsSnapshot();
        _classBrushes.Clear();
        _textBrushes.Clear();

        if (_settings.EnemyHealDisplayModeIndex != previous.EnemyHealDisplayModeIndex)
        {
            ClearEntries();
            foreach (var line in _lines)
            {
                AppendEntries(line);
            }

            return;
        }

        for (var index = 0; index < _entryRows.Count; index++)
        {
            _entries[index] = CreateEntry(_entryRows[index]);
        }
    }

    /// <summary>
    /// 内部IDの表示と名前の伏せ字は、行を取得した時点の文字列に焼き込まれている。
    /// 言語切替と同じく、先頭から読み直す。
    /// </summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        Reload();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Reload();
    }

    private void Reload()
    {
        _encounter = null;
        _nextIndex = 0;
        _combatStartUtc = null;
        _lines.Clear();
        ClearEntries();
        Refresh();
    }

    private void Refresh()
    {
        var snapshot = MeterSnapshotProvider.GetTimeline(_encounter, _nextIndex);

        if (!ReferenceEquals(snapshot.Encounter, _encounter))
        {
            _encounter = snapshot.Encounter;
            _combatStartUtc = snapshot.CombatStartUtc;
            _lines.Clear();
            ClearEntries();
        }
        else if (snapshot.CombatStartUtc != _combatStartUtc)
        {
            // 起点が立った。並びは変わらないので、持っている行の時刻だけ作り直す。
            _combatStartUtc = snapshot.CombatStartUtc;
            for (var index = 0; index < _entryRows.Count; index++)
            {
                _entries[index] = CreateEntry(_entryRows[index]);
            }
        }

        _nextIndex = snapshot.NextIndex;
        _lines.AddRange(snapshot.Lines);
        foreach (var line in snapshot.Lines)
        {
            AppendEntries(line);
        }

        RefreshDataStatus();
    }

    private void ClearEntries()
    {
        _entries.Clear();
        _entryRows.Clear();
        _layout.Clear();
    }

    private void AppendEntries(TimelineLine line)
    {
        // 表示設定「敵の回復」が非表示なら、敵の回復は相手が敵でもプレイヤーでも並べ方に渡さない。
        if (line.Kind == CombatEventLogRecordKind.EnemyHeal
            && _settings.EnemyHealDisplayModeIndex == WidgetConfigDefaults.HiddenTimelineEnemyHealDisplayModeIndex)
        {
            return;
        }

        var change = _layout.Append(line);
        var rows = _layout.Rows;
        if (change.InsertedIndex >= 0)
        {
            // 前の時刻へ差し込んだ詠唱の行。後ろの行は作り直さずにずらす。
            var inserted = rows[change.InsertedIndex];
            _entryRows.Insert(change.InsertedIndex, inserted);
            _entries.Insert(change.InsertedIndex, CreateEntry(inserted));
        }

        for (var index = change.FirstChangedIndex; index < rows.Count; index++)
        {
            var row = rows[index];
            if (index < _entryRows.Count)
            {
                if (!ReferenceEquals(_entryRows[index], row))
                {
                    _entryRows[index] = row;
                    _entries[index] = CreateEntry(row);
                }

                continue;
            }

            _entryRows.Add(row);
            _entries.Add(CreateEntry(row));
        }
    }

    private TimelineEntry CreateEntry(TimelineRow row)
    {
        var line = row.Line;

        switch (row.Kind)
        {
            case TimelineRowKind.Announcement:
            case TimelineRowKind.Cast:
                var format = row.Kind == TimelineRowKind.Announcement
                    ? "Timeline_AnnounceFormat"
                    : "Timeline_CastFormat";
                return new TimelineEntry(
                    FormatTime(line),
                    row.ShowsTime,
                    CreateSourceSegments(format, line, "Timeline_UnnamedCastSkill"),
                    _widget);
            case TimelineRowKind.Skill:
            case TimelineRowKind.AnnouncementEnd:
                return new TimelineEntry(
                    FormatTime(line),
                    row.ShowsTime,
                    CreateSourceSegments("Timeline_SkillFormat", line, GetUnnamedSourceKey(line)),
                    _widget);
            case TimelineRowKind.Heal:
                return new TimelineEntry(
                    FormatTime(line), row.ShowsTime, CreateValueSegments("Timeline_HealFormat", "Timeline_HealAmountFormat", row), _widget);
            case TimelineRowKind.Damage:
                return new TimelineEntry(
                    FormatTime(line), row.ShowsTime, CreateValueSegments("Timeline_DamageFormat", "Timeline_DamageAmountFormat", row), _widget);
            case TimelineRowKind.Buff:
                return new TimelineEntry(
                    FormatTime(line), row.ShowsTime, CreateBuffSegments(row), _widget);
            default:
                throw new ArgumentOutOfRangeException(nameof(row), row.Kind, null);
        }
    }

    /// <summary>
    /// 行の時刻。戦闘の経過(<see cref="Encounter.ToCombatOffset"/>、メーターの経過と同じで自動一時停止で止まっていた区間を除く)で、
    /// 起点の前は負。起点がまだ無い間は空欄。
    /// </summary>
    private string FormatTime(TimelineLine line)
    {
        return _combatStartUtc is not null && _encounter?.ToCombatOffset(line.Timestamp) is { } offset
            ? MeterWidgetViewModel.FormatDuration(offset)
            : string.Empty;
    }

    /// <summary>
    /// 技の行・予告のバーの終わりの行で、発生源の名前が無いときの文言。回復の技の行は「回復」、バフの技の行は「何かの効果」、
    /// ダメージの技の行と予告のバーの終わりの行は「攻撃」。
    /// </summary>
    private static string GetUnnamedSourceKey(TimelineLine line)
    {
        return line.Kind switch
        {
            CombatEventLogRecordKind.EnemyHeal => "Timeline_UnnamedHeal",
            CombatEventLogRecordKind.EnemyBuff => "Timeline_UnnamedEffect",
            _ => "CombatEventLog_UnnamedSkill"
        };
    }

    /// <summary>
    /// 技(またはバフ)の名前。名前が無ければこのウィジェットだけ <paramref name="unnamedKey"/> の文言を出す
    /// (<see cref="GetUnnamedSourceKey"/>。予告行と詠唱行は「を構えている」「を唱えている」に続くので「何か」)。
    /// </summary>
    private static string FormatSourceName(TimelineLine line, string unnamedKey)
    {
        var name = string.IsNullOrEmpty(line.SourceName)
            ? LocalizationManager.Instance.GetString(unnamedKey)
            : line.SourceName;
        return line.IsBuffSource
            ? CombatDataCatalog.AppendBuffInternalId(name, line.SourceId)
            : CombatDataCatalog.AppendSkillInternalId(name, line.SourceId);
    }

    /// <summary>
    /// 予告行・予告のバーの終わりの行・詠唱行・技の行。加害者の名前(エンティティ名)と技の名前(スキル名)に、それぞれのテキストカラーを当てる。
    /// </summary>
    private IReadOnlyList<object> CreateSourceSegments(string formatKey, TimelineLine line, string unnamedKey)
    {
        var text = LocalizationManager.Instance.Format(formatKey, AttackerNameMarker, SourceNameMarker);
        EnsureMarkers(text, formatKey, AttackerNameMarker, SourceNameMarker);

        return SplitByMarkers(text, new Dictionary<char, IReadOnlyList<object>>
        {
            [AttackerNameMarker[0]] = [ColoredText(PlayerInfoFormatFormatter.GetPartyDisplayName(line.Attacker), "EntityName")],
            [SourceNameMarker[0]] = [ColoredText(FormatSourceName(line, unnamedKey), "SkillName")]
        });
    }

    /// <summary>
    /// 回復とダメージの行。書式の {0} に相手の名前、{1} に値の部分(「12345回復」「12345ダメージ」の書式 <paramref name="amountFormatKey"/> で、
    /// 回復は実際に増えた HP、ダメージはその値。部分全体が HP 数値の色)を差し込む。
    /// </summary>
    private IReadOnlyList<object> CreateValueSegments(string formatKey, string amountFormatKey, TimelineRow row)
    {
        var text = LocalizationManager.Instance.Format(formatKey, TargetNameMarker, DamagePartMarker);
        EnsureMarkers(text, formatKey, TargetNameMarker, DamagePartMarker);
        var amount = LocalizationManager.Instance.Format(amountFormatKey, row.Value.ToString(CultureInfo.InvariantCulture));

        return SplitByMarkers(text, new Dictionary<char, IReadOnlyList<object>>
        {
            [TargetNameMarker[0]] = CreateTargetNameSegments(row.Line.Target!),
            [DamagePartMarker[0]] = [ColoredText(amount, "HpValue")]
        });
    }

    /// <summary>
    /// バフの行。書式の {0} に相手の名前、{1} にバフの部分(名前。名前が無ければ「何かの効果」。持続があれば「名前(3秒)」の書式で秒(小数1桁まで)も。
    /// 部分全体が効果の色)を差し込む。
    /// </summary>
    private IReadOnlyList<object> CreateBuffSegments(TimelineRow row)
    {
        var line = row.Line;
        var buffName = CombatDataCatalog.AppendBuffInternalId(
            string.IsNullOrEmpty(line.BuffName)
                ? LocalizationManager.Instance.GetString("Timeline_UnnamedEffect")
                : line.BuffName,
            line.BuffId);
        var effect = line.BuffDurationMs <= 0
            ? buffName
            : LocalizationManager.Instance.Format(
                "Timeline_BuffDurationFormat",
                buffName,
                (line.BuffDurationMs / 1000d).ToString("0.#", CultureInfo.InvariantCulture));

        var text = LocalizationManager.Instance.Format("Timeline_BuffFormat", TargetNameMarker, BuffNameMarker);
        EnsureMarkers(text, "Timeline_BuffFormat", TargetNameMarker, BuffNameMarker);
        return SplitByMarkers(text, new Dictionary<char, IReadOnlyList<object>>
        {
            [TargetNameMarker[0]] = CreateTargetNameSegments(line.Target!),
            [BuffNameMarker[0]] = [ColoredText(effect, "Effect")]
        });
    }

    /// <summary>
    /// 回復・ダメージ・バフの行の相手の名前。プレイヤーならクラスアイコンとプレイヤー名の色、それ以外はエンティティ名の色。
    /// </summary>
    private IReadOnlyList<object> CreateTargetNameSegments(CombatEventLogParty target)
    {
        return target.IsPlayer
            ? [CreateClassIconSegment(target), ColoredText(PlayerInfoFormatFormatter.GetPartyDisplayName(target), "PlayerName")]
            : [ColoredText(PlayerInfoFormatFormatter.GetPartyDisplayName(target), "EntityName")];
    }

    private TimelineClassIconSegment CreateClassIconSegment(CombatEventLogParty target)
    {
        var professionKey = PlayerProfession.GetKey(target.ProfessionId, target.ClassSpec);
        if (!_classBrushes.TryGetValue(professionKey, out var brush))
        {
            brush = new SolidColorBrush(GetClassColor(professionKey));
            brush.Freeze();
            _classBrushes[professionKey] = brush;
        }

        if (!_professionIconMasks.TryGetValue(professionKey, out var mask))
        {
            mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.Profession.{professionKey}"))
            {
                Stretch = Stretch.Uniform
            };
            mask.Freeze();
            _professionIconMasks[professionKey] = mask;
        }

        return new TimelineClassIconSegment(
            mask,
            brush,
            PlayerInfoFormatFormatter.GetClassSpecText(target.ClassSpec),
            _widget);
    }

    /// <summary>クラスアイコンの色。タイムラインのアイコンカラーの設定で選んでいる色(引き方はプレイヤーリストと同じ)。</summary>
    private Color GetClassColor(string professionKey)
    {
        var palette = _settings.ClassColorPalettes.TryGetValue(professionKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.Timeline, professionKey);
        var selectedIndex = _settings.ClassColorIndexes.TryGetValue(professionKey, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        var selectedColor = palette.Count == 0
            ? "#A8A8A8"
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];

        return ColorUtilities.TryParseHex(selectedColor, out var color)
            ? color
            : Color.FromRgb(0xA8, 0xA8, 0xA8);
    }

    /// <summary>
    /// マーカーを埋めた文字列を、マーカーの位置で切って segment の並びにする。
    /// <paramref name="textBrush"/> を渡すと、マーカー以外の文字もその色になる(渡さなければ行の既定の色)。
    /// </summary>
    private static IReadOnlyList<object> SplitByMarkers(
        string text,
        IReadOnlyDictionary<char, IReadOnlyList<object>> replacements,
        Brush? textBrush = null)
    {
        var segments = new List<object>();
        var start = 0;

        void AddText(int end)
        {
            if (end <= start)
            {
                return;
            }

            var part = text[start..end];
            segments.Add(textBrush is null ? part : new TextRunSegment(part, textBrush));
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (!replacements.TryGetValue(text[index], out var replacement))
            {
                continue;
            }

            AddText(index);
            segments.AddRange(replacement);
            start = index + 1;
        }

        AddText(text.Length);
        return segments;
    }

    /// <summary>
    /// 書式にプレースホルダが残っているかを見る。訳を差し替えたときに静かに消えるのを防ぐ。
    /// </summary>
    private static void EnsureMarkers(string text, string formatKey, params string[] markers)
    {
        foreach (var marker in markers)
        {
            if (!text.Contains(marker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{formatKey} must contain every placeholder.");
            }
        }
    }

    private TextRunSegment ColoredText(string text, string colorKey)
    {
        return new TextRunSegment(text, GetTextBrush(colorKey));
    }

    /// <summary>テキストカラーの1色。設定が変われば <see cref="_textBrushes"/> ごと捨てて引き直す。</summary>
    private Brush GetTextBrush(string colorKey)
    {
        if (!_textBrushes.TryGetValue(colorKey, out var brush))
        {
            brush = new SolidColorBrush(GetTextColor(colorKey));
            brush.Freeze();
            _textBrushes[colorKey] = brush;
        }

        return brush;
    }

    /// <summary>テキストカラーの設定で選んでいる色(引き方はクラスカラーと同じ)。</summary>
    private Color GetTextColor(string colorKey)
    {
        var palette = _settings.TextColorPalettes.TryGetValue(colorKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultTextColors(colorKey);
        var selectedIndex = _settings.TextColorIndexes.TryGetValue(colorKey, out var index)
            ? index
            : WidgetConfigDefaults.GetDefaultTextColorIndex(colorKey);
        var selectedColor = palette.Count == 0
            ? "#FFFFFF"
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];

        return ColorUtilities.TryParseHex(selectedColor, out var color)
            ? color
            : Colors.White;
    }
}
