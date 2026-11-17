// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// バフのアイコンの出し方(バフリストの行とバフ稼働率の行で共有)。絵の名前から、絵のパス・型抜きの形・モジュール効果と因子の置き方・はてなを出すかを決める。
/// </summary>
/// <param name="IconPath">絵のパス。Icons.xaml の絵なら null。</param>
/// <param name="IconMask">Icons.xaml の絵(装備効果)の型抜きの形。無ければ null。</param>
/// <param name="IsModuleEffectIcon">モジュール効果の絵(丸く画像いっぱいに描かれていて小さく出す)。</param>
/// <param name="IsFactorIcon">因子の絵(左へずらす)。</param>
/// <param name="ShowsUnknownIcon">絵の名前はあるのにパスも型抜きの形も無い(はてなを出す)。</param>
public sealed record BuffIconDisplay(string? IconPath, ImageBrush? IconMask, bool IsModuleEffectIcon, bool IsFactorIcon, bool ShowsUnknownIcon)
{
    public static BuffIconDisplay Resolve(string? iconName)
    {
        var iconPath = CombatIconResolver.ResolveBuffIcon(iconName);
        var iconMask = CombatIconResolver.ResolveBuffIconMask(iconName);
        return new BuffIconDisplay(
            iconPath,
            iconMask,
            CombatDataCatalog.IsModEffectIcon(iconName),
            CombatIconResolver.IsFactorIcon(iconName),
            !string.IsNullOrWhiteSpace(iconName) && iconPath is null && iconMask is null);
    }
}
