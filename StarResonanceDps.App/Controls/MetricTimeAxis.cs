// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.Controls;

/// <summary>
/// 戦闘の秒の横軸の決まり(推移グラフとバフ稼働率で共有)。数字の間隔・数字を置く秒・M:SS の文字・秒から位置への直し方。
/// 描く領域の左右は使う側が決める(推移グラフは縦軸の数字の幅、バフ稼働率は行の名前と稼働率の欄)。
/// </summary>
public static class MetricTimeAxis
{
    /// <summary>横軸の数字の区間の数の上限。数字の間隔(<see cref="GetLabelStepSeconds"/>)はこれを超えない切りのいい秒にする。</summary>
    public const int MaxLabelIntervals = 4;

    /// <summary>横軸の数字の間隔の候補(秒)。横軸の長さを <see cref="MaxLabelIntervals"/> 区間以下に分ける最小のものを使う。</summary>
    private static readonly double[] LabelStepCandidates = [1d, 2d, 5d, 10d, 15d, 30d, 60d, 120d, 300d, 600d];

    /// <summary>横軸の数字の間隔。戦闘の秒に結び付けるので、スクロールすると数字も動く。スクロールバーの1段の移動もこれ。</summary>
    public static double GetLabelStepSeconds(double visibleSeconds)
    {
        foreach (var step in LabelStepCandidates)
        {
            if (visibleSeconds / step <= MaxLabelIntervals)
            {
                return step;
            }
        }

        return Math.Ceiling(visibleSeconds / MaxLabelIntervals);
    }

    /// <summary>見ている窓の中で数字を置く秒を <paramref name="target"/> に入れ直す(多くても <paramref name="maxCount"/> 個)。</summary>
    public static void FillLabelSeconds(double offset, double visibleSeconds, List<double> target, int maxCount)
    {
        var step = GetLabelStepSeconds(visibleSeconds);
        target.Clear();
        for (var seconds = Math.Ceiling(offset / step) * step;
             seconds <= offset + visibleSeconds && target.Count < maxCount;
             seconds += step)
        {
            target.Add(seconds);
        }
    }

    /// <summary>横軸の数字の文字(M:SS)。</summary>
    public static string FormatSeconds(double seconds)
    {
        var totalSeconds = (int)Math.Round(seconds);
        return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
    }

    /// <summary>戦闘の秒を、描く領域(左端 <paramref name="left"/>・幅 <paramref name="width"/>)の横の位置に直す。</summary>
    public static double ToX(double left, double width, double horizontalOffset, double visibleSeconds, double seconds)
    {
        return left + (seconds - horizontalOffset) / visibleSeconds * width;
    }
}

/// <summary>
/// 横軸のスクロールの状態(推移グラフとバフ稼働率で共有)。最新を追いかけている間は右端へ寄せ、手で動かしたら右端にいる間だけ追いかける。
/// 位置そのものは使う側が持つ(推移グラフは依存プロパティ)。
/// </summary>
public sealed class MetricTimeAxisScroll
{
    /// <summary>最新(右端)を追いかけているか。</summary>
    public bool IsFollowingEnd { get; private set; } = true;

    /// <summary>ずらせる秒数(全体の長さ − 横軸の長さ、足りなければ 0)。スクロールバーの最大値で、0 ならバーを出さない。</summary>
    public double ScrollableSeconds { get; private set; }

    /// <summary>全体の長さと横軸の長さから、ずらせる秒数を決め直す。</summary>
    public double SetRange(double totalSeconds, double visibleSeconds)
    {
        ScrollableSeconds = Math.Max(totalSeconds - visibleSeconds, 0d);
        return ScrollableSeconds;
    }

    /// <summary>位置を 0〜<see cref="ScrollableSeconds"/> に収める。</summary>
    public double Clamp(double offset)
    {
        return double.IsNaN(offset) ? 0d : Math.Clamp(offset, 0d, ScrollableSeconds);
    }

    /// <summary>
    /// 位置が決まった(手で動かした・範囲に収めた)。右端にいる間だけ最新を追いかける
    /// (横軸を長くして右端へ押し戻されたのに、データが伸びても付いていかない状態を残さない)。
    /// </summary>
    public void OnOffsetSettled(double offset)
    {
        IsFollowingEnd = offset >= ScrollableSeconds;
    }

    /// <summary>最新を追いかけ直す(データが減った = 別の回になった)。</summary>
    public void RestartFollowing()
    {
        IsFollowingEnd = true;
    }
}
