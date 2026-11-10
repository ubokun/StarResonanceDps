![Plugins](images/en/Plugins1.png)

This tab lets you add and remove plugins that add features to the app.

## What it shows

- "Installed plugins": The list of installed plugins, in the order they were installed.
- First line of a row: The icon, name, and version.
- Second line of a row: The type, the features it adds ("Widget extension" adds widgets and "Notification extension" adds notification items), and the author.
- Depending on the state, a label appears on the right of the row.

| Label | State |
|---|---|
| Error | The plugin is not running. Hover over the label to see the reason. |
| Deleted on restart | The plugin is waiting to be deleted. It is removed when you restart the app. |
| Updated on restart | The plugin is waiting to be updated. It becomes the new version when you restart the app. |

- The bundled [ただのモジュールツール](ただのモジュールツール-(English)) is installed from the start.

## Controls

- Click a row to open the plugin's window (only for plugins that have a window).
- "Add plugin": When you choose a plugin DLL, it is placed in the `Plugins` folder of the app and becomes available right away.
- If you choose the DLL of a newer version of the same plugin with "Add plugin", the plugin is updated when you restart the app. Its settings are kept.
- Trash button: Deletes the plugin. If you click "Yes" in the confirmation window, the plugin is deleted when you restart the app. The plugin's settings are also deleted.

## Notes

- Plugin widgets come after the cards in "[Widgets](Widgets)", and plugin notification items come after the items in "[Notifications](Notifications)".
- A plugin that causes an error is stopped until the app is restarted. The app keeps running. However, some errors close the app. In that case, the plugin is not loaded until you reinstall it (the "Plugin Error" window appears at every startup).
- Plugins run inside the app and can do anything the app can do. Install only plugins from distributors you trust.
- If a plugin cannot be added, the "Plugin Add Error" window appears. Possible reasons include that it is not a plugin for this app, that it does not match the app version, or that required files are missing.
