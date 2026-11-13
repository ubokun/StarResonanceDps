// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Serilog;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// TIPSをWPFの仕組み(<see cref="ToolTipService"/>)に任せずに開け閉めする振る舞い。TIPSは全部これで付ける。
///
/// <para>
/// 使い方: <see cref="ToolTip"/>を持つ要素に<c>ToolTipBehavior.IsEnabled="True"</c>を付ける。
/// 左クリックで何も起きない要素には<c>ToolTipBehavior.KeepsOnClick="True"</c>も付ける(押しても閉じず、まだ出ていなければ押したときに出す)。
/// プラグインは本体の型を参照できないので、スタイル<c>ToolTipOwner.KeepsOnClick</c>(<c>Styles/Menus.xaml</c>)で付ける。
/// </para>
///
/// <para>
/// WPFは押すと自分が出したTIPSを閉じ、少し後に強制で閉じ直すので、押しても閉じないTIPSはWPFに開かせられない。
/// 出す時機はWPFのまま: WPFが出す直前の知らせ(<see cref="ToolTipService.ToolTipOpeningEvent"/>)を処理済みにして止め、同じ瞬間に自分で開く。
/// 閉じる条件と、続けて出すときの待ち時間はWPFと同じ考え方で持つ(閉じたTIPSがWPFの物でないので、WPFの続けて出す動きは働かない)。
/// </para>
///
/// <para>
/// 閉じる条件: マウスが要素とTIPSを囲む範囲から出た / <c>KeepsOnClick</c>でない要素で押した、右クリックなど左以外で押した /
/// 押したままドラッグの開始の距離を超えて動かした / 窓が動いた / 要素が見えなくなった、TIPSが止められた / ほかのTIPSが出た。
/// 一度に開いているのは1つだけ。
/// </para>
///
/// <para>
/// WPFは要素の窓が前面にあるときだけTIPSの窓に持ち主を付ける。前面でない窓で出したTIPSを押しても閉じずに残すと、
/// 窓がアクティブになって一番上へ上がったとき(最前面の窓どうし)TIPSが窓の下へ回るので、開いたときに持ち主が無ければ要素の窓を持ち主にする。
/// </para>
/// </summary>
public static class ToolTipBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ToolTipBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty KeepsOnClickProperty = DependencyProperty.RegisterAttached(
        "KeepsOnClick",
        typeof(bool),
        typeof(ToolTipBehavior),
        new PropertyMetadata(false));

    /// <summary>
    /// 開いているTIPSの要素の中で、マウスがほかの要素へ移ってからTIPSを出し替えるまでの待ち時間(ミリ秒)。
    /// マウスを動かし続けている間は待ち直す(TIPSへ向かって動かしている間に出し替えないため)。WPFの同じ待ち時間と同じ値。
    /// </summary>
    private const int ReplaceDelayMilliseconds = 73;

    private const uint GwOwner = 4;
    private const int GwlpHwndParent = -8;

    private static readonly DependencyPropertyDescriptor ToolTipEnabledDescriptor =
        DependencyPropertyDescriptor.FromProperty(ToolTipService.IsEnabledProperty, typeof(FrameworkElement));

    private static FrameworkElement? _owner;
    private static ToolTip? _toolTip;
    private static Window? _window;
    private static FrameworkElement? _hoverOwner;
    private static FrameworkElement? _pressedOwner;
    private static Point? _pressScreenPoint;
    private static long _closedAt;
    private static int _betweenShowDelay;
    private static DispatcherTimer? _replaceTimer;
    private static FrameworkElement? _replaceOwner;
    private static bool _isInputHooked;

    public static bool GetIsEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsEnabledProperty, value);
    }

    public static bool GetKeepsOnClick(DependencyObject obj)
    {
        return (bool)obj.GetValue(KeepsOnClickProperty);
    }

    public static void SetKeepsOnClick(DependencyObject obj, bool value)
    {
        obj.SetValue(KeepsOnClickProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement owner)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            owner.AddHandler(ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler(Owner_ToolTipOpening), true);
            owner.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Owner_PreviewMouseLeftButtonDown), true);
            owner.MouseEnter += Owner_MouseEnter;
            owner.MouseLeave += Owner_MouseLeave;
            owner.IsVisibleChanged += Owner_IsVisibleChanged;
            owner.Unloaded += Owner_Unloaded;
            EnsureInputHook();
        }
        else
        {
            owner.RemoveHandler(ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler(Owner_ToolTipOpening));
            owner.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Owner_PreviewMouseLeftButtonDown));
            owner.MouseEnter -= Owner_MouseEnter;
            owner.MouseLeave -= Owner_MouseLeave;
            owner.IsVisibleChanged -= Owner_IsVisibleChanged;
            owner.Unloaded -= Owner_Unloaded;
            ForgetOwner(owner);
        }
    }

    private static void EnsureInputHook()
    {
        if (_isInputHooked)
        {
            return;
        }

        InputManager.Current.PostProcessInput += InputManager_PostProcessInput;
        _isInputHooked = true;
    }

    /// <summary>WPFがTIPSを出す瞬間。WPFの表示を止めて、同じ瞬間に自分で開く。文字だけのTIPS(<see cref="ToolTip"/>でないもの)はWPFに任せる。</summary>
    private static void Owner_ToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (sender is not FrameworkElement owner
            || !ReferenceEquals(e.Source, owner)
            || owner.ToolTip is not ToolTip)
        {
            return;
        }

        e.Handled = true;
        Open(owner);
    }

    /// <summary>押しても閉じない要素で押した。まだ出ていなければ出す。押したまま動かしたかを見るために押した位置を控える。</summary>
    private static void Owner_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement owner || !GetKeepsOnClick(owner))
        {
            return;
        }

        _pressedOwner = owner;
        _pressScreenPoint = TryGetCursorScreenPoint(out Point cursor) ? cursor : null;
        if (!ReferenceEquals(_owner, owner))
        {
            Open(owner);
        }
    }

    private static void Owner_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement owner)
        {
            return;
        }

        _hoverOwner = owner;
        if (_owner is null)
        {
            if (IsQuickShowArmed())
            {
                Open(owner);
            }
        }
        else if (!ReferenceEquals(_owner, owner))
        {
            StartReplaceTimer(owner);
        }
    }

    private static void Owner_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement owner)
        {
            return;
        }

        if (ReferenceEquals(_hoverOwner, owner))
        {
            _hoverOwner = null;
        }

        if (ReferenceEquals(_replaceOwner, owner))
        {
            StopReplaceTimer();
        }

        if (ReferenceEquals(_owner, owner))
        {
            CheckPointer();
        }
    }

    private static void ToolTip_MouseLeave(object sender, MouseEventArgs e)
    {
        CheckPointer();
    }

    /// <summary>TIPSの窓ができた。持ち主が無ければ要素の窓(WPFが前面のときに持ち主に選ぶのと同じ窓)を持ち主にする。</summary>
    private static void ToolTip_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ToolTip toolTip)
        {
            return;
        }

        if (PresentationSource.FromVisual(toolTip) is not HwndSource popupSource
            || toolTip.PlacementTarget is null
            || PresentationSource.FromVisual(toolTip.PlacementTarget) is not HwndSource ownerSource)
        {
            Log.Warning("Could not find the window of the tooltip or of its element; the tooltip may go behind the window when it is activated");
            return;
        }

        IntPtr popup = popupSource.Handle;
        if (GetWindow(popup, GwOwner) != IntPtr.Zero)
        {
            return;
        }

        Marshal.SetLastPInvokeError(0);
        IntPtr previous = Environment.Is64BitProcess
            ? SetWindowLongPtr64(popup, GwlpHwndParent, ownerSource.Handle)
            : new IntPtr(SetWindowLong32(popup, GwlpHwndParent, ownerSource.Handle.ToInt32()));
        int error = Marshal.GetLastPInvokeError();
        if (previous == IntPtr.Zero && error != 0)
        {
            Log.Warning("Could not set the owner of the tooltip window: Win32 error {Error}", error);
        }
    }

    private static void Owner_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is FrameworkElement owner && !(bool)e.NewValue)
        {
            ForgetOwner(owner);
        }
    }

    private static void Owner_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement owner)
        {
            ForgetOwner(owner);
        }
    }

    private static void Owner_ToolTipEnabledChanged(object? sender, EventArgs e)
    {
        if (sender is FrameworkElement owner && ReferenceEquals(_owner, owner) && !ToolTipService.GetIsEnabled(owner))
        {
            Close();
        }
    }

    private static void Window_LocationChanged(object? sender, EventArgs e)
    {
        Close();
    }

    private static void InputManager_PostProcessInput(object sender, ProcessInputEventArgs e)
    {
        RoutedEvent routedEvent = e.StagingItem.Input.RoutedEvent;
        if (routedEvent == Mouse.MouseDownEvent)
        {
            bool keeps = e.StagingItem.Input is MouseButtonEventArgs { ChangedButton: MouseButton.Left }
                && _pressedOwner is not null
                && ReferenceEquals(_pressedOwner, _owner);
            if (!keeps)
            {
                _pressScreenPoint = null;
                Close();
            }

            _pressedOwner = null;
        }
        else if (routedEvent == Mouse.MouseUpEvent)
        {
            if (e.StagingItem.Input is MouseButtonEventArgs { ChangedButton: MouseButton.Left })
            {
                _pressScreenPoint = null;
            }
        }
        else if (routedEvent == Mouse.MouseMoveEvent)
        {
            if (_replaceTimer is not null)
            {
                _replaceTimer.Stop();
                _replaceTimer.Start();
            }

            if (_owner is not null)
            {
                CheckPointer();
            }
        }
    }

    /// <summary>開いているTIPSを、閉じる条件に当てて閉じる。範囲から出て、ほかの要素の上なら、その要素のTIPSを続けて出す。</summary>
    private static void CheckPointer()
    {
        FrameworkElement? owner = _owner;
        ToolTip? toolTip = _toolTip;
        if (owner is null || toolTip is null)
        {
            return;
        }

        if (!owner.IsVisible || !ToolTipService.GetIsEnabled(owner))
        {
            Close();
            return;
        }

        if (!TryGetCursorScreenPoint(out Point cursor))
        {
            return;
        }

        if (_pressScreenPoint is Point pressed
            && Mouse.LeftButton == MouseButtonState.Pressed
            && HasMovedBeyondDragDistance(owner, pressed, cursor))
        {
            _pressScreenPoint = null;
            Close();
            return;
        }

        if (IsInSafeArea(owner, toolTip, cursor))
        {
            return;
        }

        Close();
        if (_hoverOwner is FrameworkElement next && !ReferenceEquals(next, owner))
        {
            Open(next);
        }
    }

    private static void Open(FrameworkElement owner)
    {
        StopReplaceTimer();
        if (owner.ToolTip is not ToolTip toolTip
            || !ToolTipService.GetIsEnabled(owner)
            || !owner.IsVisible)
        {
            return;
        }

        if (ReferenceEquals(_owner, owner) && toolTip.IsOpen)
        {
            return;
        }

        Close(armsQuickShow: false);

        // 中身の結び付けが置き場所のデータを見ているので、置き場所を入れてから開く。
        toolTip.PlacementTarget = owner;
        toolTip.Opened += ToolTip_Opened;
        toolTip.IsOpen = true;

        _owner = owner;
        _toolTip = toolTip;
        toolTip.MouseLeave += ToolTip_MouseLeave;
        ToolTipEnabledDescriptor.AddValueChanged(owner, Owner_ToolTipEnabledChanged);
        _window = Window.GetWindow(owner);
        if (_window is not null)
        {
            _window.LocationChanged += Window_LocationChanged;
        }
    }

    private static void Close(bool armsQuickShow = true)
    {
        StopReplaceTimer();
        FrameworkElement? owner = _owner;
        ToolTip? toolTip = _toolTip;
        if (owner is null || toolTip is null)
        {
            return;
        }

        _owner = null;
        _toolTip = null;
        toolTip.Opened -= ToolTip_Opened;
        toolTip.MouseLeave -= ToolTip_MouseLeave;
        ToolTipEnabledDescriptor.RemoveValueChanged(owner, Owner_ToolTipEnabledChanged);
        if (_window is not null)
        {
            _window.LocationChanged -= Window_LocationChanged;
            _window = null;
        }

        toolTip.IsOpen = false;

        if (armsQuickShow)
        {
            _closedAt = Environment.TickCount64;
            _betweenShowDelay = ToolTipService.GetBetweenShowDelay(owner);
        }
    }

    private static void ForgetOwner(FrameworkElement owner)
    {
        if (ReferenceEquals(_hoverOwner, owner))
        {
            _hoverOwner = null;
        }

        if (ReferenceEquals(_pressedOwner, owner))
        {
            _pressedOwner = null;
        }

        if (ReferenceEquals(_replaceOwner, owner))
        {
            StopReplaceTimer();
        }

        if (ReferenceEquals(_owner, owner))
        {
            Close();
        }
    }

    /// <summary>TIPSが閉じてから要素の<see cref="ToolTipService.BetweenShowDelayProperty"/>以内なら、次のTIPSを待たずに出す(WPFと同じ)。</summary>
    private static bool IsQuickShowArmed()
    {
        return _betweenShowDelay > 0 && Environment.TickCount64 - _closedAt <= _betweenShowDelay;
    }

    private static void StartReplaceTimer(FrameworkElement owner)
    {
        StopReplaceTimer();
        _replaceOwner = owner;
        _replaceTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(ReplaceDelayMilliseconds),
        };
        _replaceTimer.Tick += ReplaceTimer_Tick;
        _replaceTimer.Start();
    }

    private static void StopReplaceTimer()
    {
        if (_replaceTimer is null)
        {
            return;
        }

        _replaceTimer.Stop();
        _replaceTimer.Tick -= ReplaceTimer_Tick;
        _replaceTimer = null;
        _replaceOwner = null;
    }

    private static void ReplaceTimer_Tick(object? sender, EventArgs e)
    {
        FrameworkElement? owner = _replaceOwner;
        StopReplaceTimer();
        if (owner is not null && ReferenceEquals(_hoverOwner, owner))
        {
            Open(owner);
        }
    }

    private static bool HasMovedBeyondDragDistance(FrameworkElement owner, Point pressedScreen, Point cursorScreen)
    {
        if (PresentationSource.FromVisual(owner) is null)
        {
            return false;
        }

        Point pressed = owner.PointFromScreen(pressedScreen);
        Point current = owner.PointFromScreen(cursorScreen);
        return Math.Abs(current.X - pressed.X) > SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(current.Y - pressed.Y) > SystemParameters.MinimumVerticalDragDistance;
    }

    /// <summary>マウスが、要素とTIPSの四角をまとめて囲む凸の範囲の中か(画面の座標)。WPFの「TIPSへマウスを動かしても閉じない範囲」と同じ考え方。</summary>
    private static bool IsInSafeArea(FrameworkElement owner, ToolTip toolTip, Point cursor)
    {
        List<Point> corners = new(8);
        AddScreenCorners(owner, corners);
        if (toolTip.IsOpen)
        {
            AddScreenCorners(toolTip, corners);
        }

        if (corners.Count == 0)
        {
            return false;
        }

        return IsInsideConvexHull(ConvexHull(corners), cursor);
    }

    private static void AddScreenCorners(FrameworkElement element, List<Point> corners)
    {
        if (PresentationSource.FromVisual(element) is null || element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return;
        }

        corners.Add(element.PointToScreen(new Point(0, 0)));
        corners.Add(element.PointToScreen(new Point(element.ActualWidth, 0)));
        corners.Add(element.PointToScreen(new Point(0, element.ActualHeight)));
        corners.Add(element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight)));
    }

    /// <summary>凸包(反時計回り)。点の数は多くて8つ。</summary>
    private static List<Point> ConvexHull(List<Point> points)
    {
        points.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        List<Point> hull = new(points.Count * 2);
        foreach (Point point in points)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], point) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(point);
        }

        int lowerCount = hull.Count + 1;
        for (int i = points.Count - 2; i >= 0; i--)
        {
            Point point = points[i];
            while (hull.Count >= lowerCount && Cross(hull[^2], hull[^1], point) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(point);
        }

        hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    private static bool IsInsideConvexHull(List<Point> hull, Point point)
    {
        if (hull.Count < 3)
        {
            return false;
        }

        for (int i = 0; i < hull.Count; i++)
        {
            if (Cross(hull[i], hull[(i + 1) % hull.Count], point) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static double Cross(Point origin, Point a, Point b)
    {
        return ((a.X - origin.X) * (b.Y - origin.Y)) - ((a.Y - origin.Y) * (b.X - origin.X));
    }

    private static bool TryGetCursorScreenPoint(out Point point)
    {
        if (GetCursorPos(out NativePoint native))
        {
            point = new Point(native.X, native.Y);
            return true;
        }

        point = default;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
}
