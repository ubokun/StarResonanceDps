using System.Windows.Controls;
using System.Windows;

internal static class DemoResources
{
    public static void Load(Application app)
    {
        app.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
        foreach (var name in new[] { "Colors", "Spacing", "Typography", "Panels", "Menus", "Buttons", "ScrollBars", "Inputs", "Icons" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri($"pack://application:,,,/StarResonanceDps;component/Styles/{name}.xaml") });
    }
}
