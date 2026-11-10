This page explains how to make, with a plugin, the window that opens when you click the plugin's row in "[Plugins](Plugins)". What every plugin needs is described in [Plugin Development](Plugin-Development).

## How to make it

Implement `IPluginWindowProvider` in the entry class. A plugin with a window needs `PluginGenre`.

```csharp
using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(MyPlugin.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "私のプラグイン")]
[assembly: PluginGenre("ja-JP", "ツール")]
[assembly: PluginAuthor("YourName")]
[assembly: PluginVersion("1.0.0")]

namespace MyPlugin;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent()
    {
        var text = new TextBlock { Text = "Hello", Margin = new Thickness(16) };
        text.SetResourceReference(TextBlock.ForegroundProperty, PluginResourceKeys.Brushes.TextPrimary);
        return text;
    }

    public PluginWindowOptions? GetWindowOptions()
    {
        return new PluginWindowOptions(width: 720, height: 640, minWidth: 480, minHeight: 320);
    }

    public void Shutdown()
    {
    }
}
```

| Member | What it does |
|---|---|
| `CreateContent` | Returns the content of the window. It is called every time the window opens, so create and return new content each time. Return an element such as a `UserControl`, not a `Window`. |
| `GetWindowOptions` | Returns the size of the window when it opens and its minimum size. If you omit it or return `null`, the app's default size is used. |

- If `CreateContent` returns `null`, the plugin is stopped.
- Every value of `PluginWindowOptions` must be greater than 0, and the size must be at least the minimum size. Otherwise, `ArgumentOutOfRangeException` occurs and the plugin is stopped.
- If the content is `IDisposable`, `Dispose` is called after the window is closed. Unsubscribe from events and stop timers there.
- `CreateContent`, `GetWindowOptions`, and `Dispose` are called on the UI thread. If they throw an exception, the plugin is stopped.

## How the window behaves

- The app makes the window frame and the top bar (the icon, the plugin's name, minimize, maximize, and close). The content is everything below the top bar, with no padding and no scrolling.
- Each plugin has one window. If you click the row while the window is open, the window is brought to the front (`CreateContent` is not called).
- The window appears near the top left of the main window. Its size and position are not saved. Whether it was open is not saved either, so it does not open at the next startup.
- The window is not always on top, and it appears on the taskbar.
- When the main window is closed, the plugin's window also closes.
- When the app stops a plugin, it closes the window and calls `Dispose` on the content, and then cancels `Stopping` and calls `Shutdown`.

## Appearance

- You can design the content freely. The window background is "Window Color" under "Theme" in Settings.
- The window does not set a text color or font. Give the text in the content the app's color (such as `Brush.TextPrimary`) and font (`Font.ProjectDefault`).
- The content's `DataContext` inherits a value from the app. If you use bindings, set it yourself.

### The app's appearance keys

You can use the app's appearance through the keys in `PluginResourceKeys`. The names of the keys listed here do not change. Keys not listed here belong to the app and can change without notice.

```xml
<TextBlock Text="Hello"
           Foreground="{DynamicResource Brush.TextPrimary}"
           FontFamily="{DynamicResource Font.ProjectDefault}" />
<Button Content="OK" Style="{StaticResource Button.Text}" />
```

- A brush referenced with `DynamicResource` follows changes to the theme colors. A brush referenced with `StaticResource` keeps the color from before the change.
- In code, use `SetResourceReference` (as in the example in "How to make it" above).

| Group (under `PluginResourceKeys`) | Keys |
|---|---|
| Colors (`Brushes`) | `Brush.WindowSurface`, `Brush.PanelBackground`, `Brush.Border`, `Brush.BorderSoft`, `Brush.ControlBackground`, `Brush.ControlActiveBackground`, `Brush.InputBackground`, `Brush.InputHoverBackground`, `Brush.TextPrimary`, `Brush.TextSecondary`, `Brush.TextMuted`, `Brush.Success` |
| Text (`Text`) | `Font.ProjectDefault`, `Text.WidgetWindow`, `Text.WidgetWindowSubtitle`, `Effect.WidgetWindowTextShadow` |
| Buttons and links (`Buttons`) | `Button.Text`, `Button.Icon`, `Hyperlink.Base`, `Hyperlink.Inline` |
| Inputs (`Inputs`) | `Input.PathBox`, `Input.MultilineTextBox`, `Input.SearchBox`, `Input.SelectBox`, `Input.SettingsComboBox`, `Input.SettingsComboBoxItem`, `Input.Switch`, `Input.ValueSlider` |
| Frames (`Panels`) | `Surface.RoundedOutline`, `Surface.RoundedButtonOutline`, `Panel.Root`, `Panel.SettingSection`, `Panel.SettingField`, `Panel.SettingFieldLast` |
| Scrolling (`Scrolling`) | `ScrollViewer.WidgetList`, `ScrollBar.WidgetList`, `ScrollViewer.WidgetWindow`, `ScrollBar.WidgetWindow` |
| Tooltips (`ToolTips`) | `ToolTip.Info`, `ToolTip.WidgetWindowInfo`, `ToolTipOwner.Default`, `ToolTipOwner.KeepsOnClick` |
| Sizes (`Sizes`) | `Radius.Default`, `Radius.Button`, `Padding.Card` |
| Icons (`Icons`) | The drawing styles `Icon.Stroke` (line) and `Icon.Fill` (fill), and the shapes `Icon.Search`, `Icon.Add`, `Icon.ChevronDown`, `Icon.ChevronRight`, `Icon.Copy`, `Icon.Settings`, `Icon.Filter`, `Icon.Sort`, `Icon.EyeOff`, `Icon.MoreVertical`, `Icon.Help`, `Icon.Wrench`, `Icon.Sliders` |

- You can see all the keys side by side in the window of [SampleWindow](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleWindow).
- `Text.WidgetWindow`, `Text.WidgetWindowSubtitle`, and `Effect.WidgetWindowTextShadow` are white text and a shadow for the dark background of widgets.
- `ToolTip.WidgetWindowInfo` and `ScrollViewer.WidgetWindow` read the colors of the widget window, so use them only inside widgets ([Plugin Development (Widget)](Plugin-Development-(Widget))).
- For an icon, set a drawing style key as the `Style` of a `Path` and a shape key as its `Data` (`Style="{StaticResource Icon.Stroke}" Data="{DynamicResource Icon.Search}"`).

### Tooltips

- To use the same tooltips as the app, set `ToolTipOwner.Default` (for an element that does something when clicked) or `ToolTipOwner.KeepsOnClick` (for an element that does nothing when clicked) as the `Style` of the element that has the tooltip, and put a tooltip with `ToolTip.Info` in its `ToolTip`.
- An element can have only one `Style`, so this cannot be used on an element that already has a style such as `Button.Text`.

### What is not available

- The app's row reordering and its text selection and copying across rows rely on the app's types, so plugins cannot use them.
- There is no window that asks the user to choose "Yes" or "No" (for message windows, see "Messages" in [Plugin Development](Plugin-Development)).

## Samples

| Sample | What it shows |
|---|---|
| [SampleWindow](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleWindow) | The smallest window and a list of the app's appearance keys |
| [SampleLocalization](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleLocalization) | XAML content, text in four languages, and unsubscribing from the language event in `Dispose` |
| [SampleGameData](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleGameData) | Content built in code, and stopping a timer in `Dispose` |
| [SampleDependency](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleDependency) | Content that uses the resources of a dependency DLL |
