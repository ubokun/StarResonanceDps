// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Notifications;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels.WidgetSettings;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.ViewModels.Notifications;

/// <summary>
/// 通知タブの項目の文章の決まり(表示言語の既定の文・プレビュー・差し込む項目)。
/// 本体の項目は文言の鍵と <see cref="NotificationTextFormatter"/>、プラグインの項目は定義と <see cref="PluginNotificationTextFormatter"/> で決まる。
/// </summary>
public interface INotificationTextSource
{
    string GetDefaultText();

    string FormatPreview(string format);

    IReadOnlyList<MeterPlayerInfoFormatField> CreateFormatFields();
}

internal sealed class HostNotificationTextSource : INotificationTextSource
{
    private readonly NotificationKind _kind;
    private readonly (string Key, string LabelResourceKey, string Placeholder)[] _fieldDefinitions;

    public HostNotificationTextSource(
        NotificationKind kind,
        (string Key, string LabelResourceKey, string Placeholder)[] fieldDefinitions)
    {
        _kind = kind;
        _fieldDefinitions = fieldDefinitions;
    }

    public string GetDefaultText()
    {
        return LocalizationManager.Instance.GetString(NotificationTextFormatter.GetDefaultKey(_kind));
    }

    public string FormatPreview(string format)
    {
        return NotificationTextFormatter.FormatPreview(_kind, format);
    }

    public IReadOnlyList<MeterPlayerInfoFormatField> CreateFormatFields()
    {
        return
        [
            .. _fieldDefinitions.Select(definition => new MeterPlayerInfoFormatField(
                definition.Key,
                LocalizationManager.Instance.GetString(definition.LabelResourceKey),
                definition.Placeholder))
        ];
    }
}

internal sealed class PluginNotificationTextSource : INotificationTextSource
{
    private readonly PluginNotificationDefinition _definition;

    public PluginNotificationTextSource(PluginNotificationDefinition definition)
    {
        _definition = definition;
    }

    public string GetDefaultText()
    {
        return PluginNotificationTextFormatter.GetDefaultText(_definition, LocalizationManager.Instance.CurrentCulture);
    }

    public string FormatPreview(string format)
    {
        return PluginNotificationTextFormatter.FormatPreview(_definition, format, LocalizationManager.Instance.CurrentCulture);
    }

    public IReadOnlyList<MeterPlayerInfoFormatField> CreateFormatFields()
    {
        var culture = LocalizationManager.Instance.CurrentCulture;
        return
        [
            .. _definition.Fields.Select(field => new MeterPlayerInfoFormatField(
                field.Key,
                PluginTexts.Resolve(field.Names, culture)
                    ?? throw new InvalidOperationException($"The validated field '{field.Key}' has no name."),
                "{" + field.Key + "}"))
        ];
    }
}
