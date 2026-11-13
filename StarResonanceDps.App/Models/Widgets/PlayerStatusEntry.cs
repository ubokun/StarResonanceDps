// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// ステータス詳細の1行。
/// </summary>
/// <param name="Name">表示名。リソース <c>PlayerStatus_(番号)</c>。</param>
/// <param name="IconMask">行のアイコンの形。塗りをこの形で抜く。</param>
/// <param name="ValueText">表示する値。</param>
/// <param name="OrderUnitAttrId">
/// 並べ替えの単位(組は代表1つ)。行を掴んで動かすときの宛先。
/// </param>
/// <param name="TextBrush">
/// 行の色。アイコンの塗りと名前の2つに使う(値はウィジェットの既定の白で、変わったときだけ増減の色)。
/// 鍵の無い行は null。
/// </param>
/// <param name="ValueChange">値が差分で変わって、まだ増減の色と三角を出している間か(<see cref="PlayerStatusEntry.ValueChangeHighlightDuration"/>)。</param>
/// <param name="ValueChangeEndsAtUtc">増減の色と三角を消す時刻。<paramref name="ValueChange"/> が無いなら既定値。</param>
/// <param name="ValueChangeBrush">増減の数値と三角の色(テキストカラーの「数値の増加」「数値の減少」)。<paramref name="ValueChange"/> が無いなら null。</param>
public sealed record PlayerStatusRow(
    string Name,
    Brush? IconMask,
    string ValueText,
    int OrderUnitAttrId,
    Brush? TextBrush = null,
    StatusValueChange ValueChange = StatusValueChange.None,
    DateTime ValueChangeEndsAtUtc = default,
    Brush? ValueChangeBrush = null);

/// <summary>ステータス詳細の値の増減の表示。</summary>
public enum StatusValueChange
{
    None,
    Increase,
    Decrease,
}

/// <summary>
/// ステータス詳細の画面の1行。行の部品は使い回し、届くたびに <see cref="PlayerStatusRow"/> の値だけを入れ替える
/// (同じ値なら通知しないので、値が変わらない間は画面の部品も描画も動かない)。
/// </summary>
public sealed partial class PlayerStatusRowItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Brush? _iconMask;

    [ObservableProperty]
    private string _valueText = string.Empty;

    [ObservableProperty]
    private Brush? _textBrush;

    [ObservableProperty]
    private StatusValueChange _valueChange;

    [ObservableProperty]
    private Brush? _valueChangeBrush;

    /// <summary>並べ替えの単位(<see cref="PlayerStatusRow.OrderUnitAttrId"/>)。</summary>
    public int OrderUnitAttrId { get; private set; }

    public void Apply(PlayerStatusRow row)
    {
        Name = row.Name;
        IconMask = row.IconMask;
        ValueText = row.ValueText;
        TextBrush = row.TextBrush;
        ValueChangeBrush = row.ValueChangeBrush;
        ValueChange = row.ValueChange;
        OrderUnitAttrId = row.OrderUnitAttrId;
    }
}

/// <summary>
/// ステータス詳細の行を作る。
///
/// <para>
/// <b>出す項目と並びは下の表で決め打つ。</b>届いていない属性は値 0 で出す —
/// 値が 0 の属性はサーバが送ってこないので、届いた分だけ並べると常時 0 の項目が行ごと消える。
/// </para>
///
/// <para>
/// 筋力・知力・敏捷と、物理/魔法で対になる項目(攻撃力・防御力無視・精錬攻撃)は
/// <b>職業ごとに1つだけ</b>出す。どれを出すかは <c>ProfessionSystemTable</c> が決める
/// (直書きの分岐にするとゲームの表とずれる)。
/// 攻撃速度と詠唱速度は職業で絞らず両方出す(どちらが効くかは技ごとに決まり、両方を使う職業がある)。
/// </para>
/// </summary>
public static class PlayerStatusEntry
{
    /// <param name="AttrId">属性の番号。表示名とアイコンの鍵。</param>
    /// <param name="IsPercent">万分率か。真なら 100 で割って % を付ける。</param>
    /// <param name="IconKey">アイコンのファイル名。<c>Icon.PlayerStatus.(これ)</c> で引く。</param>
    private readonly record struct StatusRowSpec(int AttrId, bool IsPercent, string IconKey);

    /// <summary>職業で1つに絞る組。表がどれを出すかを決める。</summary>
    private static readonly int[] PrimaryStatAttrIds = [11010, 11020, 11030];
    private static readonly int[] AttackAttrIds = [11330, 11340];

    /// <summary>
    /// 攻撃力と同じ側だけ出す組。物理攻撃力の職業なら物理、魔法攻撃力の職業なら魔法。
    /// 防御力無視の番号は素の値のほうで、割合はここへ畳まれている(<see cref="PairedRows"/>)。
    /// </summary>
    private static readonly int[] DefensePenetrationAttrIds = [11370, 11380];
    private static readonly int[] RefinedAttackAttrIds = [11410, 11430];

