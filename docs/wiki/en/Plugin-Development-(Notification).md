This page explains how to add items to "[Notifications](Notifications)" with a plugin and send notifications at times the plugin decides. What every plugin needs is described in [Plugin Development](Plugin-Development).

| What the app provides | What the plugin provides |
|---|---|
| The rows and checkboxes in the list in "Notifications", the text field and the preview, Notification method, Notification volume, Read-aloud method, Speaker, and saving the values | The item definitions (the name, the default text, the insertable items, and the item settings) and the timing of notifications (`Notify`) |

## How to make it

Implement `IPluginNotificationProvider` in the entry class. A plugin without an `IPluginWindowProvider` window does not need `PluginGenre`.

The following example sends a notification when you change maps.

```csharp
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(MyPlugin.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "私のプラグイン")]
[assembly: PluginAuthor("YourName")]
[assembly: PluginVersion("1.0.0")]

namespace MyPlugin;

public sealed class Plugin : IStarResonancePlugin, IPluginNotificationProvider
{
    private const string MapEnteredId = "map-entered";

    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        context.GameData.MapEntered += OnMapEntered;
    }

    public IReadOnlyList<PluginNotificationDefinition> GetNotificationDefinitions()
    {
        return
        [
            new PluginNotificationDefinition
            {
                Id = MapEnteredId,
                Names = new Dictionary<string, string> { ["ja-JP"] = "マップ移動", ["en-US"] = "Map change" },
                DefaultTexts = new Dictionary<string, string> { ["ja-JP"] = "{MapName}に移動しました", ["en-US"] = "Moved to {MapName}" },
                Fields =
                [
                    new PluginNotificationField
                    {
                        Key = "MapName",
                        Names = new Dictionary<string, string> { ["ja-JP"] = "マップ名", ["en-US"] = "Map name" },
                        SampleValues = new Dictionary<string, string> { ["ja-JP"] = "アステルリーズ" },
                    },
                ],
            },
        ];
    }

    private void OnMapEntered(object? sender, EventArgs e)
    {
        if (_context is null || !_context.Notifications.IsEnabled(MapEnteredId))
        {
            return;
        }

        int? sceneId = _context.GameData.Session.SceneId;
        string? mapName = sceneId is int id ? _context.Names.GetSceneName(id) : null;
        _context.Notifications.Notify(MapEnteredId, new Dictionary<string, string> { ["MapName"] = mapName ?? string.Empty });
    }

    public void Shutdown()
    {
        if (_context is not null)
        {
            _context.GameData.MapEntered -= OnMapEntered;
        }
    }
}
```

| Member | When it is called | What it does |
|---|---|---|
| `GetNotificationDefinitions` | Once after `Initialize` (after the widget definitions, if there are any) | Returns the list of items to add. They are listed in "Notifications" in the order returned. Changes made later are not reflected |

- If it returns an empty list, nothing is added to "Notifications".

### Item definition (`PluginNotificationDefinition`)

| Property | Required | Contents |
|---|---|---|
| `Id` | Required | The item ID: 1 to 128 characters of letters, digits, ".", "-", and "_". It is a key for saving, so do not change it between versions |
| `Names` | Required | The item name shown in the list in "Notifications" |
| `DefaultTexts` | Required | The text used until the user writes their own. Write insertable items as `{Key}` |
| `Fields` | Optional | The insertable items (`PluginNotificationField`) |
| `Settings` | Optional | The item settings. The types are the same as for widget settings, except that colors (`PluginColorSetting`) cannot be used ("Settings" in [Plugin Development (Widget)](Plugin-Development-(Widget))) |

| Insertable item (`PluginNotificationField`) property | Required | Contents |
|---|---|---|
| `Key` | Required | 1 to 64 letters and digits (".", "-", and "_" cannot be used). In the text, write it as `{Key}` |
| `Names` | Required | The name shown in the drop-down list of insertable items in "Notifications" |
| `SampleValues` | Required | The sample values put into the preview. An empty dictionary is fine |

