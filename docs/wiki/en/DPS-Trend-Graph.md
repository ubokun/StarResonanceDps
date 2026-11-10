![DPS Trend Graph](images/en/DpsGraph1.png)

This widget shows one player's damage per second as a line graph over the battle's elapsed time. Below it, skill icons are placed at the times the person used the skills.

## How to open

- On the "DPS Trend Graph" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The list shows the people in the [DPS Meter](DPS-Meter), following the DPS Meter's "Filter settings" (the default is yourself).
- You can also open it by right-clicking a row in the [DPS Meter](DPS-Meter) and choosing "DPS Trend Graph".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

- Title: "DPS Trend Graph - target name".
- Line: A line that connects the per-second values for each "Drawing interval" in the display settings. Its color is the color of the person's class. It starts from 0 at the beginning of the battle (0:00).
- Vertical axis: From the top, the maximum, half of it, and 0. The maximum is the maximum of the whole battle, including the time that is not visible.
- Horizontal axis: The battle's elapsed time (in the form "2:55"). The length that fits in the window is "X-axis length" in the display settings, and by default the latest 12 seconds are shown.
- Skill icons: Placed below the horizontal axis at the time they were used. The icon frame differs for Imagines, ultimate skills (and the skills derived from them), and other skills. Skills with no icon show a question mark.
- Until the target's class is known, no line is shown.

## Controls

- Turn the mouse wheel over the window to change the X-axis length. Turning it away from you makes it 2 seconds shorter, and turning it toward you makes it 2 seconds longer (4 to 120 seconds). This applies to all windows of the same widget and is also saved to the settings.
- When the battle becomes longer than the X-axis length, a horizontal scroll bar appears at the bottom of the window. Move the thumb to see earlier times. While it is at the right end, the view follows the latest time, and it follows again when you move it back to the right end.
- Hover over a skill icon to see the skill name in a tooltip. For skills with grades, the current grade (such as G1) is added after the name. Skills with no name show "Unknown".

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Drawing interval: Choose the number of seconds combined into one point of the line from 5s, 3s, 2s, and 1s. Each point is the total for that number of seconds divided by the number of seconds (default: 2s).
- X-axis length: Choose the length of time that fits in the window, from 4 to 120 seconds. It also changes with the mouse wheel over the window (default: 12s).
- Show Skill Log: Shows the row of skill icons. When it is off, the height of the icon row is also removed (default: on).

### Graph Colors

Choose the line color for each class (including transformations and unknown). The defaults are the same colors as the class colors of the [DPS Meter](DPS-Meter).

## Notes

- During a battle, the last interval that has not yet reached the drawing interval is not shown. After the battle ends (such as in the history), the last interval is also shown.
- The time is the battle's elapsed time and uses the same clock as the [DPS Meter](DPS-Meter). Time while it was stopped by auto pause is not counted. For people who joined the battle partway through, the line is 0 from 0:00 to their first point.
- Your own skill icons are recorded when the game accepts your skill use. For other players, only skill starts that arrived while they were around you are shown, and short skills may not appear.
- Skills used before the battle started appear at 0:00, and skills used during auto pause appear at the point where the time stopped.
- Basic attacks appear one icon per combo stage. Dodges appear as "Perfect Dodge", and recoveries as "Recovery".
- The numbers on the vertical axis follow "Number" in Settings (in the form "1.5M" for "KMBT (Western)" and "150万" for "Wan (East Asian)").
- Damage dealt to enemies is counted. Damage dealt by things you summoned or placed counts toward the person who summoned or placed them.
- While a benchmark is running, only your own data is recorded. Windows for other people are empty. When "Measure Only the First Enemy Attacked" is on, only damage to that enemy is counted.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While a history entry is open, the graph and icons of the opened battle are shown ("[Aggregation](Aggregation)").
