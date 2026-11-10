![HPS Trend Graph](images/en/HpsGraph1.png)

This widget shows one player's healing per second as a line graph over the battle's elapsed time. Below it, skill icons are placed at the times the person used the skills.

## How to open

- On the "HPS Trend Graph" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The list shows the people in the [HPS Meter](HPS-Meter), following the HPS Meter's "Filter settings" (the default is yourself).
- You can also open it by right-clicking a row in the [HPS Meter](HPS-Meter) and choosing "HPS Trend Graph".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

It works the same way as the [DPS Trend Graph](DPS-Trend-Graph), and the line shows healing per second.

- Title: "HPS Trend Graph - target name".
- Line: A line that connects the healing per second for each "Drawing interval" in the display settings. Its color is the color of the person's class.
- Vertical and horizontal axes: The vertical axis shows, from the top, the maximum, half of it, and 0, and the horizontal axis shows the battle's elapsed time.
- Skill icons: Placed below the horizontal axis at the time they were used.

## Controls

- Turn the mouse wheel over the window to change the X-axis length. Turning it away from you makes it 2 seconds shorter, and turning it toward you makes it 2 seconds longer (4 to 120 seconds).
- When the battle becomes longer than the X-axis length, a horizontal scroll bar appears at the bottom of the window. Move the thumb to see earlier times.
- Hover over a skill icon to see the skill name in a tooltip.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

There are Drawing interval (default: 2s), X-axis length (default: 12s), and Show Skill Log (default: on). They work the same way as in the [DPS Trend Graph](DPS-Trend-Graph).

### Graph Colors

Choose the line color for each class (including transformations and unknown). The defaults are the same colors as the class colors of the [HPS Meter](HPS-Meter).

## Notes

- The amount of healing is the HP actually gained. Healing on people at full HP and the amount beyond full HP (overhealing) are not counted.
- Healing potions, cuisine, and natural recovery also count as healing by the person who drank or ate them. Cuisine and natural recovery of people who were around you before the app started are not counted until you approach them again (the circle in the window title is yellow).
- Enemy healing is not included.
- The rules for intervals, time, and skill icons are the same as in the [DPS Trend Graph](DPS-Trend-Graph).
- While a benchmark is running, only your own data is recorded. Windows for other people are empty.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While a history entry is open, the graph and icons of the opened battle are shown ("[Aggregation](Aggregation)").
