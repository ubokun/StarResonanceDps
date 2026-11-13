// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Globalization;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 一覧に出すプラグイン1つの情報。読めなかったプラグインも、フォルダの名前で出す(名前や版などの情報は無い)。
/// </summary>
public sealed class PluginInfo
{
    public PluginInfo(
        string key,
        IReadOnlyDictionary<string, string>? displayNames,
        IReadOnlyDictionary<string, string>? genres,
        string? author,
        string? version,
        string? iconPath,
        bool hasWindow,
        bool hasWidgets,
        bool hasNotifications)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Key = key;
        DisplayNames = CopyTexts(displayNames);
        Genres = CopyTexts(genres);
        Author = author;
        Version = version;
        IconPath = iconPath;
        HasWindow = hasWindow;
        HasWidgets = hasWidgets;
        HasNotifications = hasNotifications;
    }

    /// <summary>プラグインを見分ける鍵。プラグインのフォルダの名前(= 入口の DLL のアセンブリ名)。</summary>
    public string Key { get; }

    /// <summary>言語ごとの名前。</summary>
    public IReadOnlyDictionary<string, string> DisplayNames { get; }

    /// <summary>言語ごとのジャンル名。窓の無いプラグインのものは使わない。</summary>
    public IReadOnlyDictionary<string, string> Genres { get; }

    public string? Author { get; }

    /// <summary>プラグインの版(属性)。読めなかったプラグインは null。</summary>
    public string? Version { get; }

    /// <summary>プラグインの DLL の中のアイコンのパス。無ければ null。</summary>
    public string? IconPath { get; }

    /// <summary>窓を開くプラグインか(<c>IPluginWindowProvider</c>)。</summary>
    public bool HasWindow { get; }

    /// <summary>ウィジェットを足すプラグインか(<c>IPluginWidgetProvider</c>・<c>IPluginMultiWidgetProvider</c>)。</summary>
    public bool HasWidgets { get; }

    /// <summary>通知の項目を足すプラグインか(<c>IPluginNotificationProvider</c>)。</summary>
    public bool HasNotifications { get; }

    /// <summary>本体に何かを足すか(ウィジェットか通知の項目)。</summary>
    public bool HasExtensions => HasWidgets || HasNotifications;

    /// <summary>表示中の言語の名前(引き方は <see cref="PluginTexts.Resolve"/>)。1つも無ければフォルダの名前。</summary>
    public string GetDisplayName(CultureInfo culture)
    {
        return PluginTexts.Resolve(DisplayNames, culture) ?? Key;
    }

    /// <summary>ジャンル名。引き方は <see cref="GetDisplayName"/> と同じ。窓の無いプラグインと、読めなかったプラグインは null。</summary>
    public string? GetGenre(CultureInfo culture)
    {
        return PluginTexts.Resolve(Genres, culture);
    }

    private static IReadOnlyDictionary<string, string> CopyTexts(IReadOnlyDictionary<string, string>? texts)
    {
        return texts is null || texts.Count == 0
            ? PluginTexts.Empty
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(texts, StringComparer.OrdinalIgnoreCase));
    }
}
