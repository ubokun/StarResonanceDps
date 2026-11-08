using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
namespace StarResonanceDps.App.Views;
public partial class HateMeterWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _reading, _updating;
    private long _targetUuid;
    private sealed record HistoryRow(ulong Id,string Label) { public override string ToString() => Label; }
    public HateMeterWindow(long enemyUuid = 0)
    {
        _targetUuid=enemyUuid; InitializeComponent(); ExtensionWindowLayout.Attach(this);
        _timer.Tick += async (_,_)=> await RefreshAsync();
        Loaded += (_,_)=>StartWidget();
        Closed += (_,_)=>_timer.Stop();
    }
    internal void SelectEnemy(long uuid) { _targetUuid=uuid; _=RefreshAsync(); }
    internal async void StartWidget() { ReloadHistory(); _timer.Start(); await RefreshAsync(); }
    internal void StopWidget() => _timer.Stop();
    private async Task RefreshAsync()
    {
        if (_reading || _updating) return; _reading=true;
        var target=_targetUuid;var partyOnly=PartyOnly.IsChecked==true;
        try { var snapshot=await HateMeterReader.ReadAsync(target,partyOnly).WaitAsync(TimeSpan.FromSeconds(10)); if(target==_targetUuid && partyOnly==(PartyOnly.IsChecked==true)) SetSnapshot(snapshot); }
        catch(Exception ex) { Status.Text="更新できませんでした："+ex.Message; }
        finally { _reading=false; }
    }
    internal void SetSnapshot(HateMeterSnapshot snapshot)
    {
        var selectedPlayer = (Players.SelectedItem as HatePlayerMeter)?.Bar.Uuid;
        var selectedEnemy = _targetUuid;
        _updating=true;
        var enemies = new[]{new TimelineActor(0,"すべての敵",true)}.Concat(snapshot.Enemies).ToList();
        if(selectedEnemy!=0 && !enemies.Any(x=>x.Uuid==selectedEnemy)) enemies.Add(new(selectedEnemy,$"指定した敵（この集計には未記録） [{selectedEnemy}]",true));
        if (!Enemy.Items.Cast<TimelineActor>().SequenceEqual(enemies)) { Enemy.ItemsSource=enemies; Enemy.SelectedItem=enemies.FirstOrDefault(x=>x.Uuid==selectedEnemy) ?? enemies[0]; }
        Players.ItemsSource=snapshot.Players;
        Players.SelectedItem=snapshot.Players.FirstOrDefault(x=>x.Bar.Uuid==selectedPlayer) ?? snapshot.Players.FirstOrDefault();
        Caption.Text=snapshot.Caption+$" · {snapshot.Duration:0.0}秒";
        Status.Text=$"初回観測 {snapshot.Baselines}件（増加量へ加算なし）・減少分 {snapshot.Reductions:N0}（除外）。技が複数重なる／通知間隔が2秒を超える区間は帰属不明。単一候補でも継続効果などが混ざるため確定値ではありません。" + (snapshot.Players.Count==0 ? " ヘイト増加記録なし。旧履歴にはこの記録がありません。" : "");
        _updating=false; ShowSkills();
    }
    private void ShowSkills() { var player=Players.SelectedItem as HatePlayerMeter; Skills.ItemsSource=player?.Skills; SkillCaption.Text=player is null ? "技別割合（推定）" : player.Bar.Name+"：技別割合（推定）"; }
    private void Player_Selected(object sender,SelectionChangedEventArgs e) { if(!_updating) ShowSkills(); }
    private async void Enemy_Selected(object sender,SelectionChangedEventArgs e) { if(!_updating) { _targetUuid=(Enemy.SelectedItem as TimelineActor)?.Uuid ?? 0; await RefreshAsync(); } }
    private async void Scope_Click(object sender,RoutedEventArgs e)=>await RefreshAsync();
    private void ReloadHistory() { try { History.ItemsSource=EncounterHistoryProvider.GetEntries().Select(x=>new HistoryRow(x.EncounterId,$"{x.StartTime:MM/dd HH:mm} {x.SceneName} #{x.EncounterId}")).ToArray(); } catch(Exception ex){Status.Text=ex.Message;} }
    private void Reload_Click(object sender,RoutedEventArgs e)=>ReloadHistory();
    private async void History_Selected(object sender,SelectionChangedEventArgs e) { if(History.SelectedItem is not HistoryRow row)return; var result=await EncounterHistoryProvider.SelectAsync(row.Id); if(result!=HistorySelectResult.Opened)Status.Text="履歴を開けませんでした："+result; await RefreshAsync(); }
    private async void Live_Click(object sender,RoutedEventArgs e){EncounterHistoryProvider.SelectLive();History.SelectedItem=null;await RefreshAsync();}
}
