// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Diagnostics;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class BlobType
{
    public virtual string? DebugName => null;

    public BlobType()
    {

    }

    public BlobType(ref BlobReader blob)
    {
        Read(ref blob);
    }

    public void Read(ref BlobReader blob)
    {
        var tag = blob.ReadInt();
        if (tag != -2)
        {
            System.Diagnostics.Debug.WriteLine($"Invalid begin tag: {tag}");
            return;
        }

        var size = blob.ReadInt();
        if (size == -3)
        {
            return;
        }

        if (size < 0)
        {
            System.Diagnostics.Debug.WriteLine($"BlobType.Read size was unexpectedly negative! size = {size}");
            return;
        }

        var offset = blob.Offset;
        var index = blob.ReadInt();
        while (0 < index)
        {
            if (!ParseField(index, ref blob))
            {
                // 未対応フィールドでも、値がネストしたblobならサイズが分かるのでその値だけ飛ばせる。
                // 飛ばせれば以降のフィールドを読み続けられる(ProfessionList は field 61 と番号が
                // 大きく、手前で打ち切ると到達しない)。
                if (!blob.TrySkipNestedBlob())
                {
                    // スカラー等で値の長さが分からない場合は、blob 全体の末尾へ退避するしかない。
                    // これ以降のフィールドは黙って失われるため、気付けるようログに残す。
                    LogParseAbort(DebugName, index);
                    blob.Offset = offset + size;
                }
            }

            index = blob.ReadInt();
        }

        if (index != -3)
        {
            Debug.WriteLine($"Invalid end tag {index} at {blob.Offset}");
        }
    }

    private static readonly HashSet<string> ReportedParseAborts = [];

    /// <summary>
    /// 未対応フィールドで解析を打ち切ったことを一度だけ記録する。
    /// このフォーマットは未知フィールドの値を読み飛ばせないため、打ち切ると
    /// 以降のフィールドが失われる。黙って欠落させると原因不明の不具合になるので残す。
    /// </summary>
    private static void LogParseAbort(string? debugName, int fieldIndex)
    {
        if (string.IsNullOrEmpty(debugName))
        {
            return;
        }

        var key = debugName + "#" + fieldIndex;
        lock (ReportedParseAborts)
        {
            if (!ReportedParseAborts.Add(key))
            {
                return;
            }
        }

        Serilog.Log.Debug(
            "Blob parse aborted at unhandled field {DebugName}#{FieldIndex}; remaining fields are lost.",
            debugName,
            fieldIndex);
    }

    public virtual bool ParseField(int index, ref BlobReader blob)
    {
        return false;
    }

}