    private const int PhysicalPenetrationAttrId = 11370;
    private const int MagicalPenetrationAttrId = 11380;
    private const int PhysicalRefinedAttackAttrId = 11410;
    private const int MagicalRefinedAttackAttrId = 11430;
    private const int MagicalAttackAttrId = 11340;

    /// <param name="AttrIds">畳む番号。並びは表示の順で、先頭の位置に出す。</param>
    /// <param name="MergedNameKey">全部同じ値のときに出す名前のリソース鍵。</param>
    private readonly record struct MergedGroupSpec(int[] AttrIds, string MergedNameKey);

    /// <summary>
    /// 値が全部同じなら1行に畳む組。
    ///
    /// <para>
    /// 移動速度の3つ(歩く・走る・ダッシュ)は名前も値も同じになることが多く、3行並べても読めない。
    /// <b>1つでも違えば3行に開く</b> — どれか1つを採って残りを捨てると、違いが出ても気づけない。
    /// </para>
    /// </summary>
    private static readonly MergedGroupSpec[] MergedGroups =
    [
        new([10200, 10210, 10220], "PlayerStatus_MoveSpeed"),
    ];

    /// <summary>主ステータスが表から引けないときに出すもの。</summary>
    private const int FallbackPrimaryStatAttrId = 11010;

    /// <summary>攻撃力が表から引けないときに出すもの。</summary>
    private const int FallbackAttackAttrId = 11330;

    /// <summary>スタミナだけは桁が違う。表示は 100 で割った値。</summary>
    private const int StaminaAttrId = 20020;

    /// <summary>届いていない属性に出す値。0 が真値なので、作った値にはならない。</summary>
    private const string MissingValueText = "0";

    /// <summary>
    /// 値が差分で変わってから、増減の色と三角を出しておく時間。新しい変化が来たらそこから数え直す。
    /// 短いとゲームから目を移す間に消え、長いとバフで数秒ごとに変わる行が光り続けて今の変化が分からなくなる。
    /// </summary>
    public static readonly TimeSpan ValueChangeHighlightDuration = TimeSpan.FromSeconds(2);

    /// <param name="Sign">1 は増、-1 は減。</param>
    /// <param name="ArrivalUtc">変えた差分の到着時刻。</param>
    private readonly record struct ValueChangeSample(int Sign, DateTime ArrivalUtc);

    /// <summary>鍵ごとのアイコンの形。読み込みと生成を1回で済ませる。</summary>
    private static readonly Dictionary<string, ImageBrush> IconMasks = new(StringComparer.Ordinal);

