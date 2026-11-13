// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Concurrent;
using StarResonanceDps.Plugins.ModuleTool.Models;

namespace StarResonanceDps.Plugins.ModuleTool.Services;

/// <summary>
/// 枠の数のモジュールの組から、優先する効果の条件を満たす組の戦闘力(ゲームの式)の上位を、全部の組の中から厳密に探し(分枝限定)、
/// 条件を満たす組の数も数える。
///
/// <para>
/// 効果とリンク数がまったく同じモジュールは入れ替えても戦闘力が変わらないので、1つのまとまりにして組を1つに畳む。
/// 枝の上限は、残りの枠で足せるリンクの合計の上限を、効果ごとの段階の伸びの上に凸の包みへ伸びの大きい順に配った戦闘力(分数ナップサック)。
/// 条件の段階まで足りない分は必ず足すので先に配り、足りない条件の効果は残りのどれか1つが持つので、その1つの合計は効果を持つまとまりの最大までとする。
/// どれも実際の組より大きいか同じ。上限が今の上位の最後に届かない枝と、優先する効果の条件に届かない枝を捨てる。
/// </para>
///
/// <para>
/// 最初に選ぶまとまりごとに並列に探す。上位の入れ替えはロックの中で行い、戦闘力が同じ組は表示するモジュールの uuid の並びで順を決めるので、
/// 何度回しても同じ結果になる。
/// </para>
///
/// <para>
/// 条件を満たす組の数は上位と同じ単位(同じまとまりの入れ替えは1つ)で数える。途中で条件を満たしたら、残りの枠の選び方の数を
/// 表から足して先へ進まず、条件に届かない枝は捨てる。(まとまりの添字, 残りの数, 条件の効果ごとのリンク数(条件の数で頭打ち))ごとの数は覚えて使い回す。
/// </para>
///
/// <para>キャンセルは例外で外へ出さず、<see cref="Solve"/> が null を返す。</para>
/// </summary>
internal sealed class ModuleSolver
{
    // 見積もりは傾き(小数)を掛けて足すので、丸めの誤差で上位の最後と同じ値の枝を捨てないための幅。
    private const double BoundTolerance = 1e-6;

    private readonly ModuleScorer _scorer;
    private readonly int _slotCount;
    private readonly int _resultCount;
    private readonly List<ModuleGroup> _groups;
    private readonly int _effectCount;

    // まとまりの添字 g 以降から r 個選ぶときの、効果ごとの最大のリンク数([g][r][効果])と総和の最大([g][r])、選べる数([g])。
    private readonly int[][][] _maxEffectLinks;
    private readonly int[][] _maxTotalLinks;
    private readonly int[] _availableCounts;

    // まとまりの添字 g 以降で、効果を持つまとまりのリンクの合計の最大([g][効果])。
    private readonly int[][] _maxTotalWithEffect;

    // 効果ごとの最後の段階のリンク数([効果])と、今のリンク数から足したリンク数 → 段階の戦闘力の伸びの上に凸の包み
    // ([効果][今のリンク数] = 傾きの大きい順の線分)。見積もりの作業用の配列に要る線分の数の上限。
    private readonly int[] _levelCapLinks;
    private readonly (int Length, double Slope)[][][] _gainEnvelopes;
    private readonly int _segmentCapacity;

    // まとまりの添字 g 以降から r 個選ぶ選び方の数([g][r])。
    private readonly long[][] _ways;

    private readonly (int EffectIndex, int Links)[] _requiredLinks;
    private readonly int _countKeyBits;
    private readonly ConcurrentDictionary<CountKey, long> _countMemo = new();

    private readonly object _resultLock = new();
    private readonly List<RankedCombination> _results = [];
    private int _threshold = int.MinValue;

