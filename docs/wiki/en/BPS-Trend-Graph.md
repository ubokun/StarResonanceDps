![BPS Trend Graph](images/en/BreakGraph1.png)

This widget shows one player's Resilience Break per second (how much of the enemies' Super Armor Gauge they depleted) as a line graph over the battle's elapsed time. Below it, skill icons are placed at the times the person used the skills.

## How to open

- On the "BPS Trend Graph" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The list shows the people in the [Break Meter](Break-Meter), following the Break Meter's "Filter settings" (the default is yourself).
- You can also open it by right-clicking a row in the [Break Meter](Break-Meter) and choosing "BPS Trend Graph".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

It works the same way as the [DPS Trend Graph](DPS-Trend-Graph), and the line is Resilience Break per second.

- Title: "BPS Trend Graph - target name".
- Line: A line that connects the Resilience Break per second for each "Drawing interval" in the display settings. Its color is the color of the person's class.
- Vertical and horizontal axes: The vertical axis shows, from the top, the maximum, half of it, and 0, and the horizontal axis shows the battle's elapsed time.
- Skill icons: Placed below the horizontal axis at the time they were used.

## Controls

- Turn the mouse wheel over the window to change the X-axis length. Turning it away from you makes it 2 seconds shorter, and turning it toward you makes it 2 seconds longer (4 to 120 seconds).
- When the battle becomes longer than the X-axis length, a horizontal scroll bar is shown at the bottom of the window. Move the thumb to see earlier times.
- Hover over a skill icon to see the skill name in a tooltip.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

There are Drawing interval (default: 2s), X-axis length (default: 12s), and Show Skill Log (default: on). They work the same way as in the [DPS Trend Graph](DPS-Trend-Graph).

### Graph Colors

Choose the line color for each class (including transformations and unknown). The defaults are the same colors as the class colors of the [Break Meter](Break-Meter).

## Notes

- Resilience Break is the same as in the [Break Meter](Break-Meter). Only enemies that have a Super Armor Gauge are counted, and when several attacks hit at the same moment, the amount is divided by the ratio of each skill's base break amount.
- The rules for the line's intervals, the time, and the skill icons are the same as in the [DPS Trend Graph](DPS-Trend-Graph).
- While a benchmark is running, only your own data is recorded. Windows for other people are empty.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While a history entry is open, the graph and icons of the opened battle are shown ("[Aggregation](Aggregation)").
