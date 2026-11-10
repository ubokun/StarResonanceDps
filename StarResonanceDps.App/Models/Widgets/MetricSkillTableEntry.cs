using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// スキル詳細の1行。値はすべて整形済みで、作るのは ViewModel。
///
/// <para>
/// <b>行は使い回す</b>(メーターの行と同じ)。毎回作り直すと、行の UI 要素ごと作り直されるので
/// バーのアニメーションが更新のたびに 0 から始まってしまう。
/// </para>
/// </summary>
public sealed partial class MetricSkillTableEntry(long skillId) : ObservableObject
{
    /// <summary>行の同一性。畳んだ発生源の鍵(<c>ownerId:枝番</c>)で、並び替えても行を追える。</summary>
    public long SkillId { get; } = skillId;

    /// <summary>
    /// 行のバーの塗り。属性の内訳でこの行の色を混ぜたもの。
    /// <b>内訳が無い行は <c>null</c></b> で、そのときはバーを出さない
    /// (色を作れないのに何か出すと、内訳が無いことが見えなくなる)。
    /// </summary>
    [ObservableProperty]
    private Brush? _barBrush;

    /// <summary>バーの長さ(0〜1)。一覧の1位の総量に対する比(メーターと同じ)。</summary>
    [ObservableProperty]
    private double _barRatio;

    /// <summary>バーを出すか。内訳が無い行では false。</summary>
    [ObservableProperty]
    private bool _hasBar;

    /// <summary>行番号。並びは総量の降順なので、メーターの順位と同じ振り方になる。</summary>
    [ObservableProperty]
    private string _noText = string.Empty;

    /// <summary>
    /// 属性の内訳のアイコン。量の多い上位2つを1つの枠に左右半分ずつ重ねる(<see cref="ViewModels.MetricElementIconSegment"/>)。
    /// 組み立ては <c>TextSegmentsBehavior</c> で、タイムラインの行と同じ仕組み。
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<object> _elementSegments = [];

    /// <summary>行の TIPS。「火属性33.33%/光属性66.67%, 物理60.00%/無分類40.00%」(属性の内訳、タイプの内訳)。</summary>
    [ObservableProperty]
    private string _rowToolTipText = string.Empty;

    /// <summary>行名。設定の書式(スキル名・属性・タイプ・ヒット数・会心率)で組み立てたもの。</summary>
    [ObservableProperty]
    private string _skillName = string.Empty;

    /// <summary>値の欄。メーターの行と同じ「総量 (秒間値) 割合%」。</summary>
    [ObservableProperty]
    private string _valueText = string.Empty;

    public void Update(
        string noText,
        IReadOnlyList<object> elementSegments,
        string rowToolTipText,
        string skillName,
        string valueText,
        Brush? barBrush,
        double barRatio)
    {
        NoText = noText;

        // 属性の欄とバーの塗りは、中身が同じなら入れ替えない。毎回新しい一覧・ブラシを入れると、
        // 値が同じでも属性の欄の部品を作り直し、バーを描き直すことになる(一覧とブラシの比べは参照になるため)。
        if (!ElementSegments.SequenceEqual(elementSegments))
        {
            ElementSegments = elementSegments;
        }

        RowToolTipText = rowToolTipText;
        SkillName = skillName;
        ValueText = valueText;
        if (!HasSameColor(BarBrush, barBrush))
        {
            BarBrush = barBrush;
        }

        BarRatio = barRatio;
        HasBar = barBrush is not null;
    }

    private static bool HasSameColor(Brush? current, Brush? next)
    {
        return (current, next) switch
        {
            (null, null) => true,
            (SolidColorBrush currentSolid, SolidColorBrush nextSolid) => currentSolid.Color == nextSolid.Color
                && currentSolid.Opacity.Equals(nextSolid.Opacity),
            _ => false
        };
    }
}
