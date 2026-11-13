// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Config;

public sealed class WidgetStateDocument
{
    public Dictionary<string, WidgetConfig> Widgets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WidgetConfig
{
    public bool EntityDamageWidthApplied { get; set; }
    public bool PlayerListInfoActionsWidthApplied { get; set; }
    public bool TimelineIntegratedLayoutApplied { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsPinned { get; set; }

    /// <summary>クリック透過。そのウィジェットの窓すべてがマウス入力を裏の窓へ通す。ピン留めとは別のスイッチ。</summary>
    public bool IsClickThrough { get; set; }

    public WidgetState? State { get; set; }
    public WidgetThemeConfig Theme { get; set; } = WidgetConfigDefaults.CreateTheme();
    public WidgetWindowConfig Window { get; set; } = new();
    public MeterWidgetSettingsConfig? Meter { get; set; }
    public MetricGraphWidgetSettingsConfig? MetricGraph { get; set; }
    public BuffCardWidgetSettingsConfig? BuffCard { get; set; }
    public TimelineWidgetSettingsConfig? Timeline { get; set; }
    public PlayerInfoWidgetSettingsConfig? PlayerInfo { get; set; }
    public BuffListWidgetSettingsConfig? BuffList { get; set; }
    /// <summary>
    /// 属性カラー(鍵は <c>EDamageProperty</c> の名前)。与ダメ詳細・ヒール詳細・被ダメ詳細では節の名前がアイコンカラーで、
    /// 物理・魔法・無分類の行(鍵は <c>EDamageMode</c> の名前)も持つ(<see cref="WidgetConfigDefaults.HasDamageModeColorRows"/>)。
    /// </summary>
    public ElementColorWidgetSettingsConfig? ElementColor { get; set; }

    public SkillDetailWidgetSettingsConfig? SkillDetail { get; set; }
    public EquipmentWidgetSettingsConfig? Equipment { get; set; }

    public PlayerStatusWidgetSettingsConfig? PlayerStatus { get; set; }

    public BuffDebuffGroupWidgetSettingsConfig? BuffDebuffGroup { get; set; }

    public ReviveListWidgetSettingsConfig? ReviveList { get; set; }

    public DamageTakenLogWidgetSettingsConfig? DamageTakenLog { get; set; }

    public EntityInfoWidgetSettingsConfig? EntityInfo { get; set; }

    public ChatWidgetSettingsConfig? Chat { get; set; }

    /// <summary>プラグインのウィジェットの設定(プラグインが定義した設定の値)。プラグインのウィジェットだけ持つ。</summary>
    public PluginWidgetSettingsConfig? Plugin { get; set; }

    /// <summary>
    /// ステータス詳細の行の並び(<c>PlayerStatusEntry.OrderUnitAttrIds</c> の番号)。
    ///
    /// <para>
    /// 設定の窓が開いていなければ、ウィジェットで掴んで動かした瞬間にこの項目だけを保存する(ウィンドウの位置と同じ)。
    /// 開いている間は、設定の一覧でもウィジェットでも並べ替えは保存前のプレビューになり、設定の窓の「保存」で設定一式と一緒に保存する。
    /// </para>
    /// </summary>
    public List<int>? PlayerStatusRowOrder { get; set; }

    /// <summary>
    /// 前回開いていたウィンドウの対象一覧。プレイヤー用ウィンドウのみ持つ。
    ///
    /// <para>
    /// これが無いと、状態が Running のウィジェットは再起動のたびに
    /// <b>自分の窓1枚</b>として作り直される(対象の指定が失われる)。
    /// </para>
    /// </summary>
    public List<WidgetOpenTargetConfig>? OpenTargets { get; set; }

    public WidgetConfig Clone()
    {
        return new WidgetConfig
        {
            EntityDamageWidthApplied = EntityDamageWidthApplied,
            PlayerListInfoActionsWidthApplied = PlayerListInfoActionsWidthApplied,
            TimelineIntegratedLayoutApplied = TimelineIntegratedLayoutApplied,
            IsFavorite = IsFavorite,
            IsPinned = IsPinned,
            IsClickThrough = IsClickThrough,
            State = State,
            Theme = Theme?.Clone() ?? WidgetConfigDefaults.CreateTheme(),
            Window = Window?.Clone() ?? new WidgetWindowConfig(),
            Meter = Meter?.Clone(),
            MetricGraph = MetricGraph?.Clone(),
            BuffCard = BuffCard?.Clone(),
            Timeline = Timeline?.Clone(),
            PlayerInfo = PlayerInfo?.Clone(),
            BuffList = BuffList?.Clone(),
            ElementColor = ElementColor?.Clone(),
            SkillDetail = SkillDetail?.Clone(),
            Equipment = Equipment?.Clone(),
            PlayerStatus = PlayerStatus?.Clone(),
            BuffDebuffGroup = BuffDebuffGroup?.Clone(),
            ReviveList = ReviveList?.Clone(),
            DamageTakenLog = DamageTakenLog?.Clone(),
            EntityInfo = EntityInfo?.Clone(),
            Chat = Chat?.Clone(),
            Plugin = Plugin?.Clone(),
            PlayerStatusRowOrder = PlayerStatusRowOrder is null ? null : [.. PlayerStatusRowOrder],
            OpenTargets = OpenTargets?.Select(target => target.Clone()).ToList()
        };
    }
}

public sealed class WidgetThemeConfig
{
    public int WindowColorIndex { get; set; }
    public int WindowOpacity { get; set; } = 50;
    public List<string> WindowColors { get; set; } = WidgetConfigDefaults.CreateDefaultWindowColors();
    public string? BackgroundImagePath { get; set; }
    public string? BackgroundImageAverageColor { get; set; }
    public string? BackgroundImageAverageColorSourcePath { get; set; }

    /// <summary>
    /// ピン留め中にヘッダーを隠すか。隠している間はドラッグ領域も消える。
    /// <b>保存キーは旧名のまま</b>(変えると保存済みの設定が失われる)。
    /// </summary>
    public bool HideHeaderWhenInactive { get; set; } = true;

    /// <summary>ピン留め中にフッターを隠すか。保存キーは旧名のまま。</summary>
    public bool HideFooterWhenInactive { get; set; }

    public WidgetThemeConfig Clone()
    {
        return new WidgetThemeConfig
        {
            WindowColorIndex = WindowColorIndex,
            WindowOpacity = WindowOpacity,
            WindowColors = WindowColors is null ? WidgetConfigDefaults.CreateDefaultWindowColors() : [.. WindowColors],
            BackgroundImagePath = BackgroundImagePath,
            BackgroundImageAverageColor = BackgroundImageAverageColor,
            BackgroundImageAverageColorSourcePath = BackgroundImageAverageColorSourcePath,
            HideHeaderWhenInactive = HideHeaderWhenInactive,
            HideFooterWhenInactive = HideFooterWhenInactive
        };
    }
}

/// <summary>
/// プラグインのウィジェットの設定の値。鍵は設定の ID(大文字と小文字を区別しない)、値の形は <c>PluginSettingValueCodec</c>。
/// 定義に合わせるのは読むとき(定義はプラグインを読み込んだ後でないと分からない)。
/// </summary>
public sealed class PluginWidgetSettingsConfig
{
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PluginWidgetSettingsConfig Clone()
    {
        return new PluginWidgetSettingsConfig
        {
            Values = Values is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(Values, StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class WidgetWindowConfig
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }

    public WidgetWindowConfig Clone()
    {
        return new WidgetWindowConfig
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height
        };
    }
}

/// <summary>
/// 推移グラフ(DPS / HPS)の設定。描画間隔、横軸の長さ、線の色(グラフカラー)。
/// グラフカラーの形はメーターのクラスカラーと同じ(クラスごとの色。不透明度は持たない)で、既定は読み替え先のメーターと同じ
/// (<see cref="WidgetConfigDefaults.GetMetricGraphColorDefaultSource"/>)。ここの初期値は種類が分からないので DPS の既定で、
/// 種類に合わせるのは <see cref="WidgetConfigDefaults.NormalizeMetricGraph"/>。
/// </summary>
public sealed class MetricGraphWidgetSettingsConfig
{
    public int AggregationIntervalSeconds { get; set; } = WidgetConfigDefaults.DefaultMetricGraphAggregationIntervalSeconds;

    /// <summary>
    /// 横軸に入れる秒数(<see cref="WidgetConfigDefaults.MinMetricGraphVisibleSeconds"/>〜<see cref="WidgetConfigDefaults.MaxMetricGraphVisibleSeconds"/>)。
    /// 設定の窓のスライダーと、ウィジェットの窓の上のホイールで変える。
    /// </summary>
    public int VisibleSeconds { get; set; } = WidgetConfigDefaults.DefaultMetricGraphVisibleSeconds;

    /// <summary>横軸の下に押した技のアイコンの行(スキルログ)を出すか。</summary>
    public bool ShowSkillLog { get; set; } = true;

    public Dictionary<string, int> ClassColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.DpsMeter);

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.DpsMeter);

    public MetricGraphWidgetSettingsConfig Clone()
    {
        return new MetricGraphWidgetSettingsConfig
        {
            AggregationIntervalSeconds = AggregationIntervalSeconds,
            VisibleSeconds = VisibleSeconds,
            ShowSkillLog = ShowSkillLog,
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.DpsMeter)
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.DpsMeter)
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class TimelineWidgetSettingsConfig
{
    /// <summary>
    /// 敵の回復(相手が敵でもプレイヤーでも)を出すか。0=表示 / 1=非表示。
    /// 出さないときも記録と保存は続く(画面に出すときに絞る)。
    /// </summary>
    public int EnemyHealDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultTimelineEnemyHealDisplayModeIndex;

    /// <summary>
    /// 相手がプレイヤーの行のクラスアイコンの色(アイコンカラー)。形はプレイヤーリストのクラスカラーと同じで、タイムライン専用に持つ。
    /// 既定の色はプレイヤーリストと同じ。不透明度は持たない。
    /// </summary>
    public Dictionary<string, int> ClassColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.Timeline);

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.Timeline);

    /// <summary>
    /// 行の文字の色(テキストカラー)。形はクラスカラーと同じで、鍵は <see cref="WidgetConfigDefaults.TimelineTextColorKeys"/>。
    /// </summary>
    public Dictionary<string, int> TextColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultTextColorIndexes();

    public Dictionary<string, List<string>> TextColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultTextColorPalettes();

    public TimelineWidgetSettingsConfig Clone()
    {
        return new TimelineWidgetSettingsConfig
        {
            EnemyHealDisplayModeIndex = EnemyHealDisplayModeIndex,
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.Timeline)
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.Timeline)
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase),
            TextColorIndexes = TextColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultTextColorIndexes()
                : new Dictionary<string, int>(TextColorIndexes, StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = TextColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultTextColorPalettes()
                : TextColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// プレイヤー情報の設定。バッジの絵の色(アイコンカラー)。
/// 鍵は <see cref="WidgetConfigDefaults.PlayerInfoClassColorKeys"/>(クラス・潜在心相晶・シーズン・不明)。
/// 不透明度は持たない。
/// </summary>
public sealed class PlayerInfoWidgetSettingsConfig
{
    public Dictionary<string, int> ClassColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.PlayerInfo);

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.PlayerInfo);

    public PlayerInfoWidgetSettingsConfig Clone()
    {
        return new PlayerInfoWidgetSettingsConfig
        {
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.PlayerInfo)
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.PlayerInfo)
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// バフ・デバフ一覧の設定。行に出すゲージの色と長さ、一覧に出さないバフ。
///
/// <para>
/// 色は左端と右端の2つで、間はグラデーションになる(形はクラスカラーと同じ「色見本＋選んでいる枠」)。
/// 既定の長さは種別で違う(バフは短く、デバフは長い)ので、作るときに <see cref="WidgetKind"/> を渡す。
/// </para>
/// </summary>
public sealed class BuffListWidgetSettingsConfig
{
    /// <summary>ゲージの色。バフ一覧なら「バフカラー」、デバフ一覧なら「デバフカラー」の1組。</summary>
    public BuffGaugeColorConfig GaugeColors { get; set; } = new();

    /// <summary>
    /// ゲージが満タンになる残り時間。0=自動 / 1=10秒 / 2=20秒 / 3=30秒(<see cref="WidgetConfigDefaults.BuffGaugeLengthSeconds"/>)。これ以上は満タンで頭打ち。
    /// 自動はバフごとに、出している実体の効果時間の総量(延長すると残り時間と一緒に伸びる)。
    /// </summary>
    public int GaugeLengthIndex { get; set; } = WidgetConfigDefaults.DefaultBuffListGaugeLengthIndex;

    /// <summary>
    /// 優先表示リスト(一覧の先頭からこの並びで出すバフ)。行はバフの番号だけ。並びは設定の窓で並べ替える。
    /// 既定の行(<see cref="WidgetConfigDefaults.GetBuffListPriorityDefaults"/>、バフ一覧とデバフ一覧で別)はオフにしても残り、
    /// 右クリックで足した行はオフのまま保存すると消える。
    /// </summary>
    public List<BuffDebuffGroupEntryConfig>? PriorityBuffs { get; set; }

    /// <summary>
    /// 一覧に出さないバフのID(<c>BaseId</c>)。並びは非表示にした順。
    /// 窓の右クリックで足し、設定画面の削除で外す。
    /// </summary>
    public List<int> HiddenBuffIds { get; set; } = [];

    /// <summary>
    /// 同種バフの表示(同じ対象に同じ番号のバフが複数あるとき、残り時間の一番長い1つの行にまとめるか)。
    /// 0=まとめない / 1=プレイヤーのみまとめる / 2=エンティティのみまとめる / 3=すべてまとめる(<see cref="WidgetConfigDefaults.GroupsSameBuffs"/>)。
    /// </summary>
    public int SameBuffGroupingIndex { get; set; } = WidgetConfigDefaults.DefaultSameBuffGroupingIndex;

    /// <summary>付与者プレイヤー名の表示。0=表示 / 1=非表示 / 2=他者付与のみ(付与者が対象者本人なら出さない)。</summary>
    public int CasterNameDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultCasterNameDisplayModeIndex;

    /// <summary>行のバフ名の書式。差し込む項目は <c>{BuffName}</c> <c>{TargetName}</c>(対象者名)<c>{CasterName}</c>(付与者プレイヤー名)<c>{Level}</c>。</summary>
    public string? BuffInfoFormatString { get; set; }

    public BuffListWidgetSettingsConfig Clone()
    {
        return new BuffListWidgetSettingsConfig
        {
            GaugeLengthIndex = GaugeLengthIndex,
            GaugeColors = GaugeColors?.Clone() ?? new BuffGaugeColorConfig(),
            SameBuffGroupingIndex = SameBuffGroupingIndex,
            CasterNameDisplayModeIndex = CasterNameDisplayModeIndex,
            BuffInfoFormatString = BuffInfoFormatString,
            PriorityBuffs = PriorityBuffs?.Select(entry => entry.Clone()).ToList(),
            HiddenBuffIds = HiddenBuffIds is null ? [] : [.. HiddenBuffIds]
        };
    }
}

/// <summary>
/// バフ・デバフのゲージの色の1組(設定の節「バフカラー」「デバフカラー」)。
/// 色は左端と右端の2つで、間はグラデーションになる(形はクラスカラーと同じ「色見本＋選んでいる枠」)。
/// </summary>
public sealed class BuffGaugeColorConfig
{
    /// <summary>
    /// 選んでいる枠。<b>既定がバフとデバフで違う</b>(バフは1枠目、デバフは2枠目)ので、ここでは入れない。
    /// 空のまま <see cref="WidgetConfigDefaults.CloneNormalizedBuffGaugeColors"/> を通ると、そこで側に応じて埋まる。
    /// </summary>
    public Dictionary<string, int> Indexes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<string>> Palettes { get; set; } = WidgetConfigDefaults.CreateDefaultGaugeColorPalettes();

    /// <summary>
    /// 不透明度(0〜100)。作りはメーターのクラスカラーと同じで、
    /// 表示に使う色のアルファへ掛ける(設定画面の色見本には掛けない)。
    /// </summary>
    public int Opacity { get; set; } = WidgetConfigDefaults.MaxClassColorOpacity;

