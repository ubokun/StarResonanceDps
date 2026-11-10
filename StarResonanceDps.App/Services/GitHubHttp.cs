using System.Net.Http;

namespace StarResonanceDps.App.Services;

/// <summary>
/// GitHubへの通信(更新の確認と取得、Issueの検索)。
/// 止めるのはキャンセルで、時間では打ち切らない(接続そのものができなければ Windows が失敗を返す)。
/// </summary>
internal static class GitHubHttp
{
    public static HttpClient Client { get; } = CreateClient();

    public static HttpRequestMessage CreateApiRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"StarResonanceDps/{AppUpdateService.CurrentVersion.ToString(3)}");
        return client;
    }
}
