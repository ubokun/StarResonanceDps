// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Views;
using StarResonanceDps.Core.Diagnostics;

namespace StarResonanceDps.App.ViewModels;

/// <summary>フィードバックタブ。投稿画面を開くことと、検出された項目の一覧と報告。</summary>
public sealed partial class FeedbackViewModel : ObservableObject
{
    private readonly Dictionary<(ResidentDetectionKind Kind, string Id), FeedbackItemViewModel> _itemsByDetection = [];
    private readonly List<FeedbackItemViewModel> _orderedItems = [];

    /// <summary>確認の途中だけある。</summary>
    private CancellationTokenSource? _checkCancellation;

    [ObservableProperty]
    private bool _hasItems;

    /// <summary>確認のボタンの文字。確認の途中は「確認をキャンセル」。</summary>
    [ObservableProperty]
    private string _checkReportStatusText = string.Empty;

    public FeedbackViewModel()
    {
        UpdateButtons();
    }

    public ObservableCollection<FeedbackItemViewModel> Items { get; } = [];

    /// <summary>控えから行を読み直す。行は作り直さず値だけ入れ替える。並びは未報告 → 報告済みで、それぞれ検知した順。</summary>
    public void Refresh()
    {
        var detections = ResidentDetectionStore.GetSnapshot();
        CollectRows(detections, isReported: false);
        CollectRows(detections, isReported: true);
        CollectionOrder.Apply(Items, _orderedItems);
        _orderedItems.Clear();

        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].ShowsDivider = index < Items.Count - 1;
        }

        HasItems = Items.Count > 0;
        UpdateButtons();
    }

    public void RefreshTexts()
    {
        foreach (var item in Items)
        {
            item.RefreshText();
        }

        UpdateButtons();
    }

    private void CollectRows(IReadOnlyList<ResidentDetection> detections, bool isReported)
    {
        foreach (var detection in detections)
        {
            if (detection.IsReported != isReported)
            {
                continue;
            }

            var key = (detection.Kind, detection.Id);
            if (_itemsByDetection.TryGetValue(key, out var item))
            {
                item.Apply(detection);
            }
            else
            {
                item = new FeedbackItemViewModel(detection, ReportSelectedCommand.NotifyCanExecuteChanged);
                _itemsByDetection.Add(key, item);
            }

            _orderedItems.Add(item);
        }
    }

    [RelayCommand]
    private void OpenIssue()
    {
        ExternalLinkOpener.Open(FeedbackIssueLinks.CreateNewIssueUrl());
    }

    private bool CanCheckReportStatus()
    {
        return _checkCancellation is not null || Items.Count > 0;
    }

    private bool CanReportSelected()
    {
        return _checkCancellation is null && Items.Any(item => item.IsSelected);
    }

    /// <summary>
    /// 一覧の全部の項目で、同じ報告がIssueにあるかを確かめ直す。同じ報告が無くなった報告済みの項目は未報告に戻す。投稿画面は開かない。
    /// 最後まで確かめたら、全部の行のチェックを既定(報告済みでない行だけ)へ付け直す。確認できなかったときは付け直さない。
    /// どちらのボタンの確認の途中でもキャンセルになるので、途中でも押せる。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCheckReportStatus), AllowConcurrentExecutions = true)]
    private async Task CheckReportStatusAsync()
    {
        if (_checkCancellation is { } running)
        {
            running.Cancel();
            return;
        }

        var detections = Items.Select(item => item.Detection).ToList();
        if (await RunCheckAsync(cancellationToken => FeedbackIssueLookup.CheckAllAsync(detections, cancellationToken)) is not { } lookup)
        {
            return;
        }

        ResidentDetectionStore.ApplyReportCheck(lookup.Reported, lookup.NotReported);
        Refresh();

        if (lookup.Failure is { } failure)
        {
            DetectionReportMessage.ShowCheckFailed(Application.Current?.MainWindow, failure);
            return;
        }

        foreach (var item in Items)
        {
            item.ResetSelection();
        }
    }

    /// <summary>
    /// 選んだ項目を全部確かめ、同じ報告がIssueにあるものを「報告済み」にして、無いものを1つの画面に入る分ずつ投稿画面に分けて全部開く。
    /// 確認できなければ開かない。開けた画面に入れた行は、投稿したか分からないので印を付けず、チェックだけ外す。
    /// 開けない画面があったらそこで止め、残りの行はチェックを付けたまま残す。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanReportSelected))]
    private async Task ReportSelectedAsync()
    {
        var selected = Items.Where(item => item.IsSelected).Select(item => item.Detection).ToList();
        if (await RunCheckAsync(cancellationToken => FeedbackIssueLookup.CheckAllAsync(selected, cancellationToken)) is not { } lookup)
        {
            return;
        }

        ResidentDetectionStore.ApplyReportCheck(lookup.Reported, lookup.NotReported);
        Refresh();

        var owner = Application.Current?.MainWindow;
        if (lookup.Failure is { } failure)
        {
            DetectionReportMessage.ShowCheckFailed(owner, failure);
            return;
        }

        if (lookup.NotReported.Count == 0)
        {
            DetectionReportMessage.ShowAlreadyReported(owner);
            return;
        }

        var openedCount = 0;
        foreach (var link in FeedbackIssueLinks.CreateDetectionReportLinks(lookup.NotReported))
        {
            if (!ExternalLinkOpener.Open(link.Url))
            {
                break;
            }

            ClearSelection(link.Detections);
            openedCount++;
        }

        if (openedCount > 0)
        {
            DetectionReportMessage.ShowOpened(owner, openedCount);
        }
    }

    /// <summary>キャンセルされたら null。</summary>
    private async Task<FeedbackIssueLookupResult?> RunCheckAsync(Func<CancellationToken, Task<FeedbackIssueLookupResult>> check)
    {
        using var cancellation = new CancellationTokenSource();
        _checkCancellation = cancellation;
        UpdateButtons();
        try
        {
            return await check(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Log.Information("The report check was cancelled");
            return null;
        }
        finally
        {
            _checkCancellation = null;
            UpdateButtons();
        }
    }

    private void ClearSelection(IEnumerable<ResidentDetection> detections)
    {
        foreach (var detection in detections)
        {
            if (_itemsByDetection.TryGetValue((detection.Kind, detection.Id), out var item))
            {
                item.IsSelected = false;
            }
        }
    }

    private void UpdateButtons()
    {
        CheckReportStatusText = LocalizationManager.Instance.GetString(
            _checkCancellation is null ? "Feedback_Detections_Check" : "Feedback_Detections_CancelCheck");
        CheckReportStatusCommand.NotifyCanExecuteChanged();
        ReportSelectedCommand.NotifyCanExecuteChanged();
    }
}