    public ModuleSolver(
        ModuleScorer scorer,
        IReadOnlyList<ModuleCandidate> candidates,
        IReadOnlySet<long> equippedUuids,
        int slotCount,
        int resultCount)
    {
        _scorer = scorer;
        _effectCount = scorer.Effects.Count;
        _slotCount = Math.Min(slotCount, candidates.Count);
        _resultCount = resultCount;
        _groups = CreateGroups(candidates, equippedUuids);

        _requiredLinks = [.. scorer.RequiredLinks];
        var maxRequiredLinks = _requiredLinks.Length > 0 ? _requiredLinks.Max(requirement => requirement.Links) : 0;
        _countKeyBits = 1;
        while ((1 << _countKeyBits) <= maxRequiredLinks)
        {
            _countKeyBits++;
        }

        if (_requiredLinks.Length * _countKeyBits > 128)
        {
            throw new InvalidOperationException($"Cannot count combinations for {_requiredLinks.Length} priority effects.");
        }

        var groupCount = _groups.Count;
        _maxEffectLinks = new int[groupCount + 1][][];
        _maxTotalLinks = new int[groupCount + 1][];
        _availableCounts = new int[groupCount + 1];
        _ways = new long[groupCount + 1][];
        var topEffectLinks = Enumerable.Range(0, _effectCount).Select(_ => new List<int>()).ToArray();
        var topTotalLinks = new List<int>();
        for (var g = groupCount; g >= 0; g--)
        {
            if (g < groupCount)
            {
                var group = _groups[g];
                var copies = Math.Min(group.Members.Count, _slotCount);
                foreach (var (effectIndex, links) in group.Effects)
                {
                    InsertTop(topEffectLinks[effectIndex], links, copies, _slotCount);
                }

                InsertTop(topTotalLinks, group.TotalLinks, copies, _slotCount);
                _availableCounts[g] = _availableCounts[g + 1] + group.Members.Count;
            }

            _maxEffectLinks[g] = new int[_slotCount + 1][];
            _maxTotalLinks[g] = new int[_slotCount + 1];
            for (var r = 0; r <= _slotCount; r++)
            {
                _maxEffectLinks[g][r] = new int[_effectCount];
                for (var effectIndex = 0; effectIndex < _effectCount; effectIndex++)
                {
                    _maxEffectLinks[g][r][effectIndex] = topEffectLinks[effectIndex].Take(r).Sum();
                }

                _maxTotalLinks[g][r] = topTotalLinks.Take(r).Sum();
            }

            _ways[g] = new long[_slotCount + 1];
            for (var r = 0; r <= _slotCount; r++)
            {
                if (g == groupCount)
                {
                    _ways[g][r] = r == 0 ? 1 : 0;
                    continue;
                }

                long ways = 0;
                for (var count = 0; count <= Math.Min(_groups[g].Members.Count, r); count++)
                {
                    ways = checked(ways + _ways[g + 1][r - count]);
                }

                _ways[g][r] = ways;
            }
        }

        _maxTotalWithEffect = new int[groupCount + 1][];
        _maxTotalWithEffect[groupCount] = new int[_effectCount];
        for (var g = groupCount - 1; g >= 0; g--)
        {
            var row = (int[])_maxTotalWithEffect[g + 1].Clone();
            foreach (var (effectIndex, _) in _groups[g].Effects)
            {
                row[effectIndex] = Math.Max(row[effectIndex], _groups[g].TotalLinks);
            }

            _maxTotalWithEffect[g] = row;
        }

        _levelCapLinks = new int[_effectCount];
        _gainEnvelopes = new (int, double)[_effectCount][][];
        for (var effectIndex = 0; effectIndex < _effectCount; effectIndex++)
        {
            var cap = scorer.Effects[effectIndex].Levels[^1].Links;
            _levelCapLinks[effectIndex] = cap;
            _gainEnvelopes[effectIndex] = new (int, double)[cap + 1][];
            for (var start = 0; start <= cap; start++)
            {
                _gainEnvelopes[effectIndex][start] = CreateGainEnvelope(scorer, effectIndex, start, cap);
            }
        }

        _segmentCapacity = _gainEnvelopes.Sum(envelopes => envelopes.Max(envelope => envelope.Length));
    }

    /// <summary>組むモジュールの数(枠の数と候補の数の小さいほう)。</summary>
    public int CombinationSize => _slotCount;

