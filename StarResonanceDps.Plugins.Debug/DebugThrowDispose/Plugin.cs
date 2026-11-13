// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugThrowDispose.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 閉じるときに例外")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugThrowDispose;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider, IPluginWidgetProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent()
    {
        return new ThrowingContent(DebugParts.CreateWindowContent("この窓を閉じると Dispose が例外を出します。"));
    }

    public IReadOnlyList<PluginWidgetDefinition> GetWidgetDefinitions() => DebugParts.CreateWidgetDefinitions();

    public PluginWidgetContent CreateWidget(string widgetId, IPluginWidgetContext context)
    {
        return new PluginWidgetContent { Body = new ThrowingContent(DebugParts.CreateWidget().Body) };
    }

    public void Shutdown()
    {
    }
}

internal sealed class ThrowingContent : ContentControl, IDisposable
{
    public ThrowingContent(FrameworkElement content)
    {
        Content = content;
    }

    public void Dispose()
    {
        throw new InvalidOperationException("Debug exception from Dispose.");
    }
}
