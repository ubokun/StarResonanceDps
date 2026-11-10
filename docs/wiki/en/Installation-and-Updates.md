This page explains how to install Don't Check My DPS and how to update it to a new version.

## Requirements

- Windows (64-bit)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Npcap](https://npcap.com/)

## Installation

1. Download the zip from the [release notes](https://github.com/kokoukohiro/StarResonanceDps/releases).
2. Extract the zip to a folder you can write to.
3. Start `StarResonanceDps.exe` in the folder.

- Settings and combat history are saved in this folder.
- The first time you start the app, a Windows warning (SmartScreen) may appear. Clicking "More info" lets you select "Run anyway".
- A User Account Control prompt appears when the helper program for hotkeys starts. If you do not use hotkeys, turn off "Launch hotkey helper at app startup" in the "Startup" section of Settings.

## Updates

You can update to a new version from within the app.

1. When a new version is available, a confirmation window appears at startup. You can also check by clicking "Check for updates" in "Help".
2. If you click "Yes" in the confirmation window, the app closes after the download, replaces its files, and restarts.

- Settings, combat history, and plugins you added are kept (the log and the "Detected issues" list are deleted).
- Bundled plugins that you uninstalled are not restored.
- You can stop the check at startup with "Check for updates at app startup" in the "Startup" section of Settings.
- The app connects to GitHub to check for and download updates. It does not send combat data.
- If the update does not succeed, download the zip from the release notes and overwrite the app folder with its contents.
