![Damage Taken Details](images/en/DamageTakenSummary1.png)

This widget shows the damage taken by one player in a table split into normal, crit, and lucky hits, and as shares by element and by type (physical, magic, and so on).

## How to open

- On the "Damage Taken Details" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The list shows the people who have records of taking damage, from the most damage taken (the default is yourself).
- You can also open it by clicking a row in the [Damage Taken Log](Damage-Taken-Log), or by right-clicking a row and choosing "Damage Taken Details".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

It works the same way as [Damage Details](Damage-Details), and the values are the damage taken.

- Title: "Damage Taken Details - target name".
- Top table: The value, rate, and hit count for "Total", "Normal", "Crit", and "Lucky".
- Element Share and Type Share: The total and share of the damage taken by element and by type.
- When the total is 0, nothing is shown.

## Controls

- Hovering over a share row changes the row's color. Clicking does nothing.
- When you make the window smaller by hand, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Icon Colors

Choose the bar color for each of the nine elements and for each of Physical, Magic, and Unclassified. You can change how dark the bars are with "Icon Colors Opacity" (default: 100%).

## Notes

- All damage taken whose value is not 0 is counted. Self-damage, friendly fire, and falling are also included. The "Filter settings" of the [Damage Taken Log](Damage-Taken-Log) do not apply.
- Damage absorbed entirely by a shield is also included.
- While a benchmark is running, only your own data is recorded. Windows for other people are empty.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While a history entry is open, the values of the opened battle are shown ("[Aggregation](Aggregation)").