    /// <summary>
    /// 出す行と、その順。<b>並びはこの表がすべて</b>で、番号順ではない。
    /// 割合の行は素の値の位置へ畳まれるので(<see cref="PairedRows"/>)、
    /// 表示の並びを決めているのは素の値の側の位置。
    /// </summary>
    private static readonly StatusRowSpec[] Rows =
    [
        new(11440, false, "common_icon01"),
        new(11320, false, "common_attrmaxhp"),
        new(12790, false, "common_icon03"),
        new(12550, true, "common_icon03"),
        new(12800, false, "common_icon03"),
        new(12570, true, "common_icon03"),
        new(13000, false, "common_icon03"),
        new(13100, true, "common_icon03"),
        new(13010, false, "common_icon03"),
        new(13110, true, "common_icon03"),
        new(13020, false, "common_icon03"),
        new(13120, true, "common_icon03"),
        new(13030, false, "common_icon03"),
        new(13130, true, "common_icon03"),
        new(13040, false, "common_icon03"),
        new(13140, true, "common_icon03"),
        new(13050, false, "common_icon03"),
        new(13150, true, "common_icon03"),
        new(13060, false, "common_icon03"),
        new(13160, true, "common_icon03"),
        new(13070, false, "common_icon03"),
        new(13170, true, "common_icon03"),
        new(13080, false, "common_icon03"),
        new(13180, true, "common_icon03"),
        new(13200, false, "common_icon07"),
        new(13310, true, "common_icon07"),
        new(13210, false, "common_icon07"),
        new(13320, true, "common_icon07"),
        new(13220, false, "common_icon07"),
        new(13330, true, "common_icon07"),
        new(13230, false, "common_icon07"),
        new(13340, true, "common_icon07"),
        new(13240, false, "common_icon07"),
        new(13350, true, "common_icon07"),
        new(13250, false, "common_icon07"),
        new(13360, true, "common_icon07"),
        new(13260, false, "common_icon07"),
        new(13370, true, "common_icon07"),
        new(13270, false, "common_icon07"),
        new(13380, true, "common_icon07"),
        new(13280, false, "common_icon07"),
        new(13390, true, "common_icon07"),
        new(11330, false, "common_attrattack"),
        new(11340, false, "common_attrmattack"),
        new(11010, false, "common_icon05"),
        new(11020, false, "common_icon06"),
        new(11030, false, "common_attrdexterity"),
        new(11040, false, "common_icon08"),
        new(11110, false, "common_icon12"),
        new(11710, true, "common_icon12"),
        new(12510, true, "common_icon12"),
        new(11120, false, "common_attrhaste"),
        new(11930, true, "common_attrhaste"),
        new(11720, true, "common_icon13"),
        new(11730, true, "common_icon13"),
        new(11130, false, "common_attrluck"),
        new(11780, true, "common_attrluck"),
        new(12530, true, "common_attrluck"),
        new(11140, false, "common_attrmastery"),
        new(11940, true, "common_attrmastery"),
        new(11150, false, "common_attrversatility"),
        new(11950, true, "common_attrversatility"),
        new(11840, true, "common_icon03"),
        new(11850, true, "common_icon07"),
        new(11170, false, "common_attrblock"),
        new(11970, true, "common_attrblock"),
        new(12540, true, "common_attrblock"),
        new(11960, true, "common_icon13"),
        new(11830, true, "common_attrhit"),
        new(11350, false, "common_attrdefense"),
        new(11360, false, "common_attrmdefense"),
        new(11370, false, "common_attrhit"),
        new(11390, true, "common_attrhit"),
        new(11380, false, "common_attrhit"),
        new(11400, true, "common_attrhit"),
        new(12560, true, "common_icon07"),
        new(12580, true, "common_icon07"),
        new(11410, false, "common_attrattack"),
        new(11430, false, "common_attrmattack"),
        new(11420, false, "common_icon07"),
        new(12670, true, "common_icon03"),
        new(12680, true, "common_icon07"),
        new(12590, true, "common_icon03"),
        new(12610, true, "common_icon03"),
        new(12630, true, "common_icon03"),
        new(11460, false, "common_attrdefense"),
        new(11470, false, "common_attrhit"),
        new(11480, true, "common_attrhit"),
        new(12690, true, "common_icon03"),
        new(12700, true, "common_icon07"),
        new(11500, false, "common_icon03"),
        new(11510, false, "common_icon03"),
        new(11520, false, "common_icon03"),
        new(11530, false, "common_icon03"),
        new(11540, false, "common_icon03"),
        new(11550, false, "common_icon03"),
        new(11560, false, "common_icon03"),
        new(11570, false, "common_icon03"),
        new(11580, false, "common_icon03"),
        new(11790, false, "common_icon19"),
        new(11800, false, "common_icon19"),
        new(12740, true, "common_icon11"),
        new(12720, true, "common_attrluck"),
        new(11810, false, "common_icon14"),
        new(11820, false, "common_icon14"),
        new(11880, true, "common_icon03"),
        new(11890, true, "common_icon14"),
        new(12730, true, "common_icon03"),
        new(11990, true, "common_icon13"),
        new(10200, false, "common_icon21"),
        new(10210, false, "common_icon21"),
        new(10220, false, "common_icon21"),
        new(20020, false, "common_icon21"),
        new(20120, false, "common_icon21"),
    ];

    private static readonly Dictionary<int, StatusRowSpec> RowsByAttrId =
        Rows.ToDictionary(row => row.AttrId);

    /// <param name="ValueAttrId">素の値の番号。<b>行はこの位置に出る。</b></param>
    /// <param name="PercentAttrId">割合の番号。<b>名前とアイコンはこちらを使う。</b></param>
    private readonly record struct PairedRowSpec(int ValueAttrId, int PercentAttrId);

    /// <summary>
    /// 割合と素の値を1行にまとめる組。表示は <c>割合(素の値)</c>。
    ///
    /// <para>
    /// 同じものを2つの番号で持っている項目(会心なら 11110 と 11710)を、2行に分けずに並べる。
    /// </para>
    /// </summary>
    private static readonly PairedRowSpec[] PairedRows =
    [
        new(11110, 11710),
        new(11120, 11930),
        new(11130, 11780),
        new(11140, 11940),
        new(11150, 11950),
        new(11170, 11970),
        new(11370, 11390),
        new(11380, 11400),
        new(11470, 11480),
        new(12790, 12550),
        new(12800, 12570),
        new(13000, 13100),
        new(13010, 13110),
        new(13020, 13120),
        new(13030, 13130),
        new(13040, 13140),
        new(13050, 13150),
        new(13060, 13160),
        new(13070, 13170),
        new(13080, 13180),
        new(13200, 13310),
        new(13210, 13320),
        new(13220, 13330),
        new(13230, 13340),
        new(13240, 13350),
        new(13250, 13360),
        new(13260, 13370),
        new(13270, 13380),
        new(13280, 13390),
    ];

    /// <summary>
    /// 設定の一覧に出す行。並びは表示の順で、<b>職業で片方だけ出る組も1件ずつ並ぶ</b>
    /// (物理攻撃力と魔法攻撃力は別の行)。割合の行と、畳む組の2件目以降は代表の位置で出るので入れない。
    /// </summary>
    public static IReadOnlyList<int> SettingRowAttrIds { get; } = BuildSettingRowAttrIds();

