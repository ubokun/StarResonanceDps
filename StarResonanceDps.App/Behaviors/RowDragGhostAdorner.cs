// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// 行を掴んでいる間の写し(<see cref="RowReorderBehavior"/>)。窓全体の飾りの層に置き、座標は飾る相手(窓の中身の一番外の要素)の座標。
/// 描くのは掴んだ瞬間の行の中身の絵だけで、地の色と枠は描かない。
/// マウスを受けない(下の一覧の当たり判定を邪魔しない)。
/// </summary>
internal sealed class RowDragGhostAdorner : Adorner
{
    private readonly ImageSource _image;
    private readonly Size _size;
    private Point? _ghostTopLeft;

    public RowDragGhostAdorner(UIElement adornedElement, ImageSource image, Size size)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
        _image = image;
        _size = size;
    }

    /// <param name="ghostTopLeft">写しの左上。null なら写しを出さない(窓の外にいる間)。</param>
    public void Update(Point? ghostTopLeft)
    {
        if (_ghostTopLeft == ghostTopLeft)
        {
            return;
        }

        _ghostTopLeft = ghostTopLeft;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_ghostTopLeft is { } topLeft)
        {
            drawingContext.DrawImage(_image, new Rect(topLeft, _size));
        }
    }
}
