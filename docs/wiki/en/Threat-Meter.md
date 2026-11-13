![Threat Meter](images/en/HateMeter1.png)

This widget lists players by the threat (aggro) they built up on enemies, from most to least. It shows the total, the per-second value (TPS), and each player's share of the whole.

## How to open

Click the start button on the "Threat Meter" card in "[Widgets](Widgets)".

## What it shows

It works the same way as the [DPS Meter](DPS-Meter), and the values are threat.

- Row: From the left, the rank, the class icon, the player name, the total threat, the per-second value (in parentheses), and the share.
  - The length of the bar is relative to the first place, and the color of the bar is the color of the class.
  - In your own row, the name is shown in yellow ("Self highlight" in the display settings).
- Header: "Benchmark" and "Reset" do the same as the same buttons in "[Aggregation](Aggregation)". While a benchmark is running, "Benchmark" changes to "Stop Benchmark".
- Footer: The left shows the battle's elapsed time, and the right shows "TPS" (the total of the per-second values of the people shown) and "Total" (the total threat). While a benchmark is running, "[Benchmarking]" is shown to the right of the elapsed time, and "[Benchmark Complete]" is shown when the time is up.

## Controls

- Click a row to open that player's [Skill Details (Threat)](Skill-Details-(Threat)).
- Right-click a row to choose and open that player's "Skill Details (Threat)" or "TPS Trend Graph".
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

There are Player Name, Filter settings, and Self highlight. They work the same way as in the [HPS Meter](HPS-Meter).

### Class Colors

Choose the bar color for each class (including transformations and unknown). You can change how dark the bars are with "Class Colors Opacity" (default: 100%).

## Notes

- The amount of threat is how much the threat value that the game keeps for each enemy increased. Threat added by taunts and similar effects is included as it is.
- Threat is counted only for enemies around you.
- When someone's threat goes down (for example, when an enemy's threat is reset for everyone), that change is not counted.
- The per-second value is the total divided by the battle's elapsed time. The elapsed time uses the same clock as the [DPS Meter](DPS-Meter).
- The share is relative to "Total". If you narrow down the people with the filter settings, it becomes the share of the total of the people shown.
- While a benchmark is running, only your own data is recorded.
- Even when the aggregation is split by changing maps, entering a boss room, and so on, the previous result stays shown until the next battle starts. When you click "Reset" or "Benchmark", the meter becomes empty immediately.
- By default, nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While the window is pinned, the header is hidden by default. You can still use "Benchmark" and "Reset" from "[Aggregation](Aggregation)" or with hotkeys.
- How numbers are written (such as 1.23M) can be changed with "Number" in Settings (default: "KMBT (Western)").
- While a history entry is open, the values of the opened battle are shown ("[Aggregation](Aggregation)").
