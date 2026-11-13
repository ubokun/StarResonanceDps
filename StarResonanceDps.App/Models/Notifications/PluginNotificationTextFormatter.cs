// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Text.RegularExpressions;
using StarResonanceDps.App.Services;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// プラグインの通知の項目の文章。通知タブのプレビューと通知が同じ組み立てを使う(本体の項目の <see cref="NotificationTextFormatter"/> と同じ役目)。
/// 差し込む項目は定義の <see cref="PluginNotificationField.Key"/> で、<c>{Key}</c>(大文字と小文字を区別しない)を値に置き換える。値の無い項目は空、定義に無い <c>{…}</c> はそのまま。
/// </summary>
public static partial class PluginNotificationTextFormatter
{
    /// <summary>表示言語の既定の文。</summary>
    public static string GetDefaultText(PluginNotificationDefinition definition, CultureInfo culture)
    {
        return PluginTexts.Resolve(definition.DefaultTexts, culture) ?? string.Empty;
    }

    /// <param name="values">差し込む値。鍵は大文字と小文字を区別しない辞書で渡す。</param>
    public static string Format(
        PluginNotificationDefinition definition,
        string format,
        IReadOnlyDictionary<string, string> values)
    {
        return GetFieldRegex().Replace(format, match =>
        {
            var key = match.Groups[1].Value;
            if (!definition.Fields.Any(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                return match.Value;
            }

            return values.TryGetValue(key, out var value) ? value : string.Empty;
        });
    }

    /// <summary>通知タブの見本。値は定義の見本の値(表示言語)。</summary>
    public static string FormatPreview(PluginNotificationDefinition definition, string format, CultureInfo culture)
    {
        var samples = definition.Fields.ToDictionary(
            field => field.Key,
            field => PluginTexts.Resolve(field.SampleValues, culture) ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
        return Format(definition, format, samples);
    }

    [GeneratedRegex(@"\{([A-Za-z0-9]+)\}")]
    private static partial Regex GetFieldRegex();
}
