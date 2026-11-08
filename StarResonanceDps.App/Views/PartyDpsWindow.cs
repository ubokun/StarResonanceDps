using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>Independent party DPS graph; event visibility never changes damage totals.</summary>
public sealed class PartyDpsWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Canvas _plot = new() { MinHeight = 180, ClipToBounds = true };
    private readonly TextBlock _caption = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
    private readonly CheckBox _party = new() { Content = "PTのみ", Margin = new Thickness(8), IsChecked = true };
    private bool _reading;
    private CombatTimelineSnapshot? _snapshot;

    public PartyDpsWindow()
    {
        var body = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(_party, Dock.Top); body.Children.Add(_party);
        DockPanel.SetDock(_caption, Dock.Top); body.Children.Add(_caption);
        body.Children.Add(_plot);
        var grid = new Grid(); grid.Children.Add(new ContentControl { Content = body });
        Content = new ContentControl { Content = grid };
        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += (_, _) => StartWidget();
        Closed += (_, _) => _timer.Stop();
        _party.Click += async (_, _) => await RefreshAsync();
        _plot.SizeChanged += (_, _) => Draw();
    }

    internal async void StartWidget() { _timer.Start(); await RefreshAsync(); }
    internal void StopWidget() => _timer.Stop();
    private async Task RefreshAsync()
    {
        if (_reading) return;
        _reading = true;
        try
        {
            var options = CombatExtensionSettings.Current with { PartyOnly = _party.IsChecked == true };
            _snapshot = await CombatTimelineReader.ReadAsync(options).WaitAsync(TimeSpan.FromSeconds(10));
            Draw();
        }
        catch (Exception ex) { _caption.Text = "更新できませんでした：" + ex.Message; }
        finally { _reading = false; }
    }

    private void Draw()
    {
        if (_snapshot is null || _plot.ActualWidth < 60) return;
        _plot.Children.Clear();
        var span = CombatExtensionSettings.Current.VisibleSeconds;
        var end = Math.Max(span, _snapshot.Duration);
        var start = Math.Max(0, end - span);
        var points = _snapshot.Points.Where(x => x.EndSeconds >= start && x.EndSeconds <= end).ToArray();
        var max = Math.Max(1, points.Select(x => x.ValuePerSecond).DefaultIfEmpty().Max());
        var width = _plot.ActualWidth - 50;
        var height = Math.Max(100, _plot.ActualHeight - 40);
        _caption.Text = $"{_snapshot.Caption} · 経過 {TimeSpan.FromSeconds(_snapshot.Duration):hh\\:mm\\:ss} · 最大 {max:N0} DPS";
        for (var i = 0; i <= 4; i++)
        {
            var x = 25 + width * i / 4;
            _plot.Children.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = height, Stroke = (Brush)_plot.FindResource("Brush.BorderSoft") });
            var label = new TextBlock { Text = TimeSpan.FromSeconds(start + span * i / 4d).ToString(@"hh\:mm\:ss"), FontSize = 10 };
            Canvas.SetLeft(label, x - 22); Canvas.SetTop(label, height + 8); _plot.Children.Add(label);
        }
        var graph = new Polyline { Stroke = (Brush)_plot.FindResource("Brush.Success"), StrokeThickness = 2 };
        foreach (var point in points)
            graph.Points.Add(new Point(25 + (point.EndSeconds - start) / span * width, height - point.ValuePerSecond / max * height));
        _plot.Children.Add(graph);
    }
}
