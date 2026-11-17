// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>更新の zip の中の1項目。位置は zip の先頭からのバイトで、<paramref name="EndOffset"/> は次の項目(か中身の一覧)の始まり。</summary>
internal sealed record AppUpdatePackageEntry(
    string Name,
    bool IsFolder,
    int Method,
    uint Crc32,
    long CompressedSize,
    long Size,
    long HeaderOffset,
    long EndOffset);

/// <summary>範囲を指定して1回で取る塊。zip の中で隣り合う、取るファイルの組。</summary>
internal sealed record AppUpdateDeltaRun(long Start, long End, IReadOnlyList<AppUpdatePackageEntry> Entries)
{
    public long Length => End - Start;
}

/// <summary>差分の更新の段取り。取る塊と、アプリのフォルダから作業フォルダへ写したファイルの数。</summary>
internal sealed record AppUpdateDeltaPlan(IReadOnlyList<AppUpdateDeltaRun> Runs, int CopiedCount)
{
    public long DownloadSize => Runs.Sum(run => run.Length);

    public int DownloadCount => Runs.Sum(run => run.Entries.Count);
}

/// <summary>
/// 差分の更新。更新の zip の中身の一覧(末尾のセントラルディレクトリ)だけを範囲で取り、各ファイルの大きさと CRC32 を
/// アプリのフォルダのファイルと比べる。同じファイルはアプリのフォルダから作業フォルダへ写し、違う・無いファイルだけを範囲で取って展開する。
/// 作業フォルダは、zip を全部取ったときと同じく新しいバージョンの全部になる(置き換えの処理は変わらない)。
///
/// <para>
/// 範囲で取る処理(通信)は <see cref="AppUpdateService"/> が持つ。ここは zip の形の読み取り・比べる・展開だけ。
/// 読めない形の zip(zip64・複数のディスク・暗号化・保存と Deflate 以外の圧縮・UTF-8 の印の無い英数字以外の名前)は
/// <see cref="NotSupportedException"/> で知らせ、呼び出し側が zip を全部取る。壊れた zip は <see cref="InvalidDataException"/>(全部取るときの展開と同じ)。
/// </para>
/// </summary>
internal static class AppUpdateDelta
{
    private const int EndRecordSize = 22;
    private const int Zip64LocatorSize = 20;
    private const int CentralHeaderSize = 46;
    private const int LocalHeaderSize = 30;
    private const uint EndRecordSignature = 0x06054b50;
    private const uint Zip64LocatorSignature = 0x07064b50;
    private const uint CentralHeaderSignature = 0x02014b50;
    private const uint LocalHeaderSignature = 0x04034b50;
    private const int MethodStored = 0;
    private const int MethodDeflate = 8;
    private const int FlagEncrypted = 0x0001;
    private const int FlagUtf8Name = 0x0800;
    private const ushort Zip64Count = ushort.MaxValue;
    private const uint Zip64Size = uint.MaxValue;
    private const int BufferSize = 81920;

    /// <summary>zip の末尾から取る大きさ。終わりの記録と、最長のコメント。</summary>
    public static long GetTailLength(long packageSize)
    {
        return Math.Min(packageSize, EndRecordSize + ushort.MaxValue);
    }

    /// <summary>末尾(<paramref name="tailStart"/> から zip の終わりまで)の終わりの記録から、中身の一覧の位置・大きさ・項目の数を読む。</summary>
    public static (long Offset, long Size, int Count) FindDirectory(ReadOnlySpan<byte> tail, long tailStart)
    {
        for (var position = tail.Length - EndRecordSize; position >= 0; position--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail[position..]) != EndRecordSignature
                || position + EndRecordSize + BinaryPrimitives.ReadUInt16LittleEndian(tail[(position + 20)..]) != tail.Length)
            {
                continue;
            }

            var disk = BinaryPrimitives.ReadUInt16LittleEndian(tail[(position + 4)..]);
            var directoryDisk = BinaryPrimitives.ReadUInt16LittleEndian(tail[(position + 6)..]);
            var countOnDisk = BinaryPrimitives.ReadUInt16LittleEndian(tail[(position + 8)..]);
            var count = BinaryPrimitives.ReadUInt16LittleEndian(tail[(position + 10)..]);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(tail[(position + 12)..]);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(tail[(position + 16)..]);

            var hasZip64Locator = position >= Zip64LocatorSize
                && BinaryPrimitives.ReadUInt32LittleEndian(tail[(position - Zip64LocatorSize)..]) == Zip64LocatorSignature;
            if (hasZip64Locator || count == Zip64Count || size == Zip64Size || offset == Zip64Size)
            {
                throw new NotSupportedException("The package is a zip64 archive.");
            }

            if (disk != 0 || directoryDisk != 0 || countOnDisk != count)
            {
                throw new NotSupportedException("The package spans several disks.");
            }

