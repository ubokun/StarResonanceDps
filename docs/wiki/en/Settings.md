![Settings](images/en/Settings1.png)

These are the settings for the whole app. Open them with "Settings" at the bottom left of the main window.

- Click an item in the list on the left to scroll to that section.
- Changes appear on the screen immediately (the Startup and Aggregation settings take effect after you save).
- "Save" confirms the changes and closes the window. If you close the window without saving, a confirmation window appears, and "Yes" reverts the changes.
- If you click "Reset" and then "Yes" in the confirmation window, all items in this window return to their initial values. The change is confirmed when you click "Save".

## General

- Network Adapter: Selects the network adapter used to read the game's network traffic. "Auto" selects the adapter that Windows uses to connect to the internet (default: "Auto").
- Game Executable: Selects the game to read the traffic of, by the game's distributor. "Auto" reads the traffic of any supported game (default: "Auto").
- Custom Executable Name: Appears when "Custom" is selected in Game Executable. Enter the executable name of the game to read (default: empty).

## Startup

- Launch app when Windows starts: Starts the app when you sign in to Windows (default: off).
- Check for updates at app startup: Checks whether a new version is available when the app starts (default: on).
- Launch hotkey helper at app startup: Starts the helper program that receives [Hotkeys](Hotkeys). A User Account Control prompt appears when it starts. If you turn this off, hotkeys cannot be used (default: on).
- Launch VOICEVOX at app startup: When the app starts, starts VOICEVOX if it is not running (default: off).
- Install folder: Appears when "Launch VOICEVOX at app startup" is on. Select the VOICEVOX folder. If VOICEVOX is running, its location is filled in automatically (default: empty).

![Display settings](images/en/Settings2.png)

## Display

- Language: The app's display language. "Follow system" uses Japanese, Korean, or Chinese if that is the Windows display language, and English otherwise (default: "Follow system").
- Number: How large numbers are shortened. "KMBT (Western)" uses K, M, B, and T, and "Wan (East Asian)" uses 万, 億, and 兆 (default: "KMBT (Western)").
- Player name: Whether player names are shown. Hidden names appear as `*****`. "Hide others" shows only your own name (default: "Show").
- Internal ID: Adds the in-game number after names. You can choose all, or one of skills, buffs, entities, and maps (default: "Hide").
- Unnamed skills and buffs: Chooses whether to leave blank or show with internal names the names of skills that have no display name in the game (including damage and healing from buffs) and the names of buffs in the Timeline. It does not apply to buff names in the Buff List and other widgets. Internal names are in Chinese only (default: "Show internal name").

## Widgets

- Widget Windows: Chooses whether widget windows are always on top or only pinned windows are on top (default: "Always on Top").
- Widget Status: Whether the status circle is shown on the left of the title of widget windows. "Show Only When Not Normal" shows it only when it is yellow or red (default: "Show").

![Aggregation settings](images/en/Settings3.png)

## Aggregation

- Stop Aggregation in Non-Combat Areas: Does not aggregate in Asterleeds, Homestead Courtyard, and Homestead House (default: on).
- Auto Pause: Stops the elapsed time this many seconds after combat stops. It continues counting in the next battle (default: 10s).
- Auto Reset by Progress: Splits the aggregation when a dungeon starts, when you enter a boss room, and on a wipe (default: on).
- Hold Results Until Next Event: Even when the aggregation is split, keeps showing the previous result until the next battle starts (default: on).
- Auto-exit History View: If something happens in the current battle while a history entry is open, returns to the current battle (default: on).
- Max History Entries: The number of history entries to save. When it is exceeded, the oldest are deleted first (default: 99 entries).
- Benchmark Duration: The length of time aggregated by "Benchmark". Enter 30 to 1200 seconds (default: 180s).
- Measure Only the First Enemy Attacked: During a benchmark, aggregates only damage to the first enemy you damage (default: on).

![Hotkeys and theme](images/en/Settings4.png)

## Hotkeys

The list of keys and how to set them are described in [Hotkeys](Hotkeys). This section appears only while the hotkey helper program is running.

## Theme

- Window Color: The color of windows such as the main window and settings. Click one of the colors shown, or choose one with "Select color" (default: white).

## Notes

- The color and opacity of widget windows are in each widget's settings ([Common Widget Settings](Common-Widget-Settings)).
- Resetting does not revert the "[Notifications](Notifications)" settings or the settings of each widget.
