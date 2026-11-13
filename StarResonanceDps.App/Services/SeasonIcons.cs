// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>
/// シーズンの絵。Icons.xaml の <c>Icon.Season.&lt;絵の名前&gt;</c>。白一色の絵で、形を抜いてアイコンカラーで塗る。
/// アイコンカラーの設定の鍵は <c>Season.&lt;シーズン番号&gt;</c>。
/// 表に無いシーズンと、リソースが見つからない絵は出さない(null)。「まだ届いていない」のはてなと見分けるため。
/// </summary>
public static class SeasonIcons
{
    public const string ColorKeyPrefix = "Season.";

    /// <summary>シーズン番号 → シーズンの絵(シーズン実績の1枠目の分類の絵)。</summary>
    private static readonly IReadOnlyDictionary<int, string> SeasonIconNames = new Dictionary<int, string>
    {
        [1] = "achievement_simclass_new_xushirongguang",
        [2] = "achievement_simclass_new_huanhuanhua",
        [3] = "achievement_simclass_new_jinkonghuixiang",
        [4] = "achievement_simclass_new_s4"
    };

    private static readonly Dictionary<string, Brush> IconMasks = new(StringComparer.Ordinal);

    /// <summary>アイコンカラーの設定の鍵。シーズン番号の順。</summary>
    public static IReadOnlyList<string> ColorKeys { get; } = [.. SeasonIconNames.Keys.Order().Select(GetColorKey)];

    public static bool IsColorKey(string key)
    {
        return key.StartsWith(ColorKeyPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetColorKey(int seasonId)
    {
        return ColorKeyPrefix + seasonId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>そのシーズンの絵の形。</summary>
    public static Brush? GetSeasonIconMask(int seasonId)
    {
        return SeasonIconNames.TryGetValue(seasonId, out var iconName)
            ? ResolveMask($"Icon.Season.{iconName}")
            : null;
    }

    /// <summary>アイコンカラーの設定の行の形。</summary>
    public static Brush? GetIconMask(string colorKey)
    {
        return TryGetSeasonId(colorKey, out var seasonId)
            ? GetSeasonIconMask(seasonId)
            : null;
    }

    /// <summary>アイコンカラーの設定の行の名前。シーズンの名前。</summary>
    public static string GetDisplayName(string colorKey)
    {
        return TryGetSeasonId(colorKey, out var seasonId)
            ? CombatDataCatalog.GetSeasonName(seasonId)
            : colorKey;
    }

    private static bool TryGetSeasonId(string colorKey, out int seasonId)
    {
        seasonId = 0;
        return IsColorKey(colorKey)
            && int.TryParse(colorKey[ColorKeyPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out seasonId);
    }

    /// <summary>Icons.xaml の絵から、塗りを抜く形を作る。鍵ごとに使い回す。</summary>
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
}
