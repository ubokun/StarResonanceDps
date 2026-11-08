using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
namespace StarResonanceDps.App.Views;
public sealed class EnemyCatalogRow
{
    public required EnemyCatalogEntry Entry {get;init;}
    public bool Enabled {get;set;}
    public string Name => string.IsNullOrWhiteSpace(CombatDataCatalog.GetMonsterName(Entry.TypeId)) ? "未知の敵" : CombatDataCatalog.GetMonsterName(Entry.TypeId);
    public string FirstSeen => Entry.FirstSeenUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
    public string LastSeen => Entry.LastSeenUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
}
public partial class EnemyCatalogWindow : Window
{
    private EnemyCatalogRow[] _rows=[];
    public EnemyCatalogWindow(){InitializeComponent();ExtensionWindowLayout.Attach(this);ApplyTracking.IsChecked=CombatExtensionSettings.Current.UseEnemyCatalog;Reload();}
    private void Reload(){_rows=EnemyCatalogStore.Current.Read().Select(x=>new EnemyCatalogRow {Entry=x,Enabled=x.Enabled}).ToArray();Filter();Status.Text=EnemyCatalogStore.Current.LastIssue ?? $"{_rows.Length}種類。下の一覧は観測した技です。個体別の詠唱と技の開始は拡張タイムラインで表示します。";}
    private void Filter(){if(Enemies is null)return;var query=Search.Text;Enemies.ItemsSource=_rows.Where(x=>string.IsNullOrWhiteSpace(query)||($"{x.Name} {x.Entry.TypeId} {x.Entry.Server}").Contains(query,StringComparison.CurrentCultureIgnoreCase)).ToArray();}
    private void Search_Changed(object sender,TextChangedEventArgs e)=>Filter();
    private void Reload_Click(object sender,RoutedEventArgs e)=>Reload();
    private void Save_Click(object sender,RoutedEventArgs e)
    {
        Enemies.CommitEdit(DataGridEditingUnit.Cell,true);Enemies.CommitEdit(DataGridEditingUnit.Row,true);
        foreach(var row in _rows)EnemyCatalogStore.Current.SetEnabled(row.Entry.Key,row.Enabled);
        EnemyCatalogStore.Current.Save();
        try{CombatExtensionSettings.Save(CombatExtensionSettings.Current with {UseEnemyCatalog=ApplyTracking.IsChecked==true});Status.Text=EnemyCatalogStore.Current.LastIssue ?? "追跡設定を保存しました。タイムラインの次の更新から適用します。";}catch(Exception ex){Status.Text="設定を保存できませんでした："+ex.Message;}
    }
    private void Enemy_Selected(object sender,SelectionChangedEventArgs e){ObservedSkills.ItemsSource=(Enemies.SelectedItem as EnemyCatalogRow)?.Entry.SkillIds.Select(id=>new {Name=$"{CombatDataCatalog.GetSkillDisplayName(id)} [#{id}]"}).ToArray();}
    private async void Import_Click(object sender,RoutedEventArgs e)
    {
        ImportButton.IsEnabled=false;Status.Text="過去の履歴を取り込み中…";
        try
        {
            var count=await Task.Run(()=>{var n=0;foreach(var row in EncounterHistoryProvider.GetEntries()){var encounter=DB.LoadEncounter(row.EncounterId);if(encounter is null)continue;foreach(var enemy in encounter.Entities.Values)EnemyCatalogStore.Current.Observe(encounter,enemy.UUID,row.StartTime.ToUniversalTime(),saved:true);n++;}EnemyCatalogStore.Current.Save();return n;});
            Reload();Status.Text=$"{count}件の履歴を確認しました。履歴にない敵や未受信の技は補完しません。";
        }catch(Exception ex){Status.Text="履歴からの取り込みに失敗しました："+ex.Message;}finally{ImportButton.IsEnabled=true;}
    }
    private async void OpenHistory_Click(object sender,RoutedEventArgs e){if(Enemies.SelectedItem is not EnemyCatalogRow row || row.Entry.Encounters.Count==0){Status.Text="保存済みの関連集計がありません。";return;}Status.Text="履歴を読み込み中…";var result=await EncounterHistoryProvider.SelectAsync(row.Entry.Encounters[^1]);Status.Text=result==HistorySelectResult.Opened ? "関連集計を開きました。各メーターに反映されます。" : "集計を開けませんでした（削除済みの場合があります）："+result;}
}