    /// <summary>上位と、条件を満たす組の数を求める。上位が無ければ空。キャンセルされたら null。</summary>
    public ModuleSearchResult? Solve(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (_slotCount == 0)
        {
            return new ModuleSearchResult([], 0);
        }

        // 最初に選ぶまとまり(と選ぶ数)ごとに分けて並列に探し、数える。上位が早く埋まるよう、リンクの多いまとまりから。
        var starts = new List<(int Group, int Count)>();
        for (var g = 0; g < _groups.Count; g++)
        {
            for (var count = Math.Min(_groups[g].Members.Count, _slotCount); count >= 1; count--)
            {
                starts.Add((g, count));
            }
        }

        var finished = 0;
        long matchingCount = 0;

        // キャンセルは Parallel.ForEach に渡さず、中で見て止める(渡すと OperationCanceledException が外へ出る)。
        Parallel.ForEach(starts, (start, state) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                state.Stop();
                return;
            }

            var links = new int[_effectCount];
            var bestLinks = new int[_effectCount];
            var scratch = new BoundScratch(_effectCount, _segmentCapacity);
            var counts = new int[_groups.Count];
            var group = _groups[start.Group];
            var remaining = _slotCount - start.Count;
            Add(links, group, start.Count);
            counts[start.Group] = start.Count;
            try
            {
                Search(start.Group + 1, remaining, links, group.TotalLinks * start.Count, counts, bestLinks, scratch, cancellationToken);
                Interlocked.Add(ref matchingCount, CountMatching(start.Group + 1, remaining, links, bestLinks, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                state.Stop();
                return;
            }

            progress?.Report((double)Interlocked.Increment(ref finished) / starts.Count);
        });

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        lock (_resultLock)
        {
            return new ModuleSearchResult(
                _results
                    .Select(result => new ModuleCombination(result.FightValue, result.TotalLinks, result.Modules))
                    .ToArray(),
                Interlocked.Read(ref matchingCount));
        }
    }

    private void Search(
        int g,
        int remaining,
        int[] links,
        int totalLinks,
        int[] counts,
        int[] bestLinks,
        BoundScratch scratch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (remaining == 0)
        {
            OfferResult(links, totalLinks, counts);
            return;
        }

        if (g >= _groups.Count || _availableCounts[g] < remaining || !CanReach(g, remaining, links, totalLinks, bestLinks, scratch))
        {
            return;
        }

        var group = _groups[g];
        for (var count = Math.Min(group.Members.Count, remaining); count >= 0; count--)
        {
            Add(links, group, count);
            counts[g] = count;
            Search(g + 1, remaining - count, links, totalLinks + group.TotalLinks * count, counts, bestLinks, scratch, cancellationToken);
            counts[g] = 0;
            Add(links, group, -count);
        }
    }

    /// <summary>
    /// 残りの枠を足して、条件に届き、戦闘力が今の上位の最後に届きうるか(見積もりはクラスの説明)。
    /// <paramref name="bestLinks"/> と <paramref name="scratch"/> は使い回しの作業用。
    /// </summary>
    private bool CanReach(int g, int remaining, int[] links, int totalLinks, int[] bestLinks, BoundScratch scratch)
    {
        if (!CanMeetRequirements(g, remaining, links, bestLinks))
        {
            return false;
        }

        var threshold = Volatile.Read(ref _threshold);
        if (threshold == int.MinValue)
        {
            return true;
        }

        var budget = _maxTotalLinks[g][remaining];
        var deficits = scratch.Deficits;
        Array.Clear(deficits);
        var deficitTotal = 0;
        foreach (var (effectIndex, requiredLinks) in _requiredLinks)
        {
            var deficit = requiredLinks - links[effectIndex];
            if (deficit > 0)
            {
                deficits[effectIndex] = deficit;
                deficitTotal += deficit;
                budget = Math.Min(budget, _maxTotalWithEffect[g][effectIndex] + _maxTotalLinks[g][remaining - 1]);
            }
        }

        double value = _scorer.GetLinkFightValue(totalLinks + budget);
        budget -= deficitTotal;
        if (budget < 0)
        {
            return false;
        }

        var maxEffectLinks = _maxEffectLinks[g][remaining];
        var slopes = scratch.Slopes;
        var lengths = scratch.Lengths;
        var segmentCount = 0;
        for (var effectIndex = 0; effectIndex < _effectCount; effectIndex++)
        {
            var cap = _levelCapLinks[effectIndex];
            var current = Math.Min(links[effectIndex] + deficits[effectIndex], cap);
            value += _scorer.GetEffectFightValue(effectIndex, current);
            var allowed = Math.Min(maxEffectLinks[effectIndex] - deficits[effectIndex], cap - current);
            foreach (var (length, slope) in _gainEnvelopes[effectIndex][current])
            {
                if (allowed <= 0)
                {
                    break;
                }

                // 傾きの大きい順に差し込む。
                var index = segmentCount++;
                while (index > 0 && slopes[index - 1] < slope)
                {
                    slopes[index] = slopes[index - 1];
                    lengths[index] = lengths[index - 1];
                    index--;
                }

                var take = Math.Min(length, allowed);
                slopes[index] = slope;
                lengths[index] = take;
                allowed -= take;
            }
        }

        for (var index = 0; index < segmentCount && budget > 0; index++)
        {
            var take = Math.Min(lengths[index], budget);
            value += take * slopes[index];
            budget -= take;
        }

        return value + BoundTolerance >= threshold;
    }

