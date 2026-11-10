# Don't Check My DPS

[日本語](https://github.com/kokoukohiro/StarResonanceDps) | [한국어](README.ko.md) | [简体中文](README.zh-CN.md) | [English](README.en.md)

An unofficial tool that shows combat information (such as DPS) for Blue Protocol: Star Resonance (Windows).

![DPS Meter](wiki/images/en/DpsMeter1.png)

It shows combat in real time and lets you look back on past battles. It also supports notifications for in-combat events and extensions.

It does not modify the game client or send combat data anywhere. Do not use the displayed numbers to defame other players or otherwise harm the community.

## Requirements

- Windows (64-bit)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Npcap](https://npcap.com/)

## Installation

1. Download `StarResonanceDps-<version>.zip` from [Releases](https://github.com/kokoukohiro/StarResonanceDps/releases).
2. Extract the zip to a folder you can write to.
3. Start `StarResonanceDps.exe` in the folder.

- Settings and combat history are saved in this folder.
- The first time you start the app, a Windows warning (SmartScreen) may appear. Clicking "More info" lets you select "Run anyway".
- A User Account Control prompt appears when the helper program for hotkeys starts. If you do not use hotkeys, turn off "Launch hotkey helper at app startup" in the "Startup" section of Settings.
- What to do after starting the app is described in [Initial Setup](https://github.com/kokoukohiro/StarResonanceDps/wiki/Initial-Setup).

## Updates

You can update to a new version from within the app. When a new version is available, a confirmation window appears at startup. You can also check by clicking "Check for updates" in "Help". For details, see [Installation and Updates](https://github.com/kokoukohiro/StarResonanceDps/wiki/Installation-and-Updates).

## Usage

The screens and settings are described in the [Wiki](https://github.com/kokoukohiro/StarResonanceDps/wiki/Don't-Check-My-DPS-Wiki).

## Plugins

You can add features with plugins. Plugins run inside the app and can do the same things as the app. Install only plugins from sources you trust.

For how to make plugins, see [Plugin Development](https://github.com/kokoukohiro/StarResonanceDps/wiki/Plugin-Development). The SDK package (`.nupkg`) is available in the releases, alongside the app zip.

## Bug reports and requests

You can post to GitHub Issues from "Feedback" in the app. Posting requires a GitHub account.

## Network connections

Apart from plugins you install, the app connects to external servers only in the following cases. It does not send combat data.

- Checking for and downloading updates (GitHub)
- Checking in "Feedback" whether items have already been reported (GitHub)
- Showing avatar and name card photos in "Player Info" (photo URLs received from the game)

## License

- The app itself is licensed under the GNU GPL v3 (with additional permissions), and the SDK, analyzers, samples, and documentation are licensed under the MIT License. For details, see [LICENSING.md](../LICENSING.md).
- The licenses of the libraries used are in [THIRD-PARTY-NOTICES.txt](../THIRD-PARTY-NOTICES.txt).
- This tool is unofficial and is not affiliated with the game's developers or operators. The rights to the game's name, data, and images belong to their respective owners.
