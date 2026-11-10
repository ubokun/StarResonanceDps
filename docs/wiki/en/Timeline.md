![Timeline](images/en/Timeline1.png)

This widget shows boss skill telegraphs and enemy actions (healing, damage to enemies, and applying buffs and debuffs) one per line, in order of the battle's elapsed time. Damage taken by players appears in the [Damage Taken Log](Damage-Taken-Log).

## How to open

Click the start button on the "Timeline" card in "[Widgets](Widgets)". Only one window opens.

## What it shows

- Time on the left: The battle's elapsed time (in the form "00:00:34"). It uses the same clock as the elapsed time of the [DPS Meter](DPS-Meter), and time while it was stopped by auto pause is not counted. When several lines have the same time, the time is shown only on the first of them.
- Types of lines

| Line | When it appears |
|---|---|
| Enemy is readying skill | When a telegraph for a boss skill appears on the game screen. |
| Enemy's skill (after the telegraph) | When the telegraph time ends. |
| Enemy is casting skill | When an enemy starts casting a skill that shows a warning on the combat screen. Other casts appear at the time the cast started, only when healing, damage, or a buff results from the cast. |
| Enemy's skill | When healing, damage, or a buff in the lines below results from an enemy skill. |
| Target recovered 12345 HP | When an enemy heals an enemy or a player (the HP actually gained). |
| 12345 damage to target | When an enemy deals damage to an enemy. |
| Buff name (10s) on target | When an enemy applies a buff or debuff to an enemy or a player (the number is the duration). |

- Healing, damage, and buff lines appear under the line of the skill that caused them, one per target. Healing and damage to the same target are combined into one line, and the values are added together.
- Within the same time, the order is the telegraph and cast lines (readying, after the telegraph, and casting) → damage → healing → buffs.
- In lines whose target is a player, the class icon appears before the name. Hover over it to see the class spec (such as Iaido Slash) in a tooltip.
- Colors: Enemy names, skill names, player names, buff names and seconds, and healing and damage values are shown in the Text Colors.
- Names: Player names follow "Player name" in Settings. Enemies with no name whose HP bar is visible are shown as "Enemy", and enemies that cannot be identified (such as enemies that were there before the app started) as "Unknown enemy".
- For skills and buffs that have no name, you can choose blank (shown as "something", "Attack", and so on) or the internal name (Chinese) with "Unnamed skills and buffs" in Settings (default: "Show internal name").
- Telegraph lines show the telegraph name and casting lines show the skill name, so the same skill can appear with different names.

## Controls

- Drag over text to select it across lines. Shift+click extends the selection, and clicking clears it. If you drag above or below the window or turn the mouse wheel while dragging, the view scrolls.
- When text is selected, right-click to choose "Copy". While the window is active, you can also copy with Ctrl+C.
- Copied text has a line break after each line, and each line has the time followed by the text.
- Scroll with the mouse wheel or the scroll bar on the right. While you are viewing the bottom, the view follows new lines.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Enemy healing: Choose whether enemy healing lines are shown from "Show" and "Hide" (default: "Show").

### Icon Colors

Choose the color of the class icon for each class (including transformations and unknown). The defaults are the same colors as in the Player List.

### Text Colors

The text colors of Entity name, Skill name, Player name, Effect, and HP value.

## Notes

- Only the actions of monsters are shown. Actions of players, NPCs, placed objects, and so on, and damage taken by players, are not shown.
- A buff is shown only at the moment it is applied. Extensions, stacking, and removal are not shown. Buffs that spread from buffs applied by players are not shown either.
- Enemy healing is not included in the [HPS Meter](HPS-Meter).
- When you open a history entry, the timeline of that battle is shown. The records are saved together with the battle.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is recorded in Asterleeds, Homestead Courtyard, and Homestead House.
- You can be notified of the same telegraphs as the telegraph lines with "Telegraphed skill" in [Notifications](Notifications). The notification works even when this window is not open.
