// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインが渡したウィジェットの定義を確かめ、本体が持つ写しを作る。
/// 決まりは SDK の <see cref="PluginWidgetDefinition"/> と <see cref="PluginWidgetSettingSection"/> の説明と同じ。
/// 決まりに合わなければ、理由(ログに書く英語)を返し、本体はそのプラグインを止める。
/// </summary>
internal static class PluginWidgetValidator
{
    /// <summary>節のアイコンに使える形の鍵(SDK の公開の一覧のうち、描き方の2つを除いたもの)。</summary>
    private static readonly HashSet<string> IconKeys = typeof(PluginResourceKeys.Icons)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .Where(key => key is not PluginResourceKeys.Icons.StrokeStyle and not PluginResourceKeys.Icons.FillStyle)
        .ToHashSet(StringComparer.Ordinal);

    /// <param name="ids">そのプラグインでもう使った ID(大文字と小文字を区別しない)。確かめた ID を足す。両方の口を通して重ならないように渡す。</param>
    public static bool TryValidate(
        IReadOnlyList<PluginWidgetDefinition>? definitions,
        HashSet<string> ids,
        [NotNullWhen(true)] out IReadOnlyList<PluginWidgetDefinition>? copies,
        [NotNullWhen(false)] out string? error)
    {
        copies = null;
        if (definitions is null)
        {
            error = "The list of widget definitions is null.";
            return false;
        }

        var result = new List<PluginWidgetDefinition>(definitions.Count);
        foreach (var definition in definitions)
        {
            if (definition is null)
            {
                error = "A widget definition is null.";
                return false;
            }

            if (!PluginIdRules.IsValid(definition.Id))
            {
                error = $"The widget ID '{definition.Id}' is missing or contains unsupported characters.";
                return false;
            }

            var id = definition.Id;
            if (!ids.Add(id))
            {
                error = $"The widget ID '{id}' is defined more than once.";
                return false;
            }

            if (!PluginSettingValidator.TryCopyRequiredTexts(definition.Names, out var names))
            {
                error = $"The widget '{id}' needs at least one name, and each culture only once.";
                return false;
            }

            if (!IsFinitePositive(definition.DefaultWidth) || !IsFinitePositive(definition.DefaultHeight))
            {
                error = $"The widget '{id}' needs a finite default size above 0.";
                return false;
            }

            if (!TryCopySections(definition.SettingSections, out var sections, out var sectionError))
            {
                error = $"The widget '{id}': {sectionError}";
                return false;
            }

            result.Add(new PluginWidgetDefinition
            {
                Id = id,
                Names = names,
                DefaultWidth = definition.DefaultWidth,
                DefaultHeight = definition.DefaultHeight,
                HasFooter = definition.HasFooter,
                ScrollsVertically = definition.ScrollsVertically,
                SettingSections = sections
            });
        }

        copies = result;
        error = null;
        return true;
    }

    /// <summary>相手の一覧を確かめ、本体が持つ写しを作る。決まりは SDK の <see cref="PluginWidgetTarget"/> の説明と同じ。空の一覧は決まりに合っている。</summary>
    public static bool TryCopyTargets(
        IReadOnlyList<PluginWidgetTarget>? targets,
        [NotNullWhen(true)] out IReadOnlyList<PluginWidgetTarget>? copies,
        [NotNullWhen(false)] out string? error)
    {
        copies = null;
        if (targets is null)
        {
            error = "The list of targets is null.";
            return false;
        }

        var result = new List<PluginWidgetTarget>(targets.Count);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in targets)
        {
            if (target is null)
            {
                error = "A target is null.";
                return false;
            }

            if (!PluginIdRules.IsValid(target.Id))
            {
                error = $"The target ID '{target.Id}' is missing or contains unsupported characters.";
                return false;
            }

            var id = target.Id;
            if (!ids.Add(id))
            {
                error = $"The target ID '{id}' is returned more than once.";
                return false;
            }

            if (!PluginSettingValidator.TryCopyRequiredTexts(target.Names, out var names))
            {
                error = $"The target '{id}' needs at least one name, and each culture only once.";
                return false;
            }

            result.Add(new PluginWidgetTarget
            {
                Id = id,
                Names = names,
                IsDefault = target.IsDefault
            });
        }

        copies = result;
        error = null;
        return true;
    }

    private static bool TryCopySections(
        IReadOnlyList<PluginWidgetSettingSection>? sections,
        out IReadOnlyList<PluginWidgetSettingSection> copies,
        [NotNullWhen(false)] out string? error)
    {
        var result = new List<PluginWidgetSettingSection>();
        copies = result;
        var settingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections ?? [])
        {
            if (section is null)
            {
                error = "a setting section is null.";
                return false;
            }

            if (!PluginSettingValidator.TryCopyRequiredTexts(section.Names, out var names))
            {
                error = "a setting section needs at least one name, and each culture only once.";
                return false;
            }

            if (section.Icon is null || !IconKeys.Contains(section.Icon))
            {
                error = $"the setting section icon '{section.Icon}' is not one of PluginResourceKeys.Icons.";
                return false;
            }

            if (section.IconStyle is not PluginResourceKeys.Icons.StrokeStyle and not PluginResourceKeys.Icons.FillStyle)
            {
                error = $"the setting section icon style '{section.IconStyle}' must be PluginResourceKeys.Icons.StrokeStyle or FillStyle.";
                return false;
            }

            if (!PluginSettingValidator.TryCopySettings(section.Settings, allowsColor: true, settingIds, out var settings, out var settingError))
            {
                error = settingError;
                return false;
            }

            if (settings.Count == 0)
            {
                error = "a setting section needs at least one setting.";
                return false;
            }

            result.Add(new PluginWidgetSettingSection
            {
                Names = names,
                Icon = section.Icon,
                IconStyle = section.IconStyle,
                Settings = settings
            });
        }

        error = null;
        return true;
    }

    private static bool IsFinitePositive(double value)
    {
        return double.IsFinite(value) && value > 0;
    }
}
