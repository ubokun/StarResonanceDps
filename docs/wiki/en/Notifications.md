![Notifications](images/en/Notifications1.png)

This tab notifies you of events in the game with Windows notifications or by reading them aloud. For each item, you decide whether to notify, the text, and how to notify.

## What it shows

- Center: The notification items. Only items with a check on the left are notified (all are off by default).
- Top right: The text of the selected item and "Preview" (the text with example names filled in).
- Bottom right: The settings of the selected item and how to notify.

## Controls

- Click an item to show its text and settings on the right.
- Text: Write the notification text. If you choose an item in the drop-down list and click "Insert", text such as `{PlayerName}` is inserted at the cursor position and is replaced with a name or other value when the notification is sent. If you leave it empty, the default text is used.
- Changes are saved immediately.

![When Low HP is selected](images/en/Notifications2.png)

### Items

| Item | When it notifies | Default text |
|---|---|---|
| Entry confirmation | When matchmaking succeeds, and when a party member starts entering content (when the game's entry confirmation screen opens). | Entering {Content} |
| Low HP | When the HP of a player in "Target players" stays at or below the "Low HP threshold" for the number of seconds in "Notify when". If HP recovers or the player is defeated during that time, it does not notify. | {PlayerName}'s HP is low |
| Player appeared | When a player whose name or UID matches a target appears nearby. | {PlayerName} appeared |
| Entity appeared | When an enemy whose name or ID matches a target appears nearby. | {EntityName} appeared |
| Targeted Player | When an enemy in "Target entity" keeps targeting a player in "Target players" for the number of seconds in "Notify when". | {PlayerName} became the target of {EntityName}'s attacks |
| Buff & Debuff expired | When a buff or debuff shown in a running [Buff & Debuff Card](Buff-&-Debuff-Card) or [Buff & Debuff Group](Buff-&-Debuff-Group) disappears because its time runs out. | {BuffName} on {TargetName} has expired |
| Potion/cuisine expired | When the remaining time of a potion or cuisine buff drops below the time in "Notify when". | {TargetName}'s {BuffName} effect is about to expire |
| Telegraphed skill | When a boss skill that the game announces or warns about is coming. | {SkillName} incoming |
| Entity chanting | When an enemy with an HP bar starts chanting a skill whose name or ID matches a target. | {EntityName} is chanting |
| Private Chat | When a private chat arrives from another player. It does not notify while you have the chat with that player open in the game. | Private chat from {PlayerName} |
| World Chat | When a World Chat message contains all the match words. | Recruitment from {PlayerName} |

### Settings for each item

- Low HP
  - Target players: "All", "Party members only", "Non-party members", or "Self only" (default: "All").
  - Low HP threshold: "HP 45%", "HP 35%", "HP 25%", "HP 15%", or "HP 5%" (default: "HP 15%").
  - Notify when: "Immediately", "Low HP for 1s or more", "Low HP for 2s or more", or "Low HP for 3s or more" (default: "Low HP for 3s or more").
- Player appeared
  - Target player name and Target player UID: The players to notify about (default: empty). You can enter multiple values separated by "|". If you enter a UID, it takes priority over the name.
- Entity appeared
  - Target entity name and Target entity ID: The enemies to notify about (default: empty). You can enter multiple values separated by "|". If you enter an ID, it takes priority over the name. IDs can be shown with "Internal ID" in Settings, and you can copy IDs and names from the [Timeline](Timeline).
- Targeted Player
  - Target players: "All", "Party members", "Non-Guardian classes", or "Non-Guardian party members" (default: "Non-Guardian classes").
  - Target entity: "Bosses only", "Bosses and elites", or "All" (default: "Bosses only").
  - Notify when: "Immediately", "Targeted for 1s or more", "Targeted for 2s or more", or "Targeted for 3s or more" (default: "Targeted for 3s or more").
- Buff & Debuff expired
  - Choose the buffs and debuffs to notify about in "Notification Targets" in the settings of the card and the group. You can also switch them by right-clicking a row in a card or group.
- Potion/cuisine expired
  - Notify when: "1 min or less left", "2 min or less left", "5 min or less left", or "10 min or less left" (default: "2 min or less left").
- Telegraphed skill
  - Notify when: "On activation", "1s before activation", "2s before activation", or "3s before activation" (default: "On activation"). For skills with an announcement, the timing is based on when the announcement ends and the skill activates. For skills with only a warning, it notifies when the skill starts, regardless of this setting.
- Entity chanting
  - Target skill name and Target skill ID: The skills to notify about (default: empty). You can enter multiple values separated by "|". If you enter an ID, it takes priority over the name.
- World Chat
  - Match words: The words that a message must contain (default: empty). If you enter multiple words separated by spaces, it notifies only for messages that contain all of them.

### How to notify (the same for all items)

- Notification method: "Windows notification" or "Read aloud" (default: "Windows notification").
- Notification volume: The volume of the Windows notification sound and the read-aloud voice (default: 100%). At 0%, Windows notifications are shown without sound, and nothing is read aloud.
- Read-aloud method: "Windows voice" or "VOICEVOX" (default: "Windows voice"). It appears when the notification method is "Read aloud".
- Speaker and Style: Choose the VOICEVOX voice. They appear when the read-aloud method is "VOICEVOX" (Style appears only when the speaker has two or more styles).

## Notes

- "Buff & Debuff expired", and "Potion/cuisine expired" for other players, notify only while the widget is running. The other items notify even when no widget is open.
- Player appeared, Entity appeared, Entity chanting, and World Chat do not notify even when turned on unless you enter names, IDs, or match words.
- "Player name" in Settings applies to names (`*****` when it is set to "Hide").
- During games, Windows notifications may not arrive because of the Windows setting "Turn on do not disturb automatically". If they do not arrive, turn off the options for playing a game and for full-screen apps under "Notifications" in Windows Settings.
- Read-aloud messages are read one at a time in the order they arrive, without overlapping.
- The Windows voice reads in the app's display language. It cannot read if Windows does not have a voice for that language.
- VOICEVOX is not included in the app. To use it, install [VOICEVOX](https://voicevox.hiroshiba.jp/) and keep it running (you can start it together with the app in "Startup" in [Settings](Settings)). It supports Japanese only.
- Use VOICEVOX voices according to each speaker's terms of use. You can check them with "(speaker name) terms of use" in the speaker row.
