// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 押した所などの要素から親をたどる。画面の部品(<see cref="Visual"/> / <see cref="Visual3D"/>)は見た目の木の親、
/// 文の中の要素(<see cref="System.Windows.Documents.Run"/>・<see cref="System.Windows.Documents.Hyperlink"/> など)は論理の木の親。
/// 文の中の要素に <see cref="VisualTreeHelper.GetParent"/> を使うと例外になるので、たどる処理はここを通す。
/// </summary>
internal static class ElementTree
{
    public static DependencyObject? GetParent(DependencyObject element)
    {
        return element is Visual or Visual3D
            ? VisualTreeHelper.GetParent(element)
            : LogicalTreeHelper.GetParent(element);
    }
}
