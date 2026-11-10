using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.Diagnostics;

namespace StarResonanceDps.App.ViewModels;

/// <summary>フィードバックタブの検出された項目の1行。チェックは保存しない(既定は報告済みでない行だけ入れる)。報告済みの行はチェックできない。</summary>
public sealed partial class FeedbackItemViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    [ObservableProperty]
    private string _itemText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    private bool _isReported;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _showsDivider;

    public FeedbackItemViewModel(ResidentDetection detection, Action selectionChanged)
    {
        _selectionChanged = selectionChanged;
        Detection = detection;
        IsReported = detection.IsReported;
        ResetSelection();
        TimeText = detection.DetectedAt.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
        RefreshText();
    }

    public ResidentDetection Detection { get; private set; }

    public string TimeText { get; }

    public bool CanSelect => !IsReported;

    /// <summary>報告済みの状態が変わったら、チェックを既定へ戻す。</summary>
    public void Apply(ResidentDetection detection)
    {
        Detection = detection;
        if (detection.IsReported == IsReported)
        {
            return;
        }

        IsReported = detection.IsReported;
        ResetSelection();
    }

    /// <summary>チェックを既定(報告済みでない行だけ入れる)へ戻す。</summary>
    public void ResetSelection()
    {
        IsSelected = !IsReported;
    }

    public void RefreshText()
    {
        var kindText = LocalizationManager.Instance.GetString(Detection.Kind switch
        {
            ResidentDetectionKind.BlankSourceName => "Feedback_Detection_BlankSourceName",
            ResidentDetectionKind.SourceLandingConflict => "Feedback_Detection_SourceLandingConflict",
            ResidentDetectionKind.SpecTalentConflict or ResidentDetectionKind.SpecSkillConflict => "Feedback_Detection_SpecConflict",
            _ => throw new ArgumentOutOfRangeException(nameof(Detection), Detection.Kind, null)
        });
        ItemText = LocalizationManager.Instance.Format("Feedback_Detection_ItemFormat", kindText, Detection.Id);
    }

    partial void OnIsSelectedChanged(bool value)
    {
        _selectionChanged();
    }
}
