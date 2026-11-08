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