    /// <summary>職業で1つに絞る組。<b>並べ替えでは組ごと動く</b>ので、代表は各組の先頭。</summary>
    private static readonly int[][] OrderGroupAttrIds =
    [
        PrimaryStatAttrIds,
        AttackAttrIds,
        DefensePenetrationAttrIds,
        RefinedAttackAttrIds,
    ];

    /// <summary>
    /// 並べ替えの単位。<b>組は代表1つにまとめた78件</b>で、並びが既定の表示順。
    /// 設定の一覧(<see cref="SettingRowAttrIds"/>)と違い、職業で片方だけ出る組も1件にする。
    /// </summary>
    public static IReadOnlyList<int> OrderUnitAttrIds { get; } = BuildOrderUnitAttrIds();

    /// <summary>その番号が属する並べ替えの単位。割合の行と組の2件目以降は代表へ寄せる。</summary>
    public static int GetOrderUnitAttrId(int attrId)
    {
        if (TryGetPairedRow(attrId, out var paired))
        {
            attrId = paired.ValueAttrId;
        }

        if (TryGetMergedGroup(attrId, out var merged))
        {
            return merged.AttrIds[0];
        }

        foreach (var group in OrderGroupAttrIds)
        {
            if (Array.IndexOf(group, attrId) >= 0)
            {
                return group[0];
            }
        }

        return attrId;
    }

    /// <summary>既定でオンにする行。残りはオフで、ユーザーが設定で出す。</summary>
    private static readonly int[] DefaultVisibleAttrIds =
    [
        11010, 11020, 11030,
        11110, 11120, 11130, 11140, 11150, 11170,
        11330, 11340,
        11720, 11730,
        11840, 11850,
        12510, 12530, 12540,
        12790, 12800,
        13010, 13020, 13030, 13040, 13050, 13060, 13070, 13080,
        13210, 13220, 13230, 13240, 13250, 13260, 13270, 13280,
    ];

    public static bool IsRowVisibleByDefault(int attrId)
    {
        return Array.IndexOf(DefaultVisibleAttrIds, attrId) >= 0;
    }

    /// <summary>
    /// 「無効なステータス効果を隠す」の対象。物理増強から闇属性軽減までの20行で、
    /// 番号は素の値のほう — 割合は <see cref="PairedRows"/> でこの位置へ畳まれている。
    /// </summary>
    private static readonly int[] HideWhenZeroAttrIds =
    [
        12790, 12800,
        13000, 13010, 13020, 13030, 13040, 13050, 13060, 13070, 13080,
        13200, 13210, 13220, 13230, 13240, 13250, 13260, 13270, 13280,
    ];

    /// <summary>行の表示名のリソース鍵。設定の一覧もステータス詳細と同じ名前を出す。</summary>
    public static string GetRowNameKey(int attrId)
    {
        return TryGetMergedGroup(attrId, out var group) && attrId == group.AttrIds[0]
            ? group.MergedNameKey
            : $"PlayerStatus_{ResolveNameAttrId(attrId)}";
    }

    /// <summary>行のアイコンの形。</summary>
    public static Brush? GetRowIconMask(int attrId)
    {
        return ResolveIconMask(RowsByAttrId[ResolveNameAttrId(attrId)].IconKey);
    }

    /// <summary>
    /// 属性のアイコンの形。ステータス詳細の外(装備の TIPS)から引く。
    /// <b>表に無い番号は <c>null</c></b>(アイコン無し)。
    /// </summary>
    public static Brush? FindRowIconMask(int attrId)
    {
        return RowsByAttrId.TryGetValue(ResolveNameAttrId(attrId), out var spec)
            ? ResolveIconMask(spec.IconKey)
            : null;
    }

    /// <summary>
    /// 表の絵のパス(<c>ui/atlas/…/common_icon01</c> など)の最後の名前で、ステータスのアイコンの形を引く。
    /// パスが空か、同じ名前の絵が無ければ <c>null</c>。
    /// </summary>
    public static Brush? FindIconMaskByName(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        var slash = iconPath.LastIndexOf('/');
        return ResolveIconMask(slash >= 0 ? iconPath[(slash + 1)..] : iconPath);
    }

