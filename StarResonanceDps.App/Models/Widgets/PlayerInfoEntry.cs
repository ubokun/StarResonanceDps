// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// プレイヤー情報の表示値。名前・UID・5つの文字の行(冒険者レベル / シーズン階級 / 現在地 / 能力スコア / シーズン強度)と、
/// 下に並べる3つのバッジ(シーズン / クラスと特化 / 潜在心相晶)。
/// バッジの絵の色はプレイヤー情報の設定のアイコンカラー。
/// 値で比べる。前回と同じ値なら表示を差し替えないので、絵の形と塗りは使い回す(毎回作ると、値が同じでも差し替えになる)。
/// </summary>
public sealed record PlayerInfoEntry
{
    /// <summary>アイコンカラーの不明の鍵。バッジ3つのはてなを1色で塗る。</summary>
    private const string UnknownColorKey = "Unknown";

    private static readonly Dictionary<string, Brush> IconMasks = new(StringComparer.Ordinal);

    private PlayerInfoEntry()
    {
    }

    public string NameText { get; private init; } = string.Empty;

    public string UidText { get; private init; } = string.Empty;

    /// <summary>冒険者レベル(+シーズンレベル)。</summary>
    public string LevelText { get; private init; } = string.Empty;

    /// <summary>シーズン階級(シーズンランクの名前)。段階か今のシーズンが分からなければ不明(名前はシーズンごとに違う)。</summary>
    public string SeasonRankText { get; private init; } = string.Empty;

    public string AbilityScoreText { get; private init; } = string.Empty;

    public string SeasonStrengthText { get; private init; } = string.Empty;

    /// <summary>現在地。自分と、いま周りに見えている人(自分と同じ場所)と、パーティのメンバー(パーティ情報のいる場所)だけ分かる。</summary>
    public string LocationText { get; private init; } = string.Empty;

    /// <summary>クラスアイコンの形。プレイヤーリストと同じ絵。</summary>
    public Brush? ClassIconMask { get; private init; }

    public bool HasClassIcon => ClassIconMask is not null;

    /// <summary>クラスアイコンの色。職が分からなければ不明の色。</summary>
    public Brush? ClassIconBrush { get; private init; }

    /// <summary>クラスアイコンの下の特化名。クラスR2は共通の語尾つき。</summary>
    public string ClassSpecText { get; private init; } = string.Empty;

    /// <summary>潜在心相晶の絵。型の絵、無効なら無効の絵、不明と表に無い型(絵が無い型)ははてな。</summary>
    public Brush? SeasonTalentIconMask { get; private init; }

    public bool HasSeasonTalentIcon => SeasonTalentIconMask is not null;

    /// <summary>潜在心相晶の絵の色。型・無効・不明の色。</summary>
    public Brush? SeasonTalentIconBrush { get; private init; }

    /// <summary>潜在心相晶の名前(型の名前 / 心相晶無効 / 不明)。無効の文字だけ書式の <c>{Psych}</c>(無効)と違う。</summary>
    public string SeasonTalentText { get; private init; } = string.Empty;

    /// <summary>今のシーズンの絵の形。表に無いシーズンか、リソースが見つからなければ無し。</summary>
    public Brush? SeasonIconMask { get; private init; }

    public bool HasSeasonIcon => SeasonIconMask is not null;

    /// <summary>今のシーズンの絵の色。</summary>
    public Brush? SeasonIconBrush { get; private init; }

    /// <summary>シーズンのはてなの色(不明の色)。</summary>
    public Brush? UnknownIconBrush { get; private init; }

    /// <summary>今のシーズンがまだ届いていない(はてなを出す)。</summary>
    public bool IsSeasonUnknown { get; private init; }

    public string SeasonText { get; private init; } = string.Empty;

