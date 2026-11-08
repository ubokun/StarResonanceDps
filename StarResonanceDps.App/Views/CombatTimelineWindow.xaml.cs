using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Views;

public partial class CombatTimelineWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _reading;
    private bool _updating;
    private CombatTimelineSnapshot? _snapshot;
    private sealed record HistoryRow(ulong Id, string Label);

    public CombatTimelineWindow()
    {
        InitializeComponent();
        ExtensionWindowLayout.Attach(this);
        var settings = CombatExtensionSettings.Current;
        QuestClock.IsChecked = settings.SyncQuestClock; QuestReset.IsChecked = settings.ResetOnQuestTransition;
        AutoLink.IsChecked = settings.AutoLinkSavedPlayers;
        IdleSeconds.Text = settings.IdleResetSeconds.ToString(); WindowSeconds.Text = settings.VisibleSeconds.ToString();
        IntervalSeconds.Text = settings.IntervalResetSeconds.ToString();
        PartyOnly.IsChecked = settings.PartyOnly; Skills.IsChecked = settings.ShowSkills;
        Imagines.IsChecked = settings.ShowImagines; Factors.IsChecked = settings.ShowFactors; Enemies.IsChecked = settings.ShowEnemies;
        if (CombatExtensionSettings.LoadIssue is { } issue) Status.Text = issue;
        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) => { LoadHistory(); _timer.Start(); await RefreshAsync(); };
        Closed += (_, _) => _timer.Stop();
    }

    private CombatExtensionOptions ReadOptions()
    {
        if (!int.TryParse(IdleSeconds.Text, out var idle) || !int.TryParse(WindowSeconds.Text, out var width)
            || !int.TryParse(IntervalSeconds.Text, out var interval))
            throw new ArgumentException("秒数は整数で入力してください。");
        var options = new CombatExtensionOptions { SyncQuestClock = QuestClock.IsChecked == true,
            ResetOnQuestTransition = QuestReset.IsChecked == true, IdleResetSeconds = idle, VisibleSeconds = width,
            IntervalResetSeconds = interval,
            AutoLinkSavedPlayers = AutoLink.IsChecked == true,
            PartyOnly = PartyOnly.IsChecked == true, ShowSkills = Skills.IsChecked == true,
            ShowImagines = Imagines.IsChecked == true, ShowFactors = Factors.IsChecked == true, ShowEnemies = Enemies.IsChecked == true };
        CombatExtensionSettings.Validate(options); return options;
    }

    private async Task RefreshAsync()
    {
        if (_reading) return;
        _reading = true;
        try
        {
            var snapshot = await CombatTimelineReader.ReadAsync(ReadOptions()).WaitAsync(TimeSpan.FromSeconds(10));
            SetSnapshot(snapshot);
        }
        catch (Exception ex) { Status.Text = "更新できませんでした：" + ex.Message; }
        finally { _reading = false; }
    }

    internal void SetSnapshot(CombatTimelineSnapshot snapshot)
    {
        var oldId = _snapshot?.EncounterId;
        _snapshot = snapshot;
        _updating = true;
        var selected = oldId == snapshot.EncounterId
            ? Actors.SelectedItems.Cast<TimelineActor>().Select(x => x.Uuid).ToHashSet() : [];
        if (oldId != snapshot.EncounterId || !Actors.Items.Cast<TimelineActor>().SequenceEqual(snapshot.Actors))
        {
            Actors.ItemsSource = snapshot.Actors;
            foreach (var actor in snapshot.Actors.Where(x => selected.Contains(x.Uuid))) Actors.SelectedItems.Add(actor);
        }
        TimeOffset.Maximum = Math.Max(0, snapshot.Duration - ReadOptions().VisibleSeconds);
        if (Follow.IsChecked == true) TimeOffset.Value = TimeOffset.Maximum;
        Caption.Text = snapshot.Caption + $" · 経過 {TimeSpan.FromSeconds(snapshot.Duration):hh\\:mm\\:ss}";
        _updating = false;
        Draw();
    }

    private void Draw()
    {
        if (_snapshot is null || _updating) return;
        var options = ReadOptions();
        var selected = Actors.SelectedItems.Cast<TimelineActor>().Select(x => x.Uuid).ToHashSet();
        var start = TimeOffset.Value; var end = start + options.VisibleSeconds;
        var events = _snapshot.Events.Where(x => x.Seconds >= start && x.Seconds <= end
            && (selected.Count == 0 || selected.Contains(x.EntityUuid))).ToArray();
        Plot.Children.Clear();
        const double left = 170, width = 920, top = 32, height = 145;
        double X(double seconds) => left + (seconds - start) / options.VisibleSeconds * width;
        void Label(string value, double x, double y, Brush? color = null)
        {
            var text = new TextBlock { Text = value, Foreground = color ?? (Brush)FindResource("Brush.TextSecondary"), FontSize = 11, MaxWidth = 164,
                TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = value };
            Canvas.SetLeft(text, x); Canvas.SetTop(text, y); Plot.Children.Add(text);
        }
        var visiblePoints = _snapshot.Points.Where(p => p.EndSeconds >= start && p.EndSeconds <= end).ToArray();
        var max = Math.Max(1, visiblePoints.Select(p => p.ValuePerSecond).DefaultIfEmpty(0).Max());
        Label("PT / 全員 DPS", 8, top); Label($"最大 {max:N0}", 8, top + 22);
        for (var index = 0; index <= 4; index++)
        {
            var seconds = start + index * options.VisibleSeconds / 4d;
            var line = new Line { X1 = X(seconds), X2 = X(seconds), Y1 = top, Y2 = top + height, Stroke = (Brush)FindResource("Brush.BorderSoft") };
            Plot.Children.Add(line); Label(TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss"), X(seconds) - 20, 8);
        }
        var graph = new Polyline { Stroke = (Brush)FindResource("Brush.Success"), StrokeThickness = 2 };
        foreach (var point in visiblePoints) graph.Points.Add(new Point(X(point.EndSeconds), top + height - point.ValuePerSecond / max * height));
        Plot.Children.Add(graph);
        var tracks = events.GroupBy(x => (x.Track, x.EntityUuid, x.Actor)).ToArray();
        var row = 0;
        foreach (var track in tracks)
        {
            var y = top + height + 30 + row++ * 28;
            Label($"{track.Key.Track} / {track.Key.Actor}", 8, y);
            Plot.Children.Add(new Line { X1 = left, X2 = left + width, Y1 = y + 8, Y2 = y + 8, Stroke = (Brush)FindResource("Brush.BorderSoft") });
            foreach (var item in track)
            {
                var marker = new Ellipse { Width = 8, Height = 12,
                    Fill = item.Track switch { "敵行動" => Brushes.Salmon, "因子効果" => Brushes.Gold, "イマジン" => Brushes.MediumPurple, _ => Brushes.LightGreen },
                    ToolTip = $"{item.Time} {item.Actor}\n{item.Action}" };
                Canvas.SetLeft(marker, X(item.Seconds) - 4); Canvas.SetTop(marker, y + 2); Plot.Children.Add(marker);
            }
        }
        Plot.Height = Math.Max(350, top + height + 60 + row * 28);
        EventList.ItemsSource = events.TakeLast(500).ToArray();
    }

    private void ToggleMeasurementSettings_Click(object sender, RoutedEventArgs e)
    {
        MeasurementSettings.Visibility = MeasurementSettings.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try { CombatExtensionSettings.Save(ReadOptions()); Status.Text = "設定を保存しました。時計とリセット時間は次の集計から適用します。"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { Status.Text = "保存できませんでした：" + ex.Message; }
    }
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        MessageManager.RunOnPacketThread(() => EncounterManager.EnterDungeon(false, true, EncounterStartReason.Force));
    }
    private void LoadHistory()
    {
        try { History.ItemsSource = EncounterHistoryProvider.GetEntries().Select(x => new HistoryRow(x.EncounterId,
            $"{x.StartTime:yyyy/MM/dd HH:mm:ss} {x.SceneName} #{x.EncounterId}")).ToArray(); }
        catch (Exception ex) { Status.Text = "履歴を読み込めませんでした：" + ex.Message; }
    }
    private void RefreshHistory_Click(object sender, RoutedEventArgs e) => LoadHistory();
    private async void History_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (History.SelectedItem is not HistoryRow row) return;
        var result = await EncounterHistoryProvider.SelectAsync(row.Id);
        Status.Text = result == HistorySelectResult.Opened ? "履歴を選択しました。" : $"履歴を開けませんでした：{result}";
        await RefreshAsync();
    }
    private async void Live_Click(object sender, RoutedEventArgs e)
    {
        EncounterHistoryProvider.SelectLive(); History.SelectedItem = null; await RefreshAsync();
    }
    private void TimeOffset_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (!_updating) Draw(); }
    private void Actors_Selected(object sender, SelectionChangedEventArgs e) { if (!_updating) Draw(); }
    private void AllActors_Click(object sender, RoutedEventArgs e) => Actors.SelectedItems.Clear();
}
