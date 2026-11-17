// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// マップ。地図と印の位置は、画面が出ている間だけ 0.1 秒ごとに当て直す(エンティティリストの詠唱ゲージと同じ置き方)。
/// 地図の画像の層は枠の層(<see cref="WidgetWindow.FrameOverlayHost"/>)へ差し込み、窓の背景と1枚の絵として窓の不透明度で合成する
/// (推移グラフの格子と同じ置き方)。地図は設定「マップの不透明度」の濃さで見えるよう、掛ける値で窓の不透明度を打ち消す
/// (窓のほうが薄い間は、足りない分を印の層のもう1枚の画像で足す。掛ける値は画面の資源のスタイルが決める)。
/// 印の層はこの画面(中身の層)に置き、いつも不透明。
/// 窓の上のホイールは表示範囲を変える(<see cref="IWidgetMouseWheelContent"/>)。
/// 地図を出す領域の左ボタンは、押してから Windows のドラッグの距離より動かすと地図を掴んで動かし(印の上から掴んでもよい)、
/// 動かさずに離したときプレイヤーの印の上ならその人のプレイヤー情報を開く。印の外のダブルクリックは見る中心を自分に合わせ直す。
/// マウスはここで受け、動かす量と表示範囲の計算は中身(<see cref="MapWidgetViewModel"/>)が持つ。
/// 地図を出す領域の実際の大きさ・印の名前の欄の幅(画面の資源)・Windows の表示倍率は、ここから中身へ渡す。
/// </summary>
public partial class MapWidgetView : UserControl, IWidgetMouseWheelContent, IWidgetWindowHiddenAware
{
    private const string MarkerNameWidthKey = "Map.MarkerNameWidth";
    private const string ImageLayerKey = "Map.ImageLayer";

    private readonly DispatcherTimer _positionRefreshTimer;

    /// <summary>窓が隠れているか(全体設定「ゲーム非アクティブ時にウィジェットを隠す」)。隠れている間は地図と印を当て直さない。</summary>
    private bool _isWindowHidden;

    /// <summary>枠の層へ差し込んだ地図の画像の層と、差し込んだ先の窓。差し込んでいなければ null。</summary>
    private FrameworkElement? _imageLayer;
    private WidgetWindow? _frameOverlayOwner;

    /// <summary>左ボタンを押した位置(地図を出す領域の中)。押していなければ null。</summary>
    private Point? _pressPosition;

    /// <summary>左ボタンを押したプレイヤーの印。押した位置から動かしたら(クリックでなくなったら)null。</summary>
    private MapMarkerEntry? _pressedMarker;

    /// <summary>地図を掴んで動かしているか。</summary>
    private bool _isPanning;

    /// <summary>地図を掴んでいる間の、前に中身へ渡したマウスの位置(地図を出す領域の中)。</summary>
    private Point _panPosition;

    public MapWidgetView()
    {
        InitializeComponent();
        _positionRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionRefreshTimer.Tick += PositionRefreshTimer_Tick;
    }

    public bool HandleWidgetMouseWheel(int delta)
    {
        return DataContext is MapWidgetViewModel viewModel
            && viewModel.ZoomByWheel(delta / (double)Mouse.MouseWheelDeltaForOneLine);
    }

