// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media.Animation;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// バーの幅をアニメーションで動かす。値を差し替えるだけだと更新の間隔ぶん跳んで見える。
///
/// <para>
/// <b>補間は WPF のアニメーションシステムが行う</b>ので、更新の頻度を上げずに滑らかになる。
/// UI スレッド側は値が変わったときに1回アニメーションを張るだけ。
/// </para>
///
/// <para>
/// 動き方は <see cref="DecaySecondsProperty"/> と <see cref="FillSecondsProperty"/> で変わる(両方を 0 より大きくはできない)。
/// <b>DecaySeconds が 0 より大きければ</b>「いまの比率の幅 → 0 へ、その秒数かけて線形」(残り時間が減っていくバー)。
/// <b>FillSeconds が 0 より大きければ</b>「いまの比率の幅 → 満タンの幅へ、その秒数かけて線形」(経過時間で満ちていくバー)。
/// <b>どちらも 0 なら</b>「いまの幅 → 新しい比率の幅へ <see cref="TransitionDuration"/> で移動」(値が飛び飛びに変わるバー)。
/// </para>
/// </summary>
public static class BarWidthAnimationBehavior
{
    /// <summary>減衰しないバーが新しい値へ移るのにかける時間。更新の間隔とそろえる。</summary>
    private static readonly Duration TransitionDuration = new(TimeSpan.FromMilliseconds(250));

    /// <summary>バーの長さ(0〜1)。</summary>
    public static readonly DependencyProperty RatioProperty = DependencyProperty.RegisterAttached(
        "Ratio",
        typeof(double),
        typeof(BarWidthAnimationBehavior),
        new PropertyMetadata(0d, OnInputChanged));

    /// <summary>バーが伸びきったときの幅。ふつうは置き場所の <c>ActualWidth</c>。</summary>
    public static readonly DependencyProperty AreaWidthProperty = DependencyProperty.RegisterAttached(
        "AreaWidth",
        typeof(double),
        typeof(BarWidthAnimationBehavior),
        new PropertyMetadata(0d, OnInputChanged));

    /// <summary>0 まで減るのにかかる秒数。0 なら減らさず、新しい値へ移るだけ。</summary>
    public static readonly DependencyProperty DecaySecondsProperty = DependencyProperty.RegisterAttached(
        "DecaySeconds",
        typeof(double),
        typeof(BarWidthAnimationBehavior),
        new PropertyMetadata(0d, OnInputChanged));

    /// <summary>満タンまで満ちるのにかかる秒数。0 なら満たさない。</summary>
    public static readonly DependencyProperty FillSecondsProperty = DependencyProperty.RegisterAttached(
        "FillSeconds",
        typeof(double),
        typeof(BarWidthAnimationBehavior),
        new PropertyMetadata(0d, OnInputChanged));

    /// <summary>
    /// 減らないときに、移動のアニメーションを挟まず新しい幅へ即座に合わせるか。
    ///
    /// <para>
    /// 残り時間のバーは<b>減るときだけ</b>滑らかにしたい。新しく付いたバフや効果時間の上書きで
    /// 伸びる場面は、間を補間せずその場で出す(ユーザー決定)。減っている最中に伸びる場合は、
    /// 減衰のアニメーションが開始値を目標の幅に取り直すので、これを立てなくても即座になる。
    /// </para>
    /// </summary>
    public static readonly DependencyProperty SnapsToTargetProperty = DependencyProperty.RegisterAttached(
        "SnapsToTarget",
        typeof(bool),
        typeof(BarWidthAnimationBehavior),
        new PropertyMetadata(false, OnInputChanged));

    public static double GetRatio(DependencyObject obj) => (double)obj.GetValue(RatioProperty);

    public static void SetRatio(DependencyObject obj, double value) => obj.SetValue(RatioProperty, value);

    public static double GetAreaWidth(DependencyObject obj) => (double)obj.GetValue(AreaWidthProperty);

    public static void SetAreaWidth(DependencyObject obj, double value) => obj.SetValue(AreaWidthProperty, value);

    public static double GetDecaySeconds(DependencyObject obj) => (double)obj.GetValue(DecaySecondsProperty);

    public static void SetDecaySeconds(DependencyObject obj, double value) => obj.SetValue(DecaySecondsProperty, value);

    public static double GetFillSeconds(DependencyObject obj) => (double)obj.GetValue(FillSecondsProperty);

    public static void SetFillSeconds(DependencyObject obj, double value) => obj.SetValue(FillSecondsProperty, value);

    public static bool GetSnapsToTarget(DependencyObject obj) => (bool)obj.GetValue(SnapsToTargetProperty);

    public static void SetSnapsToTarget(DependencyObject obj, bool value) => obj.SetValue(SnapsToTargetProperty, value);

    private static void OnInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            throw new InvalidOperationException(
                $"{nameof(BarWidthAnimationBehavior)} can only be attached to a FrameworkElement.");
        }

        var areaWidth = GetAreaWidth(element);
        if (double.IsNaN(areaWidth) || areaWidth <= 0d)
        {
            // 置き場所の幅がまだ決まっていない(初回のレイアウト前)。幅が入った時点でもう一度呼ばれる。
            element.BeginAnimation(FrameworkElement.WidthProperty, null);
            element.Width = 0d;
            return;
        }

        var ratio = Math.Clamp(GetRatio(element), 0d, 1d);
        var targetWidth = areaWidth * ratio;
        var decaySeconds = GetDecaySeconds(element);
        var fillSeconds = GetFillSeconds(element);

        if (decaySeconds > 0d && fillSeconds > 0d)
        {
            throw new InvalidOperationException(
                $"{nameof(BarWidthAnimationBehavior)} cannot decay and fill at the same time.");
        }

        if (fillSeconds > 0d)
        {
            // 経過ぶんの幅から満ちるまでの秒数かけて満タンまで。張り直すたびに実測へ合わせ直す。
            element.BeginAnimation(
                FrameworkElement.WidthProperty,
                new DoubleAnimation(targetWidth, areaWidth, new Duration(TimeSpan.FromSeconds(fillSeconds)))
                {
                    FillBehavior = FillBehavior.HoldEnd
                });
            return;
        }

        if (decaySeconds > 0d)
        {
            // 残り時間ぶんかけて 0 まで。張り直すたびに実測へ合わせ直す。
            element.BeginAnimation(
                FrameworkElement.WidthProperty,
                new DoubleAnimation(targetWidth, 0d, new Duration(TimeSpan.FromSeconds(decaySeconds)))
                {
                    FillBehavior = FillBehavior.HoldEnd
                });
            return;
        }

        if (GetSnapsToTarget(element))
        {
            // 伸びる場面は補間しない。アニメーションを外さないと Width への代入が効かない。
            element.BeginAnimation(FrameworkElement.WidthProperty, null);
            element.Width = targetWidth;
            return;
        }

        // まだ幅を一度も持っていない(Width が NaN)。長さの最初の値が既定値の 0 と同じだと変更通知が来ず、
        // 上の「置き場所の幅が決まる前」の分岐を通らないままここへ来る。
        // From の無いアニメーションは今の値から始めるので NaN から始められずに落ちる。最初の1回は即座に合わせる。
        if (double.IsNaN(element.Width))
        {
            element.BeginAnimation(FrameworkElement.WidthProperty, null);
            element.Width = targetWidth;
            return;
        }

        element.BeginAnimation(
            FrameworkElement.WidthProperty,
            new DoubleAnimation(targetWidth, TransitionDuration)
            {
                FillBehavior = FillBehavior.HoldEnd
            });
    }
}
