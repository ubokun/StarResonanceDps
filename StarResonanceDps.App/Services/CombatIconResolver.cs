// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Services;

public static class CombatIconResolver
{
    /// <summary>
    /// バフの絵の名前のうち、アプリの <c>Styles/Icons.xaml</c> の絵の鍵を指す値の頭(手修正 <c>BuffOverrides.json</c> の <c>Icon</c>)。
    /// 装備効果の絵(<c>Icon.EquipSlot.202</c> など)。
    /// </summary>
    private const string AppIconPrefix = "Icon.";

    /// <summary>
    /// バフの絵の名前のうち、アプリの因子の絵のフォルダ(<c>Data/Images/Factors</c>)を指す値の頭(手修正 <c>BuffOverrides.json</c> の <c>Icon</c>)。
    /// 因子のバフの絵(<c>Factors/yinzi_s2_common_011</c> など)。
    /// </summary>
    private const string FactorIconPrefix = "Factors/";

    private static readonly ConcurrentDictionary<(string Category, string? FallbackCategory, string IconName), string> ResolvedPaths = new();

    /// <summary>Icons.xaml の鍵 → 型抜きのブラシ。鍵ごとに1つ作って使い回す。Icons.xaml に無い鍵は null。画面のスレッドだけで使う。</summary>
    private static readonly Dictionary<string, ImageBrush?> AppIconMasks = new(StringComparer.Ordinal);

    /// <summary>
    /// バフのアイコン(ファイル)。モジュール効果の絵(<see cref="CombatDataCatalog.IsModEffectIcon"/>)ならモジュールのフォルダだけ、
    /// 因子の絵(頭が <see cref="FactorIconPrefix"/>)なら因子のフォルダだけ、それ以外はバフのフォルダだけを探す。
    /// Icons.xaml の絵の鍵(<see cref="ResolveBuffIconMask"/>)はファイルではないので null。
    /// </summary>
    public static string? ResolveBuffIcon(string? iconName)
    {
        if (IsAppIcon(iconName))
        {
            return null;
        }

        var category = CombatDataCatalog.IsModEffectIcon(iconName) ? "Modules"
            : IsFactorIcon(iconName) ? "Factors"
            : "Buffs";
        return Resolve(iconName, category);
    }

    /// <summary>
    /// バフの絵の名前が Icons.xaml の絵の鍵なら、その絵(白い形)を、白く塗る面の型抜きにするブラシ。
    /// 鍵でなければ null。鍵が Icons.xaml に無ければ null で、警告のログを鍵ごとに1回出す(呼ぶ側ははてなを出す)。
    /// <b>画面のスレッドで呼ぶ</b>(Icons.xaml を引く)。
    /// </summary>
    public static ImageBrush? ResolveBuffIconMask(string? iconName)
    {
        if (!IsAppIcon(iconName))
        {
            return null;
        }

        if (AppIconMasks.TryGetValue(iconName!, out var mask))
        {
            return mask;
        }

        if (Application.Current.TryFindResource(iconName) is ImageSource source)
        {
            mask = new ImageBrush(source) { Stretch = Stretch.Uniform };
            mask.Freeze();
        }
        else
        {
            Log.Warning("Buff icon {IconName} is not a resource in Icons.xaml; showing the unknown icon", iconName);
        }

        AppIconMasks[iconName!] = mask;
        return mask;
    }

    private static bool IsAppIcon(string? iconName)
        => iconName?.StartsWith(AppIconPrefix, StringComparison.Ordinal) == true;

    /// <summary>バフの絵の名前が因子の絵(頭が <see cref="FactorIconPrefix"/>)か。リスト・カードは絵の位置をこれで切り替える。</summary>
    public static bool IsFactorIcon(string? iconName)
        => iconName?.StartsWith(FactorIconPrefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// 根のバフの絵(ファイル)。種類ごとに1つのフォルダだけを探す: タレント・潜在心相晶のノードと上級は <c>Talents</c>
    /// (同じ絵を共有するので1か所)、因子は <c>Factors</c>、モジュール効果は <c>Modules</c>。
    /// </summary>
    public static string? ResolveRootBuffIcon(RootBuffKind kind, string? iconName)
    {
        var category = kind switch
        {
            RootBuffKind.Factor => "Factors",
            RootBuffKind.ModuleEffect => "Modules",
            _ => "Talents"
        };
        return Resolve(iconName, category);
    }

    /// <summary>ゲームの用意した顔のアイコン。<c>Avatars</c> のフォルダだけを探す。</summary>
    public static string? ResolveAvatarIcon(string? iconName)
    {
        return Resolve(iconName, "Avatars");
    }

    /// <summary>技のアイコン。スキルのフォルダだけを探す(イマジンの技もここ)。</summary>
    public static string? ResolveSkillIcon(string? iconName)
    {
        return Resolve(iconName, "Skills");
    }

    /// <summary>
    /// チャットのスタンプの絵。表示言語のフォルダ(<c>Stickers/ja-JP</c> など。文字の入ったスタンプの言語別の絵)を先に、
    /// 無ければ <c>Stickers</c> を探す。
    /// </summary>
    public static string? ResolveChatStickerIcon(string? iconName, string cultureName)
    {
        return Resolve(iconName, Path.Combine("Stickers", cultureName), "Stickers");
    }


    private static string? Resolve(string? iconName, string category, string? fallbackCategory = null)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return null;
        }

        var key = (category, fallbackCategory, iconName);
        if (!ResolvedPaths.TryGetValue(key, out var resolvedPath))
        {
            string[] categories = fallbackCategory is null ? [category] : [category, fallbackCategory];
            resolvedPath = ResolveUncached(iconName, categories) ?? string.Empty;
            ResolvedPaths[key] = resolvedPath;
        }

        return string.IsNullOrEmpty(resolvedPath) ? null : resolvedPath;
    }

    private static string? ResolveUncached(string iconName, IReadOnlyList<string> categories)
    {
        var candidateNames = GetCandidateNames(iconName).ToArray();

        foreach (var category in categories)
        {
            var categoryDirectory = Path.Combine(AppDataPaths.ImagesDirectory, category);
            foreach (var candidateName in candidateNames)
            {
                var candidatePath = Path.Combine(categoryDirectory, candidateName);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateNames(string iconName)
    {
        var normalized = iconName
            .Trim()
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);

        yield return fileName;

        if (Path.HasExtension(fileName))
        {
            yield break;
        }

        yield return fileName + ".png";
        yield return fileName + ".jpg";
        yield return fileName + ".jpeg";
        yield return fileName + ".webp";
    }
}
