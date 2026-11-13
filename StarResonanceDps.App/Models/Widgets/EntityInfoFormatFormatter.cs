// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

public static partial class EntityInfoFormatFormatter
{
    public static string FormatPreview(string? formatString)
    {
        return Format(
            LocalizationManager.Instance.GetString("Settings_EntityInfo_PreviewName"),
            50,
            formatString);
    }

    public static string Format(string name, int level, string? formatString)
    {
        var result = string.IsNullOrEmpty(formatString)
            ? name
            : formatString;

        result = GetNameRegex().Replace(result, name);
        result = GetLevelRegex().Replace(result, level.ToString());
        return result;
    }

    [GeneratedRegex(@"\{EntityName\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetNameRegex();

    [GeneratedRegex(@"\{Level\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetLevelRegex();
}
