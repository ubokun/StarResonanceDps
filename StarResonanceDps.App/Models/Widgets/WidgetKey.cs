// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// ウィジェットを見分ける値。本体のウィジェットは種別だけ、プラグインのウィジェットは「プラグインの鍵/ウィジェットの ID」も持つ。
/// 保存(<c>WidgetStateManager</c>)はこの値で引く。本体の種別からは暗黙に作れる。
/// </summary>
public readonly record struct WidgetKey
{
    /// <summary>プラグインのウィジェットの保存の鍵の頭。本体の種別の名前とは重ならない。</summary>
    private const string PluginDocumentKeyPrefix = "Plugin:";

    private const char PluginKeySeparator = '/';

    private WidgetKey(WidgetKind kind, string? pluginWidgetKey)
    {
        Kind = kind;
        PluginWidgetKey = pluginWidgetKey;
    }

    public WidgetKind Kind { get; }

    /// <summary>プラグインのウィジェットの「プラグインの鍵/ウィジェットの ID」。本体のウィジェットは null。</summary>
    public string? PluginWidgetKey { get; }

    public bool IsPlugin => PluginWidgetKey is not null;

    /// <summary>WidgetSettings.json の鍵。本体は種別の名前、プラグインは「Plugin:プラグインの鍵/ウィジェットの ID」。</summary>
    public string DocumentKey => PluginWidgetKey is null ? Kind.ToString() : PluginDocumentKeyPrefix + PluginWidgetKey;

    public static implicit operator WidgetKey(WidgetKind kind)
    {
        return kind == WidgetKind.Plugin
            ? throw new ArgumentException("A plugin widget needs its plugin key and widget ID.", nameof(kind))
            : new WidgetKey(kind, null);
    }

    public static WidgetKey ForPlugin(string pluginKey, string widgetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetId);
        return new WidgetKey(WidgetKind.Plugin, pluginKey + PluginKeySeparator + widgetId);
    }

    /// <summary>保存の鍵がプラグインのウィジェットのものか。</summary>
    public static bool IsPluginDocumentKey(string documentKey)
    {
        return documentKey.StartsWith(PluginDocumentKeyPrefix, StringComparison.Ordinal);
    }

    /// <summary>保存の鍵がそのプラグインのウィジェットのものか(プラグインの鍵は大文字と小文字を区別しない)。</summary>
    public static bool IsPluginDocumentKeyOf(string documentKey, string pluginKey)
    {
        var prefix = PluginDocumentKeyPrefix + pluginKey + PluginKeySeparator;
        return documentKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
