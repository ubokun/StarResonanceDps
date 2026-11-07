using System.IO;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.Models;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Views;

public partial class UserBuildWindow : Window
{
    private readonly SavedPlayerBuildStore _store = new();
    private List<SavedPlayerBuild> _records = [];
    private SavedPlayerBuild? _preview;
    private bool _refreshing;
    private sealed record UserRow(string Server, long CharacterId, string Label);

    public UserBuildWindow()
    {
        InitializeComponent();
        Reload();
    }

    public static async Task OpenForPlayerAsync(long characterId)
    {
        try
        {
            var preview = await PlayerBuildCapture.CaptureAsync(characterId).WaitAsync(TimeSpan.FromSeconds(10));
            var window = new UserBuildWindow { Owner = Application.Current.MainWindow };
            window._preview = preview;
            window.ShowBuild(preview);
            window.SaveButton.IsEnabled = true;
            window.Status.Text = "集計から取得したビルドのプレビューです。「このビルドを保存」で記録します。";
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Application.Current.MainWindow, ex.Message, "ビルドを確認できませんでした", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Reload(Guid? selectedId = null)
    {
        try
        {
            _records = _store.Load(out var failed);
            _refreshing = true;
            Users.ItemsSource = _records.GroupBy(x => (x.Server, x.CharacterId))
                .Select(g => new UserRow(g.Key.Server, g.Key.CharacterId, g.First().UserLabel)).ToList();
            Builds.ItemsSource = null;
            Encounters.ItemsSource = null;
            Details.Clear();
            Timeline.Clear();
            _refreshing = false;
            var selected = _records.FirstOrDefault(x => x.Id == selectedId);
            if (selected is not null)
            {
                Users.SelectedItem = Users.Items.Cast<UserRow>().First(x => x.Server == selected.Server && x.CharacterId == selected.CharacterId);
                Builds.SelectedItem = selected;
            }
            Status.Text = failed > 0 ? $"{failed}件を読み込めませんでした。元のファイルは保持しています。"
                : _records.Count == 0 ? "集計メーターの名前を右クリックし、「ビルドを確認・保存」から追加できます。" : "ユーザー → ビルド → 集計の順に選択できます。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Status.Text = $"読み込みに失敗しました：{ex.Message}"; }
        finally { _refreshing = false; }
    }

    private void User_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || Users.SelectedItem is not UserRow user) return;
        Builds.ItemsSource = _records.Where(x => x.Server == user.Server && x.CharacterId == user.CharacterId).ToList();
        Details.Clear(); Timeline.Clear(); Encounters.ItemsSource = null;
        _preview = null; SaveButton.IsEnabled = false;
        if (Builds.Items.Count > 0) Builds.SelectedIndex = 0;
    }

    private void Build_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || Builds.SelectedItem is not SavedPlayerBuild build) return;
        _preview = null; SaveButton.IsEnabled = false;
        ShowBuild(build);
    }

    private void ShowBuild(SavedPlayerBuild build)
    {
        Details.Text = build.Details;
        Encounters.ItemsSource = build.Encounters;
        Timeline.Clear();
        if (Encounters.Items.Count > 0) Encounters.SelectedIndex = 0;
    }

    private void Encounter_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (Encounters.SelectedItem is SavedBuildEncounter encounter)
            Timeline.Text = string.IsNullOrWhiteSpace(encounter.Timeline) ? "スキル使用の記録はありません。" : encounter.Timeline;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_preview is null) return;
        try
        {
            var saved = _store.Save(_preview);
            _preview = null; SaveButton.IsEnabled = false;
            Reload(saved.Id);
            Status.Text = "ビルドと集計の紐づけを保存しました。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { Status.Text = $"保存に失敗しました：{ex.Message}"; }
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        _preview = null; SaveButton.IsEnabled = false;
        Reload();
    }

    private async void OpenEncounter_Click(object sender, RoutedEventArgs e)
    {
        if (Encounters.SelectedItem is not SavedBuildEncounter encounter) return;
        try
        {
            var result = await EncounterHistoryProvider.SelectAsync(encounter.EncounterId);
            Status.Text = result switch
            {
                HistorySelectResult.Opened => "集計を選択しました。メーターと集計画面で確認できます。",
                HistorySelectResult.NotFound => "集計が見つかりません。未保存、または履歴の保存上限などで削除されています。ビルドは保持しています。",
                HistorySelectResult.Superseded => "ほかの集計が選択されたため切り替えを中止しました。",
                _ => "集計の読み込みに失敗しました。"
            };
        }
        catch (Exception ex) { Status.Text = $"集計を開けませんでした：{ex.Message}"; }
    }
}