    /// <summary>今のリンク数 start から足したリンク数 → 段階の戦闘力の伸びの、上に凸の包み(傾きの大きい順の線分)。</summary>
    private static (int Length, double Slope)[] CreateGainEnvelope(ModuleScorer scorer, int effectIndex, int start, int cap)
    {
        var segments = new List<(int, double)>();
        var baseValue = scorer.GetEffectFightValue(effectIndex, start);
        var x0 = 0;
        var y0 = 0.0;
        while (start + x0 < cap)
        {
            var bestX = x0 + 1;
            var bestSlope = double.NegativeInfinity;
            for (var x = x0 + 1; start + x <= cap; x++)
            {
                var slope = (scorer.GetEffectFightValue(effectIndex, start + x) - baseValue - y0) / (x - x0);
                if (slope >= bestSlope)
                {
                    bestSlope = slope;
                    bestX = x;
                }
            }

            segments.Add((bestX - x0, bestSlope));
            y0 += bestSlope * (bestX - x0);
            x0 = bestX;
        }

        return [.. segments];
    }

    /// <summary>残りから取れる最大のリンク数で条件に届くか。<paramref name="bestLinks"/> に、その最大のリンク数を入れる。</summary>
    private bool CanMeetRequirements(int g, int remaining, int[] links, int[] bestLinks)
    {
        var maxEffectLinks = _maxEffectLinks[g][remaining];
        for (var effectIndex = 0; effectIndex < _effectCount; effectIndex++)
        {
            bestLinks[effectIndex] = links[effectIndex] + maxEffectLinks[effectIndex];
        }

        return _scorer.MeetsRequirements(bestLinks);
    }

    /// <summary>まとまりの添字 g 以降から remaining 個足したときに、条件を満たす組の数。</summary>
    private long CountMatching(int g, int remaining, int[] links, int[] bestLinks, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_scorer.MeetsRequirements(links))
        {
            return _ways[g][remaining];
        }

        if (remaining == 0
            || g >= _groups.Count
            || _availableCounts[g] < remaining
            || !CanMeetRequirements(g, remaining, links, bestLinks))
        {
            return 0;
        }

        var key = CreateCountKey(g, remaining, links);
        if (_countMemo.TryGetValue(key, out var known))
        {
            return known;
        }

        var group = _groups[g];
        long total = 0;
        for (var count = Math.Min(group.Members.Count, remaining); count >= 0; count--)
        {
            Add(links, group, count);
            total = checked(total + CountMatching(g + 1, remaining - count, links, bestLinks, cancellationToken));
            Add(links, group, -count);
        }

