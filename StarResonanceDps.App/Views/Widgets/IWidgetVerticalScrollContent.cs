// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;

namespace StarResonanceDps.App.Views.Widgets;

public interface IWidgetVerticalScrollContent
{
    event EventHandler? VerticalScrollMetricsChanged;

    WidgetVerticalScrollMetrics GetVerticalScrollMetrics();

    void SetVerticalScrollOffset(double verticalOffset);
}

public readonly record struct WidgetVerticalScrollMetrics(
    double Maximum,
    double ViewportSize,
    double Value,
    double LargeChange,
    double SmallChange);
