using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 集計タブ。**エンカウンターという1つの対象への操作をまとめた場所。**
///
/// <para>
/// メーターのヘッダーにある 計測 / リセット と同じものをここにも置く。
/// どちらから押しても対象は1つ(<c>EncounterManager.Current</c>)で、
/// ウィジェットごとに分かれてはいない。
/// </para>
///
/// <para>
/// 履歴を選ぶと <c>AppState.OpenedHistoricalEncounter</c> が入り、
/// **メーター・スキル詳細・グラフ・集計のすべてがその戦闘を表示したまま止まる。**
/// **ライブへ戻す操作は「選択中の行をもう一度押す」だけ。**
/// 見出しは状態を出すだけで、押せる要素ではない。
/// </para>
/// </summary>
public sealed partial class AggregationViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<EncounterHistoryItem> _entries = [];

    /// <summary>ライブ(現在の戦闘)を見ているか。履歴を開いていない状態。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isLiveSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BenchmarkText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isBenchmarkActive;

    /// <summary>
    /// 見出しに出す状態。**優先順位は 履歴 &gt; 計測 &gt; 集計。**
    ///
    /// <para>
    /// 計測中に履歴を開いても計測は止まらない(<c>SelectAsync</c> は計測に触らない)ので、
    /// そのときは「履歴表示中」が正しい。計測 / リセット は回を作り直すので、
    /// 設定「次のイベントで解除」がオンなら履歴の表示が解除され、見出しが切り替わる。
    /// </para>
    /// </summary>
    public string StatusText => LocalizationManager.Instance.GetString(
        !IsLiveSelected ? "Aggregation_StatusHistory"
        : IsBenchmarkActive ? "Aggregation_StatusBenchmark"
        : "Aggregation_StatusLive");

    public string BenchmarkText => LocalizationManager.Instance.GetString(
        IsBenchmarkActive ? "Meter_StopBenchmark" : "Meter_Benchmark");

    public string ResetText => LocalizationManager.Instance.GetString("Meter_Reset");

    public string EmptyText => LocalizationManager.Instance.GetString("Aggregation_NoHistory");

    public bool HasEntries => Entries.Count > 0;

    /// <summary>
    /// 読み込み中の回(最後に押した行)。表示中になったとき、開けなかったとき、ライブへ戻したときに外す。
    /// 先に押した行の読み込みは、後から押した行に置き換わる(反映されるのは最後に押した行だけ)。
    /// </summary>
    private ulong? _loadingEncounterId;

    /// <summary>履歴を開けなかった(読み込みで例外)。エラーの窓はビューが出す(持ち主の窓を知っているのはビュー)。</summary>
    public event EventHandler? LoadFailed;

    /// <summary>一覧を読み直す。タブを開いたときと、戦闘が保存されたときに呼ぶ。</summary>
    public void Reload()
    {
        var selected = EncounterHistoryProvider.SelectedEncounterId;
        var items = new ObservableCollection<EncounterHistoryItem>();
        foreach (var entry in EncounterHistoryProvider.GetEntries())
        {
            items.Add(new EncounterHistoryItem(entry)
            {
                IsSelected = entry.EncounterId == selected,
                IsLoading = entry.EncounterId == _loadingEncounterId
            });
        }

        Entries = items;
        IsLiveSelected = selected is null;
        OnPropertyChanged(nameof(HasEntries));
    }

    /// <summary>
    /// ベンチマークの状態を取り直す。計測の状態は Core が通知を出さないので、
    /// ビュー側が定期的に呼ぶ。値が変わらなければ <c>ObservableProperty</c> が何も通知しない。
    /// </summary>
    public void RefreshBenchmarkState()
    {
        IsBenchmarkActive = MeterSnapshotProvider.GetBenchmarkState().IsActive;
    }

    /// <summary>
    /// ライブへ戻す。**コマンドにはしない** — 画面に「ライブへ戻す」ボタンは無く、
    /// 呼ぶのは <see cref="SelectEntry"/> のトグル1か所だけ。
    /// </summary>
    private void SelectLive()
    {
        _loadingEncounterId = null;
        EncounterHistoryProvider.SelectLive();
        ApplySelection(null);
    }

    /// <summary>
    /// 行を押した。DB は裏のスレッドで読む(画面を止めない)ので、読み終えるまで行に「読み込み中」を出す。
    /// 続けて別の行を押してもよく、反映されるのは最後に押した行だけ。
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SelectEntry(EncounterHistoryItem? item)
    {
        if (item is null || item.IsLoading)
        {
            return;
        }

        // 選択中の行をもう一度押したら解除してライブへ戻す。
        // ライブへ戻す入口はここ1つなので、押し先を探させない。
        if (item.IsSelected)
        {
            SelectLive();
            return;
        }

        var encounterId = item.EncounterId;
        _loadingEncounterId = encounterId;
        UpdateLoadingFlags();

        var result = await EncounterHistoryProvider.SelectAsync(encounterId);

        // 後から別の行が押されたか、ライブへ戻したか、もう表示中になった。読み込み中の印はそちらが持つ(外した)。
        if (_loadingEncounterId != encounterId)
        {
            return;
        }

        switch (result)
        {
            case HistorySelectResult.Opened:
                // The provider now completes only after selection has actually been applied.
                // Reconcile here too, so a missed/unsubscribed UI notification cannot strand the spinner.
                _loadingEncounterId = null;
                SyncSelection();
                return;
            case HistorySelectResult.Failed:
                // 開けなかったら選択を動かさない。読めないものを「表示中」にしない。
                _loadingEncounterId = null;
                UpdateLoadingFlags();
                LoadFailed?.Invoke(this, EventArgs.Empty);
                return;
            default:
                _loadingEncounterId = null;
                UpdateLoadingFlags();
                return;
        }
    }

    [RelayCommand]
    private void ToggleBenchmark()
    {
        MeterSnapshotProvider.ToggleBenchmark();
        RefreshBenchmarkState();
    }

    [RelayCommand]
    private void ResetEncounter()
    {
        MeterSnapshotProvider.ResetCurrentEncounter();
    }

    /// <summary>
    /// 選択が変わったときに、一覧の「表示中」を合わせ直す。
    /// 押した回の反映(読み込みの後にパケットのスレッドで行う)と、戦闘とエンカウンターの作り直しによる自動解除がここを通る。
    /// **通らないと「表示中」が出ない・残ったままになる。**
    /// </summary>
    public void SyncSelection()
    {
        ApplySelection(EncounterHistoryProvider.SelectedEncounterId);
    }

    private void ApplySelection(ulong? encounterId)
    {
        foreach (var entry in Entries)
        {
            entry.IsSelected = entry.EncounterId == encounterId;
        }

        // 読み込んでいた行が表示中になった。
        if (encounterId is not null && encounterId == _loadingEncounterId)
        {
            _loadingEncounterId = null;
        }

        UpdateLoadingFlags();
        IsLiveSelected = encounterId is null;
    }

    private void UpdateLoadingFlags()
    {
        foreach (var entry in Entries)
        {
            entry.IsLoading = entry.EncounterId == _loadingEncounterId;
        }
    }

    /// <summary>
    /// 言語が変わったときに呼ぶ。**購読はビュー側が持つ** — <c>LocalizationManager</c> は
    /// singleton なので、ここで購読するとビューを開き直すたびに古いインスタンスが残る。
    /// </summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(BenchmarkText));
        OnPropertyChanged(nameof(ResetText));
        OnPropertyChanged(nameof(EmptyText));
        foreach (var entry in Entries)
        {
            entry.RefreshTexts();
        }
    }
}

