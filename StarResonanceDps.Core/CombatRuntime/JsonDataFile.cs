// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Text;
using Newtonsoft.Json;
using Serilog;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// JSON のデータファイルを、ファイル全体を文字列にせずに、ファイルから直接解く。
/// 解き方は <c>JsonConvert.DeserializeObject</c> と同じ(既定の設定、根の後ろに余計な中身があれば誤り)。
/// </summary>
internal static class JsonDataFile
{
    private const int BufferSize = 64 * 1024;

    public static T? Read<T>(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);
        using var textReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var jsonReader = new JsonTextReader(textReader);
        var serializer = JsonSerializer.CreateDefault();
        serializer.CheckAdditionalContent = true;
        return serializer.Deserialize<T>(jsonReader);
    }
}

/// <summary>
/// 起動時に読むデータファイルを、先に全部同時に(スレッドプールで)解き始め、組み立てる側が今までの順番で結果を受け取る。
///
/// <para>
/// 裏で行うのは、ファイルを読んで解くことだけ。ログも表への書き込みも、組み立てる側が受け取った後に今までどおり行う。
/// 受け取るときは、そのファイルが解き終わるのを待つ。解けなかったときの例外は受け取る側で出る(今までの読み込みと同じ場所)。
/// </para>
///
/// <para>
/// 画面のスレッドだけが使う(始めるのも受け取るのも同じスレッド)。
/// </para>
/// </summary>
internal sealed class JsonDataFileReads
{
    private readonly Dictionary<string, PendingRead> _pending = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>解き始める。ファイルが無ければ「無い」を結果にする(受け取る側が今までどおりエラーを出す)。</summary>
    public void Start<T>(string path)
        where T : class
    {
        var fullPath = Path.GetFullPath(path);
        if (_pending.ContainsKey(fullPath))
        {
            Log.Error("{Path} was started twice; the second start is ignored", fullPath);
            return;
        }

        _pending[fullPath] = new PendingRead(
            typeof(T),
            Task.Run(() => File.Exists(fullPath)
                ? (true, (object?)JsonDataFile.Read<T>(fullPath))
                : (false, null)));
    }

    /// <summary>
    /// 始めておいたファイルの結果を受け取る(解き終わるまで待つ)。ファイルが無ければ false。
    /// 始めていない・別の型で始めた・2回目の受け取りは、始める一覧と読む側の食い違いなので、エラーログを出してその場で読む(読み込みは止めない)。
    /// </summary>
    public bool TryTake<T>(string path, out T? value)
        where T : class
    {
        var fullPath = Path.GetFullPath(path);
        if (_pending.Remove(fullPath, out var pending))
        {
            if (pending.Type == typeof(T))
            {
                var (exists, result) = pending.Task.GetAwaiter().GetResult();
                value = (T?)result;
                return exists;
            }

            Log.Error("{Path} was started as {StartedType} but taken as {TakenType}; reading it now",
                fullPath, pending.Type.Name, typeof(T).Name);
        }
        else
        {
            Log.Error("{Path} was not started before it was taken; reading it now", fullPath);
        }

        if (!File.Exists(fullPath))
        {
            value = null;
            return false;
        }

        value = JsonDataFile.Read<T>(fullPath);
        return true;
    }

    /// <summary>始めたのに受け取られなかったファイルをエラーログに出す(始める一覧と読む側の食い違い)。</summary>
    public void LogUntaken()
    {
        foreach (var path in _pending.Keys)
        {
            Log.Error("{Path} was read at startup but never used", path);
        }

        _pending.Clear();
    }

    private sealed record PendingRead(Type Type, Task<(bool Exists, object? Value)> Task);
}
