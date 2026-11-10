using System.Windows;
using System.Windows.Controls;
using SampleDependency.Library;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SampleDependency.SampleDependencyPlugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: Dependency")]
[assembly: PluginDisplayName("ja-JP", "サンプル: 部品のDLL")]
[assembly: PluginGenre("en-US", "Sample")]
[assembly: PluginGenre("ja-JP", "サンプル")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace SampleDependency;

/// <summary>A plugin that uses a type and a XAML resource from another DLL.</summary>
public sealed class SampleDependencyPlugin : IStarResonancePlugin, IPluginWindowProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent()
    {
        var text = new TextBlock { Margin = new Thickness(24, 8, 24, 24), FontSize = 16, Text = Greeting.Text };

        // The address includes the DLL's version (AssemblyVersion in its project), so that plugins carrying different versions of the same DLL
        // each get their own resources.
        var colors = new ResourceDictionary { Source = new Uri("/SampleDependency.Library;v1.0.0.0;component/Colors.xaml", UriKind.Relative) };
        text.Foreground = (System.Windows.Media.Brush)colors["SampleDependency.Accent"];
        return text;
    }

    public void Shutdown()
    {
    }
}
