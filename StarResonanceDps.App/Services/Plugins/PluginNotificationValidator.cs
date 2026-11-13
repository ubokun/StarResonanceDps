// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Diagnostics.CodeAnalysis;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインが渡した通知の項目の定義を確かめ、本体が持つ写しを作る(プラグインが後から中身を書き換えても本体の値は変わらない)。
/// 決まりは SDK の <see cref="PluginNotificationDefinition"/> と <see cref="PluginSettingDefinition"/> の説明と同じ。
/// 決まりに合わなければ、理由(ログに書く英語)を返し、本体はそのプラグインを止める。
/// </summary>
internal static class PluginNotificationValidator
{
    private const int MaxFieldKeyLength = 64;

    public static bool TryValidate(
        IReadOnlyList<PluginNotificationDefinition>? definitions,
        [NotNullWhen(true)] out IReadOnlyList<PluginNotificationDefinition>? copies,
        [NotNullWhen(false)] out string? error)
    {
        copies = null;
        if (definitions is null)
        {
            error = "GetNotificationDefinitions returned null.";
            return false;
        }

        var result = new List<PluginNotificationDefinition>(definitions.Count);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            if (definition is null)
            {
                error = "A notification definition is null.";
                return false;
            }

            if (!PluginIdRules.IsValid(definition.Id))
            {
                error = $"The notification ID '{definition.Id}' is missing or contains unsupported characters.";
                return false;
            }

            var id = definition.Id;
            if (!ids.Add(id))
            {
                error = $"The notification ID '{id}' is defined more than once.";
                return false;
            }

            if (!PluginSettingValidator.TryCopyRequiredTexts(definition.Names, out var names))
            {
                error = $"The notification '{id}' needs at least one name, and each culture only once.";
                return false;
            }

            if (!PluginSettingValidator.TryCopyRequiredTexts(definition.DefaultTexts, out var defaultTexts))
            {
                error = $"The notification '{id}' needs at least one default text, and each culture only once.";
                return false;
            }

            if (!TryCopyFields(definition.Fields, out var fields, out var fieldError))
            {
                error = $"The notification '{id}': {fieldError}";
                return false;
            }

            if (!PluginSettingValidator.TryCopySettings(
                    definition.Settings,
                    allowsColor: false,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    out var settings,
                    out var settingError))
            {
                error = $"The notification '{id}': {settingError}";
                return false;
            }

            result.Add(new PluginNotificationDefinition
            {
                Id = id,
                Names = names,
                DefaultTexts = defaultTexts,
                Fields = fields,
                Settings = settings
            });
        }

        copies = result;
        error = null;
        return true;
    }

    private static bool TryCopyFields(
        IReadOnlyList<PluginNotificationField>? fields,
        out IReadOnlyList<PluginNotificationField> copies,
        [NotNullWhen(false)] out string? error)
    {
        var result = new List<PluginNotificationField>();
        copies = result;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields ?? [])
        {
            if (field is null)
            {
                error = "a field is null.";
                return false;
            }

            var key = field.Key ?? string.Empty;
            if (key.Length is 0 or > MaxFieldKeyLength || !key.All(char.IsAsciiLetterOrDigit))
            {
                error = $"the field key '{field.Key}' must be 1 to {MaxFieldKeyLength} ASCII letters and digits.";
                return false;
            }

            if (!keys.Add(key))
            {
                error = $"the field key '{key}' is defined more than once.";
                return false;
            }

            if (!PluginSettingValidator.TryCopyRequiredTexts(field.Names, out var names))
            {
                error = $"the field '{key}' needs at least one name, and each culture only once.";
                return false;
            }

            if (PluginTexts.TryCopy(field.SampleValues) is not { } samples)
            {
                error = $"the field '{key}' has a culture more than once in its sample values.";
                return false;
            }

            result.Add(new PluginNotificationField
            {
                Key = key,
                Names = names,
                SampleValues = samples
            });
        }

        error = null;
        return true;
    }
}
