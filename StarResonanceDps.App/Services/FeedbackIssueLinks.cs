using System.Runtime.InteropServices;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Diagnostics;

namespace StarResonanceDps.App.Services;

/// <summary>GitHubのIssueの投稿画面を開くURLを作る。欄の名前は .github/ISSUE_TEMPLATE/feedback.yml と同じ。</summary>
internal static class FeedbackIssueLinks
{
    // これより長いと、GitHubのログインの後に入力済みの画面へ戻れない。
    private const int MaxUrlLength = 4000;

    public static string CreateNewIssueUrl()
    {
        return ProjectLinks.NewIssueUrl + "&environment=" + Uri.EscapeDataString(CreateEnvironmentText());
    }

    /// <summary>検出された項目の報告のURLを、1つの画面に入る分ずつ作る。項目は渡した順に入れる。</summary>
    public static IReadOnlyList<DetectionReportLink> CreateDetectionReportLinks(IReadOnlyList<ResidentDetection> detections)
    {
        var environment = CreateEnvironmentText();
        var links = new List<DetectionReportLink>();
        var start = 0;
        while (start < detections.Count)
        {
            var (url, count) = CreatePageUrl(detections, start, environment);
            links.Add(new DetectionReportLink(url, detections.Skip(start).Take(count).ToList()));
            start += count;
        }

        return links;
    }

    /// <summary>
    /// <paramref name="start"/>の行から、入る行だけを本文に入れたURLと、入れた行の数を作る。
    /// 1行だけで入らないときは、その行の末尾を切る。
    /// </summary>
    private static (string Url, int Count) CreatePageUrl(IReadOnlyList<ResidentDetection> detections, int start, string environment)
    {
        var content = string.Empty;
        var url = string.Empty;
        var count = 0;

        for (var index = start; index < detections.Count; index++)
        {
            var line = FormatLine(detections[index]);
            var nextContent = count == 0 ? line : content + "\n" + line;
            var nextUrl = CreateDetectionUrl(count + 1, environment, nextContent);
            if (nextUrl.Length > MaxUrlLength)
            {
                break;
            }

            content = nextContent;
            url = nextUrl;
            count++;
        }

        return count > 0
            ? (url, count)
            : (CreateTruncatedUrl(environment, FormatLine(detections[start])), 1);
    }

    private static string CreateTruncatedUrl(string environment, string line)
    {
        var low = 0;
        var high = line.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (CreateDetectionUrl(1, environment, TakeWholeCharacters(line, middle)).Length <= MaxUrlLength)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return CreateDetectionUrl(1, environment, TakeWholeCharacters(line, low));
    }

    // サロゲートペアの途中で切らない。
    private static string TakeWholeCharacters(string text, int length)
    {
        return length > 0 && length < text.Length && char.IsHighSurrogate(text[length - 1])
            ? text[..(length - 1)]
            : text[..length];
    }

    private static string CreateDetectionUrl(int count, string environment, string content)
    {
        var title = LocalizationManager.Instance.Format("Feedback_Report_IssueTitle", AppUpdateService.CurrentVersion.ToString(3), count);
        return ProjectLinks.NewIssueUrl
            + "&title=" + Uri.EscapeDataString(title)
            + "&environment=" + Uri.EscapeDataString(environment)
            + "&content=" + Uri.EscapeDataString(content);
    }

    /// <summary>本文の行の先頭。同じ報告があるかはここで見分ける(<see cref="FeedbackIssueLookup"/>)。</summary>
    public static string FormatLineKey(ResidentDetection detection)
    {
        return $"{detection.Kind} {detection.Id}";
    }

    /// <summary>本文の行の版。閉じたIssueの行が今の版かを見分ける。環境の欄は投稿する人が消せるので、行に持つ。</summary>
    public static string AppVersionField => $"app={AppUpdateService.CurrentVersion.ToString(3)}";

    private static string FormatLine(ResidentDetection detection)
    {
        return $"{FormatLineKey(detection)} {AppVersionField} server={detection.Server} {detection.Details}";
    }

    private static string CreateEnvironmentText()
    {
        return string.Join(
            "\n",
            $"App: {AppUpdateService.CurrentVersion.ToString(3)}",
            $"OS: {RuntimeInformation.OSDescription}",
            $".NET: {RuntimeInformation.FrameworkDescription}",
            $"Npcap: {NpcapVersionProbe.GetVersionString()}",
            $"Language: {LocalizationManager.Instance.CurrentCulture.Name}",
            $"Game server: {GameServerStore.Current}");
    }
}

/// <summary>検出された項目の報告の1つの画面のURLと、本文に入れた項目。</summary>
internal readonly record struct DetectionReportLink(string Url, IReadOnlyList<ResidentDetection> Detections);