    /// <param name="mapName">一覧の通知のマップ名(自分のいる場所)。</param>
    /// <param name="mapChannel">一覧の通知のチャンネル番号。</param>
    /// <param name="seasonId">今のシーズン番号。まだ分からなければ 0。</param>
    /// <param name="iconPalette">アイコンカラーの塗り。</param>
    /// <param name="isProfileUnknown">
    /// 冒険者レベル・シーズンレベル・能力スコア・シーズン強度がまだ届いていない(0 のまま)。「不明」と出す。
    /// </param>
    public static PlayerInfoEntry Create(
        PlayerRosterEntry player,
        string mapName,
        uint mapChannel,
        int seasonId,
        PlayerInfoIconPalette iconPalette,
        bool isProfileUnknown)
    {
        var localization = LocalizationManager.Instance;
        var unknownText = localization.GetString("PlayerInfo_Unknown");
        var nameDisplayMode = PlayerRosterPresentationStore.Instance.NameDisplayMode;

        // 名前を伏せる設定ならUIDも伏せる(名簿の伏せ方と同じ)。
        var uid = PlayerInfoFormatFormatter.ShouldHideName(player.IsSelf, nameDisplayMode)
            ? PlayerRosterPresentationStore.HiddenPlayerName
            : player.CharacterId.ToString(CultureInfo.InvariantCulture);

        // 周りに見えている人(自分を含む)は自分と同じ場所。周りにいないパーティのメンバーはパーティ情報のいる場所
        // (ダンジョンの難易度は届かないので難易度なしで引く)。どちらでもなければ分からない。
        var location = player.IsNearby
            ? WidgetListItemViewModel.FormatMapName(mapName, mapChannel)
            : player.PartySceneId > 0
                ? WidgetListItemViewModel.FormatMapName(
                    CombatDataCatalog.GetSceneName(player.PartySceneId, 0),
                    (uint)Math.Max(player.PartyLineId, 0))
                : string.Empty;

        var season = CreateSeasonBadge(seasonId, unknownText, iconPalette);
        // シーズン階級の名前はシーズンごとに違うので、段階か今のシーズンのどちらかが分からなければ不明。
        var seasonRankName = season.IsUnknown || player.SeasonRankLevel is null
            ? unknownText
            : CombatDataCatalog.GetSeasonRankName(seasonId, player.SeasonRankLevel.Value);
        var classKey = PlayerProfession.GetKey(player.ProfessionId, player.ClassSpec);
        var seasonTalent = ResolveSeasonTalentIcon(player.SeasonTalentBuffId, player.IsSeasonTalentInactive);

        return new PlayerInfoEntry
        {
            NameText = PlayerInfoFormatFormatter.GetDisplayName(
                player.Name,
                player.CharacterId,
                player.IsSelf,
                player.IsNpc,
                player.ProfessionId,
                nameDisplayMode),
            UidText = $"{localization.GetString("PlayerInfo_Uid")}: {uid}",
            LevelText = $"{localization.GetString("PlayerInfo_Level")}: {(isProfileUnknown ? unknownText : $"{FormatInteger(player.Level)}(+{FormatInteger(player.SeasonLevel)})")}",
            SeasonRankText = $"{localization.GetString("PlayerInfo_SeasonRank")}: {seasonRankName}",
            AbilityScoreText = $"{localization.GetString("PlayerInfo_AbilityScore")}: {(isProfileUnknown ? unknownText : FormatInteger(player.CombatPower))}",
            SeasonStrengthText = $"{localization.GetString("PlayerInfo_SeasonStrength")}: {(isProfileUnknown ? unknownText : FormatInteger(player.SeasonStrength))}",
            LocationText = $"{localization.GetString("PlayerInfo_Location")}: {(string.IsNullOrEmpty(location) ? unknownText : location)}",
            ClassIconMask = ResolveMask($"Icon.Profession.{classKey}"),
            ClassIconBrush = iconPalette.GetBrush(classKey),
            ClassSpecText = GetProfessionSpecDisplayName(player.ClassSpec, localization),
            SeasonTalentIconMask = seasonTalent.Mask,
            SeasonTalentIconBrush = iconPalette.GetBrush(seasonTalent.ColorKey),
            // 無効はプレイヤー情報だけ「心相晶無効」と出す(書式の {Psych} とプレイヤーリストは「無効」)。
            SeasonTalentText = player.SeasonTalentBuffId <= 0 && player.IsSeasonTalentInactive
                ? localization.GetString("PlayerInfo_SeasonTalentInactive")
                : PlayerInfoFormatFormatter.GetSeasonTalentText(player.SeasonTalentBuffId, player.IsSeasonTalentInactive),
            SeasonIconMask = season.Mask,
            SeasonIconBrush = season.Brush,
            IsSeasonUnknown = season.IsUnknown,
            SeasonText = season.Text,
            UnknownIconBrush = iconPalette.GetBrush(UnknownColorKey)
        };
    }

