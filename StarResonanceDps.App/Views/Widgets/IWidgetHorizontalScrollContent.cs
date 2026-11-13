// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// 横にスクロールする中身(<see cref="IWidgetVerticalScrollContent"/> の横向き)。
/// バーは窓の枠の層にある1本で、中身は値の受け渡しだけをする。
/// </summary>
public interface IWidgetHorizontalScrollContent
{
    event EventHandler? HorizontalScrollMetricsChanged;

    WidgetHorizontalScrollMetrics GetHorizontalScrollMetrics();

    void SetHorizontalScrollOffset(double horizontalOffset);
}

public readonly record struct WidgetHorizontalScrollMetrics(
    double Maximum,
    double ViewportSize,
    double Value,
    double LargeChange,
    double SmallChange);