/// <summary>履歴一覧の1行。</summary>
public sealed partial class EncounterHistoryItem : ObservableObject
{
    private readonly EncounterHistoryEntry _entry;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>押した回を DB から読んでいる間。「表示中」の場所に「読み込み中」を出す。</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>シーンIDが無いなどで名前を出せない行の表記。バフリストの「??」と同じく記号なのでリソースを持たない。</summary>
    private const string UnknownSceneText = "？？？";

    public EncounterHistoryItem(EncounterHistoryEntry entry)
    {
        _entry = entry;
    }

    public ulong EncounterId => _entry.EncounterId;

    public string TimeText => _entry.StartTime.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// シーン名は<b>表示時に引き直す</b>。DBの <c>SceneName</c> は記録時の言語と
    /// 内部ID注記のまま固定されているので、そのまま出すと表示言語に追従しない。
    /// 引けなかったときだけ保存値へ落とす。チャンネルのある回はプレイヤーリストの左下と同じ書き方で付ける。
    ///
    /// <para>
    /// 注記は<b>半角括弧で詰めて末尾に足す</b>(スペースを入れない)。
    /// 計測はボタンと同じ <c>Meter_Benchmark</c>で、
    /// 言語を切り替えると一緒に変わる(<see cref="RefreshTexts"/> が再通知する)。
    /// 「進行で自動リセット」で分けた記録も注記を付けず、同じ名前で出す。
    /// </para>
    /// </summary>
    public string SceneText
    {
        get
        {
            var resolved = CombatDataCatalog.GetSceneName(_entry.SceneId, _entry.DungeonDifficulty);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                resolved = _entry.SceneName;
            }

            var text = string.IsNullOrWhiteSpace(resolved)
                ? UnknownSceneText
                : WidgetListItemViewModel.FormatMapName(resolved, _entry.ChannelLine);

            if (_entry.BenchmarkSeconds > 0)
            {
                text += $"({LocalizationManager.Instance.GetString("Meter_Benchmark")})";
            }

            return text;
        }
    }

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(SceneText));
    }
}
