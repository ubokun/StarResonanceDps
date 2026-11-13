// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Runtime.InteropServices;
using ZstdSharp;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 足していくだけで、続きから順に読むだけの記録の置き場。回のあいだ全部残す記録(被弾・技の開始)を小さく持つ。
///
/// <para>
/// <see cref="ChunkRowCount"/> 件たまるごとに圧縮し、途中の塊だけ生のまま持つ。1件を指定して読む形は持たない。
/// 古い分を展開するのは、窓を開いて先頭から読むときと保存のときだけ。
/// </para>
///
/// <para>
/// 排他は持たない。読み書きは持ち主のロックの中で行う。解放は持ち主と一緒。
/// </para>
/// </summary>
/// <typeparam name="T">1件の形。生の塊が LOH に入らないよう、1件は約80Bまでにする。</typeparam>
internal sealed class CompressedRecordLog<T> where T : unmanaged
{
    private const int ChunkRowCount = 1024;

    private readonly List<byte[]> _chunks = [];
    private T[] _tail = [];
    private int _tailCount;

    public int Count => _chunks.Count * ChunkRowCount + _tailCount;

    public void Add(in T row)
    {
        if (_tailCount == _tail.Length)
        {
            Array.Resize(ref _tail, Math.Min(Math.Max(_tail.Length * 2, 8), ChunkRowCount));
        }

        _tail[_tailCount++] = row;
        if (_tailCount < ChunkRowCount)
        {
            return;
        }

        using var compressor = new Compressor();
        _chunks.Add(compressor.Wrap(MemoryMarshal.AsBytes(_tail.AsSpan())).ToArray());
        _tailCount = 0;
    }

    /// <summary><paramref name="startIndex"/> 番目から後の全部。無ければ空。</summary>
    public T[] ToArray(int startIndex)
    {
        var count = Count - startIndex;
        if (count <= 0)
        {
            return [];
        }

        var rows = new T[count];
        var chunkIndex = startIndex / ChunkRowCount;
        if (chunkIndex >= _chunks.Count)
        {
            _tail.AsSpan(startIndex - _chunks.Count * ChunkRowCount, count).CopyTo(rows);
            return rows;
        }

        using var decompressor = new Decompressor();
        var written = 0;
        var skippedRows = startIndex % ChunkRowCount;
        if (skippedRows > 0)
        {
            var partial = new T[ChunkRowCount];
            Decompress(decompressor, _chunks[chunkIndex++], partial);
            written = ChunkRowCount - skippedRows;
            partial.AsSpan(skippedRows).CopyTo(rows);
        }

        for (; chunkIndex < _chunks.Count; chunkIndex++)
        {
            Decompress(decompressor, _chunks[chunkIndex], rows.AsSpan(written, ChunkRowCount));
            written += ChunkRowCount;
        }

        _tail.AsSpan(0, _tailCount).CopyTo(rows.AsSpan(written));
        return rows;
    }

    private static void Decompress(Decompressor decompressor, byte[] chunk, Span<T> destination)
    {
        var bytes = MemoryMarshal.AsBytes(destination);
        var written = decompressor.Unwrap(chunk, bytes);
        if (written != bytes.Length)
        {
            throw new InvalidDataException($"Compressed record chunk has {written} bytes, expected {bytes.Length}.");
        }
    }
}
