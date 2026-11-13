// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 配列の先頭 <paramref name="count"/> 個を読む一覧。配列を写さずに渡す。
/// 束縛は参照の変化で気付くので、配列の中身を書き換えたら新しく作って渡す。
/// </summary>
public sealed class ArrayPrefixList<T>(T[] items, int count) : IReadOnlyList<T>
{
    public int Count => count;

    public T this[int index] => (uint)index < (uint)count
        ? items[index]
        : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < count; index++)
        {
            yield return items[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
