// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Serilog;
using StarResonanceDps.Core.Diagnostics;

namespace StarResonanceDps.App.Services;

/// <summary>同じ報告があるか確認できなかった理由。エラーの窓の詳細を決める。</summary>
public enum FeedbackIssueLookupFailure
{
    Network,
    Server,
    RateLimit
}

/// <summary>Issueの検索の結果。</summary>
/// <param name="Reported">同じ報告があった項目。確認できなかったときも、それより前に見つかった分は入る。</param>
/// <param name="NotReported">確かめて同じ報告が無かった項目。確かめなかった項目はどちらにも入らない。</param>
/// <param name="Failure">確認できなかった理由。</param>
internal sealed record FeedbackIssueLookupResult(
    IReadOnlyList<ResidentDetection> Reported,
    IReadOnlyList<ResidentDetection> NotReported,
    FeedbackIssueLookupFailure? Failure);

/// <summary>
/// 検出された項目の報告がすでにIssueにあるかをGitHubの検索で確かめる。
///
/// <para>
/// 検索は別の番号のIssueも返すので、返った本文に同じ種類と番号の行があるかで決める。
/// 開いているIssueにあれば版に関係なく報告済み。閉じたIssueは同じ版の行だけ報告済みで、前の版の行なら直したのにまた起きたと見る。
/// </para>
///
/// <para>
/// 渡した項目は全部確かめる(報告のときも、確認のときも)。
/// GitHubが検索を打ち切ったら同じページを送り直し、回数の上限に当たったら解除まで待つ。どちらも時間では諦めず、止めるのは呼び出し元のキャンセル。
/// </para>
/// </summary>
internal static class FeedbackIssueLookup
{
    // 検索のORは1回に5つまで。
    private const int KeysPerSearch = 6;
    private const int ResultsPerPage = 100;

    // 解除の時刻を過ぎても上限の応答が続いたときに、送り続けないための待ち。
    private static readonly TimeSpan MinimumLimitWait = TimeSpan.FromSeconds(1);

    /// <summary>渡した項目を全部確かめる。</summary>
    public static async Task<FeedbackIssueLookupResult> CheckAllAsync(
        IReadOnlyList<ResidentDetection> detections,
        CancellationToken cancellationToken)
    {
        var reported = new List<ResidentDetection>();
        var notReported = new List<ResidentDetection>();
        foreach (var chunk in detections.Chunk(KeysPerSearch))
        {
            var failure = await SearchAsync(chunk, reported, notReported, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return new FeedbackIssueLookupResult(reported, notReported, failure);
            }
        }

        return new FeedbackIssueLookupResult(reported, notReported, null);
    }

    private static async Task<FeedbackIssueLookupFailure?> SearchAsync(
        ResidentDetection[] detections,
        List<ResidentDetection> reported,
        List<ResidentDetection> notReported,
        CancellationToken cancellationToken)
    {
        var remaining = detections.ToList();
        var keys = string.Join(" OR ", detections.Select(detection => $"\"{FeedbackIssueLinks.FormatLineKey(detection)}\""));
        var query = Uri.EscapeDataString($"{ProjectLinks.IssueSearchRepository} is:issue in:body {keys}");

        try
        {
            var page = 1;
            while (remaining.Count > 0)
            {
                using var request = GitHubHttp.CreateApiRequest(
                    $"{ProjectLinks.IssueSearchApiUrl}?q={query}&per_page={ResultsPerPage}&page={page}");
                using var response = await GitHubHttp.Client.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                {
                    if (GetLimitWait(response) is not { } wait)
                    {
                        Log.Warning("GitHub limited the report check (HTTP {StatusCode})", (int)response.StatusCode);
                        return FeedbackIssueLookupFailure.RateLimit;
                    }

                    Log.Information("GitHub limited the report check; waiting {Seconds} seconds", (int)wait.TotalSeconds);
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("The report check failed (HTTP {StatusCode})", (int)response.StatusCode);
                    return (int)response.StatusCode >= 500
                        ? FeedbackIssueLookupFailure.Server
                        : FeedbackIssueLookupFailure.Network;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                foreach (var issue in root.GetProperty("items").EnumerateArray())
                {
                    MoveReported(issue, remaining, reported);
                }

                // 打ち切られても返った結果は有効。まだ見つかっていない項目があれば同じページを送り直す。
                if (root.GetProperty("incomplete_results").GetBoolean())
                {
                    Log.Information("GitHub cut the report check short");
                    continue;
                }

                if (page * ResultsPerPage >= root.GetProperty("total_count").GetInt32())
                {
                    break;
                }

                page++;
            }

            notReported.AddRange(remaining);
            return null;
        }
        catch (HttpRequestException exception)
        {
            Log.Warning(exception, "The report check failed");
            return FeedbackIssueLookupFailure.Network;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentOutOfRangeException)
        {
            Log.Error(exception, "Could not read the report check result");
            return FeedbackIssueLookupFailure.Server;
        }
    }

    /// <summary>
    /// 回数の上限の応答から待つ時間を読む。上限の印が無ければ null。
    /// 解除の時刻は応答の時刻と比べる(PCの時計が進んでいると待ちが無くなり、送り続けてしまう)。
    /// </summary>
    private static TimeSpan? GetLimitWait(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } retryAfter)
        {
            return retryAfter > MinimumLimitWait ? retryAfter : MinimumLimitWait;
        }

        if (GetHeaderValue(response, "x-ratelimit-remaining") != "0"
            || !long.TryParse(GetHeaderValue(response, "x-ratelimit-reset"), NumberStyles.None, CultureInfo.InvariantCulture, out var resetSeconds)
            || response.Headers.Date is not { } responseTime)
        {
            return null;
        }

        var untilReset = DateTimeOffset.FromUnixTimeSeconds(resetSeconds) - responseTime;
        return untilReset > MinimumLimitWait ? untilReset : MinimumLimitWait;
    }

    private static string? GetHeaderValue(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
    }

    private static void MoveReported(JsonElement issue, List<ResidentDetection> remaining, List<ResidentDetection> reported)
    {
        var body = issue.GetProperty("body").GetString();
        if (string.IsNullOrEmpty(body))
        {
            return;
        }

        var isOpen = issue.GetProperty("state").GetString() == "open";
        var lines = body.Split('\n');
        for (var index = remaining.Count - 1; index >= 0; index--)
        {
            if (HasReport(lines, remaining[index], isOpen))
            {
                reported.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
        }
    }

    // 空白まで含めて比べる(21424:6 と 21424:61 を分ける)。
    private static bool HasReport(string[] lines, ResidentDetection detection, bool isOpen)
    {
        var key = FeedbackIssueLinks.FormatLineKey(detection) + " ";
        var linePrefix = isOpen ? key : key + FeedbackIssueLinks.AppVersionField + " ";
        return lines.Any(line => line.StartsWith(linePrefix, StringComparison.Ordinal));
    }
}
