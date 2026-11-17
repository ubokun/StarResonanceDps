![Buff Uptime](images/en/BuffUptime1.png)

This widget lists the buffs that were on one player, one per row. Bars show when each buff was on along the battle's elapsed time, and a number shows the share of time it was on (uptime).

## How to open

- On the "Buff Uptime" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The default target is yourself.
- You can also open it by right-clicking a row in the [DPS Meter](DPS-Meter), [HPS Meter](HPS-Meter), [Threat Meter](Threat-Meter), or [Break Meter](Break-Meter) and choosing "Buff Uptime".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

- Title: "Buff Uptime - target name".
- Row: From the left, the buff icon, the name, the bar, and the uptime. Buffs whose icon file is missing show a question mark.
- Bar: Filled at the times the buff was on. Its color is the color of the person's class.
- Line through the middle of the bar: The track of the bar. It breaks for the time when another player was not around you.
- Uptime: The share of time the buff was on, in the form "12.34%". Time when another player was not around you is not counted.
- Horizontal axis: The battle's elapsed time (in the form "2:55"). The length that fits in the window is decided by the width of the bar column and "Width per second" in the display settings.
- Vertical line: While a time that has not come yet is visible in the window, a vertical line appears at the current time. To the right of the line is time that has not come yet.
- Buffs shown: The same buffs as in the [Buff List](Buff-List). Debuffs are not shown. The same buff is one row even if its casters are different.
- Order: Buffs turned on in the Priority List come first (in the order of the list), and the rest follow in the order they were first applied in that battle.

## Controls

- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.
- When the battle becomes longer than the length that fits in the window, a horizontal scroll bar appears at the bottom of the window. Move the thumb to see earlier times. While it is at the right end, the view follows the latest time, and it follows again when you move it back to the right end.
- When you change the width of the window, the bars do not stretch or shrink, and the length of time that fits in the window changes.
- Right-click a row to choose one of the following items. It does not open on cuisine and potion rows.
  - "Prioritize This Buff": Adds the buff to the Priority List. While it is prioritized, this changes to "Stop Prioritizing This Buff".
  - "Hide This Buff": Adds the buff to the Hide List.
- Changes made with the right-click menu are saved immediately.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Show permanent buffs: Shows the rows of buffs whose duration is permanent (default: off).
- Show cuisine/potions: Shows cuisine and potion buffs combined into one cuisine row and one potion row (default: off).
- Width per second: Choose the width of one second on the horizontal axis, from 2 to 60px. The smaller it is, the longer the time that fits in the window (default: 10px).

### Priority List

The buffs that are shown first. Turn them on and off with the switches, and reorder the rows by dragging. The defaults are "Lower CD", "Mechanical Power", "Highland Blood", "Fiery Battle Will", "Functional Amp", and "Inspiring Shield", all turned on. You can add one with "Prioritize This Buff" on the right-click menu of a row. Rows you added are removed when you turn them off and save.

### Hide List

The buffs that are not shown. Add one with "Hide This Buff" on the right-click menu of a row. If you turn its switch off and save, it is removed from the list and is shown again.

### Graph Colors

Choose the bar color for each class (including transformations and unknown). The defaults are the same colors as the class colors of the [DPS Meter](DPS-Meter).

## Notes

- The window is empty until the battle starts.
- The time is the battle's elapsed time and uses the same clock as the [DPS Meter](DPS-Meter). Time while it was stopped by auto pause is not counted. Buffs that were on before the battle started are treated as on from 0:00.
- Other players' buffs are recorded only while they are around you. Even for party members, the time when they were not around you is not recorded.
- All players in the battle are recorded. Even for people whose window was not open, opening it later shows the battle from the beginning.
- Rows for buffs that were never on during the counted time are not shown.
- While a benchmark is running, only your own data is recorded. Windows for other people are empty.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is recorded in Asterleeds, Homestead Courtyard, and Homestead House.
- While a history entry is open, the records of the opened battle are shown ("[Aggregation](Aggregation)"). Battles recorded in a version without Buff Uptime are empty.
