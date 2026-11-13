// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Controls;

/// <summary>
/// 子を2つだけ持ち、1つ目を左端、2つ目を右端に置く。2つが幅に入りきらなければ、半分ずつの幅を割り当て、
/// 片方が半分より短ければ余った分をもう片方に回す(短いほうは切らない)。
///
/// <para>
/// Grid の列では、残りの幅(*)の列が先に 0 まで縮み、内容の幅(Auto)の列は縮まずに行の右端からはみ出す。
/// 被ダメログの行の名前とスキル名(どちらも書式で長さが変わる)を同じ扱いで縮めるために使う。
/// 子の TextBlock には TextTrimming を付ける。割り当てた幅で測り直すので、入らない分は子が「…」で切る
/// (測った幅より狭く置くだけでは FrameworkElement は切り取るだけで「…」にならない)。
/// </para>
/// </summary>
public sealed class PairShrinkPanel : Panel
{
    private double _firstDesiredWidth;
    private double _secondDesiredWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        var (first, second) = GetPair();
        first.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        second.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        _firstDesiredWidth = first.DesiredSize.Width;
        _secondDesiredWidth = second.DesiredSize.Width;
        var height = Math.Max(first.DesiredSize.Height, second.DesiredSize.Height);
        var desiredWidth = _firstDesiredWidth + _secondDesiredWidth;
        if (double.IsPositiveInfinity(availableSize.Width) || desiredWidth <= availableSize.Width)
        {
            return new Size(desiredWidth, height);
        }

        var (firstWidth, secondWidth) = Split(availableSize.Width);
        first.Measure(new Size(firstWidth, availableSize.Height));
        second.Measure(new Size(secondWidth, availableSize.Height));
        return new Size(availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (first, second) = GetPair();
        var (firstWidth, secondWidth) = Split(finalSize.Width);
        first.Arrange(new Rect(0, 0, firstWidth, finalSize.Height));
        second.Arrange(new Rect(Math.Max(finalSize.Width - secondWidth, 0), 0, secondWidth, finalSize.Height));
        return finalSize;
    }

    /// <summary>幅の分け方。入りきれば測った幅のまま、入らなければ半分ずつで、半分より短いほうは測った幅のまま残りをもう片方に回す。</summary>
    private (double First, double Second) Split(double width)
    {
        var available = Math.Max(width, 0);
        if (_firstDesiredWidth + _secondDesiredWidth <= available)
        {
            return (_firstDesiredWidth, _secondDesiredWidth);
        }

        var half = available / 2;
        if (_firstDesiredWidth <= half)
        {
            return (_firstDesiredWidth, available - _firstDesiredWidth);
        }

        if (_secondDesiredWidth <= half)
        {
            return (available - _secondDesiredWidth, _secondDesiredWidth);
        }

        return (half, half);
    }

    private (UIElement First, UIElement Second) GetPair()
    {
        if (InternalChildren.Count != 2)
        {
            throw new InvalidOperationException($"{nameof(PairShrinkPanel)} needs exactly 2 children, but has {InternalChildren.Count}.");
        }

        return (InternalChildren[0], InternalChildren[1]);
    }
}
