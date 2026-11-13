// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Globalization;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインが言語ごとに渡す文字(名前・ジャンル・通知の項目の文字)の引き方と写し方。どれも同じ決まりで引く。
/// </summary>
internal static class PluginTexts
{
    public static readonly IReadOnlyDictionary<string, string> Empty =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    /// <summary>表示中の言語(ja-JP)の文字。無ければ言語だけの鍵(ja)→ 英語(en-US)→ どれか1つ。1つも無ければ null。</summary>
    public static string? Resolve(IReadOnlyDictionary<string, string> texts, CultureInfo culture)
    {
        if (texts.TryGetValue(culture.Name, out var text)
            || texts.TryGetValue(culture.TwoLetterISOLanguageName, out text)
            || texts.TryGetValue("en-US", out text))
        {
            return text;
        }

        return texts.Values.FirstOrDefault();
    }

    /// <summary>
    /// プラグインが渡した文字を、言語名の大文字と小文字を区別しない写しにする。言語名と文字は前後の空白を除き、空のものは捨てる。
    /// 同じ言語が2つあれば null(属性の読み込みと同じ決まり)。
    /// </summary>
    public static IReadOnlyDictionary<string, string>? TryCopy(IEnumerable<KeyValuePair<string, string>>? texts)
    {
        var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (cultureName, text) in texts ?? [])
        {
            var trimmedCultureName = cultureName?.Trim();
            var trimmedText = text?.Trim();
            if (string.IsNullOrEmpty(trimmedCultureName) || string.IsNullOrEmpty(trimmedText))
            {
                continue;
            }

            if (!copy.TryAdd(trimmedCultureName, trimmedText))
            {
                return null;
            }
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }
}
