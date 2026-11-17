// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;

namespace StarResonanceDps.App.Services;

/// <summary>ヘルプのリンクのうち、アプリの言語で開く先が変わるページ(Issueの投稿画面は <see cref="ProjectLinks.GetNewIssueUrl"/>)。</summary>
public enum LocalizedProjectPage
{
    /// <summary>Wikiのその言語のHome(題がアプリの名前とWikiのページ)。</summary>
    WikiHome,

    /// <summary>Wikiのその言語のプラグイン開発のページ。</summary>
    PluginDevelopment
}

/// <summary>プロジェクトのページ(GitHubのリポジトリとWiki、GitHub Sponsors)と、更新の確認とIssueの検索に使うGitHubのAPI。</summary>
public static class ProjectLinks
{
    public const string RepositoryUrl = "https://github.com/ubokun/StarResonanceDps";

    private const string WikiUrl = "https://github.com/kokoukohiro/StarResonanceDps/wiki";

    public const string ReleasesUrl = RepositoryUrl + "/releases";

    public const string SponsorsUrl = "https://github.com/sponsors/kokoukohiro";

    public const string LicenseUrl = RepositoryUrl + "/blob/main/LICENSING.md";

    /// <summary>公開された最新のリリース。下書きとプレリリースは返らない。リリースが1つも無ければ404。</summary>
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/ubokun/StarResonanceDps/releases/latest";

    /// <summary>Issueの検索。ログインなしは同じIPアドレスから1分に10回まで。</summary>
    public const string IssueSearchApiUrl = "https://api.github.com/search/issues";

    /// <summary>Issueの検索をこのリポジトリに絞る修飾子。</summary>
    public const string IssueSearchRepository = "repo:ubokun/StarResonanceDps";

    /// <summary>
    /// アプリの言語のページのURL。Wikiのページ名はdocs/wikiのファイル名(その言語の名前、空白はハイフン)と同じで、
    /// ファイル名を変えるとこのリンクが切れる。
    /// </summary>
    public static string GetLocalizedUrl(LocalizedProjectPage page, CultureInfo culture)
    {
        var pages = GetLocalizedPages(culture);
        var pageName = page switch
        {
            LocalizedProjectPage.WikiHome => pages.WikiHome,
            LocalizedProjectPage.PluginDevelopment => pages.PluginDevelopment,
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
        };
        return $"{WikiUrl}/{Uri.EscapeDataString(pageName)}";
    }

    /// <summary>
    /// アプリの言語のIssueの投稿画面(後ろに欄の値を <c>&amp;</c> でつなぐ)。型のファイル名は .github/ISSUE_TEMPLATE と同じで、
    /// ファイル名を変えるとこのリンクが切れる。
    /// </summary>
    public static string GetNewIssueUrl(CultureInfo culture)
    {
        return $"{RepositoryUrl}/issues/new?template={GetLocalizedPages(culture).IssueTemplate}";
    }

    /// <summary>
    /// アプリの言語(<c>LocalizationManager.CurrentCulture</c>)のWikiのページ名とIssueの型のファイル名。4言語のほかは英語(アプリの文言と同じ)。
    /// 英語の型 feedback.yml は、公開した古い版(言語に関係なくこの名前を開く)と兼ねる。
    /// </summary>
    private static LocalizedPages GetLocalizedPages(CultureInfo culture) => culture.Name switch
    {
        "ja-JP" => new("お願い、DPS見ないで…Wiki", "プラグイン開発", "feedback-ja.yml"),
        "ko-KR" => new("내-DPS-조회하지-마-Wiki", "플러그인-개발", "feedback-ko.yml"),
        "zh-CN" => new("别查我DPS-Wiki", "插件开发", "feedback-zh-CN.yml"),
        _ => new("Don't-Check-My-DPS-Wiki", "Plugin-Development", "feedback.yml")
    };

    private readonly record struct LocalizedPages(string WikiHome, string PluginDevelopment, string IssueTemplate);
}