    /// <param name="player">行を作る相手。</param>
    /// <param name="hideInactiveStatusEffects">
    /// <see cref="HideWhenZeroAttrIds"/> の行のうち、値が 0 のものを出さないか。
    /// </param>
    /// <param name="showActualValues">
    /// 割合と実数値を1行にまとめた行(<see cref="PairedRows"/>)で、括弧の実数値を出すか。偽なら割合だけ。
    /// </param>
    /// <param name="rowVisibility">
    /// 行ごとのオン/オフ(鍵は番号)。<b>オフの行は無条件で出さない。</b>
    /// 一覧に無い番号(割合の行など)は代表の位置で判定されるので見ない。
    /// </param>
    /// <param name="textBrushes">
    /// 行の色。鍵は設定の一覧の番号(<see cref="SettingRowAttrIds"/>)。
    /// 鍵の無い行は色を割り当てない(表示側の既定色になる)。
    /// </param>
    /// <param name="rowOrder">
    /// 並べ替えの単位の並び(<see cref="OrderUnitAttrIds"/> の番号)。
    /// <b>隠れている行も含めた全体の順</b>で、空なら表の既定順。
    /// </param>
    /// <param name="nowUtc">増減の色と三角を出している間かを決める今の時刻。</param>
    /// <param name="increaseBrush">値が増えた行の数値と三角の色(テキストカラーの「数値の増加」)。</param>
    /// <param name="decreaseBrush">値が減った行の数値と三角の色(テキストカラーの「数値の減少」)。</param>
    /// <param name="hasEnterSceneAttributes">
    /// 入場の全属性を受けたか。受けた後に届いていない能力値は本当に 0(全属性は値が 0 の属性を含まない)。
    /// 受ける前(途中起動)に届いていない値は分からないので「不明」と出す。
    /// </param>
    public static IReadOnlyList<PlayerStatusRow> Create(
        PlayerRosterEntry player,
        bool hideInactiveStatusEffects,
        bool showActualValues,
        IReadOnlyDictionary<string, bool> rowVisibility,
        IReadOnlyDictionary<int, Brush> textBrushes,
        IReadOnlyList<int> rowOrder,
        DateTime nowUtc,
        Brush? increaseBrush,
        Brush? decreaseBrush,
        bool hasEnterSceneAttributes)
    {
        var attributes = player.Attributes;
        if (attributes is null || attributes.Count == 0)
        {
            return [];
        }

        var localization = LocalizationManager.Instance;
        var unknownValueText = hasEnterSceneAttributes ? null : localization.GetString("PlayerInfo_Unknown");

        var arrived = new Dictionary<int, string>(attributes.Count);
        var changes = new Dictionary<int, ValueChangeSample>();
        foreach (var attribute in attributes)
        {
            arrived[attribute.AttrId] = attribute.ValueText;
            if (attribute.ChangeSign != 0)
            {
                changes[attribute.AttrId] = new ValueChangeSample(attribute.ChangeSign, attribute.ChangedAtUtc);
            }
        }

        var shownPrimaryStat = ResolveShown(
            CombatDataCatalog.GetProfessionPrimaryStatAttrId(player.ProfessionId),
            PrimaryStatAttrIds,
            FallbackPrimaryStatAttrId);
        var shownAttack = ResolveShown(
            CombatDataCatalog.GetProfessionAttackAttrId(player.ProfessionId),
            AttackAttrIds,
            FallbackAttackAttrId);

        var isMagical = shownAttack == MagicalAttackAttrId;
        var shownPenetration = isMagical ? MagicalPenetrationAttrId : PhysicalPenetrationAttrId;
        var shownRefinedAttack = isMagical ? MagicalRefinedAttackAttrId : PhysicalRefinedAttackAttrId;

        var rows = new List<PlayerStatusRow>(Rows.Length);
        var rowKeys = new List<int>(Rows.Length);
        foreach (var spec in OrderRows(rowOrder))
        {
            if (IsHidden(spec.AttrId, PrimaryStatAttrIds, shownPrimaryStat)
                || IsHidden(spec.AttrId, AttackAttrIds, shownAttack)
                || IsHidden(spec.AttrId, DefensePenetrationAttrIds, shownPenetration)
                || IsHidden(spec.AttrId, RefinedAttackAttrIds, shownRefinedAttack))
            {
                continue;
            }

            if (rowVisibility.TryGetValue(spec.AttrId.ToString(CultureInfo.InvariantCulture), out var rowVisible)
                && !rowVisible)
            {
                continue;
            }

            if (hideInactiveStatusEffects && IsInactive(spec.AttrId, arrived))
            {
                continue;
            }

            if (TryGetPairedRow(spec.AttrId, out var paired))
            {
                // 割合側は素の値の位置でまとめて出すので、ここでは行にしない。
                // 増減は見えている数字だけで決める(実数値を出さないなら割合だけ。同時なら先に並ぶ割合)。
                if (spec.AttrId == paired.ValueAttrId)
                {
                    rows.Add(ApplyValueChange(
                        CreatePairedRow(paired, arrived, showActualValues, localization, unknownValueText),
                        showActualValues ? [paired.PercentAttrId, paired.ValueAttrId] : [paired.PercentAttrId],
                        changes,
                        nowUtc));
                    rowKeys.Add(paired.ValueAttrId);
                }

                continue;
            }

            if (TryGetMergedGroup(spec.AttrId, out var group))
            {
                // 組は先頭の位置でまとめて出す。残りの番号はここでは何もしない。
                if (spec.AttrId == group.AttrIds[0])
                {
                    AppendMergedGroup(rows, rowKeys, group, spec, arrived, changes, nowUtc, localization, unknownValueText);
                }

                continue;
            }

            rows.Add(ApplyValueChange(CreateRow(spec, spec.AttrId, arrived, localization, unknownValueText), [spec.AttrId], changes, nowUtc));
            rowKeys.Add(spec.AttrId);
        }

        // 色は行ごとに決まっている。鍵の無い行はそのまま(表示側の既定色)。増減の色は向きで決まる。
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (textBrushes.TryGetValue(rowKeys[index], out var brush))
            {
                row = row with { TextBrush = brush };
            }

            row = row.ValueChange switch
            {
                StatusValueChange.Increase => row with { ValueChangeBrush = increaseBrush },
                StatusValueChange.Decrease => row with { ValueChangeBrush = decreaseBrush },
                _ => row
            };

            rows[index] = row;
        }

