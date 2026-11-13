// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 推移グラフ1枚の点の置き場(<see cref="MeterSnapshotProvider.ReadPlayerGraph"/> が書く)。配列を使い回し、値が変わった区切りだけ書き換える。
/// 持ち主は窓の ViewModel で、窓と一緒に解放する。
/// </summary>
public sealed class MetricGraphPointBuffer
{
    private MetricGraphPoint[] _points = [];
    private double[] _bucketTotals = [];
    private int _count;

    /// <summary>今の点を読む一覧。中身が変わったときに作って渡す。</summary>
    public IReadOnlyList<MetricGraphPoint> CreateView() => new ArrayPrefixList<MetricGraphPoint>(_points, _count);

    /// <summary>点を無くす。無くなる点があれば true。</summary>
    public bool Clear() => SetCount(0);

    /// <summary>区切りごとの合計を足す作業用の配列。先頭 <paramref name="bucketCount"/> 個を 0 にして返す。</summary>
    internal double[] GetClearedBucketTotals(int bucketCount)
    {
        if (_bucketTotals.Length < bucketCount)
        {
            _bucketTotals = new double[Math.Max(bucketCount, _bucketTotals.Length * 2)];
        }
        else
        {
            Array.Clear(_bucketTotals, 0, bucketCount);
        }

        return _bucketTotals;
    }

    internal void EnsureCapacity(int count)
    {
        if (_points.Length < count)
        {
            Array.Resize(ref _points, Math.Max(count, _points.Length * 2));
        }
    }

    /// <summary>区切り1つの値を書く。今の値と違えば true。</summary>
    internal bool SetPoint(int index, MetricGraphPoint point)
    {
        if (index < _count && _points[index] == point)
        {
            return false;
        }

        _points[index] = point;
        return true;
    }

    /// <summary>点の数を決める。変われば true。</summary>
    internal bool SetCount(int count)
    {
        if (count == _count)
        {
            return false;
        }

        _count = count;
        return true;
    }
}