            if (offset + size != tailStart + position)
            {
                throw new InvalidDataException("The package directory does not end where the end record starts.");
            }

            return (offset, size, count);
        }

        throw new InvalidDataException("The package has no zip end record.");
    }

    /// <summary>
    /// 中身の一覧を読む。フォルダの項目も含め、zip の中の位置の順に並べて返す。
    /// 名前がフォルダの外を指す項目は、全部取るときの展開と同じく止める(<paramref name="newDirectory"/> の外)。
    /// </summary>
    public static List<AppUpdatePackageEntry> ReadDirectory(ReadOnlySpan<byte> directory, long directoryOffset, int count, string newDirectory)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(newDirectory)) + Path.DirectorySeparatorChar;
        var headers = new List<(string Name, int Method, uint Crc32, long CompressedSize, long Size, long HeaderOffset)>(count);
        var position = 0;
        for (var index = 0; index < count; index++)
        {
            if (position + CentralHeaderSize > directory.Length
                || BinaryPrimitives.ReadUInt32LittleEndian(directory[position..]) != CentralHeaderSignature)
            {
                throw new InvalidDataException("The package directory is damaged.");
            }

            var header = directory[position..];
            var flags = BinaryPrimitives.ReadUInt16LittleEndian(header[8..]);
            var method = BinaryPrimitives.ReadUInt16LittleEndian(header[10..]);
            var crc = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
            var compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(header[24..]);
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header[28..]);
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header[30..]);
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(header[32..]);
            var headerOffset = BinaryPrimitives.ReadUInt32LittleEndian(header[42..]);
            if (position + CentralHeaderSize + nameLength + extraLength + commentLength > directory.Length)
            {
                throw new InvalidDataException("The package directory is damaged.");
            }

            var name = ReadName(header.Slice(CentralHeaderSize, nameLength), flags);
            if ((flags & FlagEncrypted) != 0)
            {
                throw new NotSupportedException($"{name} in the package is encrypted.");
            }

            if (method is not (MethodStored or MethodDeflate))
            {
                throw new NotSupportedException($"{name} in the package uses compression method {method}.");
            }

            if (compressedSize == Zip64Size || size == Zip64Size || headerOffset == Zip64Size)
            {
                throw new NotSupportedException($"{name} in the package needs zip64.");
            }

            if (!Path.GetFullPath(Path.Combine(root, name)).StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"The package has an entry outside its folder: {name}");
            }

            headers.Add((name, method, crc, compressedSize, size, headerOffset));
            position += CentralHeaderSize + nameLength + extraLength + commentLength;
        }

        headers.Sort((left, right) => left.HeaderOffset.CompareTo(right.HeaderOffset));
        var entries = new List<AppUpdatePackageEntry>(headers.Count);
        for (var index = 0; index < headers.Count; index++)
        {
            var (name, method, crc, compressedSize, size, headerOffset) = headers[index];
            var endOffset = index + 1 < headers.Count ? headers[index + 1].HeaderOffset : directoryOffset;
            if (headerOffset + LocalHeaderSize + compressedSize > endOffset)
            {
                throw new InvalidDataException($"{name} in the package overlaps the next entry.");
            }

            var isFolder = name.EndsWith('/') || name.EndsWith('\\');
            entries.Add(new AppUpdatePackageEntry(name, isFolder, method, crc, compressedSize, size, headerOffset, endOffset));
        }

        return entries;
    }

    /// <summary>
    /// アプリのフォルダのファイルと比べ、大きさと CRC32 が同じファイルは作業フォルダへ写し、残りを範囲で取る塊にまとめる。
    /// 読むのは大きさの同じファイルだけで、進み具合はその合計のバイト。
    /// 隣り合う取るファイルは1つの塊にし、間にフォルダの項目(中身なし)しか無ければつなげる。
    /// </summary>
    public static AppUpdateDeltaPlan CreatePlan(
        IReadOnlyList<AppUpdatePackageEntry> entries,
        string baseDirectory,
        string newDirectory,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var candidates = entries
            .Where(entry => !entry.IsFolder && GetLength(Path.Combine(baseDirectory, entry.Name)) == entry.Size)
            .ToList();
        var total = Math.Max(candidates.Sum(entry => entry.Size), 1);
        long done = 0;
        var lastPercent = -1L;

        var copied = new HashSet<AppUpdatePackageEntry>();
        foreach (var entry in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryCopyUnchanged(entry, Path.Combine(baseDirectory, entry.Name), Path.Combine(newDirectory, entry.Name)))
            {
                copied.Add(entry);
            }

            done += entry.Size;
            var percent = done * 100 / total;
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress.Report((double)done / total);
            }
        }

        var runs = new List<AppUpdateDeltaRun>();
        List<AppUpdatePackageEntry>? current = null;
        long start = 0;
        long end = 0;
        foreach (var entry in entries)
        {
            if (!entry.IsFolder && !copied.Contains(entry))
            {
                if (current is null)
                {
                    current = [];
                    start = entry.HeaderOffset;
                }

                current.Add(entry);
                end = entry.EndOffset;
            }
            else if (entry.IsFolder && current is not null)
            {
                end = entry.EndOffset;
            }
            else if (current is not null)
            {
                runs.Add(new AppUpdateDeltaRun(start, end, current));
                current = null;
            }
        }

        if (current is not null)
        {
            runs.Add(new AppUpdateDeltaRun(start, end, current));
        }

        return new AppUpdateDeltaPlan(runs, copied.Count);
    }

    /// <summary>
    /// 取った塊(<paramref name="deltaPath"/> に <see cref="AppUpdateDeltaPlan.Runs"/> の順で並ぶ)からファイルを作業フォルダへ展開する。
    /// 展開したファイルは大きさと CRC32 を zip の一覧と照らし、合わなければ <see cref="InvalidDataException"/>。
    /// </summary>
    public static void ExtractRuns(
        AppUpdateDeltaPlan plan,
        string deltaPath,
        string newDirectory,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        using var source = new FileStream(deltaPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        var total = Math.Max(plan.DownloadCount, 1);
        var done = 0;
        var lastPercent = -1;
        long runPosition = 0;
        foreach (var run in plan.Runs)
        {
            foreach (var entry in run.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                source.Position = runPosition + entry.HeaderOffset - run.Start;
                ExtractEntry(source, entry, Path.Combine(newDirectory, entry.Name));

                done++;
                var percent = done * 100 / total;
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress.Report((double)done / total);
                }
            }

            runPosition += run.Length;
        }
    }

    /// <summary>UTF-8 の印があれば UTF-8 で読む。無ければ英数字だけ受け付ける(他の文字コードの名前は全部取るときの展開と同じ名前になるとは限らない)。</summary>
    private static string ReadName(ReadOnlySpan<byte> bytes, int flags)
    {
        if ((flags & FlagUtf8Name) == 0 && bytes.ContainsAnyExceptInRange((byte)0, (byte)0x7F))
        {
            throw new NotSupportedException("The package has an entry name that is not ASCII or UTF-8.");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static long GetLength(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? file.Length : -1;
    }

    /// <summary>
    /// アプリのフォルダのファイルを作業フォルダへ写しながら CRC32 を数え、zip の中のファイルと同じなら true。
    /// 違えば写したものを消して false(そのファイルは範囲で取る)。開けないファイルも取る。
    /// </summary>
    private static bool TryCopyUnchanged(AppUpdatePackageEntry entry, string localPath, string newPath)
    {
        FileStream input;
        try
        {
            input = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BufferSize);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Warning(exception, "Could not read {File} in the application folder; it is downloaded", entry.Name);
            return false;
        }

        bool isSame;
        using (input)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            using var output = new FileStream(newPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
            var crc = CopyWithCrc(input, output, out var written);
            isSame = written == entry.Size && crc == entry.Crc32;
        }

        if (!isSame)
        {
            File.Delete(newPath);
        }

        return isSame;
    }

    private static void ExtractEntry(FileStream source, AppUpdatePackageEntry entry, string path)
    {
        Span<byte> header = stackalloc byte[LocalHeaderSize];
        try
        {
            source.ReadExactly(header);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"{entry.Name} in the downloaded part is cut off.", exception);
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != LocalHeaderSignature)
        {
            throw new InvalidDataException($"{entry.Name} in the downloaded part has no local header.");
        }

        source.Position += BinaryPrimitives.ReadUInt16LittleEndian(header[26..]) + BinaryPrimitives.ReadUInt16LittleEndian(header[28..]);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        uint crc;
        long written;
        using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
        {
            if (entry.Method == MethodStored)
            {
                crc = CopyWithCrc(source, output, out written, entry.Size);
            }
            else
            {
                using var inflater = new DeflateStream(source, CompressionMode.Decompress, leaveOpen: true);
                crc = CopyWithCrc(inflater, output, out written, entry.Size + 1);
            }
        }

        if (written != entry.Size || crc != entry.Crc32)
        {
            throw new InvalidDataException($"{entry.Name} in the downloaded part is damaged.");
        }
    }

    /// <summary>終わりまで(<paramref name="limit"/> があればそのバイト数まで)写し、写した中身の CRC32 を返す。</summary>
    private static uint CopyWithCrc(Stream input, Stream output, out long written, long limit = long.MaxValue)
    {
        var hash = new Crc32();
        var buffer = new byte[BufferSize];
        written = 0;
        while (written < limit)
        {
            var read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, limit - written));
            if (read == 0)
            {
                break;
            }

            hash.Append(buffer.AsSpan(0, read));
            output.Write(buffer, 0, read);
            written += read;
        }

        return hash.GetCurrentHashAsUInt32();
    }
}
