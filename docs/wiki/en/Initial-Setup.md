This page explains what to do after installing the app before you start using it, and how to check whether the app is reading the game's network traffic. Installation is covered in [Installation and Updates](Installation-and-Updates).

## First launch

When you start the app for the first time, the main window opens on "[Widgets](Widgets)". No widgets are open yet.

The following windows may appear at startup.

| Window | When it appears | What to do |
|---|---|---|
| "Install Npcap" | When Npcap is not installed or its version cannot be detected | Click "Yes" to open the Npcap website. The app closes whichever button you click, so install Npcap and then start the app again. |
| User Account Control | When the hotkey helper program starts | If you select "Yes", you can use [Hotkeys](Hotkeys). If you select "No", the "Hotkey error" window appears and hotkeys are unavailable while the app keeps running. |
| "Npcap update" | When your Npcap version is older than 1.86 (appears at every startup) | Click "Yes" to open the Npcap website. The app keeps working whichever button you click, but update Npcap to 1.86 or later. |
| "Update" | When a new version of the app is available | If you click "Yes", the app closes, updates, and restarts ([Installation and Updates](Installation-and-Updates)). |

## Npcap

- The app reads the game's network traffic that reaches your PC and displays what it reads. It uses Npcap to read the traffic.
- Install version 1.86 or later. Older versions have a bug that can make your PC freeze while traffic is being read.
- If you install Npcap while the game is running, the app may not be able to read the traffic until you restart the game.

![Settings for reading traffic](images/en/Settings1.png)

## Settings for reading traffic

You choose these in the "General" section of [Settings](Settings). Normally, the traffic can be read with the default "Auto".

- Network Adapter: "Auto" selects the adapter that Windows uses to connect to the internet. If no numbers appear, for example when you have both wired and wireless connections, select the adapter that the game traffic goes through. The names in the list are the product names of the adapters.
- Game Executable: "Auto" reads the game from any of the distributors in the list. If the game runs from an executable whose name is not in the list, select "Custom" and enter the executable name in "Custom Executable Name". You can include or omit `.exe`. The name is case-sensitive.
- When you change these settings, the app immediately starts reading the traffic again. After that, change maps once.
- The "Auto" adapter is determined when the app starts. If you switch between wired and wireless after starting the app, restart the app.

## Launch order

- Start the app first, and then log in to the game. This lets the app receive, from the beginning, the information that arrives when you log in and change maps.
- If you started the app after logging in to the game, change maps once. Until then, information such as your own buffs and equipment is missing, and the circle in the title of widget windows turns red ([Common Widget Settings](Common-Widget-Settings)).
- Some past chat messages cannot be received until you log in again ([Chat](Chat)).

## Checking whether traffic is being read

- After you log in to the game (or after you change maps, if you started the app after logging in), the traffic is being read if your own row appears in the [Player List](Player-List) and the name of your current map appears on the left of its footer.
- When you fight, numbers appear in the [DPS Meter](DPS-Meter). By default, aggregation is stopped in Asterleeds, Homestead Courtyard, and Homestead House.
- The circle in the title of widget windows turns yellow or red only when missing information is found. It stays green even when nothing is being read, so you cannot use it for this check.
- If the traffic cannot be read, see "Nothing appears" in [Troubleshooting](Troubleshooting).

## Things to know when starting out

- Start widgets from their cards in "[Widgets](Widgets)". If you mark the ones you use often as favorites, you can start them all at once.
- So that widgets do not interfere with playing the game, you can pin them or make them click-through ([Common Widget Settings](Common-Widget-Settings)).
- With [Hotkeys](Hotkeys), you can use keys to start or pin widgets, run benchmarks, and more.
- When you show your screen to others, you can hide player names with "Player name" in Settings or with Ctrl+F11.
- Closing the main window also closes all widgets and exits the app. To keep only the widgets on screen, minimize the main window.

## Notes

- You do not need to run the app as administrator. Only the hotkey helper program, which needs administrator rights, is started through User Account Control.
- Only one instance of the app can run. If you start the app while it is already running, the main window of the running app comes to the front.