- Write text for each language as a dictionary keyed by the language name (such as `ja-JP`). The text is chosen the same way as for the attributes ("Entry class and attributes" in [Plugin Development](Plugin-Development)).
- Make item `Id` values unique within the plugin, and the `Key` values of insertable items and the `Id` values of settings unique within the item. They are not case-sensitive.

## Sending notifications

Use `IPluginContext.Notifications` (`IPluginNotificationService`).

| Member | Contents |
|---|---|
| `IsEnabled(id)` | Whether the user has checked the item |
| `Notify(id, values)` | Sends a notification. `values` holds the `Key` of each insertable item and its value. It can be omitted |
| `GetSettings(id)` | The current values of the item settings (`PluginSettingValues`). They are read the same way as widget settings |
| `SettingsChanged` | An event raised on the UI thread when the item's checkbox or a setting value changes. Its value is the item `Id` |

- `IsEnabled`, `Notify`, and `GetSettings` can be called from any thread.
- The items are read after `Initialize`, so inside `Initialize` and `GetNotificationDefinitions` every `Id` throws `ArgumentException`. Subscribing to `SettingsChanged` is fine.
- Passing an `Id` that is not in the definitions or a `Key` that is not in `Fields` throws `ArgumentException`.
- `Notify` does nothing if the item is unchecked at the time of the notification. If building the values takes effort, check `IsEnabled` first.
- The text is the user's text or, if there is none, the default text in the display language. Each `{Key}` is replaced with its value, and items for which no value was passed become empty. A `{…}` that is not in the definition is left as it is. If the text is empty, no notification is sent.
- Whether the notification was sent is not returned. Even if a Windows notification cannot be shown or a voice cannot be used, no exception occurs; it is recorded in the app's log.
- The app does not thin out notifications. Every call sends a notification, and read-aloud messages and notification sounds play one at a time in order. Keep intervals and limits in the plugin so that it does not call many times in a short period.
- After the plugin is stopped, `Notify` does nothing.

### Item settings

- A value is saved right away when it is changed in "Notifications", and `SettingsChanged` arrives. Switches and drop-down lists change when changed, numbers when confirmed (Enter or leaving the field), text 0.3 seconds after typing stops, and sliders when the thumb is released.
- `SettingsChanged` arrives only when the item's checkbox or a setting value differs from before. It does not arrive when the text, Notification method, Notification volume, or voice is changed.
- It does not arrive when the app starts, so read the initial values with `IsEnabled` and `GetSettings`.

## How items appear in "Notifications"

- They are listed after the app's items, under a heading with the plugin's name. Plugins are in the order they were installed, and the items of a plugin are in the order of the definitions.
- Items are unchecked at first. A plugin cannot check an item on its own.
- The drop-down list under the text field at the top right lists the names of the insertable items, and "Insert" inserts `{Key}`. The preview uses the sample values.
- At the bottom right, the item settings are placed above Notification method.
- A Windows notification shows the app's name and icon. The plugin's name does not appear.
- The app saves the values in `Data\AppSettings.json` (separate from the settings files in the plugin's folder). They remain when the plugin is stopped, are removed when it is deleted, and remain when it is updated.
- If you change an item `Id` or the assembly name, it becomes a different item and the previous values are not carried over.

## Errors and exceptions

- A mistake in a definition (the form of an ID or key or a duplicate, the range of a number or slider, a color setting, and so on) stops the plugin, and its row in "[Plugins](Plugins)" shows "Error". The "Plugin Error" window shows "Could not be initialized." The reason is written to the log in English.
- An exception in `GetNotificationDefinitions` or `SettingsChanged` also stops the plugin.
- If an item name, a default text, or the name of an insertable item is empty, the build stops (SRDPS014).

## Samples

| Sample | What it shows |
|---|---|
| [SampleNotification](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleNotification) | A plugin without a window that has two items, which notify of map changes and new battles. Five setting types, and keeping intervals and limits in the plugin |
| [SampleAll](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleAll) | Notifications sent from buttons in a window and a widget |
