// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 属性アイコンの形(<c>Icon.DamageProperty.*</c>)。塗りをこの形で抜いて描く(クラスアイコンと同じ染め方)。
/// スキル詳細・被ダメログ・与ダメ詳細などと、属性カラーの設定の行が使う。形は凍結して全部の窓で共有する。画面のスレッドから使う。
/// </summary>
public static class ElementIconMasks
{
    private static readonly Dictionary<Zproto.EDamageProperty, ImageBrush> Masks = [];

    public static ImageBrush Get(Zproto.EDamageProperty element)
    {
        if (!Masks.TryGetValue(element, out var mask))
        {
            mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.DamageProperty.{element}"))
            {
                Stretch = Stretch.Uniform
            };
            mask.Freeze();
            Masks[element] = mask;
        }

        return mask;
    }
}
