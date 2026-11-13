// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.Plugins.Debug;

/// <summary>確かめ用のプラグインが共有する、窓・ウィジェット・通知の中身。</summary>
internal static class DebugParts
{
    public const string WidgetId = "debug-widget";
    public const string NotificationId = "debug-notification";

    public static IReadOnlyDictionary<string, string> Text(string text)
    {
        return new Dictionary<string, string> { ["ja-JP"] = text };
    }

    public static TextBlock CreateWindowContent(string text)
    {
        var block = new TextBlock { Text = text, Margin = new Thickness(24, 8, 24, 24), TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.ForegroundProperty, PluginResourceKeys.Brushes.TextPrimary);
        return block;
    }

    public static IReadOnlyList<PluginWidgetDefinition> CreateWidgetDefinitions()
    {
        return
        [
            new PluginWidgetDefinition
            {
                Id = WidgetId,
                Names = Text("デバッグ用のウィジェット"),
                DefaultWidth = 240,
                DefaultHeight = 80
            }
        ];
    }

    public static PluginWidgetContent CreateWidget()
    {
        var block = new TextBlock { Text = "デバッグ用のウィジェット", Margin = new Thickness(8, 4, 8, 4) };
        block.SetResourceReference(FrameworkElement.StyleProperty, PluginResourceKeys.Text.WidgetWindow);
        return new PluginWidgetContent { Body = block };
    }

    public static IReadOnlyList<PluginNotificationDefinition> CreateNotificationDefinitions()
    {
        return
        [
            new PluginNotificationDefinition
            {
                Id = NotificationId,
                Names = Text("デバッグ用の通知"),
                DefaultTexts = Text("デバッグ用の通知です。")
            }
        ];
    }
}
