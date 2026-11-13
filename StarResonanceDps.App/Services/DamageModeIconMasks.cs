// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 物理・魔法のアイコンの形(<c>Icon.DamageMode.*</c>、ステータス詳細の物理攻撃力・魔法攻撃力の絵)。塗りをこの形で抜いて描く。
/// 無分類は絵が無いので <c>null</c>。与ダメ詳細などの割合の行とアイコンカラーの設定の行が使う。形は凍結して全部の窓で共有する。画面のスレッドから使う。
/// </summary>
public static class DamageModeIconMasks
{
    private static readonly Dictionary<Zproto.EDamageMode, ImageBrush?> Masks = [];

    public static ImageBrush? Get(Zproto.EDamageMode mode)
    {
        if (!Masks.TryGetValue(mode, out var mask))
        {
            if (Application.Current.TryFindResource($"Icon.DamageMode.{mode}") is ImageSource image)
            {
                mask = new ImageBrush(image)
                {
                    Stretch = Stretch.Uniform
                };
                mask.Freeze();
            }

            Masks[mode] = mask;
        }

        return mask;
    }
}
