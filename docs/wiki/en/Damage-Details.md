![Damage Details](images/en/DamageSummary1.png)

This widget shows the damage dealt by one player in a table split into normal, crit, and lucky hits, and as shares by element and by type (physical, magic, and so on).

## How to open

- On the "Damage Details" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The list shows the people in the [DPS Meter](DPS-Meter), following the DPS Meter's "Filter settings" (the default is yourself).
- You can also open it by right-clicking a row in the [DPS Meter](DPS-Meter) and choosing "Damage Details".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

- Title: "Damage Details - target name".
- Top table: The rows are "Total", "Normal", "Crit", and "Lucky", and the columns are, from the left, the value, the rate, and the hit count.
  - Normal: Hits that are neither crit nor lucky.
  - Crit: Hits that were critical. The rate is the number of crit hits relative to the number of all hits.
  - Lucky: Lucky Strike hits. The rate is the number of Lucky Strike hits relative to the number of hits that were not Lucky Strikes.
  - A hit that was both crit and lucky counts in both Crit and Lucky. For that reason, Normal, Crit, and Lucky may add up to more than Total.
- Element Share: The total for each element and its share of the total. They are listed from the largest, and the length of the bar is relative to the largest element.
- Type Share: The total and share for each of Physical, Magic, and Unclassified. The order and bars are the same as in Element Share.
- When the total is 0, nothing is shown.

## Controls

- Hovering over a share row changes the row's color. Clicking does nothing.
- When you make the window smaller by hand, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Icon Colors

Choose the bar color for each of the nine elements and for each of Physical, Magic, and Unclassified. You can change how dark the bars are with "Icon Colors Opacity" (default: 100%).

## Notes

- Damage dealt to enemies is counted. Self-damage and friendly fire are not included. Damage dealt by things you summoned or placed counts toward the person who summoned or placed them.
- Misses and attacks that were nullified are counted in neither the value nor the hit count.
- While a benchmark is running, only your own data is recorded. Windows for other people are empty. When "Measure Only the First Enemy Attacked" is on, only damage to that enemy is counted.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While a history entry is open, the values of the opened battle are shown ("[Aggregation](Aggregation)").
