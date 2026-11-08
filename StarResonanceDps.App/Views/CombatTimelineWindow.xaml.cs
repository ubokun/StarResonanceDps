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
    private readonly HashSet<string> _selectedActions = [];
    private readonly HashSet<string> _forcedOn = [];
    private readonly HashSet<string> _forcedOff = [];
    private sealed record ActionChoice(string Key, string Label);
    private bool _reading;
    private bool _updating;
    private CombatTimelineSnapshot? _snapshot;
    private sealed record HistoryRow(ulong Id, string Label) { public override string ToString() => Label; }

    public CombatTimelineWindow()
    {
        InitializeComponent();
        ExtensionWindowLayout.Attach(this);
        var settings = CombatExtensionSettings.Current;
        FilterActions.IsChecked = settings.FilterActions;
        _forcedOn.UnionWith(settings.SelectedActions);
        _forcedOff.UnionWith(settings.HiddenActions);
        QuestClock.IsChecked = settings.SyncQuestClock; QuestReset.IsChecked = settings.ResetOnQuestTransition;
        AutoLink.IsChecked = settings.AutoLinkSavedPlayers;
        IdleSeconds.Text = settings.IdleResetSeconds.ToString(); WindowSeconds.Text = settings.VisibleSeconds.ToString();
        IntervalSeconds.Text = settings.IntervalResetSeconds.ToString();
        PartyOnly.IsChecked = settings.PartyOnly; Skills.IsChecked = settings.ShowSkills;
        Imagines.IsChecked = settings.ShowImagines; Factors.IsChecked = settings.ShowFactors; Enemies.IsChecked = settings.ShowEnemies;
        if (CombatExtensionSettings.LoadIssue is { } issue) Status.Text = issue;
        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += (_, _) => StartWidget();
        Closed += (_, _) => _timer.Stop();
    }

    private CombatExtensionOptions ReadOptions()
    {
        if (!int.TryParse(IdleSeconds.Text, out var idle) || !int.TryParse(WindowSeconds.Text, out var width)
            || !int.TryParse(IntervalSeconds.Text, out var interval))
            throw new ArgumentException("秒数は整数で入力してください。");
        var options = new CombatExtensionOptions { UseEnemyCatalog = CombatExtensionSettings.Current.UseEnemyCatalog, FilterActions = FilterActions.IsChecked == true, SelectedActions = _forcedOn.Order().ToArray(), HiddenActions = _forcedOff.Order().ToArray(), SyncQuestClock = QuestClock.IsChecked == true,
            ResetOnQuestTransition = QuestReset.IsChecked == true, IdleResetSeconds = idle, VisibleSeconds = width,
            IntervalResetSeconds = interval,
            AutoLinkSavedPlayers = AutoLink.IsChecked == true,
            PartyOnly = PartyOnly.IsChecked == true, ShowSkills = Skills.IsChecked == true,
            ShowImagines = Imagines.IsChecked == true, ShowFactors = Factors.IsChecked == true, ShowEnemies = Enemies.IsChecked == true };
        CombatExtensionSettings.Validate(options); return options;
    }

    internal async void StartWidget() { LoadHistory(); _timer.Start(); await RefreshAsync(); }
    internal void StopWidget() => _timer.Stop();
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
        UpdateActionChoices();
        _updating = false;
        Draw();
    }

    private void PartyDps_Click(object sender, RoutedEventArgs e) => ExtensionWidgetHost.Open(Models.Widgets.WidgetKind.PartyDps);

    private void Draw()
    {
        if (_snapshot is null || _updating) return;
        var options = ReadOptions();
        var selected = Actors.SelectedItems.Cast<TimelineActor>().Select(x => x.Uuid).ToHashSet();
        var start = TimeOffset.Value; var end = start + options.VisibleSeconds;
        var events = _snapshot.Events.Where(x => x.Seconds >= start && x.Seconds <= end
            && (selected.Count == 0 || selected.Contains(x.EntityUuid))
            && (FilterActions.IsChecked != true || _selectedActions.Contains(x.FilterKey))).ToArray();
        Plot.Children.Clear();
        const double left = 170, width = 920, top = 12, height = 0;
        double X(double seconds) => left + (seconds - start) / options.VisibleSeconds * width;
        void Label(string value, double x, double y, Brush? color = null)
        {
            var text = new TextBlock { Text = value, Foreground = color ?? (Brush)FindResource("Brush.TextSecondary"), FontSize = 11, MaxWidth = 164,
                TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = value };
            Canvas.SetLeft(text, x); Canvas.SetTop(text, y); Plot.Children.Add(text);
        }
        for (var index = 0; index <= 4; index++)
        {
            var seconds = start + index * options.VisibleSeconds / 4d;
            var line = new Line { X1 = X(seconds), X2 = X(seconds), Y1 = top, Y2 = top + height, Stroke = (Brush)FindResource("Brush.BorderSoft") };
            Plot.Children.Add(line); Label(TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss"), X(seconds) - 20, 8);
        }
        var tracks = events.GroupBy(x => (x.Track, x.EntityUuid, x.Actor))
            .Select(g => (Track: g.Key.Track, Uuid: g.Key.EntityUuid, Actor: g.Key.Actor, Events: g.ToArray())).ToList();
        var enemyActors = _snapshot.Actors.Where(x => x.IsEnemy && (selected.Count == 0 || selected.Contains(x.Uuid))).ToArray();
        foreach (var enemy in options.ShowEnemies ? enemyActors : [])
            if (!tracks.Any(x => x.Track == "敵行動" && x.Uuid == enemy.Uuid))
                tracks.Add(("敵行動", enemy.Uuid, enemy.Name, []));
        var row = 0;
        foreach (var track in tracks.OrderBy(x => x.Track == "敵行動" ? 1 : 0))
        {
            var y = top + height + 30 + row++ * 32;
            var number = Array.FindIndex(enemyActors, x => x.Uuid == track.Uuid);
            Label($"{track.Track} / {track.Actor}" + (track.Track == "敵行動" && number >= 0 ? $" ({number + 1})" : ""), 8, y + 5);
            Plot.Children.Add(new Line { X1 = left, X2 = left + width, Y1 = y + 8, Y2 = y + 8, Stroke = (Brush)FindResource("Brush.BorderSoft") });
            foreach (var item in track.Events)
            {
                var marker = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(3),
                    BorderBrush = (Brush)FindResource("Brush.Border"), BorderThickness = new Thickness(1),
                    Background = (Brush)FindResource("Brush.ControlBackground"), ToolTip = $"{item.Time} {item.Actor}\n{item.Action}" };
                marker.Child = new ContentControl { Content = item, ContentTemplate = (DataTemplate)FindResource("Extension.EventIcon") };
                Canvas.SetLeft(marker, X(item.Seconds) - 13); Canvas.SetTop(marker, y - 4); Plot.Children.Add(marker);
            }
        }
        Plot.Height = Math.Max(350, top + height + 60 + row * 32);
        EventList.ItemsSource = events.TakeLast(500).ToArray();
    }

    private void UpdateActionChoices()
    {
        if (_snapshot is null) return;
        var groups = _snapshot.Events.GroupBy(x => x.FilterKey).ToArray();
        _selectedActions.Clear();
        foreach (var group in groups)
            if (_forcedOn.Contains(group.Key) || (!_forcedOff.Contains(group.Key) && !CombatTimelineReader.IsFrequentAction(group, _snapshot.Duration)))
                _selectedActions.Add(group.Key);
        var choices = groups
            .Select(g => new ActionChoice(g.Key, g.First().FilterLabel + (CombatTimelineReader.IsFrequentAction(g, _snapshot.Duration) ? "（高頻度）" : "")))
            .Where(x => string.IsNullOrWhiteSpace(ActionSearch.Text) || x.Label.Contains(ActionSearch.Text, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(x => x.Label).ToArray();
        if (ActionChoices.Items.Cast<ActionChoice>().SequenceEqual(choices)
            && ActionChoices.SelectedItems.Cast<ActionChoice>().Select(x => x.Key).ToHashSet().SetEquals(choices.Where(x => _selectedActions.Contains(x.Key)).Select(x => x.Key))) return;
        var updating = _updating; _updating = true;
        ActionChoices.ItemsSource = choices;
        foreach (var choice in choices.Where(x => _selectedActions.Contains(x.Key))) ActionChoices.SelectedItems.Add(choice);
        _updating = updating;
    }
    private void EnemyCatalog_Click(object sender, RoutedEventArgs e) => new EnemyCatalogWindow { Owner = this }.Show();
    private void ToggleActionFilter_Click(object sender, RoutedEventArgs e)
        => ActionFilterPanel.Visibility = ActionFilterPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void ActionSearchChanged(object sender, TextChangedEventArgs e) { if (ActionChoices is not null) UpdateActionChoices(); }
    private void ActionFilterChanged(object sender, RoutedEventArgs e) => Draw();
    private void ActionChoices_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        foreach (ActionChoice choice in e.RemovedItems) { _selectedActions.Remove(choice.Key); _forcedOn.Remove(choice.Key); _forcedOff.Add(choice.Key); }
        foreach (ActionChoice choice in e.AddedItems) { _selectedActions.Add(choice.Key); _forcedOn.Add(choice.Key); _forcedOff.Remove(choice.Key); }
        Draw();
    }
    private void SelectAllActions_Click(object sender, RoutedEventArgs e)
    {
        foreach (ActionChoice choice in ActionChoices.Items) { _forcedOn.Add(choice.Key); _forcedOff.Remove(choice.Key); }
        UpdateActionChoices(); Draw();
    }
    private void DefaultActions_Click(object sender, RoutedEventArgs e)
    {
        _forcedOn.Clear(); _forcedOff.Clear(); UpdateActionChoices(); Draw();
    }
    private void ClearActions_Click(object sender, RoutedEventArgs e)
    {
        foreach (var key in _snapshot?.Events.Select(x => x.FilterKey).Distinct() ?? []) { _forcedOn.Remove(key); _forcedOff.Add(key); }
        UpdateActionChoices(); Draw();
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
