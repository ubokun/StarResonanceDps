// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 与ダメ詳細・ヒール詳細・被ダメ詳細の中身。上の表(合計・通常・会心・幸運 × 値・率・ヒット数)と、
/// 下の属性・物理魔法の割合(種類ごとのバーの行)。
/// 表の行は窓を開いたときに1回だけ作り、更新では文字だけを入れ替える
/// (同じ値なら通知しないので、値が変わらない間は画面の部品も描画も動かない)。
/// 割合の行は種類ごとに1つ作って使い回し、値のある種類だけを総量の大きい順に並べる(並べるのは VM)。
/// </summary>
public sealed partial class PlayerMetricSummaryEntry : ObservableObject
{
    /// <summary>総量が 0 より大きいか。無ければ画面は表も見出しも出さない(スキル詳細が総量 0 の技を出さないのと同じ条件)。</summary>
    [ObservableProperty]
    private bool _hasData;

    /// <summary>合計の行。率の欄は画面に「100.00%」を直に書くので、<see cref="PlayerMetricSummaryRow.RateText"/> は使わない。</summary>
    public PlayerMetricSummaryRow TotalRow { get; } = new();

    public PlayerMetricSummaryRow NormalRow { get; } = new();

    public PlayerMetricSummaryRow CritRow { get; } = new();

    public PlayerMetricSummaryRow LuckyRow { get; } = new();

    /// <summary>属性の割合の行。値のある属性だけを総量の大きい順に並べる。</summary>
    public ObservableCollection<PlayerMetricShareItem> ElementShares { get; } = [];

    /// <summary>物理・魔法・無分類の割合の行。並べ方は属性と同じ。</summary>
    public ObservableCollection<PlayerMetricShareItem> ModeShares { get; } = [];
}

/// <summary>表の1行の値・率・ヒット数。</summary>
public sealed partial class PlayerMetricSummaryRow : ObservableObject
{
    [ObservableProperty]
    private string _valueText = string.Empty;

    [ObservableProperty]
    private string _rateText = string.Empty;

    [ObservableProperty]
    private string _hitsText = string.Empty;
}

/// <summary>割合の1行(属性の1つか、物理・魔法・無分類の1つ)。</summary>
public sealed partial class PlayerMetricShareItem : ObservableObject
{
    /// <summary>バーの色。</summary>
    [ObservableProperty]
    private Brush? _barBrush;

    /// <summary>バーの長さ。まとまりの一番大きい値に対する比(スキル詳細の行と同じ)。</summary>
    [ObservableProperty]
    private double _barRatio;

    /// <summary>アイコンの形。絵の無い種類(無分類)は null で、はてなを出す。</summary>
    [ObservableProperty]
    private Brush? _iconMask;

    /// <summary>アイコンがステータス詳細の絵(物理攻撃力・魔法攻撃力)か。画面はステータス詳細と同じ大きさで出す。</summary>
    [ObservableProperty]
    private bool _usesPlayerStatusAsset;

    /// <summary>属性名か、物理・魔法・無分類。</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>「総量 割合%」。割合は合計に対する比。</summary>
    [ObservableProperty]
    private string _valueText = string.Empty;
}
