// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Buffers.Binary;
using System.Diagnostics;
using StarResonanceDps.Core.Protocols.Game.Binary;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class BlobReader
{
    public int Offset;
    public byte[] Buff;
    public bool StreamSafe = false;

    public BlobReader(byte[] buff, bool streamSafe)
    {
        Buff = buff;
        Offset = 0;
        StreamSafe = streamSafe;
    }

    public void AddOffset(int realOffset)
    {
        if (StreamSafe)
        {
            Offset += realOffset + 4;
        }
        else
        {
            Offset += realOffset;
        }
    }

    public int ReadByte()
    {
        var val = Buff.AsSpan()[Offset];
        AddOffset(1);
        return val;
    }
    
    public byte[] ReadBytes(int length)
    {
        var val = Buff.AsSpan()[Offset..(Offset + length)].ToArray();
        AddOffset(length);
        return val;
    }

    public int ReadShort()
    {
        var val = BinaryPrimitives.ReadInt16LittleEndian(Buff.AsSpan()[Offset..]);
        AddOffset(2);
        return val;
    }
    
    public int ReadUShort()
    {
        var val = BinaryPrimitives.ReadUInt16LittleEndian(Buff.AsSpan()[Offset..]);
        AddOffset(2);
        return val;
    }
    
    public int ReadInt()
    {
        var val = BinaryPrimitives.ReadInt32LittleEndian(Buff.AsSpan()[Offset..]);
        AddOffset(4);
        return val;
    }

    /// <summary>
    /// 未対応フィールドの値がネストしたblobなら、その値だけを読み飛ばす。
    ///
    /// <para>
    /// このフォーマットはフィールドごとの型タグも長さ接頭辞も持たないため、
    /// 未知フィールドの値は本来スキップできない。ただしネストしたblobだけは
    /// 先頭が開始タグ(-2)とサイズを持つので、そこだけは正確に飛ばせる。
    /// </para>
    ///
    /// <para>
    /// スキップ後に終端タグ(-3)を検証し、想定と違えば位置を元に戻して false を返す。
    /// 位置がずれたまま読み進めて壊れた値を拾うより、打ち切って失われたと分かる方がよい。
    /// </para>
    /// </summary>
    public bool TrySkipNestedBlob()
    {
        var restoreOffset = Offset;

        if (!CanRead(4))
        {
            Offset = restoreOffset;
            return false;
        }

        if (ReadInt() != -2)
        {
            Offset = restoreOffset;
            return false;
        }

        if (!CanRead(4))
        {
            Offset = restoreOffset;
            return false;
        }

        var size = ReadInt();
        if (size == -3)
        {
            // 空のblob。開始タグと -3 だけで値は終わっている。
            return true;
        }

        if (size < 0 || !CanRead(size + 4))
        {
            Offset = restoreOffset;
            return false;
        }

        Offset += size;
        if (ReadInt() != -3)
        {
            Offset = restoreOffset;
            return false;
        }

        return true;
    }

    private bool CanRead(int byteCount)
    {
        return byteCount >= 0
            && Offset >= 0
            && Offset <= Buff.Length - byteCount;
    }
    
    public uint ReadUInt()
    {
        var val = BinaryPrimitives.ReadUInt32LittleEndian(Buff.AsSpan()[Offset..]);
        AddOffset(4);
        return val;
    }
    
    public long ReadLong()
    {
        var val = BinaryPrimitives.ReadInt64LittleEndian(Buff.AsSpan()[Offset..]);
        AddOffset(8);
        return val;
    }
    
    public ulong ReadULong()
    {
        var val = BinaryPrimitives.ReadUInt64LittleEndian(Buff.AsSpan()[Offset..]);
        AddOffset(8);
        return val;
    }
    
    public float ReadFloat()
    {
        var val = BinaryPrimitives.ReadSingleLittleEndian(Buff.AsSpan()[Offset..]);
        AddOffset(4);
        return val;
    }
    
    public double ReadDouble()
    {
        var val = BinaryPrimitives.ReadDoubleLittleEndian(Buff.AsSpan()[Offset..]);
        AddOffset(8);
        return val;
    }

    public string ReadString()
    {
        var length = ReadUInt();

        var bytes = Buff.AsSpan()[Offset..(Offset + (int)length)];

        AddOffset((int)length);

        return bytes.Length == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(bytes);
    }

    public Dictionary<T, X> ReadHashMap<T, X>() where T : notnull
    {
        int add = ReadInt();
        int remove = 0;
        int update = 0;
        if (add == -4)
        {
            Debug.WriteLine($"HashMap.add={add} (Early Exit)");
            return [];
        }

        if (add == -1)
        {
            Debug.WriteLine($"HashMap.add={add} (Get New Value)");
            add = ReadInt();
        }
        else
        {
            remove = ReadInt();
            update = ReadInt();
        }

        Debug.WriteLine($"HashMap.add={add}, remove={remove}, update={update}");

        var hashMap = new Dictionary<T, X>();

        for (int i = 0; i < add; i++)
        {
            var dk = ReadType<T>(this);
            var val = ReadType<X>(this);
            hashMap.Add(dk, val);
        }

        for (int i = 0; i < remove; i++)
        {
            var dk = ReadType<T>(this);
            if (hashMap.Remove(dk))
            {
                Debug.WriteLine($"HashMap did not find key to remove: {dk}");
            }
        }

        for (int i = 0; i < update; i++)
        {
            var dk = ReadType<T>(this);
            if (hashMap.ContainsKey(dk))
                hashMap[dk] = ReadType<X>(this);
            else
            {
                Debug.WriteLine($"HashMap did not find key to update: {dk}");
                hashMap.Add(dk, ReadType<X>(this));
            }
        }

        return hashMap;
    }

    public BlobHashMapDelta<T, X> ReadHashMapDelta<T, X>() where T : notnull
    {
        int add = ReadInt();
        if (add == -4)
        {
            return new BlobHashMapDelta<T, X>(false, [], [], []);
        }

        bool replacesExisting = false;
        int remove = 0;
        int update = 0;
        if (add == -1)
        {
            replacesExisting = true;
            add = ReadInt();
        }
        else
        {
            remove = ReadInt();
            update = ReadInt();
        }

        var added = new Dictionary<T, X>();
        for (int i = 0; i < add; i++)
        {
            var key = ReadType<T>(this);
            added[key] = ReadType<X>(this);
        }

        var removed = new List<T>(Math.Max(remove, 0));
        for (int i = 0; i < remove; i++)
        {
            removed.Add(ReadType<T>(this));
        }

        var updated = new Dictionary<T, X>();
        for (int i = 0; i < update; i++)
        {
            var key = ReadType<T>(this);
            updated[key] = ReadType<X>(this);
        }

        return new BlobHashMapDelta<T, X>(
            replacesExisting,
            added,
            removed,
            updated);
    }

    public List<T> ReadList<T>()
    {
        int count = ReadInt();

        if (count == -4)
        {
            return [];
        }

        List<T> values = new();

        for (int i = 0; i < count; i++)
        {
            var value = ReadType<T>(this);
            values.Add(value);
        }

        return values;
    }

    public void ReadContainer(Dictionary<int, Action<BlobReader>> map)
    {
        var tag = ReadInt();
        if (tag != -2)
            return;
        
        var size = ReadInt();
        if (size == -3)
            return;
        var offset = Offset;
        var index = ReadInt();
        while (0 < index) {
            if (map.TryGetValue(index, out var action)) {
                action(this);
            }
            else {
                Offset = offset + size;
            }
            index = ReadInt();
        }
    }

    private T ReadType<T>(BlobReader blob)
    {
        if (typeof(T) == typeof(int))
        {
            return (T)(object)blob.ReadInt();
        }
        if (typeof(T) == typeof(uint))
        {
            return (T)(object)blob.ReadUInt();
        }
        if (typeof(T) == typeof(long))
        {
            return (T)(object)blob.ReadLong();
        }
        if (typeof(T) == typeof(ulong))
        {
            return (T)(object)blob.ReadULong();
        }
        if (typeof(T) == typeof(short))
        {
            return (T)(object)blob.ReadShort();
        }
        if (typeof(T) == typeof(ushort))
        {
            return (T)(object)blob.ReadUShort();
        }
        if (typeof(T) == typeof(byte))
        {
            return (T)(object)blob.ReadByte();
        }
        if (typeof(T) == typeof(string))
        {
            return (T)(object)blob.ReadString();
        }
        if (typeof(T).IsSubclassOf(typeof(BlobType)))
        {
            var item = Activator.CreateInstance(typeof(T)) as BlobType;
            item!.Read(ref blob);
            return (T)(object)item!;
        }

        return default!;
    }
}