    /// <summary>窓が表示倍率の違う画面へ移った。地図の画像の画素と画面の画素の比べ方が変わるので、渡し直す。</summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateViewport();
    }

    private void MapWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        AttachImageLayer();
        UpdateViewport();
        if (!_isWindowHidden)
        {
            _positionRefreshTimer.Start();
        }
    }

    private void MapWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        _positionRefreshTimer.Stop();
        DetachImageLayer();
    }

    /// <summary>窓が隠れたら地図と印の定期の当て直しを止め、見えたら(読み込み済みなら)当て直してから再開する。</summary>
    public void SetWindowHidden(bool isHidden)
    {
        _isWindowHidden = isHidden;
        if (isHidden)
        {
            _positionRefreshTimer.Stop();
            return;
        }

        if (IsLoaded)
        {
            PositionRefreshTimer_Tick(this, EventArgs.Empty);
            _positionRefreshTimer.Start();
        }
    }

    /// <summary>地図の画像の層は別の層にあって中身を引き継がないので、中身が替わったら入れ直す。</summary>
    private void MapWidgetView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_imageLayer is not null)
        {
            _imageLayer.DataContext = DataContext;
        }
    }

    private void MapArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateViewport();
    }

    private void PositionRefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (DataContext is MapWidgetViewModel viewModel)
        {
            viewModel.Refresh();
        }
    }

    private void MapArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MapWidgetViewModel viewModel)
        {
            return;
        }

        var marker = GetMarker(e.OriginalSource);
        if (e.ClickCount == 2)
        {
            // 印の上のダブルクリックは、1回目のクリックでプレイヤー情報かターゲット情報を開いているので何もしない。
            if (marker is null)
            {
                viewModel.ResumeFollowingSelf();
            }

            e.Handled = true;
            return;
        }

        if (e.ClickCount != 1 || !MapArea.CaptureMouse())
        {
            return;
        }

        _pressPosition = e.GetPosition(MapArea);
        _pressedMarker = marker;
        e.Handled = true;
    }

    private void MapArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pressPosition is not { } pressPosition || DataContext is not MapWidgetViewModel viewModel)
        {
            return;
        }

        var position = e.GetPosition(MapArea);
        if (!_isPanning)
        {
            if (Math.Abs(position.X - pressPosition.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(position.Y - pressPosition.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            // 押した位置から動かしたのでクリックではない。地図がはみ出していれば掴む。
            _pressedMarker = null;
            if (!viewModel.CanPan)
            {
                return;
            }

            _isPanning = true;
            _panPosition = pressPosition;
            viewModel.BeginPan();
        }

        var previous = _panPosition;
        _panPosition = position;
        viewModel.PanBy(position.X - previous.X, position.Y - previous.Y);
    }

    private void MapArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_pressPosition is null)
        {
            return;
        }

        var clickedMarker = _pressedMarker;
        MapArea.ReleaseMouseCapture();
        e.Handled = true;

        if (clickedMarker is not null && DataContext is MapWidgetViewModel viewModel)
        {
            viewModel.OpenMarkerWindow(clickedMarker);
        }
    }

    /// <summary>押すのをやめた(ボタンを離した・ほかがマウスを取った)。掴んでいたら離す。</summary>
    private void MapArea_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isPanning && DataContext is MapWidgetViewModel viewModel)
        {
            viewModel.EndPan();
        }

        _pressPosition = null;
        _pressedMarker = null;
        _isPanning = false;
    }

    /// <summary>押した要素が印(アイコンか名前)なら、その印。得点の文字は押せない。</summary>
    private static MapMarkerEntry? GetMarker(object originalSource)
    {
        return originalSource is FrameworkElement { DataContext: MapMarkerEntry marker }
            ? marker
            : null;
    }

    /// <summary>地図の画像の層(画面の資源)に中身を入れて、持ち主の窓の枠の層へ差し込む。</summary>
    private void AttachImageLayer()
    {
        if (_imageLayer is not null)
        {
            return;
        }

        if (Window.GetWindow(this) is not WidgetWindow owner
            || TryFindResource(ImageLayerKey) is not FrameworkElement imageLayer)
        {
            return;
        }

        _imageLayer = imageLayer;
        _frameOverlayOwner = owner;
        imageLayer.DataContext = DataContext;
        owner.FrameOverlayHost.Content = imageLayer;
    }

    private void DetachImageLayer()
    {
        if (_frameOverlayOwner is not null
            && ReferenceEquals(_frameOverlayOwner.FrameOverlayHost.Content, _imageLayer))
        {
            _frameOverlayOwner.FrameOverlayHost.Content = null;
        }

        if (_imageLayer is not null)
        {
            _imageLayer.DataContext = null;
        }

        _imageLayer = null;
        _frameOverlayOwner = null;
    }

    /// <summary>印は中心が領域の外でも、名前の欄の幅の半分までは端からはみ出して見える。</summary>
    private void UpdateViewport()
    {
        if (DataContext is MapWidgetViewModel viewModel)
        {
            viewModel.SetViewport(
                MapArea.ActualWidth,
                MapArea.ActualHeight,
                (double)FindResource(MarkerNameWidthKey) / 2d);
        }
    }
}
