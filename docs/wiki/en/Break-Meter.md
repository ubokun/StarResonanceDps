![Break Meter](images/en/BreakMeter1.png)

This widget lists players by how much of the enemies' Super Armor Gauge they depleted (Resilience Break), from most to least. It shows the total, the per-second value (BPS), and each player's share of the whole.

## How to open

Click the start button on the "Break Meter" card in "[Widgets](Widgets)".

## What it shows

It works the same way as the [DPS Meter](DPS-Meter), and the values are Resilience Break.

- Row: From the left, the rank, the class icon, the player name, the total Resilience Break, the per-second value (in parentheses), and the share.
  - The length of the bar is relative to the first place, and the color of the bar is the color of the class.
  - In your own row, the name is shown in yellow ("Self highlight" in the display settings).
- Header: "Benchmark" and "Reset" do the same as the same buttons in "[Aggregation](Aggregation)". While a benchmark is running, "Benchmark" changes to "Stop Benchmark".
- Footer: The left shows the battle's elapsed time, and the right shows "BPS" (the total of the per-second values of the people shown) and "Total" (the total Resilience Break). While a benchmark is running, "[Benchmarking]" is shown to the right of the elapsed time, and "[Benchmark Complete]" is shown when the time is up.

## Controls

- Click a row to open that player's [Skill Details (Break)](Skill-Details-(Break)).
- Right-click a row to choose and open that player's "Skill Details (Break)" or "BPS Trend Graph".
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

There are Player Name, Filter settings, and Self highlight. They work the same way as in the [HPS Meter](HPS-Meter).

### Class Colors

Choose the bar color for each class (including transformations and unknown). You can change how dark the bars are with "Class Colors Opacity" (default: 100%).

## Notes

- Resilience Break is the amount by which an enemy's Super Armor Gauge actually went down. Only enemies that have a Super Armor Gauge (enemies with a gauge on the third line of the [Entity List](Entity-List)) are counted.
- When several attacks hit at the same moment, the amount that went down is divided by the ratio of each skill's base break amount. Bonuses such as Resilience Break Efficiency are not used for the division, so when people with different bonuses hit at the same time, the values per person are off. The total is the same as the amount that actually went down.
- The amount the gauge recovers and the amount it goes down when its maximum changes at an enemy's phase change are not counted.
- Resilience Break is counted only for enemies around you.
- The per-second value is the total divided by the battle's elapsed time. The elapsed time uses the same clock as the [DPS Meter](DPS-Meter).
- The share is relative to "Total". If you narrow down the people with the filter settings, it becomes the share of the total of the people shown.
- While a benchmark is running, only your own data is recorded.
- Even when the aggregation is split by changing maps, entering a boss room, and so on, the previous result stays shown until the next battle starts. When you click "Reset" or "Benchmark", the meter becomes empty immediately.
- By default, nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While the window is pinned, the header is hidden by default. You can still use "Benchmark" and "Reset" from "[Aggregation](Aggregation)" or with hotkeys.
- How numbers are written (such as 1.23M) can be changed with "Number" in Settings (default: "KMBT (Western)").
- While a history entry is open, the values of the opened battle are shown ("[Aggregation](Aggregation)").
