// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Diagnostics;

/// <summary>常設の計測が検知したものの種類。</summary>
public enum ResidentDetectionKind
{
    /// <summary>スキル詳細の行に名前が入っていない鍵(<see cref="BlankSourceNameProbe"/>)。番号は <c>ownerId:枝番</c>。</summary>
    BlankSourceName,

    /// <summary>着地先が決まった鍵が、別の先に着いた(<see cref="BlankSourceNameProbe"/>)。番号は <c>ownerId:枝番</c>。</summary>
    SourceLandingConflict,

    /// <summary>タレント由来のバフが、控えてあるマーカーと違う特化を書こうとした(<see cref="SpecConflictProbe"/>)。番号は届いたバフID。</summary>
    SpecTalentConflict,

    /// <summary>置換後スキルIDが、控えてあるマーカーと違う特化を示した(<see cref="SpecConflictProbe"/>)。番号は技ID。</summary>
    SpecSkillConflict
}

/// <summary>検知の1件。</summary>
/// <param name="Id">種類ごとの番号。同じ種類と番号は1件にまとめる。</param>
/// <param name="DetectedAt">最初に検知した時刻。</param>
/// <param name="Server">検知したときのゲームのサーバー。</param>
/// <param name="Details">報告の本文に入れる文字。公開されるので、ほかのプレイヤーの番号(charId・uuid)を入れない。</param>
/// <param name="IsReported">最後に確かめたときに同じ報告がIssueにあった。検索の結果だけで決める(投稿画面を開いただけでは付けない)。</param>
public sealed record ResidentDetection(
    ResidentDetectionKind Kind,
    string Id,
    DateTimeOffset DetectedAt,
    GameServer Server,
    string Details,
    bool IsReported);

/// <summary>
/// 常設の計測の検知をフィードバックタブのために控え、<c>Data\ResidentDetections.json</c> に残す。並びは検知した順。
///
/// <para>
/// 種類と番号ごとに1件で、増えるのは新しい番号の分だけ。アプリのバージョンが変わると全部消す(更新で直った項目を残さない)。
/// ログを消す合図も兼ねる(<see cref="Load"/> の戻り値、消すのは <c>CombatRuntimeHost.ClearLogs</c>)。
/// 通信の控えではないので、ログアウト・キャプチャの停止・マップ移動では消さない。
/// </para>
///
/// <para>保存できなくてもアプリの処理は止めない。ログに残し、その起動の間は控えの一覧で出す。</para>
/// </summary>
public static class ResidentDetectionStore
{
    private static readonly object Sync = new();
    private static readonly List<ResidentDetection> Items = [];
    private static string _appVersion = string.Empty;

    /// <summary>控えが変わった。検知したスレッドから出る。</summary>
    public static event EventHandler? Changed;

    /// <summary>
    /// 起動の最初に1回呼ぶ。読めないファイルはログに残して空から始める。
    /// 検知が1件も無くても今のバージョンを書く(次の更新を見分けるため)。
    /// </summary>
    /// <returns>アプリのバージョンが前の起動から変わったか。前のバージョンが分からないときは false。</returns>
    public static bool Load(string appVersion)
    {
        lock (Sync)
        {
            _appVersion = appVersion;
            Items.Clear();

            var saved = ReadFile();
            if (saved?.AppVersion == appVersion)
            {
                Items.AddRange(saved.Items);
                return false;
            }

            // 書けなければ次の起動も同じ判定になるので、変わったと返さない(ログを消すと失敗の行も消え、起動のたびに消える)。
            return Save() && saved is not null;
        }
    }

    public static IReadOnlyList<ResidentDetection> GetSnapshot()
    {
        lock (Sync)
        {
            return Items.ToArray();
        }
    }

    public static void Add(ResidentDetectionKind kind, string id, string details)
    {
        lock (Sync)
        {
            if (Items.Exists(item => item.Kind == kind && item.Id == id))
            {
                return;
            }

            Items.Add(new ResidentDetection(kind, id, DateTimeOffset.Now, GameServerStore.Current, details, IsReported: false));
            Save();
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// 検索の結果で報告済みの印を決め直す。報告済みだった項目でも、同じ報告が無くなっていれば(Issueが消された)外す。
    /// 確かめなかった項目はそのまま。変わったときだけ保存して知らせる。
    /// </summary>
    public static void ApplyReportCheck(IReadOnlyCollection<ResidentDetection> reported, IReadOnlyCollection<ResidentDetection> notReported)
    {
        lock (Sync)
        {
            var isChanged = false;
            for (var index = 0; index < Items.Count; index++)
            {
                var item = Items[index];
                var isReported = reported.Any(detection => IsSameItem(detection, item))
                    || (item.IsReported && !notReported.Any(detection => IsSameItem(detection, item)));
                if (isReported != item.IsReported)
                {
                    Items[index] = item with { IsReported = isReported };
                    isChanged = true;
                }
            }

            if (!isChanged)
            {
                return;
            }

            Save();
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static bool IsSameItem(ResidentDetection detection, ResidentDetection item)
    {
        return detection.Kind == item.Kind && detection.Id == item.Id;
    }

    private static SavedDetections? ReadFile()
    {
        var path = CombatRuntimePaths.ResidentDetectionsPath;
        try
        {
            return File.Exists(path)
                ? JsonConvert.DeserializeObject<SavedDetections>(File.ReadAllText(path))
                : null;
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not read {Path}; detected items start empty", path);
            return null;
        }
    }

    private static bool Save()
    {
        var path = CombatRuntimePaths.ResidentDetectionsPath;
        var temporaryPath = path + ".tmp";
        try
        {
            var json = JsonConvert.SerializeObject(
                new SavedDetections(_appVersion, Items),
                Formatting.Indented,
                new StringEnumConverter());
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite: true);
            return true;
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not save {Path}", path);
            return false;
        }
    }

    private sealed record SavedDetections(string AppVersion, List<ResidentDetection> Items);
}
