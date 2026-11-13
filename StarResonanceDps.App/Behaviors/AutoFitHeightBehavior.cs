// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// 行の数が変わったら、ウィジェットの窓の高さを全部の行が入る高さに合わせる振る舞い。
///
/// <para>
/// 使い方: 測る <see cref="ScrollViewer"/> に <c>AutoFitHeightBehavior.IsEnabled="True"</c> を付ける。
/// 全体の高さが変わると、その <see cref="ScrollViewer"/> から <see cref="FitHeightRequestedEvent"/> が窓へ上がり、
/// 窓(<c>Views.Widgets.WidgetWindow</c>)が高さを決める(上限・ピン留め・最小化の扱いは窓)。
/// 足りない高さは <see cref="GetHeightShortfall"/>。1つの窓で付けるのは1つの <see cref="ScrollViewer"/> だけ。
/// </para>
///
/// <para>
/// 合図を出すのは全体の高さが変わったときだけ(行の高さは一定なので、行の数が変わったとき。手で窓の大きさを変えても出ない)。
/// 行が無くなったときは出さない(見出しだけまで縮み、次に行が来たら伸びる、を繰り返さないため)。
/// </para>
/// </summary>
public static class AutoFitHeightBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(AutoFitHeightBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>全体の高さが変わった合図。測る <see cref="ScrollViewer"/> から上がる(<see cref="RoutedEventArgs.OriginalSource"/>)。</summary>
    public static readonly RoutedEvent FitHeightRequestedEvent = EventManager.RegisterRoutedEvent(
        "FitHeightRequested",
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(AutoFitHeightBehavior));

    public static bool GetIsEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsEnabledProperty, value);
    }

    /// <summary>中身の全体の高さから、いま見えている高さを引いた値。正なら足りない、負なら余っている。</summary>
    public static double GetHeightShortfall(ScrollViewer scrollViewer)
    {
        return scrollViewer.ExtentHeight - scrollViewer.ViewportHeight;
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scrollViewer)
        {
            return;
        }

        scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
        if (e.NewValue is true)
        {
            scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
        }
    }

    private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // 中の一覧が持つ別の ScrollViewer の変化も上がってくるので、付けた ScrollViewer 自身の変化だけを見る。
        if (sender is not ScrollViewer scrollViewer
            || !ReferenceEquals(e.OriginalSource, scrollViewer)
            || e.ExtentHeightChange == 0d
            || scrollViewer.ExtentHeight <= 0d)
        {
            return;
        }

        scrollViewer.RaiseEvent(new RoutedEventArgs(FitHeightRequestedEvent, scrollViewer));
    }
}
