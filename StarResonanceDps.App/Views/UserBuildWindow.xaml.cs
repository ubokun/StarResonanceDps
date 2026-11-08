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
        ExtensionWindowLayout.Attach(this);
        Reload();
        Loaded += (_,_) => StartWidget();
        Closed += (_,_) => { ++_previewRequest; StarResonanceDps.Core.Services.PlayerRosterPresentationStore.Instance.RosterChanged -= RosterChanged; };
    }
    internal void StartWidget() { StarResonanceDps.Core.Services.PlayerRosterPresentationStore.Instance.RosterChanged += RosterChanged; RefreshUsers(); }
    internal void StopWidget() { ++_previewRequest; StarResonanceDps.Core.Services.PlayerRosterPresentationStore.Instance.RosterChanged -= RosterChanged; }
    private void RosterChanged(object? sender, StarResonanceDps.Core.Services.PlayerRosterChangedEventArgs e) => Dispatcher.BeginInvoke(RefreshUsers);
    private void RefreshUsers()
    {
        var previous=Users.SelectedItem as UserRow;
        var rows=_records.GroupBy(x=>(x.Server,x.CharacterId)).Select(g=>new UserRow(g.Key.Server,g.Key.CharacterId,g.First().UserLabel))
            .Concat(StarResonanceDps.Core.Services.PlayerRosterPresentationStore.Instance.Current.Entries.Where(x=>!x.IsNpc)
                .Select(x=>new UserRow(x.GameServer.ToString()!,x.CharacterId,x.Name+"（観測中）")))
            .DistinctBy(x=>(x.Server,x.CharacterId)).ToList();
        if(Users.Items.Cast<UserRow>().SequenceEqual(rows))
        {
            if(Users.SelectedItem is null && rows.Count>0) Users.SelectedIndex=0;
            return;
        }
        _refreshing=true; Users.ItemsSource=rows;
        Users.SelectedItem=rows.FirstOrDefault(x=>x.Server==previous?.Server && x.CharacterId==previous.CharacterId);
        _refreshing=false;
        if(Users.SelectedItem is null && rows.Count>0) Users.SelectedIndex=0;
    }

    public static Task OpenForPlayerAsync(long characterId)
    {
        ExtensionWidgetHost.Open(Models.Widgets.WidgetKind.UserBuildLibrary, characterId);
        return Task.CompletedTask;
    }

    internal async Task PreviewPlayerAsync(long characterId)
    {
        var request=++_previewRequest;
        try
        {
            var preview=await PlayerBuildCapture.CaptureAsync(characterId, allowObserved: true).WaitAsync(TimeSpan.FromSeconds(10));
            if(request!=_previewRequest)return;
            _refreshing=true;
            var users=Users.Items.Cast<UserRow>().ToList();
            var user=users.FirstOrDefault(x=>x.CharacterId==characterId && x.Server==preview.Server);
            if(user is null)
            {
                user=new UserRow(preview.Server,characterId,preview.PlayerName+"（観測中）");
                users.Add(user); Users.ItemsSource=users;
            }
            Users.SelectedItem=user;
            Builds.ItemsSource=_records.Where(x=>x.CharacterId==characterId && x.Server==preview.Server).ToList();
            _refreshing=false;
            _preview=preview; ShowBuild(preview); SaveButton.IsEnabled=true;
            Status.Text="観測したビルドです。「このビルドを保存」で記録します。未受信の構成は未取得と表示します。";
        }
        catch(Exception ex) { _refreshing=false; if(request==_previewRequest) Status.Text=ex.Message; }
    }
    private int _previewRequest;

    private void Reload(Guid? selectedId = null)
    {
        try
        {
            _records = _store.Load(out var failed);
            _refreshing = true;
            Users.ItemsSource = _records.GroupBy(x => (x.Server, x.CharacterId))
                .Select(g => new UserRow(g.Key.Server, g.Key.CharacterId, g.First().UserLabel))
                .Concat(StarResonanceDps.Core.Services.PlayerRosterPresentationStore.Instance.Current.Entries
                    .Where(x=>!x.IsNpc).Select(x=>new UserRow(x.GameServer.ToString()!,x.CharacterId,x.Name+"（観測中）")))
                .DistinctBy(x=>(x.Server,x.CharacterId)).ToList();
            Builds.ItemsSource = null;
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
                : _records.Count == 0 ? "観測中のユーザーを選択してビルドを確認・保存できます。未表示の場合は再読込してください。" : "ユーザーを選択して情報ウィジェットや保存ビルドを確認できます。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Status.Text = $"読み込みに失敗しました：{ex.Message}"; }
        finally { _refreshing = false; }
    }

    private async void User_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || Users.SelectedItem is not UserRow user) return;
        ++_previewRequest;
        Builds.ItemsSource = _records.Where(x => x.Server == user.Server && x.CharacterId == user.CharacterId).ToList();
        Details.Clear(); Timeline.Clear();
        _preview = null; SaveButton.IsEnabled = false;
        if (Builds.Items.Count > 0) Builds.SelectedIndex = 0;
        else await PreviewPlayerAsync(user.CharacterId);
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (Users.SelectedItem is UserRow user) await PreviewPlayerAsync(user.CharacterId);
    }

    private void Build_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || Builds.SelectedItem is not SavedPlayerBuild build) return;
        ++_previewRequest; _preview = null; SaveButton.IsEnabled = false;
        ShowBuild(build);
    }

    private void ShowBuild(SavedPlayerBuild build)
    {
        Details.Text = build.Details;
        Timeline.Text = !string.IsNullOrWhiteSpace(build.ObservedTimeline) ? build.ObservedTimeline
            : build.Encounters.OrderByDescending(x => x.CapturedUtc).FirstOrDefault()?.Timeline ?? "スキル使用の記録はありません。";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_preview is null) return;
        try
        {
            var saved = _store.Save(_preview);
            _preview = null; SaveButton.IsEnabled = false;
            Reload(saved.Id);
            Status.Text = "ビルドを保存しました。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { Status.Text = $"保存に失敗しました：{ex.Message}"; }
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        ++_previewRequest;
        _preview = null; SaveButton.IsEnabled = false;
        Reload();
        RefreshUsers();
    }

    private void OpenUserWidget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: UserRow user, Tag: string kind }
            || !Enum.TryParse<Models.Widgets.WidgetKind>(kind, out var widgetKind)) return;
        var roster = StarResonanceDps.Core.Services.PlayerRosterPresentationStore.Instance.Current.Entries;
        if (!roster.Any(p => p.CharacterId == user.CharacterId && p.GameServer.ToString() == user.Server))
        {
            Status.Text = "現在の受信情報にこのユーザーがいません。保存ビルドの詳細を確認してください。";
            return;
        }
        WidgetWindowManager.Instance.OpenPlayerWindow(widgetKind, user.CharacterId);
    }

}
