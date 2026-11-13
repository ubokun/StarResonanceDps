This page lists the causes and fixes for problems by symptom. If a problem is not fixed, report it from "[Feedback](Feedback)".

## The app does not start

### A window in English appears and the app does not start

A window in English such as "You must install .NET to run this application." appears when the .NET 10 Desktop Runtime is not installed. Install it from "Requirements" in [Installation and Updates](Installation-and-Updates).

### The "Install Npcap" window appears and the app closes

This window appears when Npcap is not installed or its version cannot be detected. The app closes whichever button you click. Install Npcap from the Npcap website that opens when you click "Yes", and then start the app again ([Initial Setup](Initial-Setup)).

### The "Startup error" window appears and the app closes

- "Could not start the app.": The app cannot write to `Data` in the app folder. Check the write permissions of the folder shown in the window and your security software settings, or move the whole app folder to a location you can write to.
- "Initialization failed.": Some of the app's data files may be missing or damaged. Download the zip from Releases and overwrite the app folder with its contents. Your settings and combat history are kept.

### The "Error" window appears and the app closes

An unexpected error occurred. Start the app again. If it keeps appearing, report it from "[Feedback](Feedback)".

### No new window appears when you start the app

Only one instance of the app can run. If the app is already running, a new instance does not start, and the main window of the running app comes to the front.

## A window appears at startup

### User Account Control appears

It appears when the hotkey helper program starts. If you select "Yes", you can use hotkeys. If you do not use hotkeys, turn off "Launch hotkey helper at app startup" in the "Startup" section of Settings, and it will no longer appear.

### The "Hotkey error" window appears

- "Hotkeys have been disabled.": The helper program could not start or stopped while running. Restart the app and select "Yes" in User Account Control.
- "The following hotkeys could not be set.": Another app is using the keys shown in the window. Assign different keys in the "Hotkeys" section of Settings.

### The "Npcap update" window appears

This window appears at every startup when your Npcap version is older than 1.86. Older versions have a bug that can make your PC freeze. You can keep using the app, but update Npcap to 1.86 or later.

### The "Settings load error" window appears

A settings file was damaged or could not be read, so the app started with the default values. The file shown in the window is overwritten the next time the settings are saved. Redo your settings as needed.

### The "Startup settings error" window appears

This window appears when "Launch app when Windows starts" in the "Startup" section of Settings is on and the app could not be added to Windows startup. Check whether security software is blocking adding the app to startup.

### The "Plugin Error" window appears

Some plugins could not be loaded. The details in the window show the reason and what to do for each plugin ([Plugins](Plugins)).

## Nothing appears

### Nothing appears in any widget

The app is not reading the game's network traffic.

- Set "Game Executable" in Settings to "Auto".
- In "Network Adapter", select the adapter that the game traffic goes through. If you switched between wired and wireless after starting the app, restart the app.
- To check whether the traffic is being read, see [Initial Setup](Initial-Setup).

### No numbers appear, or only you appear

- By default, aggregation is stopped in Asterleeds, Homestead Courtyard, and Homestead House ("Aggregation" in [Settings](Settings)).
- If "[Benchmarking]" appears in the meter footer, a benchmark is running. During a benchmark, only your own data is aggregated. Click "Stop Benchmark" to return to normal ([Aggregation](Aggregation)).
- Check whether "Filter settings" in the "Display" section of the meter's settings is limiting the players shown.

### Values do not change

- If the upper left of "[Aggregation](Aggregation)" shows "Viewing History", a history entry is open. Click the open history row again to return to the current combat.
- If "[Benchmark Complete]" appears in the meter footer, the benchmark time has ended. Click "Stop Benchmark" to return to normal.

## Information is missing

### The status circle turns yellow or red

Hover over the circle or the title to see the reason ([Common Widget Settings](Common-Widget-Settings)). If you started the app after logging in to the game, change maps once.

### A name appears as "UID:" followed by a number

This is a player whose name has not arrived yet. The name appears when you approach that player again or click them in the game.

### A name appears as a class name

Helper NPCs are shown with their class name instead of a name.

### The class appears as "Unknown" or "Class I"

"Unknown" means the class is not known yet. It becomes known when that player comes near you. "Class I" means the player has not set any talents.

### You appear as "Class I" and "Not Enabled" in Homestead Courtyard

In Homestead Courtyard, the game does not apply any buffs other than cuisine and potion buffs, so your class and Deep-Slumber Psychoscope cannot be determined. They return to normal when you go somewhere else.

## Windows cannot be seen or clicked