    /// <summary>
    /// 相手がまだ一覧に載っていないときの表示値。窓が覚えている素性(保存から戻した名前と UID を含む)だけを出し、
    /// ほかの値は「不明」。今のシーズンは相手に依らないので、分かれば出す。
    /// </summary>
    /// <param name="name">覚えている名前。無ければ空。</param>
    /// <param name="uid">覚えている UID。無ければ 0。</param>
    /// <param name="seasonId">今のシーズン番号。まだ分からなければ 0。</param>
    /// <param name="iconPalette">アイコンカラーの塗り。</param>
    public static PlayerInfoEntry CreateUnknown(
        string? name,
        long uid,
        bool isSelf,
        bool isNpc,
        int professionId,
        int seasonId,
        PlayerInfoIconPalette iconPalette)
    {
        var localization = LocalizationManager.Instance;
        var unknownText = localization.GetString("PlayerInfo_Unknown");
        var nameDisplayMode = PlayerRosterPresentationStore.Instance.NameDisplayMode;
        var isIdentityUnknown = string.IsNullOrWhiteSpace(name) && uid == 0;

        // 名前を伏せる設定ならUIDも伏せる(名簿の伏せ方と同じ)。
        var uidText = PlayerInfoFormatFormatter.ShouldHideName(isSelf, nameDisplayMode)
            ? PlayerRosterPresentationStore.HiddenPlayerName
            : uid != 0
                ? uid.ToString(CultureInfo.InvariantCulture)
                : unknownText;

        var season = CreateSeasonBadge(seasonId, unknownText, iconPalette);
        var classKey = PlayerProfession.GetKey(professionId);
        var seasonTalent = ResolveSeasonTalentIcon(0, isInactive: false);

        return new PlayerInfoEntry
        {
            NameText = isIdentityUnknown
                ? unknownText
                : PlayerInfoFormatFormatter.GetDisplayName(name, uid, isSelf, isNpc, professionId, nameDisplayMode),
            UidText = $"{localization.GetString("PlayerInfo_Uid")}: {uidText}",
            LevelText = $"{localization.GetString("PlayerInfo_Level")}: {unknownText}",
            SeasonRankText = $"{localization.GetString("PlayerInfo_SeasonRank")}: {unknownText}",
            AbilityScoreText = $"{localization.GetString("PlayerInfo_AbilityScore")}: {unknownText}",
            SeasonStrengthText = $"{localization.GetString("PlayerInfo_SeasonStrength")}: {unknownText}",
            LocationText = $"{localization.GetString("PlayerInfo_Location")}: {unknownText}",
            ClassIconMask = ResolveMask($"Icon.Profession.{classKey}"),
            ClassIconBrush = iconPalette.GetBrush(classKey),
            ClassSpecText = localization.GetString("ClassSpec_Unknown"),
            SeasonTalentIconMask = seasonTalent.Mask,
            SeasonTalentIconBrush = iconPalette.GetBrush(seasonTalent.ColorKey),
            SeasonTalentText = PlayerInfoFormatFormatter.GetSeasonTalentText(0, isSeasonTalentInactive: false),
            SeasonIconMask = season.Mask,
            SeasonIconBrush = season.Brush,
            IsSeasonUnknown = season.IsUnknown,
            SeasonText = season.Text,
            UnknownIconBrush = iconPalette.GetBrush(UnknownColorKey)
        };
    }

    /// <summary>シーズンのバッジ。今のシーズンで決まり、相手には依らない。まだ届いていなければはてなと「不明」。</summary>
    private static (Brush? Mask, Brush? Brush, bool IsUnknown, string Text) CreateSeasonBadge(
        int seasonId,
        string unknownText,
        PlayerInfoIconPalette iconPalette)
    {
        var isUnknown = seasonId <= 0;
        return (
            isUnknown ? null : SeasonIcons.GetSeasonIconMask(seasonId),
            isUnknown ? null : iconPalette.GetBrush(SeasonIcons.GetColorKey(seasonId)),
            isUnknown,
            isUnknown ? unknownText : CombatDataCatalog.GetSeasonName(seasonId));
    }

