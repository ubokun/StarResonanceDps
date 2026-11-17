// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 推移グラフ(DPS / HPS)の設定。表示設定(描画間隔・横軸の長さ・スキルログを表示)と、線の色(グラフカラー)。
/// グラフカラーの節はバフ稼働率と共有の部品(<see cref="GraphColorSettingsViewModel"/>)で、アイコンの欄には折れ線の見本を出す。
/// 行の並びと既定は読み替え先のメーター(<see cref="WidgetConfigDefaults.GetMetricGraphColorDefaultSource"/>)と同じ。
/// 横軸の長さはウィジェットの窓の上のホイールでも変わり、そのときは <see cref="SetVisibleSecondsFromWidget"/> で受ける。
/// </summary>
public sealed partial class MetricGraphWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly WidgetKind _kind;
    private readonly ObservableCollection<MetricGraphSecondsOption> _availableAggregationIntervals = [];
    private MetricGraphWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private int _aggregationIntervalSeconds = WidgetConfigDefaults.DefaultMetricGraphAggregationIntervalSeconds;

    /// <summary>横軸の長さのスライダーの値。保存する値は <see cref="CreateConfig"/> で整数に丸めて範囲に収める。</summary>
    [ObservableProperty]
    private double _visibleSeconds = WidgetConfigDefaults.DefaultMetricGraphVisibleSeconds;

    [ObservableProperty]
    private bool _showSkillLog = true;

    public MetricGraphWidgetSettingsViewModel(WidgetKind kind, MetricGraphWidgetSettingsConfig? config)
    {
        _kind = kind;
        AvailableAggregationIntervals = new ReadOnlyObservableCollection<MetricGraphSecondsOption>(_availableAggregationIntervals);

        GraphColors = new GraphColorSettingsViewModel(WidgetConfigDefaults.GetMetricGraphColorDefaultSource(kind), usesBarSwatch: false);
        GraphColors.Changed += NotifyChanged;

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        RebuildSecondsOptions();
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMetricGraph(_kind, config);
        Load(_lastSaved);
    }

    public event Action<MetricGraphWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MetricGraphSecondsOption> AvailableAggregationIntervals { get; }

    /// <summary>横軸の長さのスライダーの最小。値の束縛より先に決まるよう、画面からは <c>x:Static</c> で読む。</summary>
    public const double VisibleSecondsSliderMinimum = WidgetConfigDefaults.MinMetricGraphVisibleSeconds;

    /// <summary>横軸の長さのスライダーの最大。値の束縛より先に決まるよう、画面からは <c>x:Static</c> で読む。</summary>
    public const double VisibleSecondsSliderMaximum = WidgetConfigDefaults.MaxMetricGraphVisibleSeconds;

    /// <summary>スライダーの右に出す今の横軸の長さ。</summary>
    public string VisibleSecondsText => FormatVisibleSeconds(RoundVisibleSeconds(VisibleSeconds));

    /// <summary>
    /// いちばん長い横軸の長さの文字。スライダーの右の欄に見えない文字として重ね、欄の幅をこの文字に合わせる
    /// (値が変わっても溝の長さが変わらない)。
    /// </summary>
    public string VisibleSecondsMaxText => FormatVisibleSeconds(WidgetConfigDefaults.MaxMetricGraphVisibleSeconds);

    /// <summary>スキルログのスイッチの右に出す ON / OFF。</summary>
    public string ShowSkillLogStateText => LocalizationManager.Instance.GetString(
        ShowSkillLog ? "Settings_Switch_On" : "Settings_Switch_Off");

    /// <summary>グラフカラーの節(線の色)。</summary>
    public GraphColorSettingsViewModel GraphColors { get; }

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        GraphColors.Changed -= NotifyChanged;
        GraphColors.Dispose();
    }

    public MetricGraphWidgetSettingsConfig CreateConfig()
    {
        var config = new MetricGraphWidgetSettingsConfig
        {
            AggregationIntervalSeconds = AggregationIntervalSeconds,
            VisibleSeconds = RoundVisibleSeconds(VisibleSeconds),
            ShowSkillLog = ShowSkillLog,
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        GraphColors.WriteTo(config.ClassColorIndexes, config.ClassColorPalettes);
        return WidgetConfigDefaults.CloneNormalizedMetricGraph(_kind, config);
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateMetricGraphSettings(_kind));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(MetricGraphWidgetSettingsConfig config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMetricGraph(_kind, config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// ウィジェットの窓の上のホイールで変わった横軸の長さを受ける。ウィジェットの側で保存するので、
    /// 保存済みの控えと画面の値の両方を合わせる(この項目を未保存に数えず、取り消しで戻さず、保存で古い値を書き戻さない)。
    /// ほかの項目の未保存の変更はそのまま残す。
    /// </summary>
    public void SetVisibleSecondsFromWidget(int visibleSeconds)
    {
        _lastSaved.VisibleSeconds = visibleSeconds;

        _isLoading = true;
        try
        {
            VisibleSeconds = visibleSeconds;
        }
        finally
        {
            _isLoading = false;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(MetricGraphWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedMetricGraph(_kind, config);

        _isLoading = true;
        try
        {
            AggregationIntervalSeconds = normalized.AggregationIntervalSeconds;
            VisibleSeconds = normalized.VisibleSeconds;
            ShowSkillLog = normalized.ShowSkillLog;
            GraphColors.Load(normalized.ClassColorIndexes, normalized.ClassColorPalettes);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RebuildSecondsOptions()
    {
        _availableAggregationIntervals.Clear();
        foreach (var seconds in WidgetConfigDefaults.MetricGraphAggregationIntervals)
        {
            _availableAggregationIntervals.Add(new MetricGraphSecondsOption(
                seconds,
                LocalizationManager.Instance.Format("Settings_Graph_AggregationInterval_Option", seconds)));
        }
    }

    private static int RoundVisibleSeconds(double visibleSeconds)
    {
        return WidgetConfigDefaults.ClampMetricGraphVisibleSeconds(
            (int)Math.Round(visibleSeconds, MidpointRounding.AwayFromZero));
    }

    private static string FormatVisibleSeconds(int visibleSeconds)
    {
        return LocalizationManager.Instance.Format("Settings_Graph_VisibleSeconds_Option", visibleSeconds);
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RebuildSecondsOptions();
        OnPropertyChanged(nameof(ShowSkillLogStateText));
        OnPropertyChanged(nameof(VisibleSecondsText));
        OnPropertyChanged(nameof(VisibleSecondsMaxText));
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private bool SettingsEqual(
        MetricGraphWidgetSettingsConfig left,
        MetricGraphWidgetSettingsConfig right)
    {
        return left.AggregationIntervalSeconds == right.AggregationIntervalSeconds
            && left.VisibleSeconds == right.VisibleSeconds
            && left.ShowSkillLog == right.ShowSkillLog
            && GraphColors.ColorsEqual(
                left.ClassColorIndexes,
                left.ClassColorPalettes,
                right.ClassColorIndexes,
                right.ClassColorPalettes);
    }

    private void NotifyChanged()
    {
        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    partial void OnAggregationIntervalSecondsChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnVisibleSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(VisibleSecondsText));
        NotifyChanged();
    }

    partial void OnShowSkillLogChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSkillLogStateText));
        NotifyChanged();
    }
}

/// <summary>秒数の選択肢(描画間隔)。</summary>
public sealed class MetricGraphSecondsOption
{
    public MetricGraphSecondsOption(int seconds, string displayName)
    {
        Seconds = seconds;
        DisplayName = displayName;
    }

    public int Seconds { get; }

    public string DisplayName { get; }
}