    public BuffGaugeColorConfig Clone()
    {
        return new BuffGaugeColorConfig
        {
            Opacity = Opacity,
            Indexes = Indexes is null
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(Indexes, StringComparer.OrdinalIgnoreCase),
            Palettes = Palettes is null
                ? WidgetConfigDefaults.CreateDefaultGaugeColorPalettes()
                : Palettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// バフ・デバフグループの1行と、バフ・デバフカードのショートカットの1行、両方の通知対象の1行、バフ・デバフ一覧とエンティティ情報の優先表示リストの1行。
/// バフの番号(<c>BaseId</c>)か、料理・薬剤のまとまり(<see cref="BuffGroup"/>)のどちらか(<see cref="BuffEntryKey"/>)。優先表示リストはバフの番号だけ。
/// </summary>
public sealed class BuffDebuffGroupEntryConfig
{
    public int BuffId { get; set; }

    public BuffGroup Group { get; set; }

    public bool IsEnabled { get; set; } = true;

    public BuffEntryKey GetKey() => new(BuffId, Group);

    public BuffDebuffGroupEntryConfig Clone()
    {
        return new BuffDebuffGroupEntryConfig
        {
            BuffId = BuffId,
            Group = Group,
            IsEnabled = IsEnabled
        };
    }
}

/// <summary>
/// バフ・デバフグループ(自分のバフ・デバフのうち、選んだものだけを並べるウィジェット)の設定。
///
/// <para>
/// 行の並びは <see cref="Entries"/> の順。既定の行(<see cref="WidgetConfigDefaults.DefaultBuffDebuffGroupEntries"/>)は
/// オフにしても残り、右クリックで足した行はオフのまま保存すると消える。
/// </para>
/// </summary>
public sealed class BuffDebuffGroupWidgetSettingsConfig
{
    /// <summary>ゲージが満タンになる残り時間。添字はバフ一覧と同じ(<see cref="WidgetConfigDefaults.BuffGaugeLengthSeconds"/>)。既定は自動。</summary>
    public int GaugeLengthIndex { get; set; } = WidgetConfigDefaults.DefaultBuffDebuffGroupGaugeLengthIndex;

    /// <summary>バフの行のゲージの色(「バフカラー」)。</summary>
    public BuffGaugeColorConfig? BuffGaugeColors { get; set; }

    /// <summary>デバフの行のゲージの色(「デバフカラー」)。</summary>
    public BuffGaugeColorConfig? DebuffGaugeColors { get; set; }

    public List<BuffDebuffGroupEntryConfig>? Entries { get; set; }

    /// <summary>
    /// 通知対象(窓に出ている行のうち、バフ・デバフ効果切れを通知するバフ・デバフ)。並びに意味は無い。
    /// 既定の行(<see cref="WidgetConfigDefaults.DefaultBuffDebuffGroupNotificationTargetEntries"/>)はオフにしても残り、右クリックで足した行はオフのまま保存すると消える。
    /// </summary>
    public List<BuffDebuffGroupEntryConfig>? NotificationTargets { get; set; }

    public BuffDebuffGroupWidgetSettingsConfig Clone()
    {
        return new BuffDebuffGroupWidgetSettingsConfig
        {
            GaugeLengthIndex = GaugeLengthIndex,
            BuffGaugeColors = BuffGaugeColors?.Clone(),
            DebuffGaugeColors = DebuffGaugeColors?.Clone(),
            Entries = Entries?.Select(entry => entry.Clone()).ToList(),
            NotificationTargets = NotificationTargets?.Select(entry => entry.Clone()).ToList()
        };
    }
}

/// <summary>
/// 開いていたウィンドウ1枚ぶんの対象。
///
/// <para>
/// プレイヤーとエンティティリストの実体(モンスター)とチャットのチャンネルとプラグインの相手を1つの型で表す。
/// <see cref="PluginTargetId"/> があればプラグインの相手、<see cref="ChatChannel"/> があればチャット、<see cref="EntityId"/> が 0 以外なら実体、そうでなければプレイヤー
/// (エンティティ情報にプレイヤーの窓は無いので、エンティティ情報でそうなら敵を待つ空の窓)。
/// </para>
/// </summary>
public sealed class WidgetOpenTargetConfig
{
    /// <summary>
    /// プレイヤーのID。<c>null</c> は「自分」。
    /// <b>解決後の自分のIDではなく、窓を開いたときの指定をそのまま保存する。</b>
    /// 解決後の値を保存すると、キャラを変えたときに前のキャラの窓として復元される。
    /// </summary>
    public long? CharacterId { get; set; }

    /// <summary>
    /// 実体の種別ID(<c>AttrId</c>)。0 ならプレイヤーの窓。
    /// 実体IDは再起動で消えるので、種別で捕まえ直す。
    /// </summary>
    public long EntityId { get; set; }

    /// <summary>
    /// 実体の種類(<c>EEntityType</c> の値)。<see cref="EntityId"/> がどの表の番号かを決めるので、
    /// 捕まえ直すときは種類と番号の両方が一致する個体だけを捕まえる。無い保存は捕まえない。
    /// </summary>
    public int? EntityType { get; set; }

    /// <summary>
    /// 最後に分かっていた対象の名前。<b>復元直後にタイトルを正しく出すために持つ。</b>
    /// これが無いと、対象がAOIに現れるまでタイトルがウィジェット名だけになる。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 実際に観測したプレイヤーのUID。<b>表示用</b>で、対象の指定ではない。
    ///
    /// <para>
    /// 自分の窓は <see cref="CharacterId"/> が <c>null</c>(＝「自分」という指定)なので、
    /// これが無いとタイトルに出すUIDが分からない。かといって <see cref="CharacterId"/> を
    /// 観測値で埋めてしまうと、キャラを変えたときに<b>前のキャラ固定の窓として復元される</b>。
    /// 指定と観測値は別に持つ。
    /// </para>
    /// </summary>
    public long? ResolvedCharacterId { get; set; }

    /// <summary>バフ・デバフカード専用。<c>PlayerBuffListKind</c> の値。</summary>
    public int? BuffListKind { get; set; }

    /// <summary>バフ・デバフカード専用。まとまりを追う窓では <c>null</c>。</summary>
    public string? BuffKey { get; set; }

    /// <summary>
    /// バフ・デバフカード専用。最後に分かっていたバフ名。<b>復元直後のタイトルに使う。</b>
    ///
    /// <para>
    /// 個別のバフを追うカードは、そのバフが失効しているとタイトルを組み直せない。
    /// まとまり(料理・薬剤)を追うカードは名前が静的なので、これが無くても出る。
    /// </para>
    /// </summary>
    public string? BuffName { get; set; }

    /// <summary>
    /// バフ・デバフカード専用。<c>BuffGroup</c> の値。0(None)なら個別のバフを追う窓。
    /// </summary>
    public int? BuffGroup { get; set; }

    /// <summary>
    /// この窓の位置と大きさ。<b>ウィンドウ1枚ごと</b>に持つ。
    ///
    /// <para>
    /// <c>WidgetConfig.Window</c> は種別ごとに1組しか無いため、同じウィジェットを複数開くと
    /// 最後に動かした窓が全部を上書きし、次の起動で全員が同じ位置に出る。
    /// <c>null</c> のときは従来どおり種別の位置とカスケードを使う。
    /// </para>
    /// </summary>
    public WidgetWindowConfig? Window { get; set; }

    /// <summary>
    /// チャット専用。<c>ChitChatChannelType</c> の値。プライベートトークの相手は <see cref="CharacterId"/>、
    /// 最後に分かっていた相手の名前は <see cref="Name"/>。
    /// </summary>
    public int? ChatChannel { get; set; }

    /// <summary>
    /// プラグインの複数起動のウィジェット専用。相手の ID(プラグインが返した値)。最後に分かっていた相手の名前は <see cref="Name"/>。
    /// </summary>
    public string? PluginTargetId { get; set; }

    public bool IsEntity => EntityId != 0;

    public WidgetOpenTargetConfig Clone()
    {
        return new WidgetOpenTargetConfig
        {
            CharacterId = CharacterId,
            EntityId = EntityId,
            EntityType = EntityType,
            Name = Name,
            ResolvedCharacterId = ResolvedCharacterId,
            BuffListKind = BuffListKind,
            BuffKey = BuffKey,
            BuffName = BuffName,
            BuffGroup = BuffGroup,
            Window = Window?.Clone(),
            ChatChannel = ChatChannel,
            PluginTargetId = PluginTargetId
        };
    }
}

public sealed class BuffCardWidgetSettingsConfig
{
    public string? BuffInfoFormatString { get; set; }

    /// <summary>
    /// 倍率(%)。キーは <c>{対象ID}:{バフキー}</c>。
    ///
    /// <para>
    /// 対象IDはプレイヤーなら <c>CharacterId</c>、モンスターなら
    /// <c>NearbyEntityEntry.EntityId</c>(= <c>MonsterTable</c> のキー = <b>種別</b>)。
    /// どちらも再起動をまたいで同じ値になる。
    /// </para>
    /// </summary>
    public Dictionary<string, int> Scales { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// ショートカット(メイン画面のカードの選択リストの先頭に並べる自分のバフ・デバフ)。並びは選択リストの並び。
    /// 既定の行(<see cref="WidgetConfigDefaults.DefaultBuffCardShortcutEntries"/>)はオフにしても残り、右クリックで足した行はオフのまま保存すると消える。
    /// </summary>
    public List<BuffDebuffGroupEntryConfig>? Shortcuts { get; set; }

    /// <summary>
    /// 通知対象(カードに出ているバフのうち、バフ・デバフ効果切れを通知するバフ・デバフ)。全部のカード(自分・他人・エンティティ)で共通。並びに意味は無い。
    /// 既定の行(<see cref="WidgetConfigDefaults.DefaultBuffCardNotificationTargetEntries"/>)はオフにしても残り、右クリックで足した行はオフのまま保存すると消える。
    /// </summary>
    public List<BuffDebuffGroupEntryConfig>? NotificationTargets { get; set; }

    public BuffCardWidgetSettingsConfig Clone()
    {
        return new BuffCardWidgetSettingsConfig
        {
            BuffInfoFormatString = BuffInfoFormatString,
            Scales = Scales is null
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(Scales, StringComparer.OrdinalIgnoreCase),
            Shortcuts = Shortcuts?.Select(entry => entry.Clone()).ToList(),
            NotificationTargets = NotificationTargets?.Select(entry => entry.Clone()).ToList()
        };
    }
}

public sealed class MeterWidgetSettingsConfig
{
    public string? PlayerInfoFormatString { get; set; }

    public bool EntityHpAsPercentage { get; set; } = true;

    public int HealthValueDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultHealthValueDisplayModeIndex;

    /// <summary>プレイヤーリストに潜在心相晶の型の絵を出すか。使うのはプレイヤーリストだけ。</summary>
    public bool ShowSeasonTalent { get; set; } = true;

    /// <summary>
    /// プレイヤーリストとエンティティリストの設定「戦闘状態」(戦闘状態の人・実体の名前の左に印を出すか)。
    /// 0=表示 / 1=非表示 / 2=自分のみ表示(<see cref="WidgetConfigDefaults.CombatStateDisplayShowIndex"/> ほか)。
    /// 2 はプレイヤーリストだけ(エンティティリストには自分の行が無いので、読み込みで 0〜1 に収める)。
    /// </summary>
    public int CombatStateDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultCombatStateDisplayModeIndex;

    /// <summary>
    /// エンティティリストの設定「狙われるプレイヤー」(ヘイトの枠の左端に今狙っている相手を出すか)。
    /// 0=表示 / 1=非表示(<see cref="WidgetConfigDefaults.TargetedPlayerDisplayShowIndex"/> ほか)。
    /// </summary>
    public int TargetedPlayerDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultTargetedPlayerDisplayModeIndex;

    /// <summary>
    /// エンティティリストの設定「ヘイト値」(ヘイトの枠に何位まで並べるか)。
    /// 人数は <see cref="WidgetConfigDefaults.HateRankDisplayCounts"/> の添字で引く(0=上位5人 … 4=ヘイト1位のみ)。
    /// </summary>
    public int HateRankDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultHateRankDisplayModeIndex;

    public int PartyDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultPartyDisplayModeIndex;

    /// <summary>エンティティリストのフィルター。0=すべて表示 / 1=オブジェクト以外(<c>EntityDisplayMode</c>)。</summary>
    public int EntityDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultEntityDisplayModeIndex;

    /// <summary>自分の行の見せ方。0=強調表示 / 1=通常表示。</summary>
    public int SelfDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultSelfDisplayModeIndex;

    /// <summary>
    /// 一覧の並び替え。0=発見順 / 1=名前順。既定はプレイヤーリストが発見順、エンティティリストが名前順。
    /// <b>群(プレイヤーリストの 自分→パーティ→灰色→戦闘中→その他、エンティティリストの 戦闘状態→それ以外、ボス→精鋭→普通)の順は変えない。</b>
    /// この設定が決めるのは、その群の中の並びだけ。使うのはプレイヤーリストとエンティティリストだけ。
    /// </summary>
    public int ListSortModeIndex { get; set; } = WidgetConfigDefaults.FirstSeenListSortModeIndex;

    public int ClassColorOpacity { get; set; } = WidgetConfigDefaults.MaxClassColorOpacity;

    public Dictionary<string, int> ClassColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorIndexes();

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.PlayerList);

    /// <summary>
    /// 他人のロールスキルを表示するか。キーはスキルID。
    /// オンにしたものだけを他人のプレイヤーリストに出す。<b>自分は対象外。</b>
    /// </summary>
    public Dictionary<string, bool> OtherRoleSkillVisibility { get; set; } =
        WidgetConfigDefaults.CreateDefaultOtherRoleSkillVisibility();

    public MeterWidgetSettingsConfig Clone()
    {
        return new MeterWidgetSettingsConfig
        {
            PlayerInfoFormatString = PlayerInfoFormatString,
            HealthValueDisplayModeIndex = HealthValueDisplayModeIndex,
            EntityHpAsPercentage = EntityHpAsPercentage,
            ShowSeasonTalent = ShowSeasonTalent,
            CombatStateDisplayModeIndex = CombatStateDisplayModeIndex,
            TargetedPlayerDisplayModeIndex = TargetedPlayerDisplayModeIndex,
            HateRankDisplayModeIndex = HateRankDisplayModeIndex,
            PartyDisplayModeIndex = PartyDisplayModeIndex,
            EntityDisplayModeIndex = EntityDisplayModeIndex,
            SelfDisplayModeIndex = SelfDisplayModeIndex,
            ListSortModeIndex = ListSortModeIndex,
            ClassColorOpacity = ClassColorOpacity,
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes()
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.PlayerList)
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase),
            OtherRoleSkillVisibility = OtherRoleSkillVisibility is null
                ? WidgetConfigDefaults.CreateDefaultOtherRoleSkillVisibility()
                : new Dictionary<string, bool>(OtherRoleSkillVisibility, StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>スキル詳細の表示設定。いまは行名の書式だけ。</summary>
public sealed class SkillDetailWidgetSettingsConfig
{
    /// <remarks><c>null</c> は「設定されていない」。既定は <see cref="WidgetConfigDefaults.DefaultSkillInfoFormatString"/>。</remarks>
    public string? SkillInfoFormatString { get; set; }

    public SkillDetailWidgetSettingsConfig Clone()
    {
        return new SkillDetailWidgetSettingsConfig
        {
            SkillInfoFormatString = SkillInfoFormatString
        };
    }
}

/// <summary>装備詳細の表示設定。行の書式と、品質ごとのテキストカラー。</summary>
public sealed class EquipmentWidgetSettingsConfig
{
    /// <remarks><c>null</c> は「設定されていない」。既定は <see cref="WidgetConfigDefaults.DefaultEquipmentInfoFormatString"/>。</remarks>
    public string? InfoFormat { get; set; }

    /// <summary>
    /// 装備名の文字の色。形はクラスカラーと同じで、<b>鍵はアイテムの品質の番号</b>
    /// (<see cref="WidgetConfigDefaults.EquipmentTextColorKeys"/>)。
    /// </summary>
    public Dictionary<string, int> TextColorIndexes { get; set; } =
        WidgetConfigDefaults.CreateDefaultEquipmentTextColorIndexes();

    public Dictionary<string, List<string>> TextColorPalettes { get; set; } =
        WidgetConfigDefaults.CreateDefaultEquipmentTextColorPalettes();

    public EquipmentWidgetSettingsConfig Clone()
    {
        return new EquipmentWidgetSettingsConfig
        {
            InfoFormat = InfoFormat,
            TextColorIndexes = TextColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultEquipmentTextColorIndexes()
                : new Dictionary<string, int>(TextColorIndexes, StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = TextColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultEquipmentTextColorPalettes()
                : TextColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// 蘇生リストの設定。プレイヤー名の書式と自分の強調表示(どちらもプレイヤーリストと同じ値の形)、ゲージの長さと色(デバフカラー、デバフ一覧と同じ形)、
/// クラスアイコンの色(アイコンカラー)、「蘇生可能」の文字の色(テキストカラー)。
/// アイコンカラーの形と既定の色はプレイヤーリストと同じで、不透明度は持たない。
/// </summary>
public sealed class ReviveListWidgetSettingsConfig
{
    /// <remarks><c>null</c> は「設定されていない」。既定は <see cref="WidgetConfigDefaults.DefaultReviveListPlayerInfoFormatString"/>。</remarks>
    public string? PlayerInfoFormatString { get; set; }

    /// <summary>自分の行の見せ方。0=強調表示 / 1=通常表示(プレイヤーリストと同じ値)。</summary>
    public int SelfDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultSelfDisplayModeIndex;

    /// <summary>
    /// ゲージが満タンになる残り時間。添字はバフ一覧と同じ(<see cref="WidgetConfigDefaults.BuffGaugeLengthSeconds"/>)。
    /// 自動は蘇生不可デバフの効果時間の総量。
    /// </summary>
    public int GaugeLengthIndex { get; set; } = WidgetConfigDefaults.DefaultReviveListGaugeLengthIndex;

    /// <summary>ゲージの色(「デバフカラー」)。</summary>
    public BuffGaugeColorConfig? GaugeColors { get; set; }

    public Dictionary<string, int> ClassColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.ReviveList);

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.ReviveList);

    /// <summary>文字の色。形はクラスカラーと同じで、鍵は <see cref="WidgetConfigDefaults.ReviveListTextColorKeys"/>。</summary>
    public Dictionary<string, int> TextColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultReviveListTextColorIndexes();

    public Dictionary<string, List<string>> TextColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultReviveListTextColorPalettes();

    public ReviveListWidgetSettingsConfig Clone()
    {
        return new ReviveListWidgetSettingsConfig
        {
            PlayerInfoFormatString = PlayerInfoFormatString,
            SelfDisplayModeIndex = SelfDisplayModeIndex,
            GaugeLengthIndex = GaugeLengthIndex,
            GaugeColors = GaugeColors?.Clone(),
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.ReviveList)
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.ReviveList)
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase),
            TextColorIndexes = TextColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultReviveListTextColorIndexes()
                : new Dictionary<string, int>(TextColorIndexes, StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = TextColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultReviveListTextColorPalettes()
                : TextColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// 被ダメログの設定。プレイヤー名の書式と自分の強調表示(蘇生リストと同じ値の形)、右の書式(設定「スキル名」)、
/// 戦闘不能の原因を表示するか、フィルター、最新の被弾だけを出すか、クラスアイコンの色(アイコンカラー、蘇生リストと同じ形と既定の色)。
/// バーの色(節「属性カラー」)は属性カラーの設定(<see cref="WidgetConfig.ElementColor"/>)が持つ。
/// </summary>
public sealed class DamageTakenLogWidgetSettingsConfig
{
    /// <remarks><c>null</c> は「設定されていない」。既定は <see cref="WidgetConfigDefaults.DefaultDamageTakenLogPlayerInfoFormatString"/>。</remarks>
    public string? PlayerInfoFormatString { get; set; }

    /// <summary>自分の行の見せ方。0=強調表示 / 1=通常表示(プレイヤーリストと同じ値)。</summary>
    public int SelfDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultSelfDisplayModeIndex;

    /// <remarks>右の書式。<c>null</c> は「設定されていない」。既定は <see cref="WidgetConfigDefaults.DefaultDamageTakenLogHitInfoFormatString"/>。</remarks>
    public string? HitInfoFormatString { get; set; }

    /// <summary>その被弾で倒れた行の被ダメ値の後ろに「(戦闘不能)」を出すか(設定「戦闘不能の原因を表示」)。</summary>
    public bool ShowsDefeatCause { get; set; } = WidgetConfigDefaults.DefaultDamageTakenLogShowsDefeatCause;

    /// <summary>
    /// 設定「フィルター設定」。0=すべて表示 / 1=自傷・フレンドリーファイア以外(加害者がプレイヤーの被弾を出さない)。
    /// <see cref="WidgetConfigDefaults.NoSelfOrFriendlyFireDamageTakenLogAttackerFilterIndex"/>。
    /// </summary>
    public int AttackerFilterIndex { get; set; } = WidgetConfigDefaults.DefaultDamageTakenLogAttackerFilterIndex;

    /// <summary>設定「最新の被ダメのみ表示」。真なら1人1行でその人の一番新しい被弾だけ、偽なら被弾を全部出す。ヘッダーのボタンでも切り替わる。</summary>
    public bool ShowsLatestHitOnly { get; set; } = WidgetConfigDefaults.DefaultDamageTakenLogShowsLatestHitOnly;

    public Dictionary<string, int> ClassColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.DamageTakenLog);

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.DamageTakenLog);

    public DamageTakenLogWidgetSettingsConfig Clone()
    {
        return new DamageTakenLogWidgetSettingsConfig
        {
            PlayerInfoFormatString = PlayerInfoFormatString,
            SelfDisplayModeIndex = SelfDisplayModeIndex,
            HitInfoFormatString = HitInfoFormatString,
            ShowsDefeatCause = ShowsDefeatCause,
            AttackerFilterIndex = AttackerFilterIndex,
            ShowsLatestHitOnly = ShowsLatestHitOnly,
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.DamageTakenLog)
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.DamageTakenLog)
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// エンティティ情報の設定。エンティティ名とバフ名の書式、HP数値・戦闘状態・狙われるプレイヤー・ヘイト値(エンティティリストと同じ値の形で、
/// ヘイト値だけ先頭に「全員を表示」がある)、同種バフの表示・付与者プレイヤー名の表示、優先表示リスト、一覧に出さないバフ、
/// アイコンカラー(エンティティリストと同じ鍵と既定の色)。
/// </summary>
public sealed class EntityInfoWidgetSettingsConfig
{
    /// <remarks><c>null</c> は「設定されていない」。既定は <see cref="WidgetConfigDefaults.DefaultEntityInfoFormatString"/>。</remarks>
    public string? EntityInfoFormatString { get; set; }

    /// <remarks>
    /// バフ・デバフの枠の名前の書式(差し込む項目はバフ・デバフ一覧と同じ)。
    /// <c>null</c> は「設定されていない」。既定は <see cref="WidgetConfigDefaults.DefaultBuffListBuffInfoFormatString"/>。
    /// </remarks>
    public string? BuffInfoFormatString { get; set; }

    public int HealthValueDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultHealthValueDisplayModeIndex;

    /// <summary>名前の左に戦闘状態の印を出すか。0=表示 / 1=非表示(エンティティリストと同じ値)。</summary>
    public int CombatStateDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultCombatStateDisplayModeIndex;

    /// <summary>ヘイトの行の左端に今狙っている相手を出すか。0=表示 / 1=非表示(エンティティリストと同じ値)。</summary>
    public int TargetedPlayerDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultTargetedPlayerDisplayModeIndex;

    /// <summary>
    /// ヘイトの行に何位まで並べるか。人数は <see cref="WidgetConfigDefaults.EntityInfoHateRankDisplayCounts"/> の添字で引く
    /// (0=全員を表示 / 1=上位5人 … 5=ヘイト1位のみ)。
    /// </summary>
    public int HateRankDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultEntityInfoHateRankDisplayModeIndex;

    /// <summary>同じ番号のバフを、残り時間の一番長い1つの枠にまとめるか。0=まとめない / 1=まとめる。</summary>
    public int SameBuffGroupingIndex { get; set; } = WidgetConfigDefaults.DefaultEntityInfoSameBuffGroupingIndex;

    /// <summary>付与者プレイヤー名の表示。0=表示 / 1=非表示 / 2=他者付与のみ(バフ・デバフ一覧と同じ値)。</summary>
    public int CasterNameDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultCasterNameDisplayModeIndex;

    /// <summary>
    /// 優先表示リスト(バフとデバフの枠の1枠目からこの並びで出すバフ)。扱いはバフ・デバフ一覧と同じで、
    /// 既定の行は <see cref="WidgetConfigDefaults.DefaultEntityInfoPriorityBuffEntries"/>。
    /// </summary>
    public List<BuffDebuffGroupEntryConfig>? PriorityBuffs { get; set; }

    /// <summary>
    /// 枠に出さないバフのID(<c>BaseId</c>)。並びは非表示にした順。
    /// 枠の右クリックで足し、設定画面のスイッチで外す。
    /// </summary>
    public List<int> HiddenBuffIds { get; set; } = [];

    public Dictionary<string, int> ClassColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.EntityInfo);

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.EntityInfo);

    public EntityInfoWidgetSettingsConfig Clone()
    {
        return new EntityInfoWidgetSettingsConfig
        {
            EntityInfoFormatString = EntityInfoFormatString,
            BuffInfoFormatString = BuffInfoFormatString,
            HealthValueDisplayModeIndex = HealthValueDisplayModeIndex,
            CombatStateDisplayModeIndex = CombatStateDisplayModeIndex,
            TargetedPlayerDisplayModeIndex = TargetedPlayerDisplayModeIndex,
            HateRankDisplayModeIndex = HateRankDisplayModeIndex,
            SameBuffGroupingIndex = SameBuffGroupingIndex,
            CasterNameDisplayModeIndex = CasterNameDisplayModeIndex,
            PriorityBuffs = PriorityBuffs?.Select(entry => entry.Clone()).ToList(),
            HiddenBuffIds = HiddenBuffIds is null ? [] : [.. HiddenBuffIds],
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.EntityInfo)
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.EntityInfo)
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>チャットの設定。発言者の名前の色(テキストカラー)。形は蘇生リストのテキストカラーと同じ。</summary>
public sealed class ChatWidgetSettingsConfig
{
    /// <summary>文字の色。形はクラスカラーと同じで、鍵は <see cref="WidgetConfigDefaults.ChatTextColorKeys"/>。</summary>
    public Dictionary<string, int> TextColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultChatTextColorIndexes();

    public Dictionary<string, List<string>> TextColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultChatTextColorPalettes();

    public ChatWidgetSettingsConfig Clone()
    {
        return new ChatWidgetSettingsConfig
        {
            TextColorIndexes = TextColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultChatTextColorIndexes()
                : new Dictionary<string, int>(TextColorIndexes, StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = TextColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultChatTextColorPalettes()
                : TextColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class PlayerStatusWidgetSettingsConfig
{
    /// <remarks><c>null</c> は「設定されていない」。既定は <see cref="WidgetConfigDefaults.DefaultHideInactiveStatusEffects"/>。</remarks>
    public bool? HideInactiveStatusEffects { get; set; }

    /// <summary>割合と実数値を1行にまとめた行で、括弧の実数値を出すか(<c>12%(3456)</c> の括弧の部分)。</summary>
    /// <remarks><c>null</c> は「設定されていない」。既定は <see cref="WidgetConfigDefaults.DefaultPlayerStatusShowActualValues"/>。</remarks>
    public bool? ShowActualValues { get; set; }

    /// <summary>行ごとに出すか。鍵は属性の番号。</summary>
    public Dictionary<string, bool> RowVisibility { get; set; } =
        WidgetConfigDefaults.CreateDefaultPlayerStatusRowVisibility();

    /// <summary>
    /// 行の文字とアイコンの色、値の増減の色。形はクラスカラーと同じで、鍵は増減の2つの名前と属性の番号
    /// (<see cref="WidgetConfigDefaults.PlayerStatusTextColorKeys"/>)。
    /// </summary>
    public Dictionary<string, int> TextColorIndexes { get; set; } =
        WidgetConfigDefaults.CreateDefaultPlayerStatusTextColorIndexes();

    public Dictionary<string, List<string>> TextColorPalettes { get; set; } =
        WidgetConfigDefaults.CreateDefaultPlayerStatusTextColorPalettes();

    public PlayerStatusWidgetSettingsConfig Clone()
    {
        return new PlayerStatusWidgetSettingsConfig
        {
            HideInactiveStatusEffects = HideInactiveStatusEffects,
            ShowActualValues = ShowActualValues,
            RowVisibility = RowVisibility is null
                ? WidgetConfigDefaults.CreateDefaultPlayerStatusRowVisibility()
                : new Dictionary<string, bool>(RowVisibility, StringComparer.OrdinalIgnoreCase),
            TextColorIndexes = TextColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultPlayerStatusTextColorIndexes()
                : new Dictionary<string, int>(TextColorIndexes, StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = TextColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultPlayerStatusTextColorPalettes()
                : TextColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// スキル詳細の属性カラー。作りはメーターのクラスカラーと同じで、行が9属性になる。
///
/// <para>
/// ウィジェットが実際に使う色は<b>その行に出た属性の割合で混ぜたもの</b>で、そこへ
/// 不透明度が掛かる。<b>設定画面の色見本には掛けない</b>(クラスカラーと同じ)。
/// </para>
/// </summary>
public sealed class ElementColorWidgetSettingsConfig
{
    public int ColorOpacity { get; set; } = WidgetConfigDefaults.MaxClassColorOpacity;

    public Dictionary<string, int> ColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultElementColorIndexes();

    public Dictionary<string, List<string>> ColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultElementColorPalettes();

    public ElementColorWidgetSettingsConfig Clone()
    {
        return new ElementColorWidgetSettingsConfig
        {
            ColorOpacity = ColorOpacity,
            ColorIndexes = ColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultElementColorIndexes()
                : new Dictionary<string, int>(ColorIndexes, StringComparer.OrdinalIgnoreCase),
            ColorPalettes = ColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultElementColorPalettes()
                : ColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

public static class WidgetConfigDefaults
{
    public const int MaxPaletteColorCount = 5;
    public const int MinColorIndex = 0;
    public const int MinWindowOpacity = 0;
    public const int MaxWindowOpacity = 100;
    public const int MinClassColorIndex = 0;
    public const int MinClassColorOpacity = 0;
    public const int MaxClassColorOpacity = 100;
    public const int DefaultMetricGraphAggregationIntervalSeconds = 2;
    public const int DefaultMetricGraphVisibleSeconds = 12;
    public const int MinMetricGraphVisibleSeconds = 4;
    public const int MaxMetricGraphVisibleSeconds = 120;

    /// <summary>推移グラフの窓の上でホイールを1回回したときに横軸の長さを変える秒数。</summary>
    public const int MetricGraphVisibleSecondsWheelStep = 2;
    public const int DefaultHealthValueDisplayModeIndex = 0;
    public const int SeparateShieldHealthValueDisplayModeIndex = 1;
    public const int DefaultPartyDisplayModeIndex = 0;
    public const int MaxPartyDisplayModeIndex = 3;
    public const int MinEntityDisplayModeIndex = (int)EntityDisplayMode.All;
    public const int MaxEntityDisplayModeIndex = (int)EntityDisplayMode.HideObjects;
    public const int DefaultEntityDisplayModeIndex = (int)EntityDisplayMode.HideObjects;
    public const int DefaultSelfDisplayModeIndex = 0;
    public const int MaxSelfDisplayModeIndex = 1;

    /// <summary>一覧の並び替え: 発見順(群の中は初めて現れた順のまま)。</summary>
    public const int FirstSeenListSortModeIndex = 0;

    /// <summary>一覧の並び替え: 名前順(群の中を画面に出している名前で並べる)。</summary>
    public const int NameListSortModeIndex = 1;
    public const int DefaultTimelineEnemyHealDisplayModeIndex = 0;
    public const int HiddenTimelineEnemyHealDisplayModeIndex = 1;
    public const string DefaultEntityInfoFormatString = "Lv.{Level} {EntityName}";
    public const string DefaultMeterPlayerInfoFormatString = "{PlayerName}[{Psych}] - {Spec} ({PowerLevel}-{SeasonStrength})";
    public const string DefaultSkillInfoFormatString = "{SkillName} - {Type} ({Hits}hits-CRT{CritRate})";
    public const string DefaultEquipmentInfoFormatString = "{EquipName}({MainStat})";

    /// <summary>ステータス詳細で、値が 0 の属性効果の行を隠すか。</summary>
    public const bool DefaultHideInactiveStatusEffects = true;

    /// <summary>ステータス詳細で、割合と実数値を1行にまとめた行の括弧の実数値を出すか。</summary>
    public const bool DefaultPlayerStatusShowActualValues = true;
    public const string DefaultPlayerListPlayerInfoFormatString = "{PlayerName}({PowerLevel}-{SeasonStrength})";
    public const string DefaultReviveListPlayerInfoFormatString = "{PlayerName} - {Spec} ({PowerLevel}-{SeasonStrength})";
    public const string DefaultDamageTakenLogPlayerInfoFormatString = "{PlayerName} - {Spec} ({PowerLevel}-{SeasonStrength})";
    public const string DefaultBuffInfoFormatString = "{BuffName}({TargetName})";

    public const int MinBuffCardScale = 100;
    public const int MaxBuffCardScale = 1000;
    public const int DefaultBuffCardScale = 200;
    public const int BuffCardScaleStep = 25;

    private const double PlayerListInitialWindowWidth = 457d;
    public const double PlayerListInfoActionsWidth = 180d;
    private const double PlayerListInitialWindowHeight = 460d;
    private const double MeterInitialWindowWidth = 440d;
    private const double MeterInitialWindowHeight = 460d;
    private const double HateMeterInitialWindowWidth = 440d;
    private const double HateMeterInitialWindowHeight = 460d;
    private const double BreakMeterInitialWindowWidth = 440d;
    private const double BreakMeterInitialWindowHeight = 460d;
    private const double TimelineInitialWindowWidth = 1100d;
    private const double TimelineInitialWindowHeight = 720d;
    private const double PlayerInfoInitialWindowWidth = 406d;
    private const double PlayerInfoInitialWindowHeight = 265d;
    private const double PlayerStatusInitialWindowWidth = 280d;
    private const double PlayerStatusInitialWindowHeight = 400d;
    private const double PlayerEquipmentInitialWindowWidth = 360d;
    private const double PlayerEquipmentInitialWindowHeight = 381d;
    private const double PlayerBuffListInitialWindowWidth = 360d;
    private const double PlayerBuffListInitialWindowHeight = 400d;
    private const double PlayerTalentInitialWindowWidth = 360d;
    private const double PlayerTalentInitialWindowHeight = 440d;
    private const double PlayerSeasonTalentInitialWindowWidth = 360d;
    private const double PlayerSeasonTalentInitialWindowHeight = 400d;
    private const double PlayerModuleInitialWindowWidth = 280d;
    private const double PlayerModuleInitialWindowHeight = 240d;
    private const double BuffDebuffCardInitialWindowWidth = 260d;
    private const double BuffDebuffCardInitialWindowHeight = 260d;
    private const double BuffDebuffGroupInitialWindowWidth = 280d;
    private const double ReviveListInitialWindowWidth = 360d;
    private const double ReviveListInitialWindowHeight = 400d;
    private const double DamageTakenLogInitialWindowWidth = 440d;
    private const double DamageTakenLogInitialWindowHeight = 460d;
    private const double EntityInfoInitialWindowWidth = 360d;
    private const double EntityInfoInitialWindowHeight = 240d;
    private const double ChatInitialWindowWidth = 360d;
    private const double ChatInitialWindowHeight = 400d;
    private const double SkillDetailInitialWindowWidth = 520d;
    private const double SkillDetailInitialWindowHeight = 460d;
    private const double HateSkillDetailInitialWindowWidth = 520d;
    private const double HateSkillDetailInitialWindowHeight = 460d;
    private const double BreakSkillDetailInitialWindowWidth = 520d;
    private const double BreakSkillDetailInitialWindowHeight = 460d;
    private const double MetricSummaryInitialWindowWidth = 280d;
    private const double MetricSummaryInitialWindowHeight = 327d;
    private const double DamageTakenSummaryInitialWindowWidth = 280d;
    private const double DamageTakenSummaryInitialWindowHeight = 327d;
    private const double MetricGraphInitialWindowWidth = 440d;
    private const double MetricGraphInitialWindowHeight = 260d;
    private const double HateGraphInitialWindowWidth = 440d;
    private const double HateGraphInitialWindowHeight = 260d;
    private const double BreakGraphInitialWindowWidth = 440d;
    private const double BreakGraphInitialWindowHeight = 260d;

    /// <summary>
    /// バフ・デバフグループの既定の高さ。既定でオンの行(<see cref="DefaultBuffDebuffGroupEntries"/>)がちょうど入る高さで、
    /// 窓のヘッダー30 + 一覧の上下の余白10 + 行の高さ31 × オンの行の数。
    /// 既定の行の一覧はこれより後ろで初期化されるので、値を控える欄にせず読むたびに数える。
    /// </summary>
    private static double BuffDebuffGroupInitialWindowHeight =>
        30d + 10d + 31d * DefaultBuffDebuffGroupEntries.Count(entry => entry.IsEnabled);

    public static IReadOnlyList<int> MetricGraphAggregationIntervals { get; } = [5, 3, 2, 1];

    /// <summary>推移グラフの横軸の長さ(秒)を範囲に収める。軸の数字の間隔は <c>MetricGraphChart</c> が長さから決める。</summary>
    public static int ClampMetricGraphVisibleSeconds(int seconds)
    {
        return Math.Clamp(seconds, MinMetricGraphVisibleSeconds, MaxMetricGraphVisibleSeconds);
    }

    /// <summary>
    /// 設定に並べるロールスキル。全20種。
    ///
    /// <para>
    /// 3021〜3028 は全職務共通で、レベルを4段階持つ(<c>SkillFightLevelTable</c> で確認)。
    /// 残り12件は職務専用で、ゲームのスキル表で <c>SlotPositionId</c> に 21〜24 を持つもの。
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> OtherRoleSkillIds { get; } =
    [
        3021, 3022, 3023, 3024, 3025, 3026, 3027, 3028,
        3611, 3612, 3613, 3614,
        3011, 3012, 3013, 3014,
        3311, 3312, 3313, 3314
    ];

    /// <summary>
    /// 既定でオンにするロールスキル。
    ///
    /// <para>
    /// 3021 Thunderfall Grasp / 3024 Infernal Thrust / 3027 Blessing of Life /
    /// 3312 Renewal Prayer の4件。
    /// </para>
    /// </summary>
    private static readonly int[] DefaultVisibleOtherRoleSkillIds = [3021, 3024, 3027, 3312];

    /// <summary>既定でオンかどうか。</summary>
    public static bool IsOtherRoleSkillVisibleByDefault(int skillId)
    {
        return Array.IndexOf(DefaultVisibleOtherRoleSkillIds, skillId) >= 0;
    }

    /// <summary>既定は上の4件だけオン。残りはオフ。</summary>
    public static Dictionary<string, bool> CreateDefaultOtherRoleSkillVisibility()
    {
        var result = new Dictionary<string, bool>(
            OtherRoleSkillIds.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var skillId in OtherRoleSkillIds)
        {
            result[skillId.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                IsOtherRoleSkillVisibleByDefault(skillId);
        }

        return result;
    }

    private static readonly string[] DefaultWindowColorHexes =
    [
        "#1F1F1F",
        "#FCFCFC"
    ];

    public static readonly string[] ClassColorKeys =
    [
        "ShieldKnight",
        "HeavyGuardian",
        "VerdantOracle",
        "SoulMusician",
        "FlameBerserker",
        "Stormblade",
        "FrostMage",
        "WindKnight",
        "Marksman",
        "Transformation",
        "Unknown"
    ];

    /// <summary>プレイヤーリストのアイコンカラーの鍵。クラスの鍵の後ろに、潜在心相晶の絵の10件と不明。</summary>
    public static readonly string[] PlayerListClassColorKeys =
        [.. ClassColorKeys, .. StarResonanceDps.App.Services.SeasonTalentIcons.ColorKeys];

    /// <summary>
    /// プレイヤー情報のアイコンカラーの鍵。クラス(不明を除く)、潜在心相晶の絵の10件と無効、シーズン、最後に不明。
    /// 不明はバッジ3つ(シーズン・クラス・潜在心相晶)のはてなを1色で塗る。絵が同じなので分けない。
    /// </summary>
    public static readonly string[] PlayerInfoClassColorKeys =
    [
        .. ClassColorKeys.Where(key => key != "Unknown"),
        .. StarResonanceDps.App.Services.SeasonTalentIcons.TypeColorKeys,
        StarResonanceDps.App.Services.SeasonTalentIcons.InactiveColorKey,
        .. StarResonanceDps.App.Services.SeasonIcons.ColorKeys,
        "Unknown"
    ];

    /// <summary>
    /// エンティティリストのアイコンカラーの鍵。敵の種類、ヘイトの枠のプレイヤーのクラス、最後に不明。
    /// 不明は敵の不明とプレイヤーの不明の両方を1色で塗る(絵が同じなので分けない)。
    /// </summary>
    public static readonly string[] EntityClassColorKeys =
    [
        "Monster",
        "Elite",
        "Boss",
        .. ClassColorKeys.Where(key => key != "Unknown"),
        "Unknown"
    ];

    /// <summary>ゲージの色の鍵。左端と右端の2つで、間はグラデーションになる。バフ・デバフ一覧・バフ・デバフグループ・蘇生リストで共通。</summary>
    public static readonly string[] BuffGaugeColorKeys =
    [
        "GaugeStart",
        "GaugeEnd"
    ];

    public const int MinBuffGaugeLengthIndex = 0;

    /// <summary>
    /// ゲージが満タンになる残り時間。添字は 0=自動 / 1=10秒 / 2=20秒 / 3=30秒。バフ・デバフ一覧・バフ・デバフグループ・蘇生リストで共通。
    /// 自動は null で、行ごとの長さ(<c>PlayerBuffSnapshot.GaugeLengthSeconds</c>、実体の効果時間の総量)を使う。
    /// </summary>
    public static readonly int?[] BuffGaugeLengthSeconds = [null, 10, 20, 30];

    /// <summary>バフ一覧の既定。短いバフを見るので10秒。</summary>
    public const int DefaultBuffListGaugeLengthIndex = 1;

    /// <summary>デバフ一覧の既定。長いデバフを見るので30秒。</summary>
    public const int DefaultDebuffListGaugeLengthIndex = 3;

    /// <summary>バフ・デバフグループの既定。行ごとに実体の効果時間の総量(自動)。</summary>
    public const int DefaultBuffDebuffGroupGaugeLengthIndex = 0;

    /// <summary>蘇生リストの既定。蘇生不可デバフの効果時間の総量(自動)。</summary>
    public const int DefaultReviveListGaugeLengthIndex = 0;

    /// <summary>
    /// バフ・デバフグループの既定の行(この並び)。既定の行はオフにしても消えず、初期化でこの並びと既定のオン・オフに戻る。
    /// </summary>
    public static IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> DefaultBuffDebuffGroupEntries { get; } =
    [
        (BuffEntryKey.ForGroup(BuffGroup.Cuisine), true),
        (BuffEntryKey.ForGroup(BuffGroup.Potion), true),
        (BuffEntryKey.ForBuff(2110056), true),  // 時間凝固(ティナの奥義のデバフ)
        (BuffEntryKey.ForBuff(2110049), true),  // 機械の故障(ガトグリフの奥義のデバフ)
        (BuffEntryKey.ForBuff(2110050), true),  // 属性停滞(バジリスクの奥義のデバフ)
        (BuffEntryKey.ForBuff(2110055), true),  // 疲労・烈火の焼身(タータの奥義のデバフ)
        (BuffEntryKey.ForBuff(2110057), false)  // 虚弱・祈り禁止(アルーナの奥義のデバフ)
    ];

    /// <summary>
    /// バフ・デバフカードのショートカットの既定の行(この並び)。中身もオン・オフもバフ・デバフグループの既定の行と同じ。
    /// 既定の行はオフにしても消えず、初期化でこの並びと既定のオン・オフに戻る。
    /// </summary>
    public static IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> DefaultBuffCardShortcutEntries { get; } =
    [
        .. DefaultBuffDebuffGroupEntries
    ];

    /// <summary>
    /// バフ・デバフカードの通知対象の既定の行。中身もオン・オフもバフ・デバフグループの既定の行と同じ。
    /// 既定の行はオフにしても消えず、初期化で既定のオン・オフに戻る。
    /// </summary>
    public static IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> DefaultBuffCardNotificationTargetEntries { get; } =
    [
        .. DefaultBuffDebuffGroupEntries
    ];

    /// <summary>
    /// バフ・デバフグループの通知対象の既定の行。中身もオン・オフもバフ・デバフグループの既定の行と同じ。
    /// 既定の行はオフにしても消えず、初期化で既定のオン・オフに戻る。
    /// </summary>
    public static IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> DefaultBuffDebuffGroupNotificationTargetEntries { get; } =
    [
        .. DefaultBuffDebuffGroupEntries
    ];

    /// <summary>
    /// バフ一覧の優先表示リストの既定の行(この並び)。既定の行はオフにしても消えず、初期化でこの並びと既定のオン・オフに戻る。
    /// </summary>
    public static IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> DefaultBuffListPriorityBuffEntries { get; } =
    [
        (BuffEntryKey.ForBuff(2110034), false),  // リキャスト短縮(ティナの奥義のバフ)
        (BuffEntryKey.ForBuff(2110140), false),  // 機械の力(ガトグリフの奥義のバフ)
        (BuffEntryKey.ForBuff(2110125), false)   // 高原血統(バジリスクの奥義のバフ)
    ];

    /// <summary>
    /// デバフ一覧の優先表示リストの既定の行(この並び)。既定の行はオフにしても消えず、初期化でこの並びと既定のオン・オフに戻る。
    /// </summary>
    public static IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> DefaultDebuffListPriorityBuffEntries { get; } =
    [
        (BuffEntryKey.ForBuff(2110056), false),  // 時間凝固(ティナの奥義のデバフ)
        (BuffEntryKey.ForBuff(2110049), false),  // 機械の故障(ガトグリフの奥義のデバフ)
        (BuffEntryKey.ForBuff(2110050), false),  // 属性停滞(バジリスクの奥義のデバフ)
        (BuffEntryKey.ForBuff(510571), true),    // 重傷
        (BuffEntryKey.ForBuff(2110100), true),   // 回復禁止(鉄牙のデバフ、15秒)
        (BuffEntryKey.ForBuff(2110305), true)    // 回復禁止(ロールスキルの鉄牙のデバフ、10秒)
    ];

    /// <summary>
    /// エンティティ情報の優先表示リストの既定の行(この並び)。既定の行はオフにしても消えず、初期化でこの並びと既定のオン・オフに戻る。
    /// </summary>
    public static IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> DefaultEntityInfoPriorityBuffEntries { get; } =
    [
        (BuffEntryKey.ForBuff(510571), true),    // 重傷
        (BuffEntryKey.ForBuff(2110100), true),   // 回復禁止(鉄牙のデバフ、15秒)
        (BuffEntryKey.ForBuff(2110305), true)    // 回復禁止(ロールスキルの鉄牙のデバフ、10秒)
    ];

    /// <summary>バフ・デバフ一覧の優先表示リストの既定の行。バフ一覧とデバフ一覧で別。</summary>
    public static IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> GetBuffListPriorityDefaults(WidgetKind kind)
    {
        return kind == WidgetKind.DebuffList
            ? DefaultDebuffListPriorityBuffEntries
            : DefaultBuffListPriorityBuffEntries;
    }

    public static bool IsDefaultBuffEntry(IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> defaults, BuffEntryKey key)
    {
        return defaults.Any(entry => entry.Key == key);
    }

    /// <summary>
    /// 行(バフ・デバフグループの行、バフ・デバフカードのショートカット、カードとグループの通知対象、バフ・デバフ一覧とエンティティ情報の優先表示リスト)をオン/オフにする
    /// (右クリックの「グループに追加」「ショートカットに追加」「通知対象にする」「優先表示」と「グループから削除」「通知対象にしない」「優先表示をやめる」)。
    /// オンにするときは、行があればオンに戻し、無ければ末尾に足す。オフにするときは、既定の行(<paramref name="defaults"/>)はオフにして残し、足した行は消す
    /// (足した行をオフのまま保存したときと同じ)。ウィジェットの今の値・保存する値・設定の窓の保存済みの値がこの決まりを使う。
    /// </summary>
    public static void SetBuffEntryEnabled(
        List<BuffDebuffGroupEntryConfig> entries,
        BuffEntryKey key,
        bool isEnabled,
        IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> defaults)
    {
        var index = entries.FindIndex(entry => entry.GetKey() == key);
        if (isEnabled)
        {
            if (index >= 0)
            {
                entries[index].IsEnabled = true;
                return;
            }

            entries.Add(new BuffDebuffGroupEntryConfig
            {
                BuffId = key.BuffId,
                Group = key.Group,
                IsEnabled = true
            });
            return;
        }

        if (index < 0)
        {
            return;
        }

        if (IsDefaultBuffEntry(defaults, key))
        {
            entries[index].IsEnabled = false;
        }
        else
        {
            entries.RemoveAt(index);
        }
    }

    /// <summary>
    /// バフ・デバフグループの行を、鍵の並び <paramref name="order"/> のとおりに並べ直す(ウィジェットでの並べ替え)。
    /// 並びに無い行は後ろへ、元の順のまま置く。
    /// </summary>
    public static List<BuffDebuffGroupEntryConfig> OrderBuffDebuffGroupEntries(
        IReadOnlyList<BuffDebuffGroupEntryConfig> entries,
        IReadOnlyList<BuffEntryKey> order)
    {
        var position = new Dictionary<BuffEntryKey, int>(order.Count);
        for (var index = 0; index < order.Count; index++)
        {
            position.TryAdd(order[index], index);
        }

        return
        [
            .. entries
                .Select((entry, index) => (Entry: entry, Index: index))
                .OrderBy(pair => position.TryGetValue(pair.Entry.GetKey(), out var at) ? at : order.Count + pair.Index)
                .Select(pair => pair.Entry)
        ];
    }

    /// <summary>バフ・デバフ一覧の設定「同種バフの表示」の選択肢の数(まとめない / プレイヤーのみ / エンティティのみ / すべて)。</summary>
    public const int SameBuffGroupingCount = 4;

    /// <summary>「同種バフの表示」の既定(プレイヤーのみまとめる)。</summary>
    public const int DefaultSameBuffGroupingIndex = 1;

    /// <summary>バフ・デバフ一覧の設定「付与者プレイヤー名の表示」の添字。</summary>
    public const int CasterNameDisplayShowIndex = 0;
    public const int CasterNameDisplayHideIndex = 1;
    public const int CasterNameDisplayAppliedByOthersIndex = 2;

    /// <summary>「付与者プレイヤー名の表示」の既定(他者付与のみ)。</summary>
    public const int DefaultCasterNameDisplayModeIndex = CasterNameDisplayAppliedByOthersIndex;

    /// <summary>プレイヤーリストとエンティティリストの設定「戦闘状態」の添字。自分のみ表示はプレイヤーリストだけ。</summary>
    public const int CombatStateDisplayShowIndex = 0;
    public const int CombatStateDisplayHideIndex = 1;
    public const int CombatStateDisplaySelfOnlyIndex = 2;

    /// <summary>「戦闘状態」の既定(表示)。</summary>
    public const int DefaultCombatStateDisplayModeIndex = CombatStateDisplayShowIndex;

    /// <summary>エンティティリストの設定「狙われるプレイヤー」の添字。</summary>
    public const int TargetedPlayerDisplayShowIndex = 0;
    public const int TargetedPlayerDisplayHideIndex = 1;

    /// <summary>「狙われるプレイヤー」の既定(表示)。</summary>
    public const int DefaultTargetedPlayerDisplayModeIndex = TargetedPlayerDisplayShowIndex;

    /// <summary>エンティティリストの設定「ヘイト値」の選択肢(並べる人数)。並びは設定の選択リストと同じ。</summary>
    public static IReadOnlyList<int> HateRankDisplayCounts { get; } = [5, 4, 3, 2, 1];

    /// <summary>「ヘイト値」の既定(上位5人)。</summary>
    public const int DefaultHateRankDisplayModeIndex = 0;

    /// <summary>「ヘイト値」の選択の添字から並べる人数。範囲の外は端の値。</summary>
    public static int GetHateRankDisplayCount(int index)
    {
        return HateRankDisplayCounts[Math.Clamp(index, 0, HateRankDisplayCounts.Count - 1)];
    }

    /// <summary>エンティティ情報の設定「ヘイト値」の選択肢(並べる人数)。先頭は全員。並びは設定の選択リストと同じ。</summary>
    public static IReadOnlyList<int> EntityInfoHateRankDisplayCounts { get; } = [int.MaxValue, 5, 4, 3, 2, 1];

    /// <summary>エンティティ情報の「ヘイト値」の既定(全員を表示)。</summary>
    public const int DefaultEntityInfoHateRankDisplayModeIndex = 0;

    /// <summary>エンティティ情報の「ヘイト値」の選択の添字から並べる人数。範囲の外は端の値。</summary>
    public static int GetEntityInfoHateRankDisplayCount(int index)
    {
        return EntityInfoHateRankDisplayCounts[Math.Clamp(index, 0, EntityInfoHateRankDisplayCounts.Count - 1)];
    }

    /// <summary>エンティティ情報の設定「同種バフの表示」の添字。</summary>
    public const int EntityInfoSameBuffUngroupedIndex = 0;
    public const int EntityInfoSameBuffGroupedIndex = 1;

    /// <summary>エンティティ情報の「同種バフの表示」の既定(まとめない)。</summary>
    public const int DefaultEntityInfoSameBuffGroupingIndex = EntityInfoSameBuffUngroupedIndex;

    /// <summary>バフ・デバフ一覧の行のバフ名の書式の既定。付与者が出ないときは空になった括弧ごと消える。</summary>
    public const string DefaultBuffListBuffInfoFormatString = "{BuffName}({CasterName})";

    /// <summary>
    /// 設定「同種バフの表示」で、その対象の一覧を同じ番号のバフ1行にまとめるか。
    /// </summary>
    /// <param name="isEntityTarget">対象がエンティティ(エンティティリストに載る実体)か。偽ならプレイヤー。</param>
    public static bool GroupsSameBuffs(BuffListWidgetSettingsConfig settings, bool isEntityTarget)
    {
        return settings.SameBuffGroupingIndex switch
        {
            1 => !isEntityTarget,
            2 => isEntityTarget,
            3 => true,
            _ => false,
        };
    }

    /// <summary>
    /// ダメージの属性の鍵。<c>EDamageProperty</c> の並び(属性ID 0〜8)。
    /// 表示名は <c>DamageProperty_*</c>、アイコンは <c>Icon.DamageProperty.*</c> で引く。
    /// </summary>
    public static readonly string[] DamagePropertyKeys =
    [
        "General",
        "Fire",
        "Water",
        "Electricity",
        "Wood",
        "Wind",
        "Rock",
        "Light",
        "Dark"
    ];

    /// <summary>
    /// ダメージの種類(タイプ)の鍵。<c>EDamageMode</c> の名前で、アイコンカラーの物理・魔法・無分類の行の並び。
    /// 表示名は <c>Metric_DamageMode_*</c>、アイコンは <c>Icon.DamageMode.*</c>(無分類は無い)。
    /// </summary>
    public static readonly string[] DamageModeKeys =
    [
        "DamagePhysical",
        "DamageMagical",
        "DamageNormal"
    ];

    /// <summary>タイムラインのテキストカラーの鍵。</summary>
    public static readonly string[] TimelineTextColorKeys =
    [
        "EntityName",
        "SkillName",
        "PlayerName",
        "Effect",
        "HpValue"
    ];

    private static readonly Dictionary<string, string[]> PlayerListDefaultClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ShieldKnight"] = ["#68A6CD", "#0F68B3"],
        ["HeavyGuardian"] = ["#68A6CD", "#08A0DC"],
        ["VerdantOracle"] = ["#83C49A", "#32BF0F"],
        ["SoulMusician"] = ["#83C49A", "#1F9F0E"],
        ["FlameBerserker"] = ["#DB8787", "#B33000"],
        ["Stormblade"] = ["#DB8787", "#6B39DE"],
        ["FrostMage"] = ["#DB8787", "#5C82E1"],
        ["WindKnight"] = ["#DB8787", "#11B5B2"],
        ["Marksman"] = ["#DB8787", "#D4D116"],
        ["Transformation"] = ["#FFFFFF", "#B06BE8"],
        ["Unknown"] = ["#FFFFFF", "#A8A8A8"]
    };

    /// <summary>プレイヤーリストの潜在心相晶の既定色。鍵は SeasonTalent.(絵の名前)。</summary>
    private static readonly Dictionary<string, string[]> PlayerListDefaultSeasonTalentColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SeasonTalent.s2talent01_01"] = ["#FFFFFF", "#F5EABF"],
        ["SeasonTalent.s2talent02_01"] = ["#FFFFFF", "#F6D3A8"],
        ["SeasonTalent.s2talent03_01"] = ["#FFFFFF", "#F5CECE"],
        ["SeasonTalent.s2talent04_01"] = ["#FFFFFF", "#F5CEEA"],
        ["SeasonTalent.s2talent05_01"] = ["#FFFFFF", "#C2E9F5"],
        ["SeasonTalent.s2talent06_01"] = ["#FFFFFF", "#DFD2F5"],
        ["SeasonTalent.s2talent07_01"] = ["#FFFFFF", "#C8F5EB"],
        ["SeasonTalent.s2talent08_01"] = ["#FFFFFF", "#CCF5DD"],
        ["SeasonTalent.s2talent051_01"] = ["#FFFFFF", "#C2F5F1"],
        ["SeasonTalent.s2talent054_01"] = ["#FFFFFF", "#F5D8F2"],
        ["SeasonTalent.Unknown"] = ["#FFFFFF", "#EEE9F2"]
    };

    /// <summary>
    /// プレイヤー情報だけにある行の既定色。心相晶の無効は不明と同じ色、シーズンは白と灰色の2枠で、2枠目から始める。
    /// ほかの行はプレイヤーリストと同じ既定色。
    /// </summary>
    private static readonly Dictionary<string, string[]> PlayerInfoDefaultExtraColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SeasonTalent.Inactive"] = ["#FFFFFF", "#EEE9F2"],
        ["Season.1"] = ["#FFFFFF", "#8C52F7"],
        ["Season.2"] = ["#FFFFFF", "#EF66B1"],
        ["Season.3"] = ["#FFFFFF", "#EFAE65"],
        ["Season.4"] = ["#FFFFFF", "#7F8ECF"]
    };

    /// <summary>
    /// ゲージの既定色。左から右へこの2色でグラデーションになる。
    ///
    /// <para>
    /// 見本は2枠で、<b>1枠目がバフの青緑、2枠目がデバフの紫</b>。2枠目は1枠目の R と G を入れ替えたもので、
    /// 彩度と明度はそのままに色相だけ反対側へ回る。どちらを選んだ状態で始めるかは
    /// <see cref="GetDefaultGaugeColorIndex"/> がバフかデバフかで決める。
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string[]> BuffGaugeDefaultColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GaugeStart"] = ["#5688A4", "#8856A4"],
        ["GaugeEnd"] = ["#56C8C1", "#C856C1"]
    };

    /// <summary>
    /// タイムラインのテキストカラーの既定色。白と差し色の2枠。
    /// </summary>
    private static readonly Dictionary<string, string[]> TimelineDefaultTextColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EntityName"] = ["#FFFFFF", "#FFD98A"],
        ["SkillName"] = ["#FFFFFF", "#D9866C"],
        ["PlayerName"] = ["#FFFFFF", "#70D0E8"],
        ["Effect"] = ["#FFFFFF", "#C9A8FF"],
        ["HpValue"] = ["#FFFFFF", "#81E5C6"]
    };

    /// <summary>
    /// 属性カラーの既定色(2枠)。属性カラーの節を持つウィジェット(<see cref="SupportsElementColorSettings"/>)で共通。
    /// </summary>
    private static readonly Dictionary<string, string[]> ElementDefaultColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["General"] = ["#A4C2D0", "#A8A8A8"],
        ["Fire"] = ["#F4501B", "#CC6333"],
        ["Water"] = ["#66CDE7", "#4494A9"],
        ["Electricity"] = ["#6B6BEB", "#826DD5"],
        ["Wood"] = ["#94E727", "#519F44"],
        ["Wind"] = ["#70DAB2", "#4A997A"],
        ["Rock"] = ["#997951", "#9C714F"],
        ["Light"] = ["#F9ED73", "#F5CF64"],
        ["Dark"] = ["#B36AF4", "#9D66AA"]
    };

    private static readonly Dictionary<string, string[]> DamageModeDefaultColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DamagePhysical"] = ["#E7BC69", "#927742"],
        ["DamageMagical"] = ["#A369E7", "#674292"],
        ["DamageNormal"] = ["#B8B8B8", "#747474"]
    };

    private static readonly Dictionary<string, string[]> EntityListDefaultClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Monster"] = ["#FFFFFF", "#FFB15C"],
        ["Elite"] = ["#FFFFFF", "#C490FF"],
        ["Boss"] = ["#FFFFFF", "#FF6A6A"],
        ["Unknown"] = ["#FFFFFF", "#A8A8A8"]
    };

    private static readonly Dictionary<string, string[]> MeterDefaultClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ShieldKnight"] = ["#0F68B3", "#08406F"],
        ["HeavyGuardian"] = ["#08A0DC", "#056482"],
        ["VerdantOracle"] = ["#32BF0F", "#1D7410"],
        ["SoulMusician"] = ["#1F9F0E", "#145F0A"],
        ["FlameBerserker"] = ["#B33000", "#6F1F00"],
        ["Stormblade"] = ["#6B39DE", "#3F2485"],
        ["FrostMage"] = ["#5C82E1", "#355094"],
        ["WindKnight"] = ["#11B5B2", "#0A6E6C"],
        ["Marksman"] = ["#D4D116", "#8A8810"],
        ["Transformation"] = ["#B06BE8", "#6E3A9C"],
        ["Unknown"] = ["#A8A8A8", "#707070"]
    };

    private static readonly Dictionary<string, string[]> HpsMeterDefaultClassColorHexes = MeterDefaultClassColorHexes;

    /// <summary>
    /// テキストカラーの見本。<b>一覧の全項目ぶんを1件ずつ並べる</b>(並びは「ステータス」の一覧と同じ)。
    /// </summary>
    private static readonly Dictionary<int, string[]> PlayerStatusDefaultTextColorHexes = new()
    {
        [11440] = ["#FFFFFF", "#C3A7FF"],    // シーズン強度
        [11320] = ["#FFFFFF", "#FFB3B3"],    // 最大HP
        [12790] = ["#FFFFFF", "#FBE8C3"],    // 物理増強
        [12800] = ["#FFFFFF", "#DFC8FA"],    // 魔法増強
        [13000] = ["#FFFFFF", "#D6E8F0"],    // 全属性ボーナス
        [13010] = ["#FFFFFF", "#FFB38A"],    // 火属性ボーナス
        [13020] = ["#FFFFFF", "#B5ECFA"],    // 氷属性ボーナス
        [13030] = ["#FFFFFF", "#C8F49A"],    // 森属性ボーナス
        [13040] = ["#FFFFFF", "#B5B0FF"],    // 雷属性ボーナス
        [13050] = ["#FFFFFF", "#A8F2D7"],    // 風属性ボーナス
        [13060] = ["#FFFFFF", "#F7D98A"],    // 岩属性ボーナス
        [13070] = ["#FFFFFF", "#F7F0C9"],    // 光属性ボーナス
        [13080] = ["#FFFFFF", "#C8B4F2"],    // 闇属性ボーナス
        [13200] = ["#FFFFFF", "#D6E8F0"],    // 全属性軽減
        [13210] = ["#FFFFFF", "#FFB38A"],    // 火属性軽減
        [13220] = ["#FFFFFF", "#B5ECFA"],    // 氷属性軽減
        [13230] = ["#FFFFFF", "#C8F49A"],    // 森属性軽減
        [13240] = ["#FFFFFF", "#B5B0FF"],    // 雷属性軽減
        [13250] = ["#FFFFFF", "#A8F2D7"],    // 風属性軽減
        [13260] = ["#FFFFFF", "#F7D98A"],    // 岩属性軽減
        [13270] = ["#FFFFFF", "#F7F0C9"],    // 光属性軽減
        [13280] = ["#FFFFFF", "#C8B4F2"],    // 闇属性軽減

        [11330] = ["#FFFFFF", "#FBE8C3"],    // 物理攻撃力
        [11340] = ["#FFFFFF", "#DFC8FA"],    // 魔法攻撃力
        [11010] = ["#FFFFFF", "#F6D3A8"],    // 筋力
        [11020] = ["#FFFFFF", "#DCC1FA"],    // 知力
        [11030] = ["#FFFFFF", "#A2E3F5"],    // 敏捷
        [11040] = ["#FFFFFF", "#B3F4C6"],    // 耐久力

        [11110] = ["#FFFFFF", "#FABFCB"],    // 会心
        [12510] = ["#FFFFFF", "#F8B6C7"],    // 会心ダメージ
        [11120] = ["#FFFFFF", "#A2E3F5"],    // ファスト
        [11130] = ["#FFFFFF", "#B3F4C6"],    // 幸運
        [12530] = ["#FFFFFF", "#C1F8D2"],    // 幸運の一撃のダメージ倍率
        [11140] = ["#FFFFFF", "#DFC8FA"],    // 器用さ
        [11150] = ["#FFFFFF", "#FBF2B9"],    // 万能
        [11840] = ["#FFFFFF", "#F7EFBE"],    // 万能増強
        [11850] = ["#FFFFFF", "#F3EDC2"],    // 万能軽減
        [11170] = ["#FFFFFF", "#CDC6FA"],    // レジスト
        [12540] = ["#FFFFFF", "#DCC1FA"],    // レジストダメージ軽減

        [11720] = ["#FFFFFF", "#C2F0F5"],    // 攻撃速度
        [11730] = ["#FFFFFF", "#C2DDF5  "],    // 詠唱速度
        [11960] = ["#FFFFFF", "#A7B6FF"],    // リキャスト加速
        [11830] = ["#FFFFFF", "#FFD77A"],    // ブレイク効率

        [11350] = ["#FFFFFF", "#AEDAFA"],    // 物理防御力
        [11360] = ["#FFFFFF", "#CDC6FA"],    // 魔法防御力
        [11370] = ["#FFFFFF", "#F4E8B9"],    // 物理防御力無視
        [11380] = ["#FFFFFF", "#D9C7FA"],    // 魔法防御力無視
        [12560] = ["#FFFFFF", "#BFE9FB"],    // 物理軽減
        [12580] = ["#FFFFFF", "#DCC1FA"],    // 魔法軽減

        [11410] = ["#FFFFFF", "#FFE0A8"],    // 精錬物攻
        [11430] = ["#FFFFFF", "#D8C0FA"],    // 精錬魔攻
        [11420] = ["#FFFFFF", "#BBDFF5"],    // 精錬防御力

        [12670] = ["#FFFFFF", "#FFB9AD"],    // ダメージボーナス
        [12680] = ["#FFFFFF", "#B8DFF5"],    // ダメージ軽減
        [12590] = ["#FFFFFF", "#FFC7A8"],    // 近距離ダメージボーナス
        [12610] = ["#FFFFFF", "#A9E7F5"],    // 遠距離ダメージボーナス
        [12630] = ["#FFFFFF", "#FFD69A"],    // 対ボスダメージボーナス

        [11460] = ["#FFFFFF", "#C3A7FF"],    // シーズン防御力
        [11470] = ["#FFFFFF", "#D0B6FA"],    // シーズン防御力無視
        [12690] = ["#FFFFFF", "#FFB3D0"],    // シーズンダメージボーナス
        [12700] = ["#FFFFFF", "#C6C4FA"],    // シーズンダメージ軽減

        [11500] = ["#FFFFFF", "#D6E8F0"],    // 全属性攻撃力
        [11510] = ["#FFFFFF", "#FFB38A"],    // 火属性攻撃力
        [11520] = ["#FFFFFF", "#B5ECFA"],    // 氷属性攻撃力
        [11530] = ["#FFFFFF", "#C8F49A"],    // 森属性攻撃力
        [11540] = ["#FFFFFF", "#B5B0FF"],    // 雷属性攻撃力
        [11550] = ["#FFFFFF", "#A8F2D7"],    // 風属性攻撃力
        [11560] = ["#FFFFFF", "#F7D98A"],    // 岩属性攻撃力
        [11570] = ["#FFFFFF", "#F7F0C9"],    // 光属性攻撃力
        [11580] = ["#FFFFFF", "#C8B4F2"],    // 闇属性攻撃力

        [11790] = ["#FFFFFF", "#9EF0C1"],    // 回復力
        [11800] = ["#FFFFFF", "#B8F2D0"],    // 被回復力
        [12740] = ["#FFFFFF", "#C1F8D2"],    // 会心回復
        [12720] = ["#FFFFFF", "#C1F8D2"],    // 幸運の一撃回復の倍率
        [11810] = ["#FFFFFF", "#A9E8F5"],    // バリア強度
        [11820] = ["#FFFFFF", "#C3DFF5"],    // 被バリア強度

        [11880] = ["#FFFFFF", "#F3B8D5"],    // 抑圧ダメージ
        [11890] = ["#FFFFFF", "#CDBEFA"],    // 抑圧ダメージ軽減
        [12730] = ["#FFFFFF", "#FFCAA8"],    // 臣獣ダメージ
        [11990] = ["#FFFFFF", "#A5ECF3"],    // 臣獣の攻撃速度

        [10200] = ["#FFFFFF", "#A2E3F5"],    // 移動速度
        [20020] = ["#FFFFFF", "#F5D6A8"],    // 最大スタミナ
        [20120] = ["#FFFFFF", "#B3F4C6"],    // 戦闘時のスタミナ回復率
    };

    /// <summary>
    /// 装備のテキストカラーの鍵。アイテムの品質の番号(0〜5)で、低い順。
    /// レアリティはアイテム共通なので、装備に無い品質0も持つ。
    /// </summary>
    public static readonly string[] EquipmentTextColorKeys = ["0", "1", "2", "3", "4", "5"];

    /// <summary>
    /// 装備のテキストカラーの見本。1枠目は白で、2枠目にゲームのアイテムの品質の色を置く。
    /// <b>表は鍵の全項目を持つ</b>ので、引けない鍵は表の書き忘れ。埋めずに落とす。
    /// </summary>
    private static readonly Dictionary<string, string[]> EquipmentDefaultTextColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0"] = ["#A4AAB2", "#C4C9CF"],
        ["1"] = ["#89BD92", "#ACD3B3"],
        ["2"] = ["#ADCAFF", "#C2D7FD"],
        ["3"] = ["#AB85F6", "#C4ABF6"],
        ["4"] = ["#EED335", "#F2DE75"],
        ["5"] = ["#EF8E60", "#F2AE8E"]
    };

    public static bool SupportsMeterSettings(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerList
            or WidgetKind.EntityList
            or WidgetKind.DpsMeter
            or WidgetKind.HpsMeter
            or WidgetKind.HateMeter
            or WidgetKind.BreakMeter;
    }

    public static bool UsesMeterClassColorOpacity(WidgetKind kind)
    {
        return kind is WidgetKind.DpsMeter or WidgetKind.HpsMeter or WidgetKind.HateMeter or WidgetKind.BreakMeter;
    }

    public static bool SupportsMetricGraphSettings(WidgetKind kind)
    {
        return kind is WidgetKind.DpsGraph or WidgetKind.HpsGraph or WidgetKind.HateGraph or WidgetKind.BreakGraph;
    }

    public static bool SupportsBuffCardSettings(WidgetKind kind)
    {
        return kind is WidgetKind.BuffDebuffCard;
    }

    public static bool SupportsTimelineSettings(WidgetKind kind)
    {
        return kind is WidgetKind.Timeline;
    }

    /// <summary>プレイヤー情報の設定(アイコンカラー)を持つ種別か。</summary>
    public static bool SupportsPlayerInfoSettings(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerInfo;
    }

    /// <summary>スキル詳細の表示設定(行名の書式)を持つ種別か。</summary>
    public static bool SupportsSkillDetailSettings(WidgetKind kind)
    {
        return kind is WidgetKind.DamageSkillDetail
            or WidgetKind.HealingSkillDetail
            or WidgetKind.HateSkillDetail
            or WidgetKind.BreakSkillDetail;
    }

    /// <summary>装備詳細の表示設定(行の書式)を持つ種別か。</summary>
    public static bool SupportsEquipmentSettings(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerEquipment;
    }

    /// <summary>ステータス詳細の表示設定を持つ種別か。</summary>
    public static bool SupportsPlayerStatusSettings(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerStatus;
    }

    /// <summary>バフ・デバフグループの設定(ゲージ・色・行)を持つ種別か。</summary>
    public static bool SupportsBuffDebuffGroupSettings(WidgetKind kind)
    {
        return kind is WidgetKind.BuffDebuffGroup;
    }

    /// <summary>蘇生リストの設定(プレイヤー名・自分の強調表示・アイコンカラー・テキストカラー)を持つ種別か。</summary>
    public static bool SupportsReviveListSettings(WidgetKind kind)
    {
        return kind is WidgetKind.ReviveList;
    }

    /// <summary>被ダメログの設定(プレイヤー名・スキル名・フィルター設定・自分の強調表示・戦闘不能の原因を表示・アイコンカラー)を持つ種別か。</summary>
    public static bool SupportsDamageTakenLogSettings(WidgetKind kind)
    {
        return kind is WidgetKind.DamageTakenLog;
    }

    /// <summary>エンティティ情報の設定(表示設定・非表示リスト・アイコンカラー)を持つ種別か。</summary>
    public static bool SupportsEntityInfoSettings(WidgetKind kind)
    {
        return kind is WidgetKind.EntityInfo;
    }

    /// <summary>チャットの設定(テキストカラー)を持つ種別か。</summary>
    public static bool SupportsChatSettings(WidgetKind kind)
    {
        return kind is WidgetKind.Chat;
    }

    /// <summary>自分だけを出す、1枚だけ開くプレイヤーの窓の種別か(ステータス詳細・バフ・デバフグループ)。</summary>
    public static bool IsSelfOnlyPlayerWindowKind(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerStatus or WidgetKind.BuffDebuffGroup;
    }

    /// <summary>
    /// 開いていた窓の対象を保存する種別か。プレイヤー用ウィンドウだけが持つ。
    /// 自分だけを出す窓(<see cref="IsSelfOnlyPlayerWindowKind"/>)は常に自分1枚なので対象外。
    /// </summary>
    public static bool SupportsOpenTargets(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerInfo
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

    public static WidgetConfig Create(WidgetKind kind)
    {
        return new WidgetConfig
        {
            IsFavorite = false,
            IsPinned = false,
            IsClickThrough = false,
            State = WidgetState.Stopped,
            Theme = CreateTheme(),
            Window = CreateDefaultWindowConfig(kind),
            PlayerListInfoActionsWidthApplied = kind == WidgetKind.PlayerList,
            TimelineIntegratedLayoutApplied = kind == WidgetKind.Timeline,
            Meter = SupportsMeterSettings(kind) ? CreateMeterSettings(kind) : null,
            MetricGraph = SupportsMetricGraphSettings(kind) ? CreateMetricGraphSettings(kind) : null,
            Timeline = SupportsTimelineSettings(kind) ? CreateTimelineSettings() : null,
            PlayerInfo = SupportsPlayerInfoSettings(kind) ? CreatePlayerInfoSettings() : null,
            BuffList = SupportsBuffListSettings(kind) ? CreateBuffListSettings(kind) : null,
            ElementColor = SupportsElementColorSettings(kind) ? CreateElementColorSettings(kind) : null,
            SkillDetail =SupportsSkillDetailSettings(kind) ? CreateSkillDetailSettings() : null,
            Equipment = SupportsEquipmentSettings(kind) ? CreateEquipmentSettings() : null,
            PlayerStatus = SupportsPlayerStatusSettings(kind) ? CreatePlayerStatusSettings() : null,
            BuffDebuffGroup = SupportsBuffDebuffGroupSettings(kind) ? CreateBuffDebuffGroupSettings() : null,
            ReviveList = SupportsReviveListSettings(kind) ? CreateReviveListSettings() : null,
            DamageTakenLog = SupportsDamageTakenLogSettings(kind) ? CreateDamageTakenLogSettings() : null,
            EntityInfo = SupportsEntityInfoSettings(kind) ? CreateEntityInfoSettings() : null,
            Chat = SupportsChatSettings(kind) ? CreateChatSettings() : null,
            Plugin = kind == WidgetKind.Plugin ? new PluginWidgetSettingsConfig() : null
        };
    }

    /// <summary>ステータス詳細の表示設定の既定。</summary>
    public static PlayerStatusWidgetSettingsConfig CreatePlayerStatusSettings()
    {
        return new PlayerStatusWidgetSettingsConfig
        {
            HideInactiveStatusEffects = DefaultHideInactiveStatusEffects,
            ShowActualValues = DefaultPlayerStatusShowActualValues,
            RowVisibility = CreateDefaultPlayerStatusRowVisibility(),
            TextColorIndexes = CreateDefaultPlayerStatusTextColorIndexes(),
            TextColorPalettes = CreateDefaultPlayerStatusTextColorPalettes()
        };
    }

    /// <summary>ステータス詳細のテキストカラーの鍵。値が差分で増えたときの数値と三角の色。</summary>
    public const string PlayerStatusValueIncreaseTextColorKey = "ValueIncrease";

    /// <summary>ステータス詳細のテキストカラーの鍵。値が差分で減ったときの数値と三角の色。</summary>
    public const string PlayerStatusValueDecreaseTextColorKey = "ValueDecrease";

    /// <summary>
    /// ステータス詳細のテキストカラーの鍵の全部。並びは設定の一覧の順で、先頭が増減の2つ、続いて行の番号
    /// (<see cref="PlayerStatusEntry.SettingRowAttrIds"/>)。既定・正規化・比べ・保存はこの一覧で回す。
    /// </summary>
    public static IReadOnlyList<string> PlayerStatusTextColorKeys { get; } =
    [
        PlayerStatusValueIncreaseTextColorKey,
        PlayerStatusValueDecreaseTextColorKey,
        .. PlayerStatusEntry.SettingRowAttrIds.Select(attrId => attrId.ToString(CultureInfo.InvariantCulture))
    ];

    /// <summary>増減の色の見本。行の色と違い、1枠目も白ではなく色(2枠目が既定で選ばれる淡い色)。</summary>
    private static readonly Dictionary<string, string[]> PlayerStatusValueChangeDefaultTextColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        [PlayerStatusValueIncreaseTextColorKey] = ["#43D978", "#83E4A4"],
        [PlayerStatusValueDecreaseTextColorKey] = ["#FF5A5A", "#FA8E8E"],
    };

    /// <summary>
    /// テキストカラーの見本。鍵は <see cref="PlayerStatusTextColorKeys"/>。行の色は1枠目が白で、2枠目に色を置く(クラスカラーと同じ2色)。
    /// <b>表は一覧の全項目を持つ</b>ので、引けない鍵は表の書き忘れ。埋めずに落とす。
    /// </summary>
    public static List<string> CreateDefaultPlayerStatusTextColors(string key)
    {
        return PlayerStatusValueChangeDefaultTextColorHexes.TryGetValue(key, out var valueChangeHexes)
            ? [.. valueChangeHexes]
            : [.. PlayerStatusDefaultTextColorHexes[int.Parse(key, NumberStyles.None, CultureInfo.InvariantCulture)]];
    }

    /// <summary>テキストカラーで最初に選ばれている枠。</summary>
    public const int DefaultPlayerStatusTextColorIndex = 1;

    public static Dictionary<string, int> CreateDefaultPlayerStatusTextColorIndexes()
    {
        var indexes = new Dictionary<string, int>(
            PlayerStatusTextColorKeys.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var key in PlayerStatusTextColorKeys)
        {
            indexes[key] = DefaultPlayerStatusTextColorIndex;
        }

        return indexes;
    }

    public static Dictionary<string, List<string>> CreateDefaultPlayerStatusTextColorPalettes()
    {
        var palettes = new Dictionary<string, List<string>>(
            PlayerStatusTextColorKeys.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var key in PlayerStatusTextColorKeys)
        {
            palettes[key] = CreateDefaultPlayerStatusTextColors(key);
        }

        return palettes;
    }

    /// <summary>
    /// 行の並びを正規化する。<b>表に無い番号は捨て、足りない番号は既定の位置へ補う</b>ので、
    /// 表が増減しても保存値が取り残されない。
    /// </summary>
    public static List<int> NormalizePlayerStatusRowOrder(IReadOnlyList<int>? order)
    {
        var units = PlayerStatusEntry.OrderUnitAttrIds;
        var known = new HashSet<int>(units);
        var result = new List<int>(units.Count);
        var seen = new HashSet<int>();

        if (order is not null)
        {
            foreach (var unit in order)
            {
                if (known.Contains(unit) && seen.Add(unit))
                {
                    result.Add(unit);
                }
            }
        }

        // 並びに無い単位(表が増えた・組を分けた)は、既定の順で1つ前の単位のすぐ後ろへ入れる。
        for (var index = 0; index < units.Count; index++)
        {
            var unit = units[index];
            if (seen.Add(unit))
            {
                result.Insert(index == 0 ? 0 : result.IndexOf(units[index - 1]) + 1, unit);
            }
        }

        return result;
    }

    /// <summary>行ごとのオン/オフの既定。一覧の全件を埋める。</summary>
    public static Dictionary<string, bool> CreateDefaultPlayerStatusRowVisibility()
    {
        var visibility = new Dictionary<string, bool>(
            PlayerStatusEntry.SettingRowAttrIds.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            visibility[attrId.ToString(CultureInfo.InvariantCulture)] =
                PlayerStatusEntry.IsRowVisibleByDefault(attrId);
        }

        return visibility;
    }

    /// <summary>保存値を正規化して写す。設定が無ければ既定を入れる。</summary>
    public static PlayerStatusWidgetSettingsConfig CloneNormalizedPlayerStatus(
        PlayerStatusWidgetSettingsConfig? playerStatus)
    {
        var normalized = (playerStatus ?? CreatePlayerStatusSettings()).Clone();
        normalized.HideInactiveStatusEffects ??= DefaultHideInactiveStatusEffects;
        normalized.ShowActualValues ??= DefaultPlayerStatusShowActualValues;

        // 一覧に無い鍵は捨て、足りない鍵は既定で埋める。表が変わっても保存値が取り残されない。
        var rowVisibility = new Dictionary<string, bool>(
            PlayerStatusEntry.SettingRowAttrIds.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            var key = attrId.ToString(CultureInfo.InvariantCulture);
            rowVisibility[key] = normalized.RowVisibility is not null
                && normalized.RowVisibility.TryGetValue(key, out var visible)
                    ? visible
                    : PlayerStatusEntry.IsRowVisibleByDefault(attrId);
        }

        normalized.RowVisibility = rowVisibility;

        // 一覧に無い鍵は捨て、足りない鍵は既定で埋める。選択はパレットの範囲に収める。
        var textIndexes = new Dictionary<string, int>(
            PlayerStatusTextColorKeys.Count,
            StringComparer.OrdinalIgnoreCase);
        var textPalettes = new Dictionary<string, List<string>>(
            PlayerStatusTextColorKeys.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var key in PlayerStatusTextColorKeys)
        {
            var palette = normalized.TextColorPalettes is not null
                && normalized.TextColorPalettes.TryGetValue(key, out var saved)
                && saved is { Count: > 0 }
                    ? new List<string>(saved)
                    : CreateDefaultPlayerStatusTextColors(key);

            var index = normalized.TextColorIndexes is not null
                && normalized.TextColorIndexes.TryGetValue(key, out var savedIndex)
                    ? savedIndex
                    : DefaultPlayerStatusTextColorIndex;

            textPalettes[key] = palette;
            textIndexes[key] = Math.Clamp(index, 0, palette.Count - 1);
        }

        normalized.TextColorIndexes = textIndexes;
        normalized.TextColorPalettes = textPalettes;
        return normalized;
    }

    /// <summary>スキル詳細の表示設定の既定。</summary>
    public static SkillDetailWidgetSettingsConfig CreateSkillDetailSettings()
    {
        return new SkillDetailWidgetSettingsConfig
        {
            SkillInfoFormatString = DefaultSkillInfoFormatString
        };
    }

    /// <summary>保存値を正規化して写す。書式が無ければ既定を入れる。</summary>
    public static SkillDetailWidgetSettingsConfig CloneNormalizedSkillDetail(SkillDetailWidgetSettingsConfig? skillDetail)
    {
        var normalized = (skillDetail ?? CreateSkillDetailSettings()).Clone();
        normalized.SkillInfoFormatString ??= DefaultSkillInfoFormatString;
        return normalized;
    }

    /// <summary>装備詳細の表示設定の既定。</summary>
    public static EquipmentWidgetSettingsConfig CreateEquipmentSettings()
    {
        return new EquipmentWidgetSettingsConfig
        {
            InfoFormat = DefaultEquipmentInfoFormatString,
            TextColorIndexes = CreateDefaultEquipmentTextColorIndexes(),
            TextColorPalettes = CreateDefaultEquipmentTextColorPalettes()
        };
    }

    /// <summary>装備のテキストカラーで最初に選ばれている枠(品質の色)。</summary>
    public const int DefaultEquipmentTextColorIndex = 1;

    public static List<string> CreateDefaultEquipmentTextColors(string key)
    {
        return [.. EquipmentDefaultTextColorHexes[key]];
    }

    public static Dictionary<string, int> CreateDefaultEquipmentTextColorIndexes()
    {
        return EquipmentTextColorKeys.ToDictionary(
            key => key,
            _ => DefaultEquipmentTextColorIndex,
            StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultEquipmentTextColorPalettes()
    {
        return EquipmentTextColorKeys.ToDictionary(
            key => key,
            CreateDefaultEquipmentTextColors,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>保存値を正規化して写す。書式が無ければ既定を入れる。</summary>
    public static EquipmentWidgetSettingsConfig CloneNormalizedEquipment(EquipmentWidgetSettingsConfig? equipment)
    {
        var normalized = (equipment ?? CreateEquipmentSettings()).Clone();
        normalized.InfoFormat ??= DefaultEquipmentInfoFormatString;

        // 一覧に無い鍵は捨て、足りない鍵は既定で埋める。選択はパレットの範囲に収める。
        var textIndexes = new Dictionary<string, int>(EquipmentTextColorKeys.Length, StringComparer.OrdinalIgnoreCase);
        var textPalettes = new Dictionary<string, List<string>>(EquipmentTextColorKeys.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var key in EquipmentTextColorKeys)
        {
            var palette = normalized.TextColorPalettes.TryGetValue(key, out var saved)
                && saved is { Count: > 0 }
                    ? new List<string>(saved)
                    : CreateDefaultEquipmentTextColors(key);

            var index = normalized.TextColorIndexes.TryGetValue(key, out var savedIndex)
                ? savedIndex
                : DefaultEquipmentTextColorIndex;

            textPalettes[key] = palette;
            textIndexes[key] = Math.Clamp(index, 0, palette.Count - 1);
        }

        normalized.TextColorIndexes = textIndexes;
        normalized.TextColorPalettes = textPalettes;
        return normalized;
    }

    /// <summary>蘇生リストの設定の既定。</summary>
    public static ReviveListWidgetSettingsConfig CreateReviveListSettings()
    {
        return new ReviveListWidgetSettingsConfig
        {
            PlayerInfoFormatString = DefaultReviveListPlayerInfoFormatString,
            SelfDisplayModeIndex = DefaultSelfDisplayModeIndex,
            GaugeLengthIndex = DefaultReviveListGaugeLengthIndex,
            GaugeColors = CreateBuffGaugeColors(PlayerBuffListKind.Debuff),
            ClassColorIndexes = CreateDefaultClassColorIndexes(WidgetKind.ReviveList),
            ClassColorPalettes = CreateDefaultClassColorPalettes(WidgetKind.ReviveList),
            TextColorIndexes = CreateDefaultReviveListTextColorIndexes(),
            TextColorPalettes = CreateDefaultReviveListTextColorPalettes()
        };
    }

    /// <summary>蘇生リストのテキストカラーの鍵。蘇生不可デバフの無い人の「蘇生可能」の文字の色。</summary>
    public const string ReviveListRevivableTextColorKey = "Revivable";

    /// <summary>蘇生リストのテキストカラーの鍵の全部。並びは設定の一覧の順。</summary>
    public static readonly string[] ReviveListTextColorKeys = [ReviveListRevivableTextColorKey];

    /// <summary>
    /// 蘇生リストのテキストカラーの見本。「蘇生可能」はステータス詳細の「数値の増加」と同じ2色。
    /// <b>表は鍵の全項目を持つ</b>ので、引けない鍵は表の書き忘れ。埋めずに落とす。
    /// </summary>
    private static readonly Dictionary<string, string[]> ReviveListDefaultTextColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        [ReviveListRevivableTextColorKey] = ["#43D978", "#83E4A4"]
    };

    /// <summary>蘇生リストのテキストカラーで最初に選ばれている枠(ステータス詳細の「数値の増加」と同じ2枠目)。</summary>
    public const int DefaultReviveListTextColorIndex = 1;

    public static List<string> CreateDefaultReviveListTextColors(string key)
    {
        return [.. ReviveListDefaultTextColorHexes[key]];
    }

    public static Dictionary<string, int> CreateDefaultReviveListTextColorIndexes()
    {
        return ReviveListTextColorKeys.ToDictionary(
            key => key,
            _ => DefaultReviveListTextColorIndex,
            StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultReviveListTextColorPalettes()
    {
        return ReviveListTextColorKeys.ToDictionary(
            key => key,
            CreateDefaultReviveListTextColors,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>保存値を正規化して写す。書式が無ければ既定を入れ、色は一覧に無い鍵を捨てて足りない鍵を既定で埋める。</summary>
    public static ReviveListWidgetSettingsConfig CloneNormalizedReviveList(ReviveListWidgetSettingsConfig? reviveList)
    {
        var normalized = (reviveList ?? CreateReviveListSettings()).Clone();
        normalized.PlayerInfoFormatString ??= DefaultReviveListPlayerInfoFormatString;
        normalized.SelfDisplayModeIndex = Math.Clamp(
            normalized.SelfDisplayModeIndex,
            DefaultSelfDisplayModeIndex,
            MaxSelfDisplayModeIndex);
        normalized.GaugeLengthIndex = ClampBuffGaugeLengthIndex(normalized.GaugeLengthIndex);
        normalized.GaugeColors = CloneNormalizedBuffGaugeColors(normalized.GaugeColors, PlayerBuffListKind.Debuff);

        (normalized.ClassColorIndexes, normalized.ClassColorPalettes) = NormalizeClassColors(
            WidgetKind.ReviveList,
            normalized.ClassColorIndexes,
            normalized.ClassColorPalettes);

        var textIndexes = new Dictionary<string, int>(ReviveListTextColorKeys.Length, StringComparer.OrdinalIgnoreCase);
        var textPalettes = new Dictionary<string, List<string>>(ReviveListTextColorKeys.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var key in ReviveListTextColorKeys)
        {
            var palette = normalized.TextColorPalettes.TryGetValue(key, out var saved)
                && saved is { Count: > 0 }
                    ? new List<string>(saved)
                    : CreateDefaultReviveListTextColors(key);

            var index = normalized.TextColorIndexes.TryGetValue(key, out var savedIndex)
                ? savedIndex
                : DefaultReviveListTextColorIndex;

            textPalettes[key] = palette;
            textIndexes[key] = Math.Clamp(index, 0, palette.Count - 1);
        }

        normalized.TextColorIndexes = textIndexes;
        normalized.TextColorPalettes = textPalettes;
        return normalized;
    }

    /// <summary>被ダメログの右(設定「スキル名」)の既定の書式。</summary>
    public const string DefaultDamageTakenLogHitInfoFormatString = "{EntityName}-{SkillName}({Type})";

    /// <summary>被ダメログの「戦闘不能の原因を表示」の既定。</summary>
    public const bool DefaultDamageTakenLogShowsDefeatCause = true;

    /// <summary>被ダメログの「フィルター設定」の添字。並びは設定の選択リストと同じ。</summary>
    public const int AllDamageTakenLogAttackerFilterIndex = 0;
    public const int NoSelfOrFriendlyFireDamageTakenLogAttackerFilterIndex = 1;

    /// <summary>「フィルター設定」の既定(自傷・フレンドリーファイア以外)。</summary>
    public const int DefaultDamageTakenLogAttackerFilterIndex = NoSelfOrFriendlyFireDamageTakenLogAttackerFilterIndex;

    /// <summary>被ダメログの「最新の被ダメのみ表示」の既定。</summary>
    public const bool DefaultDamageTakenLogShowsLatestHitOnly = true;

    /// <summary>被ダメログの設定の既定。</summary>
    public static DamageTakenLogWidgetSettingsConfig CreateDamageTakenLogSettings()
    {
        return new DamageTakenLogWidgetSettingsConfig
        {
            PlayerInfoFormatString = DefaultDamageTakenLogPlayerInfoFormatString,
            SelfDisplayModeIndex = DefaultSelfDisplayModeIndex,
            HitInfoFormatString = DefaultDamageTakenLogHitInfoFormatString,
            ShowsDefeatCause = DefaultDamageTakenLogShowsDefeatCause,
            AttackerFilterIndex = DefaultDamageTakenLogAttackerFilterIndex,
            ShowsLatestHitOnly = DefaultDamageTakenLogShowsLatestHitOnly,
            ClassColorIndexes = CreateDefaultClassColorIndexes(WidgetKind.DamageTakenLog),
            ClassColorPalettes = CreateDefaultClassColorPalettes(WidgetKind.DamageTakenLog)
        };
    }

    /// <summary>保存値を正規化して写す。書式が無ければ既定を入れ、クラスの色は一覧に無い鍵を捨てて足りない鍵を既定で埋める。</summary>
    public static DamageTakenLogWidgetSettingsConfig CloneNormalizedDamageTakenLog(DamageTakenLogWidgetSettingsConfig? damageTakenLog)
    {
        var normalized = (damageTakenLog ?? CreateDamageTakenLogSettings()).Clone();
        normalized.PlayerInfoFormatString ??= DefaultDamageTakenLogPlayerInfoFormatString;
        normalized.HitInfoFormatString ??= DefaultDamageTakenLogHitInfoFormatString;
        normalized.SelfDisplayModeIndex = Math.Clamp(
            normalized.SelfDisplayModeIndex,
            DefaultSelfDisplayModeIndex,
            MaxSelfDisplayModeIndex);
        normalized.AttackerFilterIndex = Math.Clamp(
            normalized.AttackerFilterIndex,
            AllDamageTakenLogAttackerFilterIndex,
            NoSelfOrFriendlyFireDamageTakenLogAttackerFilterIndex);

        (normalized.ClassColorIndexes, normalized.ClassColorPalettes) = NormalizeClassColors(
            WidgetKind.DamageTakenLog,
            normalized.ClassColorIndexes,
            normalized.ClassColorPalettes);
        return normalized;
    }

    /// <summary>エンティティ情報の設定の既定。</summary>
    public static EntityInfoWidgetSettingsConfig CreateEntityInfoSettings()
    {
        return new EntityInfoWidgetSettingsConfig
        {
            EntityInfoFormatString = DefaultEntityInfoFormatString,
            BuffInfoFormatString = DefaultBuffListBuffInfoFormatString,
            HealthValueDisplayModeIndex = DefaultHealthValueDisplayModeIndex,
            CombatStateDisplayModeIndex = DefaultCombatStateDisplayModeIndex,
            TargetedPlayerDisplayModeIndex = DefaultTargetedPlayerDisplayModeIndex,
            HateRankDisplayModeIndex = DefaultEntityInfoHateRankDisplayModeIndex,
            SameBuffGroupingIndex = DefaultEntityInfoSameBuffGroupingIndex,
            CasterNameDisplayModeIndex = DefaultCasterNameDisplayModeIndex,
            PriorityBuffs = CreateBuffEntries(DefaultEntityInfoPriorityBuffEntries),
            HiddenBuffIds = [],
            ClassColorIndexes = CreateDefaultClassColorIndexes(WidgetKind.EntityInfo),
            ClassColorPalettes = CreateDefaultClassColorPalettes(WidgetKind.EntityInfo)
        };
    }

    /// <summary>
    /// 保存値を正規化して写す。書式が無ければ既定を入れ、選択は範囲に収め、優先表示リストは <see cref="NormalizeBuffEntries"/> で直し、
    /// クラスの色は一覧に無い鍵を捨てて足りない鍵を既定で埋める。
    /// </summary>
    public static EntityInfoWidgetSettingsConfig CloneNormalizedEntityInfo(EntityInfoWidgetSettingsConfig? entityInfo)
    {
        var normalized = (entityInfo ?? CreateEntityInfoSettings()).Clone();
        normalized.EntityInfoFormatString ??= DefaultEntityInfoFormatString;
        normalized.BuffInfoFormatString ??= DefaultBuffListBuffInfoFormatString;
        normalized.HealthValueDisplayModeIndex = Math.Clamp(
            normalized.HealthValueDisplayModeIndex,
            DefaultHealthValueDisplayModeIndex,
            SeparateShieldHealthValueDisplayModeIndex);
        normalized.CombatStateDisplayModeIndex = Math.Clamp(
            normalized.CombatStateDisplayModeIndex,
            CombatStateDisplayShowIndex,
            CombatStateDisplayHideIndex);
        normalized.TargetedPlayerDisplayModeIndex = Math.Clamp(
            normalized.TargetedPlayerDisplayModeIndex,
            TargetedPlayerDisplayShowIndex,
            TargetedPlayerDisplayHideIndex);
        normalized.HateRankDisplayModeIndex = Math.Clamp(
            normalized.HateRankDisplayModeIndex,
            0,
            EntityInfoHateRankDisplayCounts.Count - 1);
        normalized.SameBuffGroupingIndex = Math.Clamp(
            normalized.SameBuffGroupingIndex,
            EntityInfoSameBuffUngroupedIndex,
            EntityInfoSameBuffGroupedIndex);
        normalized.CasterNameDisplayModeIndex = Math.Clamp(
            normalized.CasterNameDisplayModeIndex,
            CasterNameDisplayShowIndex,
            CasterNameDisplayAppliedByOthersIndex);

        normalized.PriorityBuffs = NormalizeBuffEntries(normalized.PriorityBuffs, DefaultEntityInfoPriorityBuffEntries);

        // 0 以下はバフのIDではない。重なりは先に足したほうを残し、非表示にした順を保つ(バフ・デバフ一覧と同じ)。
        normalized.HiddenBuffIds = [.. normalized.HiddenBuffIds.Where(baseId => baseId > 0).Distinct()];

        (normalized.ClassColorIndexes, normalized.ClassColorPalettes) = NormalizeClassColors(
            WidgetKind.EntityInfo,
            normalized.ClassColorIndexes,
            normalized.ClassColorPalettes);
        return normalized;
    }

    /// <summary>チャットの設定の既定。</summary>
    public static ChatWidgetSettingsConfig CreateChatSettings()
    {
        return new ChatWidgetSettingsConfig
        {
            TextColorIndexes = CreateDefaultChatTextColorIndexes(),
            TextColorPalettes = CreateDefaultChatTextColorPalettes()
        };
    }

    /// <summary>チャットのテキストカラーの鍵。発言者の名前。</summary>
    public const string ChatPlayerNameTextColorKey = "PlayerName";

    /// <summary>チャットのテキストカラーの鍵。送られた時刻。</summary>
    public const string ChatTimeTextColorKey = "Time";

    /// <summary>チャットのテキストカラーの鍵。メッセージの本文(本文の中のリンクは除く)。</summary>
    public const string ChatMessageTextColorKey = "Message";

    /// <summary>チャットのテキストカラーの鍵の全部。並びは設定の一覧の順で、行の上から出る順。</summary>
    public static readonly string[] ChatTextColorKeys = [ChatPlayerNameTextColorKey, ChatTimeTextColorKey, ChatMessageTextColorKey];

    /// <summary>チャットのテキストカラーの既定色。白と差し色の2枠。プレイヤー名はタイムラインの「プレイヤー名」と同じ。</summary>
    private static readonly Dictionary<string, string[]> ChatDefaultTextColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        [ChatPlayerNameTextColorKey] = ["#FFFFFF", "#70D0E8"],
        [ChatTimeTextColorKey] = ["#FFFFFF", "#DDDDDD"],
        [ChatMessageTextColorKey] = ["#FFFFFF", "#EEEEEE"]
    };

    public static List<string> CreateDefaultChatTextColors(string key)
    {
        return [.. ChatDefaultTextColorHexes[key]];
    }

    /// <summary>チャットのテキストカラーで最初に選ばれている枠。メッセージだけ1枠目(白)で、残りは2枠目の差し色から始める。</summary>
    public static int GetDefaultChatTextColorIndex(string key)
    {
        return string.Equals(key, ChatMessageTextColorKey, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    public static Dictionary<string, int> CreateDefaultChatTextColorIndexes()
    {
        return ChatTextColorKeys.ToDictionary(
            key => key,
            GetDefaultChatTextColorIndex,
            StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultChatTextColorPalettes()
    {
        return ChatTextColorKeys.ToDictionary(
            key => key,
            CreateDefaultChatTextColors,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>保存値を正規化して写す。色は一覧に無い鍵を捨てて足りない鍵を既定で埋める。</summary>
    public static ChatWidgetSettingsConfig CloneNormalizedChat(ChatWidgetSettingsConfig? chat)
    {
        var normalized = (chat ?? CreateChatSettings()).Clone();
        var textIndexes = new Dictionary<string, int>(ChatTextColorKeys.Length, StringComparer.OrdinalIgnoreCase);
        var textPalettes = new Dictionary<string, List<string>>(ChatTextColorKeys.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var key in ChatTextColorKeys)
        {
            var palette = normalized.TextColorPalettes.TryGetValue(key, out var saved)
                && saved is { Count: > 0 }
                    ? new List<string>(saved)
                    : CreateDefaultChatTextColors(key);

            var index = normalized.TextColorIndexes.TryGetValue(key, out var savedIndex)
                ? savedIndex
                : GetDefaultChatTextColorIndex(key);

            textPalettes[key] = palette;
            textIndexes[key] = Math.Clamp(index, 0, palette.Count - 1);
        }

        normalized.TextColorIndexes = textIndexes;
        normalized.TextColorPalettes = textPalettes;
        return normalized;
    }

    private static WidgetWindowConfig CreateDefaultWindowConfig(WidgetKind kind)
    {
        return kind switch
        {
            WidgetKind.PlayerList => new WidgetWindowConfig
            {
                Width = PlayerListInitialWindowWidth + PlayerListInfoActionsWidth,
                Height = PlayerListInitialWindowHeight
            },
            WidgetKind.EntityList => new WidgetWindowConfig
            {
                Width = PlayerListInitialWindowWidth,
                Height = PlayerListInitialWindowHeight
            },
            WidgetKind.DpsMeter or WidgetKind.HpsMeter => new WidgetWindowConfig
            {
                Width = MeterInitialWindowWidth,
                Height = MeterInitialWindowHeight
            },
            WidgetKind.HateMeter => new WidgetWindowConfig
            {
                Width = HateMeterInitialWindowWidth,
                Height = HateMeterInitialWindowHeight
            },
            WidgetKind.BreakMeter => new WidgetWindowConfig
            {
                Width = BreakMeterInitialWindowWidth,
                Height = BreakMeterInitialWindowHeight
            },
            WidgetKind.PlayerInfo => new WidgetWindowConfig
            {
                Width = PlayerInfoInitialWindowWidth,
                Height = PlayerInfoInitialWindowHeight
            },
            WidgetKind.PlayerStatus => new WidgetWindowConfig
            {
                Width = PlayerStatusInitialWindowWidth,
                Height = PlayerStatusInitialWindowHeight
            },
            WidgetKind.PlayerEquipment => new WidgetWindowConfig
            {
                Width = PlayerEquipmentInitialWindowWidth,
                Height = PlayerEquipmentInitialWindowHeight
            },
            WidgetKind.BuffList or WidgetKind.DebuffList => new WidgetWindowConfig
            {
                Width = PlayerBuffListInitialWindowWidth,
                Height = PlayerBuffListInitialWindowHeight
            },
            WidgetKind.BuffDebuffGroup => new WidgetWindowConfig
            {
                Width = BuffDebuffGroupInitialWindowWidth,
                Height = BuffDebuffGroupInitialWindowHeight
            },
            WidgetKind.ReviveList => new WidgetWindowConfig
            {
                Width = ReviveListInitialWindowWidth,
                Height = ReviveListInitialWindowHeight
            },
            WidgetKind.EntityInfo => new WidgetWindowConfig
            {
                Width = EntityInfoInitialWindowWidth,
                Height = EntityInfoInitialWindowHeight
            },
            WidgetKind.Chat => new WidgetWindowConfig
            {
                Width = ChatInitialWindowWidth,
                Height = ChatInitialWindowHeight
            },
            WidgetKind.DamageTakenLog => new WidgetWindowConfig
            {
                Width = DamageTakenLogInitialWindowWidth,
                Height = DamageTakenLogInitialWindowHeight
            },
            WidgetKind.PlayerTalent => new WidgetWindowConfig
            {
                Width = PlayerTalentInitialWindowWidth,
                Height = PlayerTalentInitialWindowHeight
            },
            WidgetKind.PlayerSeasonTalent => new WidgetWindowConfig
            {
                Width = PlayerSeasonTalentInitialWindowWidth,
                Height = PlayerSeasonTalentInitialWindowHeight
            },
            WidgetKind.PlayerModule => new WidgetWindowConfig
            {
                Width = PlayerModuleInitialWindowWidth,
                Height = PlayerModuleInitialWindowHeight
            },
            WidgetKind.BuffDebuffCard => new WidgetWindowConfig
            {
                Width = BuffDebuffCardInitialWindowWidth,
                Height = BuffDebuffCardInitialWindowHeight
            },
            WidgetKind.DamageSkillDetail or WidgetKind.HealingSkillDetail => new WidgetWindowConfig
            {
                Width = SkillDetailInitialWindowWidth,
                Height = SkillDetailInitialWindowHeight
            },
            WidgetKind.HateSkillDetail => new WidgetWindowConfig
            {
                Width = HateSkillDetailInitialWindowWidth,
                Height = HateSkillDetailInitialWindowHeight
            },
            WidgetKind.BreakSkillDetail => new WidgetWindowConfig
            {
                Width = BreakSkillDetailInitialWindowWidth,
                Height = BreakSkillDetailInitialWindowHeight
            },
            WidgetKind.Timeline => new WidgetWindowConfig
            {
                Width = TimelineInitialWindowWidth,
                Height = TimelineInitialWindowHeight
            },
            WidgetKind.DamageSummary or WidgetKind.HealingSummary => new WidgetWindowConfig
            {
                Width = MetricSummaryInitialWindowWidth,
                Height = MetricSummaryInitialWindowHeight
            },
            WidgetKind.DamageTakenSummary => new WidgetWindowConfig
            {
                Width = DamageTakenSummaryInitialWindowWidth,
                Height = DamageTakenSummaryInitialWindowHeight
            },
            WidgetKind.DpsGraph or WidgetKind.HpsGraph => new WidgetWindowConfig
            {
                Width = MetricGraphInitialWindowWidth,
                Height = MetricGraphInitialWindowHeight
            },
            WidgetKind.HateGraph => new WidgetWindowConfig
            {
                Width = HateGraphInitialWindowWidth,
                Height = HateGraphInitialWindowHeight
            },
            WidgetKind.BreakGraph => new WidgetWindowConfig
            {
                Width = BreakGraphInitialWindowWidth,
                Height = BreakGraphInitialWindowHeight
            },
            _ => new WidgetWindowConfig()
        };
    }

    /// <summary>
    /// 大きさを保存しない辺と、その辺でいつも開く大きさ。null の辺は保存した大きさで開く。
    /// 出すものの大きさが決まっている窓(プレイヤー情報の幅と高さ、装備情報の高さ)は、手で変えても次に開くときは既定の大きさに戻す。
    /// </summary>
    public static (double? Width, double? Height) GetFixedWindowSize(WidgetKind kind)
    {
        return kind switch
        {
            WidgetKind.PlayerInfo => (PlayerInfoInitialWindowWidth, PlayerInfoInitialWindowHeight),
            WidgetKind.PlayerEquipment => (null, PlayerEquipmentInitialWindowHeight),
            _ => (null, null)
        };
    }

    public static WidgetThemeConfig CreateTheme()
    {
        return new WidgetThemeConfig
        {
            WindowColorIndex = 0,
            WindowOpacity = 50,
            WindowColors = CreateDefaultWindowColors(),
            HideHeaderWhenInactive = true
        };
    }

    /// <summary>推移グラフの既定。グラフカラーの既定は読み替え先のメーター(<see cref="GetMetricGraphColorDefaultSource"/>)と同じ。</summary>
    public static MetricGraphWidgetSettingsConfig CreateMetricGraphSettings(WidgetKind kind)
    {
        var source = GetMetricGraphColorDefaultSource(kind);

        return new MetricGraphWidgetSettingsConfig
        {
            AggregationIntervalSeconds = DefaultMetricGraphAggregationIntervalSeconds,
            VisibleSeconds = DefaultMetricGraphVisibleSeconds,
            ShowSkillLog = true,
            ClassColorIndexes = CreateDefaultClassColorIndexes(source),
            ClassColorPalettes = CreateDefaultClassColorPalettes(source)
        };
    }

    /// <summary>
    /// 推移グラフのグラフカラーの既定を引くときの読み替え先。DPS・HPS・TPS・BPS推移グラフは、それぞれ DPS・HPS・ヘイト・ブレイクメーターに合わせる
    /// (行の並び・色・最初に選ぶ枠)。推移グラフ以外(設定の初期値で種類が分からないとき)は DPS メーター。
    /// </summary>
    public static WidgetKind GetMetricGraphColorDefaultSource(WidgetKind kind)
    {
        return kind switch
        {
            WidgetKind.HpsGraph => WidgetKind.HpsMeter,
            WidgetKind.HateGraph => WidgetKind.HateMeter,
            WidgetKind.BreakGraph => WidgetKind.BreakMeter,
            _ => WidgetKind.DpsMeter
        };
    }

    public static BuffCardWidgetSettingsConfig CreateBuffCardSettings()
    {
        return new BuffCardWidgetSettingsConfig
        {
            BuffInfoFormatString = DefaultBuffInfoFormatString,
            Shortcuts = CreateBuffEntries(DefaultBuffCardShortcutEntries),
            NotificationTargets = CreateBuffEntries(DefaultBuffCardNotificationTargetEntries)
        };
    }

    public static PlayerInfoWidgetSettingsConfig CreatePlayerInfoSettings()
    {
        return new PlayerInfoWidgetSettingsConfig
        {
            ClassColorIndexes = CreateDefaultClassColorIndexes(WidgetKind.PlayerInfo),
            ClassColorPalettes = CreateDefaultClassColorPalettes(WidgetKind.PlayerInfo)
        };
    }

    public static TimelineWidgetSettingsConfig CreateTimelineSettings()
    {
        return new TimelineWidgetSettingsConfig
        {
            EnemyHealDisplayModeIndex = DefaultTimelineEnemyHealDisplayModeIndex,
            ClassColorIndexes = CreateDefaultClassColorIndexes(WidgetKind.Timeline),
            ClassColorPalettes = CreateDefaultClassColorPalettes(WidgetKind.Timeline),
            TextColorIndexes = CreateDefaultTextColorIndexes(),
            TextColorPalettes = CreateDefaultTextColorPalettes()
        };
    }

    /// <summary>ゲージの長さの既定は種別で違う。バフは10秒、デバフは30秒。優先表示リストの既定の行も種別で違う。</summary>
    public static BuffListWidgetSettingsConfig CreateBuffListSettings(WidgetKind kind)
    {
        return new BuffListWidgetSettingsConfig
        {
            GaugeLengthIndex = kind == WidgetKind.DebuffList
                ? DefaultDebuffListGaugeLengthIndex
                : DefaultBuffListGaugeLengthIndex,
            GaugeColors = CreateBuffGaugeColors(GetBuffListSide(kind)),
            PriorityBuffs = CreateBuffEntries(GetBuffListPriorityDefaults(kind)),
            HiddenBuffIds = [],
            SameBuffGroupingIndex = DefaultSameBuffGroupingIndex,
            CasterNameDisplayModeIndex = DefaultCasterNameDisplayModeIndex,
            BuffInfoFormatString = DefaultBuffListBuffInfoFormatString
        };
    }

    public static bool SupportsBuffListSettings(WidgetKind kind)
    {
        return kind is WidgetKind.BuffList or WidgetKind.DebuffList;
    }

    public static BuffListWidgetSettingsConfig CloneNormalizedBuffList(WidgetKind kind, BuffListWidgetSettingsConfig? buffList)
    {
        var normalized = (buffList ?? CreateBuffListSettings(kind)).Clone();
        NormalizeBuffList(kind, normalized);
        return normalized;
    }

    /// <summary>
    /// 保存値を表示できる形に直す。<b>選んでいる枠が入っていないときの既定と、優先表示リストの既定の行は種別で違う</b>ので
    /// <see cref="WidgetKind"/> を受け取る(<see cref="GetDefaultGaugeColorIndex"/>・<see cref="GetBuffListPriorityDefaults"/>)。
    /// </summary>
    public static void NormalizeBuffList(WidgetKind kind, BuffListWidgetSettingsConfig buffList)
    {
        buffList.GaugeLengthIndex = ClampBuffGaugeLengthIndex(buffList.GaugeLengthIndex);
        buffList.GaugeColors = CloneNormalizedBuffGaugeColors(buffList.GaugeColors, GetBuffListSide(kind));

        buffList.SameBuffGroupingIndex = Math.Clamp(buffList.SameBuffGroupingIndex, 0, SameBuffGroupingCount - 1);
        buffList.CasterNameDisplayModeIndex = Math.Clamp(
            buffList.CasterNameDisplayModeIndex,
            CasterNameDisplayShowIndex,
            CasterNameDisplayAppliedByOthersIndex);
        buffList.BuffInfoFormatString ??= DefaultBuffListBuffInfoFormatString;
        buffList.PriorityBuffs = NormalizeBuffEntries(buffList.PriorityBuffs, GetBuffListPriorityDefaults(kind));

        // 0 以下はバフのIDではない。重なりは先に足したほうを残し、非表示にした順を保つ。
        buffList.HiddenBuffIds = buffList.HiddenBuffIds is null
            ? []
            : [.. buffList.HiddenBuffIds.Where(baseId => baseId > 0).Distinct()];
    }

    /// <summary>バフ一覧の色はバフの側、デバフ一覧の色はデバフの側。</summary>
    public static PlayerBuffListKind GetBuffListSide(WidgetKind kind)
    {
        return kind == WidgetKind.DebuffList
            ? PlayerBuffListKind.Debuff
            : PlayerBuffListKind.Buff;
    }

    public static int ClampBuffGaugeLengthIndex(int index)
    {
        return Math.Clamp(index, MinBuffGaugeLengthIndex, BuffGaugeLengthSeconds.Length - 1);
    }

    public static BuffGaugeColorConfig CreateBuffGaugeColors(PlayerBuffListKind side)
    {
        return new BuffGaugeColorConfig
        {
            Indexes = CreateDefaultGaugeColorIndexes(side),
            Palettes = CreateDefaultGaugeColorPalettes(),
            Opacity = MaxClassColorOpacity
        };
    }

    /// <summary>
    /// ゲージの色の保存値を表示できる形に直して写す。選んでいる枠が入っていないときの既定はバフとデバフで違う
    /// (<see cref="GetDefaultGaugeColorIndex"/>)。
    /// </summary>
    public static BuffGaugeColorConfig CloneNormalizedBuffGaugeColors(BuffGaugeColorConfig? colors, PlayerBuffListKind side)
    {
        var source = colors ?? CreateBuffGaugeColors(side);
        var savedIndexes = source.Indexes ?? CreateDefaultGaugeColorIndexes(side);
        var savedPalettes = source.Palettes ?? CreateDefaultGaugeColorPalettes();

        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in BuffGaugeColorKeys)
        {
            var defaultColors = CreateDefaultGaugeColors(key);
            var sourceColors = savedPalettes.TryGetValue(key, out var saved) ? saved : defaultColors;
            var palette = NormalizeColorList(sourceColors, defaultColors, MaxPaletteColorCount);
            normalizedPalettes[key] = palette;

            var selectedIndex = savedIndexes.TryGetValue(key, out var index)
                ? index
                : GetDefaultGaugeColorIndex(side);
            normalizedIndexes[key] = Math.Clamp(selectedIndex, MinClassColorIndex, palette.Count - 1);
        }

        return new BuffGaugeColorConfig
        {
            Indexes = normalizedIndexes,
            Palettes = normalizedPalettes,
            Opacity = Math.Clamp(source.Opacity, MinClassColorOpacity, MaxClassColorOpacity)
        };
    }

    /// <summary>バフ・デバフグループの設定の既定。ゲージは自動、色はバフ一覧・デバフ一覧と同じ、行は既定の7行。</summary>
    public static BuffDebuffGroupWidgetSettingsConfig CreateBuffDebuffGroupSettings()
    {
        return new BuffDebuffGroupWidgetSettingsConfig
        {
            GaugeLengthIndex = DefaultBuffDebuffGroupGaugeLengthIndex,
            BuffGaugeColors = CreateBuffGaugeColors(PlayerBuffListKind.Buff),
            DebuffGaugeColors = CreateBuffGaugeColors(PlayerBuffListKind.Debuff),
            Entries = CreateBuffEntries(DefaultBuffDebuffGroupEntries),
            NotificationTargets = CreateBuffEntries(DefaultBuffDebuffGroupNotificationTargetEntries)
        };
    }

    /// <summary>既定の行(<paramref name="defaults"/>)を、既定の並びとオン・オフで作る。</summary>
    public static List<BuffDebuffGroupEntryConfig> CreateBuffEntries(IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> defaults)
    {
        return
        [
            .. defaults.Select(entry => new BuffDebuffGroupEntryConfig
            {
                BuffId = entry.Key.BuffId,
                Group = entry.Key.Group,
                IsEnabled = entry.IsEnabled
            })
        ];
    }

    /// <summary>
    /// 保存値の行を表示できる形に直す。鍵として成り立たないものと重なり(先の行を残す)を落とし、
    /// 欠けた既定の行(<paramref name="defaults"/>)を既定のオン・オフで末尾に足す(既定の行は消えない)。行が無ければ既定の行。
    /// </summary>
    public static List<BuffDebuffGroupEntryConfig> NormalizeBuffEntries(
        IReadOnlyList<BuffDebuffGroupEntryConfig?>? entries,
        IReadOnlyList<(BuffEntryKey Key, bool IsEnabled)> defaults)
    {
        var normalized = new List<BuffDebuffGroupEntryConfig>();
        var seen = new HashSet<BuffEntryKey>();
        foreach (var entry in entries ?? CreateBuffEntries(defaults))
        {
            if (entry is null)
            {
                continue;
            }

            var key = entry.GetKey();
            if (key.IsValid && seen.Add(key))
            {
                normalized.Add(entry);
            }
        }

        foreach (var (key, isEnabled) in defaults)
        {
            if (seen.Add(key))
            {
                normalized.Add(new BuffDebuffGroupEntryConfig
                {
                    BuffId = key.BuffId,
                    Group = key.Group,
                    IsEnabled = isEnabled
                });
            }
        }

        return normalized;
    }

    /// <summary>保存値を表示できる形に直して写す。行は <see cref="NormalizeBuffEntries"/> で直す。</summary>
    public static BuffDebuffGroupWidgetSettingsConfig CloneNormalizedBuffDebuffGroup(BuffDebuffGroupWidgetSettingsConfig? settings)
    {
        var normalized = (settings ?? CreateBuffDebuffGroupSettings()).Clone();
        normalized.GaugeLengthIndex = ClampBuffGaugeLengthIndex(normalized.GaugeLengthIndex);
        normalized.BuffGaugeColors = CloneNormalizedBuffGaugeColors(normalized.BuffGaugeColors, PlayerBuffListKind.Buff);
        normalized.DebuffGaugeColors = CloneNormalizedBuffGaugeColors(normalized.DebuffGaugeColors, PlayerBuffListKind.Debuff);
        normalized.Entries = NormalizeBuffEntries(normalized.Entries, DefaultBuffDebuffGroupEntries);
        normalized.NotificationTargets = NormalizeBuffEntries(normalized.NotificationTargets, DefaultBuffDebuffGroupNotificationTargetEntries);
        return normalized;
    }

    public static MeterWidgetSettingsConfig CreateMeterSettings(WidgetKind kind)
    {
        return new MeterWidgetSettingsConfig
        {
            PlayerInfoFormatString = GetDefaultPlayerInfoFormatString(kind),
            HealthValueDisplayModeIndex = DefaultHealthValueDisplayModeIndex,
            PartyDisplayModeIndex = DefaultPartyDisplayModeIndex,
            EntityDisplayModeIndex = DefaultEntityDisplayModeIndex,
            ListSortModeIndex = GetDefaultListSortModeIndex(kind),
            ClassColorOpacity = MaxClassColorOpacity,
            ClassColorIndexes = CreateDefaultClassColorIndexes(kind),
            ClassColorPalettes = CreateDefaultClassColorPalettes(kind)
        };
    }

    public static List<string> CreateDefaultWindowColors()
    {
        return [.. DefaultWindowColorHexes];
    }

    public static string GetDefaultPlayerInfoFormatString(WidgetKind kind)
    {
        return kind switch
        {
            WidgetKind.EntityList => DefaultEntityInfoFormatString,
            WidgetKind.PlayerList => DefaultPlayerListPlayerInfoFormatString,
            _ => DefaultMeterPlayerInfoFormatString
        };
    }

    /// <summary>
    /// 一覧の並び替えの既定。エンティティリストは名前順、それ以外(プレイヤーリスト)は発見順。
    /// </summary>
    public static int GetDefaultListSortModeIndex(WidgetKind kind)
    {
        return kind == WidgetKind.EntityList
            ? NameListSortModeIndex
            : FirstSeenListSortModeIndex;
    }

    /// <summary>並び替えの設定を出すウィジェット。</summary>
    public static bool UsesListSort(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerList or WidgetKind.EntityList;
    }

    public static IReadOnlyList<string> GetClassColorKeys(WidgetKind kind)
    {
        return kind switch
        {
            WidgetKind.EntityList or WidgetKind.EntityInfo => EntityClassColorKeys,
            WidgetKind.PlayerList => PlayerListClassColorKeys,
            WidgetKind.PlayerInfo => PlayerInfoClassColorKeys,
            _ => ClassColorKeys
        };
    }

    /// <summary>
    /// クラスカラーで最初に選ばれている枠。プレイヤーリストの潜在心相晶(不明を含む)と、
    /// プレイヤー情報の潜在心相晶(無効を含む)とシーズンだけ2枠目。
    /// </summary>
    public static int GetDefaultClassColorIndex(WidgetKind kind, string key)
    {
        return (kind == WidgetKind.PlayerList
                && PlayerListDefaultSeasonTalentColorHexes.ContainsKey(key))
            || (kind == WidgetKind.PlayerInfo
                && (PlayerListDefaultSeasonTalentColorHexes.ContainsKey(key)
                    || string.Equals(key, StarResonanceDps.App.Services.SeasonTalentIcons.InactiveColorKey, StringComparison.OrdinalIgnoreCase)
                    || StarResonanceDps.App.Services.SeasonIcons.IsColorKey(key)))
            ? MinClassColorIndex + 1
            : MinClassColorIndex;
    }

    public static Dictionary<string, int> CreateDefaultClassColorIndexes(WidgetKind kind = WidgetKind.PlayerList)
    {
        return GetClassColorKeys(kind)
            .ToDictionary(key => key, key => GetDefaultClassColorIndex(kind, key), StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultClassColorPalettes(WidgetKind kind)
    {
        return GetClassColorKeys(kind).ToDictionary(
            key => key,
            key => CreateDefaultClassColors(kind, key),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// タイムラインのテキストカラーで最初に選ばれている枠。エンティティ名とプレイヤー名だけ1枠目(白)で、
    /// 残りは2枠目の差し色から始める。
    /// </summary>
    public static int GetDefaultTextColorIndex(string key)
    {
        return string.Equals(key, "EntityName", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "PlayerName", StringComparison.OrdinalIgnoreCase)
            ? MinClassColorIndex
            : MinClassColorIndex + 1;
    }

    /// <summary>
    /// ゲージの色で最初に選ばれている枠。<b>デバフの色だけ2枠目(紫)</b>で、バフの色は1枠目(青緑)から始める。
    /// 見本そのものは両方で同じ(<see cref="BuffGaugeDefaultColorHexes"/>)。
    /// </summary>
    public static int GetDefaultGaugeColorIndex(PlayerBuffListKind side)
    {
        return side == PlayerBuffListKind.Debuff
            ? MinClassColorIndex + 1
            : MinClassColorIndex;
    }

    public static Dictionary<string, int> CreateDefaultGaugeColorIndexes(PlayerBuffListKind side)
    {
        return BuffGaugeColorKeys
            .ToDictionary(key => key, _ => GetDefaultGaugeColorIndex(side), StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultGaugeColorPalettes()
    {
        return BuffGaugeColorKeys.ToDictionary(
            key => key,
            CreateDefaultGaugeColors,
            StringComparer.OrdinalIgnoreCase);
    }

    public static List<string> CreateDefaultGaugeColors(string key)
    {
        return BuffGaugeDefaultColorHexes.TryGetValue(key, out var colors)
            ? [.. colors]
            : ["#5688A4"];
    }

    public static Dictionary<string, int> CreateDefaultTextColorIndexes()
    {
        return TimelineTextColorKeys
            .ToDictionary(key => key, GetDefaultTextColorIndex, StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultTextColorPalettes()
    {
        return TimelineTextColorKeys.ToDictionary(
            key => key,
            CreateDefaultTextColors,
            StringComparer.OrdinalIgnoreCase);
    }

    public static List<string> CreateDefaultTextColors(string key)
    {
        return TimelineDefaultTextColorHexes.TryGetValue(key, out var colors)
            ? [.. colors]
            : ["#FFFFFF", "#A8A8A8"];
    }

    /// <summary>
    /// フッターを持つウィジェット。持たないウィジェットでは「ピン留め中フッターを隠す」を出さない。
    ///
    /// <para>
    /// <b>実際にフッターを作っているのは <c>WidgetWindowManager.CreateWidgetWindowComposition</c>。</b>
    /// 増減させたらここも合わせること。
    /// </para>
    /// </summary>
    public static bool HasFooter(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerList
            or WidgetKind.EntityList
            or WidgetKind.DpsMeter
            or WidgetKind.HpsMeter
            or WidgetKind.HateMeter
            or WidgetKind.BreakMeter
            or WidgetKind.DamageTakenLog;
    }

    /// <summary>属性カラーの節を持つウィジェット。スキル詳細の4種・被ダメログ・与ダメ詳細・ヒール詳細・被ダメ詳細。</summary>
    public static bool SupportsElementColorSettings(WidgetKind kind)
    {
        return kind is WidgetKind.DamageSkillDetail
            or WidgetKind.HealingSkillDetail
            or WidgetKind.HateSkillDetail
            or WidgetKind.BreakSkillDetail
            or WidgetKind.DamageTakenLog
            or WidgetKind.DamageSummary
            or WidgetKind.HealingSummary
            or WidgetKind.DamageTakenSummary;
    }

    public static ElementColorWidgetSettingsConfig CreateElementColorSettings(WidgetKind kind)
    {
        var config = new ElementColorWidgetSettingsConfig
        {
            ColorOpacity = MaxClassColorOpacity,
            ColorIndexes = CreateDefaultElementColorIndexes(),
            ColorPalettes = CreateDefaultElementColorPalettes()
        };

        if (HasDamageModeColorRows(kind))
        {
            // 物理・魔法・無分類の行は、最初に選ばれている枠が1枠目。
            foreach (var key in DamageModeKeys)
            {
                config.ColorIndexes[key] = MinClassColorIndex;
                config.ColorPalettes[key] = CreateDefaultDamageModeColors(key);
            }
        }

        return config;
    }

    /// <summary>属性カラーで最初に選ばれている枠。9属性とも1枠目。</summary>
    public static Dictionary<string, int> CreateDefaultElementColorIndexes()
    {
        return DamagePropertyKeys
            .ToDictionary(key => key, _ => MinClassColorIndex, StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultElementColorPalettes()
    {
        return DamagePropertyKeys.ToDictionary(
            key => key,
            CreateDefaultElementColors,
            StringComparer.OrdinalIgnoreCase);
    }

    public static List<string> CreateDefaultElementColors(string key)
    {
        return ElementDefaultColorHexes.TryGetValue(key, out var colors)
            ? [.. colors]
            : [.. ElementDefaultColorHexes["General"]];
    }

    public static ElementColorWidgetSettingsConfig CloneNormalizedElementColor(
        WidgetKind kind,
        ElementColorWidgetSettingsConfig? elementColor)
    {
        var normalized = (elementColor ?? CreateElementColorSettings(kind)).Clone();
        NormalizeElementColor(kind, normalized);
        return normalized;
    }

    public static void NormalizeElementColor(WidgetKind kind, ElementColorWidgetSettingsConfig elementColor)
    {
        elementColor.ColorIndexes ??= CreateDefaultElementColorIndexes();
        elementColor.ColorPalettes ??= CreateDefaultElementColorPalettes();
        if (HasDamageModeColorRows(kind))
        {
            NormalizeKeyedColor(
                elementColor,
                (DamagePropertyKeys, CreateDefaultElementColors, MinClassColorIndex + 1),
                (DamageModeKeys, CreateDefaultDamageModeColors, MinClassColorIndex));
        }
        else
        {
            NormalizeKeyedColor(
                elementColor,
                (DamagePropertyKeys, CreateDefaultElementColors, MinClassColorIndex + 1));
        }
    }

    /// <summary>
    /// 属性カラーの設定に物理・魔法・無分類の行も持つウィジェット(節の名前はアイコンカラー)。与ダメ詳細・ヒール詳細・被ダメ詳細。
    /// 不透明度は属性の行と1組。
    /// </summary>
    public static bool HasDamageModeColorRows(WidgetKind kind)
    {
        return kind is WidgetKind.DamageSummary or WidgetKind.HealingSummary or WidgetKind.DamageTakenSummary;
    }

    public static List<string> CreateDefaultDamageModeColors(string key)
    {
        return DamageModeDefaultColorHexes.TryGetValue(key, out var colors)
            ? [.. colors]
            : [.. DamageModeDefaultColorHexes["DamageNormal"]];
    }

    /// <summary>
    /// 属性カラーの保存値を正規化する。行は <paramref name="rowGroups"/> の鍵だけに揃え(無い鍵は既定、余分な鍵は捨てる)、
    /// 選んでいる枠が無い行はその組の <c>MissingColorIndex</c> の枠にする。
    /// </summary>
    private static void NormalizeKeyedColor(
        ElementColorWidgetSettingsConfig colorConfig,
        params (IReadOnlyList<string> Keys, Func<string, List<string>> CreateDefaultColors, int MissingColorIndex)[] rowGroups)
    {
        colorConfig.ColorOpacity = Math.Clamp(
            colorConfig.ColorOpacity,
            MinClassColorOpacity,
            MaxClassColorOpacity);

        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (keys, createDefaultColors, missingColorIndex) in rowGroups)
        {
            foreach (var key in keys)
            {
                var defaultColors = createDefaultColors(key);
                var sourceColors = colorConfig.ColorPalettes.TryGetValue(key, out var colors) ? colors : defaultColors;
                var palette = NormalizeColorList(sourceColors, defaultColors, MaxPaletteColorCount);
                normalizedPalettes[key] = palette;

                var selectedIndex = colorConfig.ColorIndexes.TryGetValue(key, out var index)
                    ? index
                    : missingColorIndex;
                normalizedIndexes[key] = Math.Clamp(selectedIndex, MinClassColorIndex, palette.Count - 1);
            }
        }

        colorConfig.ColorIndexes = normalizedIndexes;
        colorConfig.ColorPalettes = normalizedPalettes;
    }

    public static List<string> CreateDefaultClassColors(WidgetKind kind, string key)
    {
        if (kind == WidgetKind.PlayerInfo
            && PlayerInfoDefaultExtraColorHexes.TryGetValue(key, out var playerInfoColors))
        {
            return [.. playerInfoColors];
        }

        if ((kind is WidgetKind.PlayerList or WidgetKind.PlayerInfo)
            && PlayerListDefaultSeasonTalentColorHexes.TryGetValue(key, out var seasonTalentColors))
        {
            return [.. seasonTalentColors];
        }

        var source = kind switch
        {
            // タイムライン・プレイヤー情報・蘇生リスト・被ダメログのクラスアイコンは、プレイヤーリストと同じ既定の色で始める。
            WidgetKind.PlayerList or WidgetKind.Timeline or WidgetKind.PlayerInfo or WidgetKind.ReviveList or WidgetKind.DamageTakenLog
                => PlayerListDefaultClassColorHexes,
            // エンティティリスト・エンティティ情報のプレイヤーのクラスの行(ヘイトの枠)は、プレイヤーリストと同じ既定の色で始める。
            WidgetKind.EntityList or WidgetKind.EntityInfo => EntityListDefaultClassColorHexes.ContainsKey(key)
                ? EntityListDefaultClassColorHexes
                : PlayerListDefaultClassColorHexes,
            WidgetKind.HpsMeter => HpsMeterDefaultClassColorHexes,
            _ => MeterDefaultClassColorHexes
        };

        return source.TryGetValue(key, out var colors)
            ? [.. colors]
            : ["#A8A8A8", "#707070"];
    }

    public static WidgetConfig CloneNormalized(WidgetKind kind, WidgetConfig? config)
    {
        var normalized = (config ?? Create(kind)).Clone();
        Normalize(kind, normalized);
        return normalized;
    }

    public static WidgetThemeConfig CloneNormalizedTheme(WidgetThemeConfig? theme)
    {
        var normalized = (theme ?? CreateTheme()).Clone();
        NormalizeTheme(normalized);
        return normalized;
    }

    public static MeterWidgetSettingsConfig CloneNormalizedMeter(WidgetKind kind, MeterWidgetSettingsConfig? meter)
    {
        var normalized = (meter ?? CreateMeterSettings(kind)).Clone();
        NormalizeMeter(kind, normalized);
        return normalized;
    }

    public static MetricGraphWidgetSettingsConfig CloneNormalizedMetricGraph(
        WidgetKind kind,
        MetricGraphWidgetSettingsConfig? metricGraph)
    {
        var normalized = (metricGraph ?? CreateMetricGraphSettings(kind)).Clone();
        NormalizeMetricGraph(kind, normalized);
        return normalized;
    }

    public static BuffCardWidgetSettingsConfig CloneNormalizedBuffCard(BuffCardWidgetSettingsConfig? buffCard)
    {
        var normalized = (buffCard ?? CreateBuffCardSettings()).Clone();
        NormalizeBuffCard(normalized);
        return normalized;
    }

    public static TimelineWidgetSettingsConfig CloneNormalizedTimeline(TimelineWidgetSettingsConfig? timeline)
    {
        var normalized = (timeline ?? CreateTimelineSettings()).Clone();
        NormalizeTimeline(normalized);
        return normalized;
    }

    public static PlayerInfoWidgetSettingsConfig CloneNormalizedPlayerInfo(PlayerInfoWidgetSettingsConfig? playerInfo)
    {
        var normalized = (playerInfo ?? CreatePlayerInfoSettings()).Clone();
        NormalizePlayerInfo(normalized);
        return normalized;
    }

    public static void Normalize(WidgetConfig config)
    {
        config.Theme ??= CreateTheme();
        config.Window ??= new WidgetWindowConfig();

        if (config.State is { } state
            && state is not WidgetState.Stopped
            && state is not WidgetState.Running)
        {
            config.State = WidgetState.Stopped;
        }

        NormalizeTheme(config.Theme);
    }

    public static void Normalize(WidgetKind kind, WidgetConfig config)
    {
        Normalize(config);
        if(kind==WidgetKind.EntityList && !config.EntityDamageWidthApplied)
        {config.Window.Width=(config.Window.Width ?? 457d)+180d;config.EntityDamageWidthApplied=true;}
        // Preserve the user's former skill area, and migrate a saved width only once.
        if (kind == WidgetKind.PlayerList && !config.PlayerListInfoActionsWidthApplied)
        {
            config.Window.Width = (config.Window.Width ?? PlayerListInitialWindowWidth) + PlayerListInfoActionsWidth;
            config.PlayerListInfoActionsWidthApplied = true;
        }
        if (kind == WidgetKind.Timeline && !config.TimelineIntegratedLayoutApplied)
        {
            config.Window.Width = Math.Max(config.Window.Width ?? TimelineInitialWindowWidth, TimelineInitialWindowWidth);
            config.Window.Height = Math.Max(config.Window.Height ?? TimelineInitialWindowHeight, TimelineInitialWindowHeight);
            config.TimelineIntegratedLayoutApplied = true;
        }
        config.Meter = SupportsMeterSettings(kind)
            ? CloneNormalizedMeter(kind, config.Meter)
            : null;
        config.MetricGraph = SupportsMetricGraphSettings(kind)
            ? CloneNormalizedMetricGraph(kind, config.MetricGraph)
            : null;
        config.BuffCard = SupportsBuffCardSettings(kind)
            ? CloneNormalizedBuffCard(config.BuffCard)
            : null;
        config.Timeline = SupportsTimelineSettings(kind)
            ? CloneNormalizedTimeline(config.Timeline)
            : null;
        config.PlayerInfo = SupportsPlayerInfoSettings(kind)
            ? CloneNormalizedPlayerInfo(config.PlayerInfo)
            : null;
        config.BuffList = SupportsBuffListSettings(kind)
            ? CloneNormalizedBuffList(kind, config.BuffList)
            : null;
        config.ElementColor = SupportsElementColorSettings(kind)
            ? CloneNormalizedElementColor(kind, config.ElementColor)
            : null;
        config.PlayerStatusRowOrder = kind == WidgetKind.PlayerStatus
            ? NormalizePlayerStatusRowOrder(config.PlayerStatusRowOrder)
            : null;

        config.PlayerStatus = SupportsPlayerStatusSettings(kind)
            ? CloneNormalizedPlayerStatus(config.PlayerStatus)
            : null;
        config.SkillDetail = SupportsSkillDetailSettings(kind)
            ? CloneNormalizedSkillDetail(config.SkillDetail)
            : null;
        config.Equipment = SupportsEquipmentSettings(kind)
            ? CloneNormalizedEquipment(config.Equipment)
            : null;
        config.BuffDebuffGroup = SupportsBuffDebuffGroupSettings(kind)
            ? CloneNormalizedBuffDebuffGroup(config.BuffDebuffGroup)
            : null;
        config.ReviveList = SupportsReviveListSettings(kind)
            ? CloneNormalizedReviveList(config.ReviveList)
            : null;
        config.DamageTakenLog = SupportsDamageTakenLogSettings(kind)
            ? CloneNormalizedDamageTakenLog(config.DamageTakenLog)
            : null;
        config.EntityInfo = SupportsEntityInfoSettings(kind)
            ? CloneNormalizedEntityInfo(config.EntityInfo)
            : null;
        config.Chat = SupportsChatSettings(kind)
            ? CloneNormalizedChat(config.Chat)
            : null;
        config.Plugin = kind == WidgetKind.Plugin
            ? CloneNormalizedPlugin(config.Plugin)
            : null;
        // プラグインのウィジェットは、複数起動かが定義でしか分からない。1枚だけ開くウィジェットは書かないので null のまま。
        config.OpenTargets = SupportsOpenTargets(kind) || kind == WidgetKind.Plugin
            ? config.OpenTargets
            : null;

        switch (kind)
        {
            case WidgetKind.PlayerList:
            case WidgetKind.EntityList:
                config.Window.Width ??= PlayerListInitialWindowWidth;
                config.Window.Height ??= PlayerListInitialWindowHeight;
                break;

            case WidgetKind.DpsMeter:
            case WidgetKind.HpsMeter:
                config.Window.Width ??= MeterInitialWindowWidth;
                config.Window.Height ??= MeterInitialWindowHeight;
                break;

            case WidgetKind.HateMeter:
                config.Window.Width ??= HateMeterInitialWindowWidth;
                config.Window.Height ??= HateMeterInitialWindowHeight;
                break;

            case WidgetKind.BreakMeter:
                config.Window.Width ??= BreakMeterInitialWindowWidth;
                config.Window.Height ??= BreakMeterInitialWindowHeight;
                break;

            case WidgetKind.PlayerInfo:
                config.Window.Width ??= PlayerInfoInitialWindowWidth;
                config.Window.Height ??= PlayerInfoInitialWindowHeight;
                break;

            case WidgetKind.PlayerStatus:
                config.Window.Width ??= PlayerStatusInitialWindowWidth;
                config.Window.Height ??= PlayerStatusInitialWindowHeight;
                break;

            case WidgetKind.PlayerEquipment:
                config.Window.Width ??= PlayerEquipmentInitialWindowWidth;
                config.Window.Height ??= PlayerEquipmentInitialWindowHeight;
                break;
            case WidgetKind.BuffList:
            case WidgetKind.DebuffList:
                config.Window.Width ??= PlayerBuffListInitialWindowWidth;
                config.Window.Height ??= PlayerBuffListInitialWindowHeight;
                break;
            case WidgetKind.BuffDebuffGroup:
                config.Window.Width ??= BuffDebuffGroupInitialWindowWidth;
                config.Window.Height ??= BuffDebuffGroupInitialWindowHeight;
                break;
            case WidgetKind.ReviveList:
                config.Window.Width ??= ReviveListInitialWindowWidth;
                config.Window.Height ??= ReviveListInitialWindowHeight;
                break;
            case WidgetKind.EntityInfo:
                config.Window.Width ??= EntityInfoInitialWindowWidth;
                config.Window.Height ??= EntityInfoInitialWindowHeight;
                break;
            case WidgetKind.Chat:
                config.Window.Width ??= ChatInitialWindowWidth;
                config.Window.Height ??= ChatInitialWindowHeight;
                break;
            case WidgetKind.DamageTakenLog:
                config.Window.Width ??= DamageTakenLogInitialWindowWidth;
                config.Window.Height ??= DamageTakenLogInitialWindowHeight;
                break;
            case WidgetKind.PlayerTalent:
                config.Window.Width ??= PlayerTalentInitialWindowWidth;
                config.Window.Height ??= PlayerTalentInitialWindowHeight;
                break;
            case WidgetKind.PlayerSeasonTalent:
                config.Window.Width ??= PlayerSeasonTalentInitialWindowWidth;
                config.Window.Height ??= PlayerSeasonTalentInitialWindowHeight;
                break;
            case WidgetKind.PlayerModule:
                config.Window.Width ??= PlayerModuleInitialWindowWidth;
                config.Window.Height ??= PlayerModuleInitialWindowHeight;
                break;
            case WidgetKind.BuffDebuffCard:
                config.Window.Width ??= BuffDebuffCardInitialWindowWidth;
                config.Window.Height ??= BuffDebuffCardInitialWindowHeight;
                break;
            case WidgetKind.DamageSkillDetail:
            case WidgetKind.HealingSkillDetail:
                config.Window.Width ??= SkillDetailInitialWindowWidth;
                config.Window.Height ??= SkillDetailInitialWindowHeight;
                break;
            case WidgetKind.HateSkillDetail:
                config.Window.Width ??= HateSkillDetailInitialWindowWidth;
                config.Window.Height ??= HateSkillDetailInitialWindowHeight;
                break;
            case WidgetKind.BreakSkillDetail:
                config.Window.Width ??= BreakSkillDetailInitialWindowWidth;
                config.Window.Height ??= BreakSkillDetailInitialWindowHeight;
                break;
            case WidgetKind.Timeline:
                config.Window.Width ??= TimelineInitialWindowWidth;
                config.Window.Height ??= TimelineInitialWindowHeight;
                break;
            case WidgetKind.DamageSummary:
            case WidgetKind.HealingSummary:
                config.Window.Width ??= MetricSummaryInitialWindowWidth;
                config.Window.Height ??= MetricSummaryInitialWindowHeight;
                break;
            case WidgetKind.DamageTakenSummary:
                config.Window.Width ??= DamageTakenSummaryInitialWindowWidth;
                config.Window.Height ??= DamageTakenSummaryInitialWindowHeight;
                break;
            case WidgetKind.DpsGraph:
            case WidgetKind.HpsGraph:
                config.Window.Width ??= MetricGraphInitialWindowWidth;
                config.Window.Height ??= MetricGraphInitialWindowHeight;
                break;
            case WidgetKind.HateGraph:
                config.Window.Width ??= HateGraphInitialWindowWidth;
                config.Window.Height ??= HateGraphInitialWindowHeight;
                break;
            case WidgetKind.BreakGraph:
                config.Window.Width ??= BreakGraphInitialWindowWidth;
                config.Window.Height ??= BreakGraphInitialWindowHeight;
                break;
        }
    }

    public static void NormalizeTheme(WidgetThemeConfig theme)
    {
        theme.WindowColors = NormalizeColorList(theme.WindowColors, DefaultWindowColorHexes, MaxPaletteColorCount);
        theme.WindowColorIndex = Math.Clamp(theme.WindowColorIndex, MinColorIndex, theme.WindowColors.Count - 1);
        theme.WindowOpacity = Math.Clamp(theme.WindowOpacity, MinWindowOpacity, MaxWindowOpacity);
        theme.BackgroundImagePath = string.IsNullOrWhiteSpace(theme.BackgroundImagePath)
            ? null
            : theme.BackgroundImagePath.Trim();

        if (theme.BackgroundImagePath is null)
        {
            theme.BackgroundImageAverageColor = null;
            theme.BackgroundImageAverageColorSourcePath = null;
            return;
        }

        theme.BackgroundImageAverageColor = TryNormalizeHexColor(theme.BackgroundImageAverageColor, out var averageColor)
            ? averageColor
            : null;
        theme.BackgroundImageAverageColorSourcePath = string.IsNullOrWhiteSpace(theme.BackgroundImageAverageColorSourcePath)
            ? null
            : theme.BackgroundImageAverageColorSourcePath.Trim();
    }

    public static void NormalizeMetricGraph(WidgetKind kind, MetricGraphWidgetSettingsConfig metricGraph)
    {
        if (!MetricGraphAggregationIntervals.Contains(metricGraph.AggregationIntervalSeconds))
        {
            metricGraph.AggregationIntervalSeconds = DefaultMetricGraphAggregationIntervalSeconds;
        }

        metricGraph.VisibleSeconds = ClampMetricGraphVisibleSeconds(metricGraph.VisibleSeconds);

        // グラフカラー。行の並び・既定の色は読み替え先のメーターと同じ。
        var source = GetMetricGraphColorDefaultSource(kind);
        metricGraph.ClassColorIndexes ??= CreateDefaultClassColorIndexes(source);
        metricGraph.ClassColorPalettes ??= CreateDefaultClassColorPalettes(source);
        (metricGraph.ClassColorIndexes, metricGraph.ClassColorPalettes) = NormalizeClassColors(
            source,
            metricGraph.ClassColorIndexes,
            metricGraph.ClassColorPalettes);
    }

    public static void NormalizeBuffCard(BuffCardWidgetSettingsConfig buffCard)
    {
        buffCard.BuffInfoFormatString ??= DefaultBuffInfoFormatString;
        buffCard.Scales ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        buffCard.Shortcuts = NormalizeBuffEntries(buffCard.Shortcuts, DefaultBuffCardShortcutEntries);
        buffCard.NotificationTargets = NormalizeBuffEntries(buffCard.NotificationTargets, DefaultBuffCardNotificationTargetEntries);

        foreach (var key in buffCard.Scales.Keys.ToList())
        {
            buffCard.Scales[key] = ClampBuffCardScale(buffCard.Scales[key]);
        }
    }

    public static void NormalizeTimeline(TimelineWidgetSettingsConfig timeline)
    {
        timeline.EnemyHealDisplayModeIndex = Math.Clamp(
            timeline.EnemyHealDisplayModeIndex,
            DefaultTimelineEnemyHealDisplayModeIndex,
            HiddenTimelineEnemyHealDisplayModeIndex);

        timeline.ClassColorIndexes ??= CreateDefaultClassColorIndexes(WidgetKind.Timeline);
        timeline.ClassColorPalettes ??= CreateDefaultClassColorPalettes(WidgetKind.Timeline);
        (timeline.ClassColorIndexes, timeline.ClassColorPalettes) = NormalizeClassColors(
            WidgetKind.Timeline,
            timeline.ClassColorIndexes,
            timeline.ClassColorPalettes);

        timeline.TextColorIndexes ??= CreateDefaultTextColorIndexes();
        timeline.TextColorPalettes ??= CreateDefaultTextColorPalettes();
        (timeline.TextColorIndexes, timeline.TextColorPalettes) = NormalizeTextColors(
            timeline.TextColorIndexes,
            timeline.TextColorPalettes);
    }

    public static void NormalizePlayerInfo(PlayerInfoWidgetSettingsConfig playerInfo)
    {
        playerInfo.ClassColorIndexes ??= CreateDefaultClassColorIndexes(WidgetKind.PlayerInfo);
        playerInfo.ClassColorPalettes ??= CreateDefaultClassColorPalettes(WidgetKind.PlayerInfo);
        (playerInfo.ClassColorIndexes, playerInfo.ClassColorPalettes) = NormalizeClassColors(
            WidgetKind.PlayerInfo,
            playerInfo.ClassColorIndexes,
            playerInfo.ClassColorPalettes);
    }

    public static int ClampBuffCardScale(int scale)
    {
        var stepped = (int)Math.Round(
            (double)scale / BuffCardScaleStep,
            MidpointRounding.AwayFromZero) * BuffCardScaleStep;

        return Math.Clamp(stepped, MinBuffCardScale, MaxBuffCardScale);
    }

    public static void NormalizeMeter(WidgetKind kind, MeterWidgetSettingsConfig meter)
    {
        meter.PlayerInfoFormatString ??= GetDefaultPlayerInfoFormatString(kind);
        meter.HealthValueDisplayModeIndex = Math.Clamp(
            meter.HealthValueDisplayModeIndex,
            DefaultHealthValueDisplayModeIndex,
            SeparateShieldHealthValueDisplayModeIndex);
        meter.PartyDisplayModeIndex = Math.Clamp(
            meter.PartyDisplayModeIndex,
            DefaultPartyDisplayModeIndex,
            MaxPartyDisplayModeIndex);
        meter.EntityDisplayModeIndex = Math.Clamp(
            meter.EntityDisplayModeIndex,
            MinEntityDisplayModeIndex,
            MaxEntityDisplayModeIndex);
        meter.SelfDisplayModeIndex = Math.Clamp(
            meter.SelfDisplayModeIndex,
            DefaultSelfDisplayModeIndex,
            MaxSelfDisplayModeIndex);
        meter.ListSortModeIndex = Math.Clamp(
            meter.ListSortModeIndex,
            FirstSeenListSortModeIndex,
            NameListSortModeIndex);
        meter.CombatStateDisplayModeIndex = Math.Clamp(
            meter.CombatStateDisplayModeIndex,
            CombatStateDisplayShowIndex,
            kind == WidgetKind.EntityList ? CombatStateDisplayHideIndex : CombatStateDisplaySelfOnlyIndex);
        meter.TargetedPlayerDisplayModeIndex = Math.Clamp(
            meter.TargetedPlayerDisplayModeIndex,
            TargetedPlayerDisplayShowIndex,
            TargetedPlayerDisplayHideIndex);
        meter.HateRankDisplayModeIndex = Math.Clamp(
            meter.HateRankDisplayModeIndex,
            0,
            HateRankDisplayCounts.Count - 1);

        meter.ClassColorOpacity = UsesMeterClassColorOpacity(kind)
            ? Math.Clamp(meter.ClassColorOpacity, MinClassColorOpacity, MaxClassColorOpacity)
            : MaxClassColorOpacity;
        meter.ClassColorIndexes ??= CreateDefaultClassColorIndexes(kind);
        meter.ClassColorPalettes ??= CreateDefaultClassColorPalettes(kind);

        // 20件ぶんのキーを必ず揃える。設定に無いスキルはそのスキルの既定値で埋める。
        var normalizedRoleSkills = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var skillId in OtherRoleSkillIds)
        {
            var key = skillId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            normalizedRoleSkills[key] =
                meter.OtherRoleSkillVisibility is not null
                && meter.OtherRoleSkillVisibility.TryGetValue(key, out var visible)
                    ? visible
                    : IsOtherRoleSkillVisibleByDefault(skillId);
        }

        meter.OtherRoleSkillVisibility = normalizedRoleSkills;

        (meter.ClassColorIndexes, meter.ClassColorPalettes) = NormalizeClassColors(
            kind,
            meter.ClassColorIndexes,
            meter.ClassColorPalettes);
    }

    /// <summary>
    /// クラスカラーの色の一覧と選んでいる枠を、<paramref name="kind"/> の職の並びと既定値で揃える。
    /// メーター系の設定とタイムラインとプレイヤー情報と推移グラフ(読み替え先のメーターの種類で呼ぶ)が使う。
    /// </summary>
    private static (Dictionary<string, int> Indexes, Dictionary<string, List<string>> Palettes) NormalizeClassColors(
        WidgetKind kind,
        Dictionary<string, int> indexes,
        Dictionary<string, List<string>> palettes)
    {
        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in GetClassColorKeys(kind))
        {
            var defaultColors = CreateDefaultClassColors(kind, key);
            var sourceColors = palettes.TryGetValue(key, out var colors)
                ? colors
                : defaultColors;
            var palette = NormalizeColorList(sourceColors, defaultColors, MaxPaletteColorCount);
            normalizedPalettes[key] = palette;

            var selectedIndex = indexes.TryGetValue(key, out var index)
                ? index
                : GetDefaultClassColorIndex(kind, key);
            normalizedIndexes[key] = Math.Clamp(selectedIndex, MinClassColorIndex, palette.Count - 1);
        }

        return (normalizedIndexes, normalizedPalettes);
    }

    /// <summary>
    /// タイムラインのテキストカラーを、鍵の並びと既定値で揃える。クラスカラーと同じ規則。
    /// </summary>
    private static (Dictionary<string, int> Indexes, Dictionary<string, List<string>> Palettes) NormalizeTextColors(
        Dictionary<string, int> indexes,
        Dictionary<string, List<string>> palettes)
    {
        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in TimelineTextColorKeys)
        {
            var defaultColors = CreateDefaultTextColors(key);
            var sourceColors = palettes.TryGetValue(key, out var colors)
                ? colors
                : defaultColors;
            var palette = NormalizeColorList(sourceColors, defaultColors, MaxPaletteColorCount);
            normalizedPalettes[key] = palette;

            var selectedIndex = indexes.TryGetValue(key, out var index)
                ? index
                : GetDefaultTextColorIndex(key);
            normalizedIndexes[key] = Math.Clamp(selectedIndex, MinClassColorIndex, palette.Count - 1);
        }

        return (normalizedIndexes, normalizedPalettes);
    }

    /// <summary>プラグインのウィジェットの設定。値の無い行を捨てる(値を定義に合わせるのは読むとき)。</summary>
    public static PluginWidgetSettingsConfig CloneNormalizedPlugin(PluginWidgetSettingsConfig? plugin)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (settingId, value) in plugin?.Values ?? [])
        {
            if (settingId is not null && value is not null)
            {
                values[settingId] = value;
            }
        }

        return new PluginWidgetSettingsConfig { Values = values };
    }

    private static List<string> NormalizeColorList(IEnumerable<string>? colors, IEnumerable<string> fallback, int maxCount)
    {
        var result = new List<string>();

        if (colors is not null)
        {
            foreach (var color in colors)
            {
                if (!TryNormalizeHexColor(color, out var normalized)
                    || result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(normalized);
                if (result.Count >= maxCount)
                {
                    break;
                }
            }
        }

        if (result.Count == 0)
        {
            foreach (var color in fallback)
            {
                if (!TryNormalizeHexColor(color, out var normalized)
                    || result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(normalized);
                if (result.Count >= maxCount)
                {
                    break;
                }
            }
        }

        if (result.Count == 0)
        {
            result.Add("#FFFFFF");
        }

        return result;
    }

    private static bool TryNormalizeHexColor(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text[1..];
        }

        if (text.Length != 6)
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        normalized = "#" + text.ToUpperInvariant();
        return true;
    }
}
