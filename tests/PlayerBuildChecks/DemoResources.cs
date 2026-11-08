using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;

internal static class DemoResources
{
    public static void Load(Application app)
    {
        app.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
        foreach (var name in new[] { "Colors", "Spacing", "Typography", "Panels", "Menus", "Buttons", "ScrollBars", "Inputs", "Icons" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri($"pack://application:,,,/StarResonanceDps;component/Styles/{name}.xaml") });
        // Test executable uses a different entry assembly; qualify relative bitmap resources.
        var icons = app.Resources.MergedDictionaries.Last();
        foreach (var key in icons.Keys.Cast<object>().ToArray())
            if (icons[key] is System.Windows.Media.Imaging.BitmapImage bitmap && bitmap.UriSource.OriginalString.StartsWith("/Assets/"))
                app.Resources[key] = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/StarResonanceDps;component" + bitmap.UriSource.OriginalString));
        var combatMask = new Style(typeof(System.Windows.Shapes.Rectangle));
        combatMask.Setters.Add(new Setter(System.Windows.Shapes.Shape.FillProperty, System.Windows.Media.Brushes.LimeGreen));
        combatMask.Setters.Add(new Setter(UIElement.OpacityMaskProperty, new System.Windows.Media.ImageBrush((System.Windows.Media.ImageSource)app.Resources["Icon.CombatState"])));
        app.Resources["Icon.CombatStateMask"] = combatMask;
        var settings = new SettingsConfig();
        var path = Environment.GetEnvironmentVariable("STARRESONANCE_PREVIEW_SETTINGS");
        if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
        {
            using var document = JsonDocument.Parse(System.IO.File.ReadAllText(path));
            var root = document.RootElement;
            if (root.TryGetProperty("Config", out var config)) root = config;
            if (root.TryGetProperty("Settings", out var saved))
            {
                settings.WindowColorIndex = saved.GetProperty("WindowColorIndex").GetInt32();
                settings.WindowColors = saved.GetProperty("WindowColors").EnumerateArray().Select(x => x.GetString()!).ToList();
            }
        }
        ThemeManager.Instance.ApplyGlobalTheme(settings);
    }
}