    /// <summary>
    /// 潜在心相晶の絵と色の鍵。型が分かれば型の絵と型の色、無効なら無効の絵と無効の色、どちらでもなければはてなと不明の色。
    /// 表に無い型(絵が無い型)も、はてなと不明の色。
    /// </summary>
    private static (Brush? Mask, string ColorKey) ResolveSeasonTalentIcon(int rootBuffId, bool isInactive)
    {
        if (rootBuffId > 0)
        {
            var typeColorKey = SeasonTalentIcons.GetColorKey(rootBuffId);
            return (
                SeasonTalentIcons.GetIconMask(rootBuffId, isInactive: false),
                string.Equals(typeColorKey, SeasonTalentIcons.UnknownColorKey, StringComparison.OrdinalIgnoreCase)
                    ? UnknownColorKey
                    : typeColorKey);
        }

        return isInactive
            ? (SeasonTalentIcons.GetInactiveIconMask(), SeasonTalentIcons.InactiveColorKey)
            : (SeasonTalentIcons.GetIconMask(0, isInactive: false), UnknownColorKey);
    }

    /// <summary>
    /// 特化名。クラスR2の特化名は、名前の表が落としている共通の語尾(<c>ClassSpec_NameSuffix</c>)を後ろに付け直す(プレイヤー情報だけ)。
    /// 名前が引けないときは語尾だけにならないよう付けない。
    /// </summary>
    private static string GetProfessionSpecDisplayName(PlayerClassSpec classSpec, LocalizationManager localization)
    {
        if (PlayerClassSpecResolver.ToSubProfessionId(classSpec) > 0)
        {
            var specName = PlayerInfoFormatFormatter.GetClassSpecText(classSpec);
            return string.IsNullOrEmpty(specName)
                ? specName
                : specName + localization.GetString("ClassSpec_NameSuffix");
        }

        var key = $"ClassSpec_{classSpec}";
        var value = localization.GetString(key);
        return string.Equals(value, key, StringComparison.Ordinal)
            ? localization.GetString("ClassSpec_Unknown")
            : value;
    }

    /// <summary>Icons.xaml の絵から、塗りを抜く形を作る。鍵ごとに使い回す。リソースが無ければ無し。</summary>
    private static Brush? ResolveMask(string resourceKey)
    {
        if (IconMasks.TryGetValue(resourceKey, out var cached))
        {
            return cached;
        }

        if (Application.Current?.TryFindResource(resourceKey) is not ImageSource source)
        {
            return null;
        }

        var mask = new ImageBrush(source) { Stretch = Stretch.Uniform };
        mask.Freeze();
        IconMasks[resourceKey] = mask;
        return mask;
    }

    private static string FormatInteger(int value)
    {
        return value.ToString(CultureInfo.CurrentCulture);
    }
}

/// <summary>
/// プレイヤー情報のアイコンカラーの塗り。色の鍵ごとに一度だけ作って使い回す(毎回作ると、色が同じでも表示値が変わった扱いになる)。
/// 設定(保存かプレビュー)が変わったら新しく作り直す。
/// </summary>
public sealed class PlayerInfoIconPalette(PlayerInfoWidgetSettingsConfig settings)
{
    private readonly Dictionary<string, Brush> _brushes = new(StringComparer.Ordinal);

    /// <summary>アイコンカラーで選んでいる色の塗り。引き方はプレイヤーリストのクラスカラーと同じ。</summary>
    public Brush GetBrush(string colorKey)
    {
        if (_brushes.TryGetValue(colorKey, out var cached))
        {
            return cached;
        }

        var palette = settings.ClassColorPalettes.TryGetValue(colorKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.PlayerInfo, colorKey);
        var selectedIndex = settings.ClassColorIndexes.TryGetValue(colorKey, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        var selectedColor = palette.Count == 0
            ? "#A8A8A8"
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];
        var color = ColorUtilities.TryParseHex(selectedColor, out var parsed)
            ? parsed
            : Color.FromRgb(0xA8, 0xA8, 0xA8);

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _brushes[colorKey] = brush;
        return brush;
    }
}
