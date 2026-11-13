// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 一覧の行を指定の並びにそろえる。順の合っている行の最長の並びは動かさず、残りだけを前の行のすぐ後ろへ動かす。
/// 仮想化した一覧は動かした行の部品を捨てて作り直すので、順がよく変わるものは行を位置ごとに当て直す(プレイヤーリスト・エンティティリスト)。
/// </summary>
internal static class CollectionOrder
{
    /// <summary>
    /// <paramref name="items"/> を <paramref name="ordered"/> と同じ並びにする。<paramref name="ordered"/> に無い行は外し、
    /// <paramref name="items"/> に無い行は足す。同じ行を2回渡さない。順が同じなら何も作らず何も知らせない。
    /// </summary>
    public static void Apply<T>(ObservableCollection<T> items, IReadOnlyList<T> ordered)
        where T : class
    {
        if (IsSameOrder(items, ordered))
        {
            return;
        }

        for (var index = items.Count - 1; index >= 0; index--)
        {
            if (IndexOf(ordered, items[index]) < 0)
            {
                items.RemoveAt(index);
            }
        }

        var stays = FindRowsThatStay(items, ordered);
        for (var target = 0; target < ordered.Count; target++)
        {
            if (stays[target])
            {
                continue;
            }

            var row = ordered[target];
            var current = IndexOf(items, row);
            var previous = target == 0 ? -1 : IndexOf(items, ordered[target - 1]);
            if (current < 0)
            {
                items.Insert(previous + 1, row);
                continue;
            }

            var destination = current < previous ? previous : previous + 1;
            if (current != destination)
            {
                items.Move(current, destination);
            }
        }
    }

    private static bool IsSameOrder<T>(IReadOnlyList<T> items, IReadOnlyList<T> ordered)
        where T : class
    {
        if (items.Count != ordered.Count)
        {
            return false;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (!ReferenceEquals(items[index], ordered[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static int IndexOf<T>(IReadOnlyList<T> list, T row)
        where T : class
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (ReferenceEquals(list[index], row))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>順の合っている行の最長の並び(最長増加部分列)を求める。結果は <paramref name="ordered"/> の番号ごとの印。</summary>
    private static bool[] FindRowsThatStay<T>(IReadOnlyList<T> items, IReadOnlyList<T> ordered)
        where T : class
    {
        var count = items.Count;
        var positions = new int[count];
        var tails = new int[count];
        var previous = new int[count];
        var length = 0;
        for (var index = 0; index < count; index++)
        {
            positions[index] = IndexOf(ordered, items[index]);
            var low = 0;
            var high = length;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (positions[tails[middle]] < positions[index])
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            previous[index] = low > 0 ? tails[low - 1] : -1;
            tails[low] = index;
            if (low == length)
            {
                length++;
            }
        }

        var stays = new bool[ordered.Count];
        for (var index = length > 0 ? tails[length - 1] : -1; index >= 0; index = previous[index])
        {
            stays[positions[index]] = true;
        }

        return stays;
    }
}
