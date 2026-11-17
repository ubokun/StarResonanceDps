// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media.Imaging;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 地図の画像を、開いているマップの窓のあいだで共有する。画像は元の大きさのままで、1枚がメモリを数十MB使う。
///
/// <para>
/// 画面のスレッドだけで使う。同じファイルの画像は1つだけ持ち、借りている窓(<see cref="MapImageLease"/>)が全部手放したら画像も手放す。
/// 持つのは、開いているマップの窓が映している地図の数まで。
/// </para>
///
/// <para>
/// 読み込みは裏のスレッドで行い、終わったら画面のスレッドで知らせる。読めなかったファイルは失敗として知らせ、ログに残す
/// (窓は状態の印で読めないことを出す)。
/// </para>
/// </summary>
public sealed class MapImageCache
{
    private readonly Dictionary<string, Entry> _entriesByPath = new(StringComparer.OrdinalIgnoreCase);

    private MapImageCache()
    {
    }

    public static MapImageCache Instance { get; } = new();

    /// <summary>その画像を借りる。まだ読んでいなければ読み始める。使い終わったら <see cref="MapImageLease.Dispose"/> で手放す。</summary>
    public MapImageLease Acquire(string path)
    {
        if (!_entriesByPath.TryGetValue(path, out var entry))
        {
            entry = new Entry(path);
            _entriesByPath.Add(path, entry);
            _ = LoadAsync(entry);
        }

        entry.LeaseCount++;
        return new MapImageLease(this, entry);
    }

    private void Release(Entry entry)
    {
        entry.LeaseCount--;
        if (entry.LeaseCount == 0)
        {
            _entriesByPath.Remove(entry.Path);
        }
    }

    /// <summary>裏のスレッドで読み、続きは呼んだ画面のスレッドへ戻って知らせる。</summary>
    private static async Task LoadAsync(Entry entry)
    {
        BitmapSource? image = null;
        try
        {
            image = await Task.Run(() => Load(entry.Path));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load map image {Path}", entry.Path);
        }

        entry.Complete(image);
    }

    private static BitmapSource Load(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>借りている画像1つ。<see cref="Image"/> は読み終わるまでと、読めなかったときは null。</summary>
    public sealed class MapImageLease : IDisposable
    {
        private readonly MapImageCache _cache;
        private readonly Entry _entry;
        private bool _isDisposed;

        internal MapImageLease(MapImageCache cache, Entry entry)
        {
            _cache = cache;
            _entry = entry;
        }

        public BitmapSource? Image => _entry.Image;

        /// <summary>読めなかった。</summary>
        public bool HasFailed => _entry.HasFailed;

        /// <summary>読み終わった(か、読めなかった)。画面のスレッドで上がる。</summary>
        public event EventHandler? Completed
        {
            add => _entry.Completed += value;
            remove => _entry.Completed -= value;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _cache.Release(_entry);
        }
    }

    internal sealed class Entry(string path)
    {
        public string Path { get; } = path;

        public int LeaseCount { get; set; }

        public BitmapSource? Image { get; private set; }

        public bool HasFailed { get; private set; }

        public event EventHandler? Completed;

        public void Complete(BitmapSource? image)
        {
            Image = image;
            HasFailed = image is null;
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