        return rows;
    }

    /// <summary>
    /// <see cref="HideWhenZeroAttrIds"/> の行が効いていない(値が 0)か。畳む組は、組の全部が 0 のときだけ真 —
    /// 1つでも値があるなら、畳めずに並ぶ行から一部だけ消えないようにする。
    /// </summary>
    private static bool IsInactive(int attrId, Dictionary<int, string> arrived)
    {
        if (Array.IndexOf(HideWhenZeroAttrIds, attrId) < 0)
        {
            return false;
        }

        if (TryGetPairedRow(attrId, out var paired))
        {
            return IsZero(arrived.GetValueOrDefault(paired.ValueAttrId))
                && IsZero(arrived.GetValueOrDefault(paired.PercentAttrId));
        }

        if (TryGetMergedGroup(attrId, out var group))
        {
            foreach (var groupAttrId in group.AttrIds)
            {
                if (!IsZero(arrived.GetValueOrDefault(groupAttrId)))
                {
                    return false;
                }
            }

            return true;
        }

        return IsZero(arrived.GetValueOrDefault(attrId));
    }

    /// <summary>届いていない、または 0。数値として読めない値は 0 と見なさない。</summary>
    private static bool IsZero(string? arrivedText)
    {
        return string.IsNullOrEmpty(arrivedText)
            || (long.TryParse(arrivedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                && value == 0);
    }

    /// <summary>並べ替えの単位を、既定の表示順のまま拾う。</summary>
    private static int[] BuildOrderUnitAttrIds()
    {
        var units = new List<int>(SettingRowAttrIds.Count);
        var seen = new HashSet<int>();
        foreach (var attrId in SettingRowAttrIds)
        {
            var unit = GetOrderUnitAttrId(attrId);
            if (seen.Add(unit))
            {
                units.Add(unit);
            }
        }

        return [.. units];
    }

    /// <summary>保存された並びのとおりに <see cref="Rows"/> を並べ替える(<see cref="OrderByUnits"/>)。</summary>
    private static IReadOnlyList<StatusRowSpec> OrderRows(IReadOnlyList<int> rowOrder)
    {
        return rowOrder.Count == 0 ? Rows : OrderByUnits(Rows, spec => spec.AttrId, rowOrder);
    }

    /// <summary>
    /// 設定の一覧の番号(<see cref="SettingRowAttrIds"/>)を、行の並び(ウィジェットか設定の一覧で並べ替えたもの)のとおりに並べる。
    /// ウィジェットの行と同じ決まり(<see cref="OrderByUnits"/>)で、組の項目はまとまって動き、組の中は既定の順。
    /// </summary>
    public static IReadOnlyList<int> OrderSettingRowAttrIds(IReadOnlyList<int> rowOrder)
    {
        return rowOrder.Count == 0
            ? SettingRowAttrIds
            : OrderByUnits(SettingRowAttrIds, attrId => attrId, rowOrder);
    }

    /// <summary>
    /// 並べ替えの単位(<see cref="GetOrderUnitAttrId"/>)ごとにまとめ、保存された並びの順に並べる。単位の中は渡された順のまま。
    /// 並びに無い単位(表が増えたとき)は既定の順で後ろへ回し、<b>落とさない</b>。
    /// </summary>
    private static List<T> OrderByUnits<T>(IReadOnlyList<T> items, Func<T, int> attrIdOf, IReadOnlyList<int> rowOrder)
    {
        var byUnit = new Dictionary<int, List<T>>(OrderUnitAttrIds.Count);
        foreach (var item in items)
        {
            var unit = GetOrderUnitAttrId(attrIdOf(item));
            if (!byUnit.TryGetValue(unit, out var list))
            {
                list = [];
                byUnit[unit] = list;
            }

            list.Add(item);
        }

        var ordered = new List<T>(items.Count);
        var used = new HashSet<int>();
        foreach (var unit in rowOrder)
        {
            if (used.Add(unit) && byUnit.TryGetValue(unit, out var list))
            {
                ordered.AddRange(list);
            }
        }

        foreach (var unit in OrderUnitAttrIds)
        {
            if (used.Add(unit) && byUnit.TryGetValue(unit, out var list))
            {
                ordered.AddRange(list);
            }
        }

        return ordered;
    }

    /// <summary>設定の一覧に出す番号を、表示の順のまま拾う。</summary>
    private static int[] BuildSettingRowAttrIds()
    {
        var percentAttrIds = new HashSet<int>();
        foreach (var paired in PairedRows)
        {
            percentAttrIds.Add(paired.PercentAttrId);
        }

        var attrIds = new List<int>(Rows.Length);
        foreach (var spec in Rows)
        {
            if (percentAttrIds.Contains(spec.AttrId))
            {
                continue;
            }

            if (TryGetMergedGroup(spec.AttrId, out var group) && spec.AttrId != group.AttrIds[0])
            {
                continue;
            }

            attrIds.Add(spec.AttrId);
        }

        return attrIds.ToArray();
    }

    /// <summary>名前とアイコンを引く番号。割合と素の値の組は割合の側。</summary>
    private static int ResolveNameAttrId(int attrId)
    {
        return TryGetPairedRow(attrId, out var paired) && attrId == paired.ValueAttrId
            ? paired.PercentAttrId
            : attrId;
    }

    /// <summary>その番号が、割合と素の値を1行にする組に入っているか。</summary>
    private static bool TryGetPairedRow(int attrId, out PairedRowSpec paired)
    {
        foreach (var candidate in PairedRows)
        {
            if (candidate.ValueAttrId == attrId || candidate.PercentAttrId == attrId)
            {
                paired = candidate;
                return true;
            }
        }

        paired = default;
        return false;
    }

    /// <summary>
    /// 割合と素の値をまとめた1行。表示は <c>割合(素の値)</c>(設定「実数値を表示」がオフなら割合だけ)で、名前とアイコンは割合のほう。
    /// </summary>
    private static PlayerStatusRow CreatePairedRow(
        PairedRowSpec paired,
        Dictionary<int, string> arrived,
        bool showActualValues,
        LocalizationManager localization,
        string? unknownValueText)
    {
        var percentSpec = RowsByAttrId[paired.PercentAttrId];
        var valueSpec = RowsByAttrId[paired.ValueAttrId];

        var percentArrived = arrived.GetValueOrDefault(paired.PercentAttrId);
        var valueArrived = arrived.GetValueOrDefault(paired.ValueAttrId);
        var percentText = FormatValue(percentSpec, percentArrived, unknownValueText);

        // 両方とも分からなければ「不明」を1つだけ出す。
        var text = showActualValues
            && !(unknownValueText is not null && string.IsNullOrEmpty(percentArrived) && string.IsNullOrEmpty(valueArrived))
            ? $"{percentText}({FormatValue(valueSpec, valueArrived, unknownValueText)})"
            : percentText;

        return new PlayerStatusRow(
            localization.GetString($"PlayerStatus_{paired.PercentAttrId}"),
            ResolveIconMask(percentSpec.IconKey),
            text,
            GetOrderUnitAttrId(paired.ValueAttrId));
    }

    /// <summary>その番号が、値の一致で畳む組に入っているか。</summary>
    private static bool TryGetMergedGroup(int attrId, out MergedGroupSpec group)
    {
        foreach (var candidate in MergedGroups)
        {
            if (Array.IndexOf(candidate.AttrIds, attrId) >= 0)
            {
                group = candidate;
                return true;
            }
        }

        group = default;
        return false;
    }

    /// <summary>
    /// 畳む組を出す。値が全部同じなら1行、1つでも違えば組の全部を行にする。
    /// アイコンと単位は組の先頭の行のものを使う(組の中では同じ)。
    /// </summary>
    private static void AppendMergedGroup(
        List<PlayerStatusRow> rows,
        List<int> rowKeys,
        MergedGroupSpec group,
        StatusRowSpec spec,
        Dictionary<int, string> arrived,
        Dictionary<int, ValueChangeSample> changes,
        DateTime nowUtc,
        LocalizationManager localization,
        string? unknownValueText)
    {
        var first = arrived.GetValueOrDefault(group.AttrIds[0]) ?? MissingValueText;
        var allSame = true;
        foreach (var attrId in group.AttrIds)
        {
            if ((arrived.GetValueOrDefault(attrId) ?? MissingValueText) != first)
            {
                allSame = false;
                break;
            }
        }

        if (allSame)
        {
            rows.Add(ApplyValueChange(
                new PlayerStatusRow(
                    localization.GetString(group.MergedNameKey),
                    ResolveIconMask(spec.IconKey),
                    FormatValue(spec, arrived.GetValueOrDefault(group.AttrIds[0]), unknownValueText),
                    GetOrderUnitAttrId(group.AttrIds[0])),
                group.AttrIds,
                changes,
                nowUtc));
            rowKeys.Add(group.AttrIds[0]);
            return;
        }

        foreach (var attrId in group.AttrIds)
        {
            rows.Add(ApplyValueChange(CreateRow(spec, attrId, arrived, localization, unknownValueText), [attrId], changes, nowUtc));

            // 畳めずに開いた行も、色は組の代表のものを使う(一覧には代表しか無い)。
            rowKeys.Add(group.AttrIds[0]);
        }
    }

    /// <summary>
    /// 行の数字に使った番号のうち、差分で一番新しく変わったものの向きを行に付ける(同時なら先に並べた番号)。
    /// 変わってから <see cref="ValueChangeHighlightDuration"/> を過ぎていたら付けない。
    /// </summary>
    private static PlayerStatusRow ApplyValueChange(
        PlayerStatusRow row,
        ReadOnlySpan<int> shownAttrIds,
        Dictionary<int, ValueChangeSample> changes,
        DateTime nowUtc)
    {
        ValueChangeSample? latest = null;
        foreach (var attrId in shownAttrIds)
        {
            if (changes.TryGetValue(attrId, out var change)
                && (latest is null || change.ArrivalUtc > latest.Value.ArrivalUtc))
            {
                latest = change;
            }
        }

        if (latest is not { } shown)
        {
            return row;
        }

        var endsAtUtc = shown.ArrivalUtc + ValueChangeHighlightDuration;
        if (endsAtUtc <= nowUtc)
        {
            return row;
        }

        return row with
        {
            ValueChange = shown.Sign > 0 ? StatusValueChange.Increase : StatusValueChange.Decrease,
            ValueChangeEndsAtUtc = endsAtUtc,
        };
    }

    /// <summary>1行を作る。名前は番号ごとのリソース。</summary>
    private static PlayerStatusRow CreateRow(
        StatusRowSpec spec,
        int attrId,
        Dictionary<int, string> arrived,
        LocalizationManager localization,
        string? unknownValueText)
    {
        return new PlayerStatusRow(
            localization.GetString($"PlayerStatus_{attrId}"),
            ResolveIconMask(spec.IconKey),
            FormatValue(spec, arrived.GetValueOrDefault(attrId), unknownValueText),
            GetOrderUnitAttrId(attrId));
    }

    /// <summary>
    /// 職業が出す番号。表の値がその組に無い(行が無い・別の番号)なら既定へ落とす。
    /// </summary>
    private static int ResolveShown(int fromTable, int[] group, int fallback)
    {
        return Array.IndexOf(group, fromTable) >= 0 ? fromTable : fallback;
    }

    /// <summary>1つに絞る組のうち、その職業では出さないものか。</summary>
    private static bool IsHidden(int attrId, int[] group, int shown)
    {
        return Array.IndexOf(group, attrId) >= 0 && attrId != shown;
    }

    /// <summary>
    /// 値の表示。届いていなければ 0(入場の全属性を受ける前は <paramref name="unknownValueText"/>)。
    /// 万分率は 100 で割って % を付け、スタミナは 100 で割る。数値として読めない値(一覧・構造体)は、届いたままを出す。
    /// </summary>
    /// <param name="unknownValueText">入場の全属性を受ける前の、届いていない値の表示(「不明」)。受けた後は null。</param>
    private static string FormatValue(StatusRowSpec spec, string? arrivedText, string? unknownValueText)
    {
        if (string.IsNullOrEmpty(arrivedText) && unknownValueText is not null)
        {
            return unknownValueText;
        }

        var text = string.IsNullOrEmpty(arrivedText) ? MissingValueText : arrivedText;
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return text;
        }

        if (spec.IsPercent)
        {
            return (value / 100d).ToString("0.##", CultureInfo.CurrentCulture) + "%";
        }

        if (spec.AttrId == StaminaAttrId)
        {
            return (value / 100d).ToString("0.##", CultureInfo.CurrentCulture);
        }

        // 桁区切りは入れない。
        return value.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>行のアイコン。同梱していない鍵は <c>null</c>(欄は空になる)。</summary>
    /// <summary>
    /// アイコンの形。塗りをこの形で抜く(クラスアイコンと同じ染め方)。
    /// <see cref="Freezable.Freeze"/> して鍵ごとに使い回す。
    /// </summary>
    private static Brush? ResolveIconMask(string iconKey)
    {
        if (string.IsNullOrEmpty(iconKey))
        {
            return null;
        }

        if (IconMasks.TryGetValue(iconKey, out var mask))
        {
            return mask;
        }

        if (Application.Current?.TryFindResource($"Icon.PlayerStatus.{iconKey}") is not ImageSource source)
        {
            return null;
        }

        mask = new ImageBrush(source) { Stretch = Stretch.Uniform };
        mask.Freeze();
        IconMasks[iconKey] = mask;
        return mask;
    }
}