### A widget has no header or cannot be moved

The widget is pinned. By default, the header is hidden while a widget is pinned. If you unpin it on its card in "[Widgets](Widgets)", the header appears and you can move the widget.

### A widget cannot be clicked

Click-through is on. Turn it off on its card in "[Widgets](Widgets)".

### Widgets are hidden behind the game

When "Widget Windows" in Settings is set to "On Top Only While Pinned", windows that are not pinned are hidden behind the game. Set it back to "Always on Top" or pin the window.

### A widget went off the screen

If you stop the widget on its card in "[Widgets](Widgets)" and start it again, it appears on the screen.

### The widget display suddenly changed

Check whether you pressed a hotkey. The table shows what happens with the default keys and how to undo it ([Hotkeys](Hotkeys)).

| What happened | Key | How to undo |
|---|---|---|
| All running widgets closed | Ctrl+F8 | Start them on their cards in "Widgets" |
| Windows cannot be moved, or headers disappeared | Ctrl+F7 | Press the key again |
| Windows cannot be clicked | Ctrl+F6 | Press the key again |
| Windows are hidden behind the game | Ctrl+F5 | Press the key again |
| Names became `*****` | Ctrl+F11 | Press the key again |
| The meter became empty, and "[Benchmarking]" appeared in the footer | Ctrl+F10 | Press the key again |
| The meter became empty | Ctrl+F9 | The previous results can be opened from the history in "[Aggregation](Aggregation)" |

### Closing the main window also closed the widgets

Closing the main window exits the app. To keep only the widgets on screen, minimize the main window.

## Names or values differ

### Skill names appear in Chinese

Skills that have no display name in the game are shown with their internal name (in Chinese). You can also leave them blank with "Unnamed skills and buffs" in Settings.

### Values differ from the in-game meter

- Per-second values are the total divided by the elapsed combat time. Because the app counts the elapsed time differently from the game, per-second values may differ slightly.
- Healing counts the HP that was actually restored. Healing potions, cuisine, and natural regeneration are also counted ([HPS Meter](HPS-Meter)).

## Hotkeys do not work

- Hotkeys work only when the game or this app is in the foreground.
- If you selected "No" in User Account Control, hotkeys are unavailable while the app keeps running. Restart the app and select "Yes".
- Hotkeys are unavailable when "Launch hotkey helper at app startup" is turned off in the "Startup" section of Settings.
- Keys that other apps are using cannot be assigned. Assign a different key.
- If hotkeys do not work even though the game is in the foreground, set "Game Executable" in Settings to "Auto". This setting is used to tell whether the game is in the foreground.
- For details, see [Hotkeys](Hotkeys).

## Notifications do not arrive or are not read aloud

- Some notification items notify you only while a widget is running ("Notes" in [Notifications](Notifications)).
- If Windows notifications do not arrive during games, go to "Notifications" in Windows Settings and, under "Turn on do not disturb automatically", turn off "When playing a game" and "When using an app in full-screen mode".
- When "Read-aloud method" is VOICEVOX, keep VOICEVOX running. If it is not running, the "VOICEVOX connection error" window appears, for example at startup. If something could not be read aloud during the game, no window appears, and a warning line is left in "[Logs](Logs)".
- If the "VOICEVOX launch error" window appears, check "Install folder" in the "Startup" section of Settings.
- If the "Windows voice" window appears, go to Windows Settings and add a voice for the language shown in the window.

## The app cannot be updated

- The details in the "Update Error" window show how to fix the problem.
- If the update stops at "Waiting for files in use to be released…", close File Explorer or any other program that has the app folder open.
- If the update does not succeed, download the zip from Releases and overwrite the app folder with its contents ([Installation and Updates](Installation-and-Updates)).

## A plugin does not work

When a plugin's row in "[Plugins](Plugins)" shows "Error", the reason is shown in the "Plugin Error" window. If some plugins could not be loaded, this window appears every time the app starts.

## A history entry cannot be opened

"History load error" appears when the data for that history entry cannot be found or is damaged ([Aggregation](Aggregation)).

## Reporting a bug

- "Bug reports and feature requests" in "[Feedback](Feedback)" opens a submission page with the app version and other details already filled in.
- If the app does not start, report the problem from the [submission page on GitHub](https://github.com/kokoukohiro/StarResonanceDps/issues/new?template=feedback.yml). Enter your Windows and app versions in the Environment field.
- If you are asked for a log, attach `Data\CombatRuntime.log` in the app folder ([Logs](Logs)).