        _countMemo.TryAdd(key, total);
        return total;
    }

    private CountKey CreateCountKey(int g, int remaining, int[] links)
    {
        UInt128 state = 0;
        foreach (var (effectIndex, requiredLinks) in _requiredLinks)
        {
            state = (state << _countKeyBits) | (uint)Math.Min(links[effectIndex], requiredLinks);
        }

        return new CountKey(g, remaining, state);
    }

    private void OfferResult(int[] links, int totalLinks, int[] counts)
    {
        if (!_scorer.MeetsRequirements(links))
        {
            return;
        }

        var fightValue = _scorer.GetFightValue(links, totalLinks);
        if (fightValue < Volatile.Read(ref _threshold))
        {
            return;
        }

        var modules = new List<ModuleCandidate>(_slotCount);
        for (var g = 0; g < counts.Length; g++)
        {
            if (counts[g] > 0)
            {
                modules.AddRange(_groups[g].Members.Take(counts[g]));
            }
        }

        var result = new RankedCombination(
            fightValue,
            totalLinks,
            modules.OrderBy(module => module.Uuid).ToArray());

        lock (_resultLock)
        {
            var index = _results.BinarySearch(result, RankedCombination.Order);
            _results.Insert(index < 0 ? ~index : index, result);
            if (_results.Count > _resultCount)
            {
                _results.RemoveAt(_results.Count - 1);
            }

            if (_results.Count == _resultCount)
            {
                Volatile.Write(ref _threshold, _results[^1].FightValue);
            }
        }
    }

    private static void Add(int[] links, ModuleGroup group, int count)
    {
        if (count == 0)
        {
            return;
        }

        foreach (var (effectIndex, effectLinks) in group.Effects)
        {
            links[effectIndex] += effectLinks * count;
        }
    }

    /// <summary>降順の並びに value を copies 個入れ、上から limit 個だけ残す。</summary>
    private static void InsertTop(List<int> top, int value, int copies, int limit)
    {
        for (var copy = 0; copy < copies; copy++)
        {
            var index = top.FindIndex(existing => existing < value);
            if (index < 0)
            {
                if (top.Count >= limit)
                {
                    return;
                }

                top.Add(value);
            }
            else
            {
                top.Insert(index, value);
                if (top.Count > limit)
                {
                    top.RemoveAt(top.Count - 1);
                }
            }
        }
    }

    /// <summary>効果とリンク数が同じモジュールのまとまり。中は装着中を先、次に uuid の小さい順(表示に使う代表の順)。</summary>
    private static List<ModuleGroup> CreateGroups(IReadOnlyList<ModuleCandidate> candidates, IReadOnlySet<long> equippedUuids)
    {
        return candidates
            .GroupBy(candidate => string.Join(
                ",",
                candidate.Effects
                    .OrderBy(effect => effect.EffectIndex)
                    .Select(effect => $"{effect.EffectIndex}:{effect.Links}")))
            .Select(group =>
            {
                var members = group
                    .OrderBy(candidate => equippedUuids.Contains(candidate.Uuid) ? 0 : 1)
                    .ThenBy(candidate => candidate.Uuid)
                    .ToArray();
                var effects = members[0].Effects
                    .GroupBy(effect => effect.EffectIndex)
                    .Select(effect => (effect.Key, effect.Sum(part => part.Links)))
                    .ToArray();
                return new ModuleGroup(group.Key, effects, members[0].TotalLinks, members);
            })
            .OrderByDescending(group => group.TotalLinks)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToList();
    }

    private readonly record struct CountKey(int Group, int Remaining, UInt128 State);

    /// <summary>見積もりの作業用の配列。最初に選ぶまとまりごとに1つ作って使い回す。</summary>
    private sealed class BoundScratch(int effectCount, int segmentCapacity)
    {
        public int[] Deficits { get; } = new int[effectCount];

        public double[] Slopes { get; } = new double[segmentCapacity];

        public int[] Lengths { get; } = new int[segmentCapacity];
    }

    // Effects は配列のまま持つ。探す処理の中で回すので、IReadOnlyList で回すと1回ごとに列挙の部品を割り当てる。
    private sealed record ModuleGroup(
        string Key,
        (int EffectIndex, int Links)[] Effects,
        int TotalLinks,
        IReadOnlyList<ModuleCandidate> Members);

    private sealed record RankedCombination(int FightValue, int TotalLinks, IReadOnlyList<ModuleCandidate> Modules)
    {
        /// <summary>戦闘力の高い順。同じなら uuid の並びの小さい順。</summary>
        public static readonly IComparer<RankedCombination> Order = Comparer<RankedCombination>.Create((left, right) =>
        {
            var compare = right.FightValue.CompareTo(left.FightValue);
            if (compare != 0)
            {
                return compare;
            }

            for (var index = 0; index < Math.Min(left.Modules.Count, right.Modules.Count); index++)
            {
                compare = left.Modules[index].Uuid.CompareTo(right.Modules[index].Uuid);
                if (compare != 0)
                {
                    return compare;
                }
            }

            return left.Modules.Count.CompareTo(right.Modules.Count);
        });
    }
}
