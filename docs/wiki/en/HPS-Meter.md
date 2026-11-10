![HPS Meter](images/en/HpsMeter1.png)

This widget lists players by the amount they healed, from most to least. It shows the total, the per-second value (HPS), and each player's share of the whole.

## How to open

Click the start button on the "HPS Meter" card in "[Widgets](Widgets)".

## What it shows

- Row: From the left, the rank, the class icon, the player name, the total healing, the per-second value (in parentheses), and the share.
  - The length of the bar is relative to the first place, and the color of the bar is the color of the class.
  - In your own row, the name is shown in yellow ("Self highlight" in the display settings).
- Header: "Benchmark" and "Reset" do the same as the same buttons in "[Aggregation](Aggregation)". While a benchmark is running, "Benchmark" changes to "Stop Benchmark".
- Footer: The left shows the battle's elapsed time, and the right shows "HPS" (the total of the per-second values of the people shown) and "Total" (the total healing). While a benchmark is running, "[Benchmarking]" appears to the right of the elapsed time, and "[Benchmark Complete]" appears when the time is up.

## Controls

- Click a row to open that player's [Skill Details (Healing)](Skill-Details-(Healing)).
- Right-click a row to choose and open that player's "Skill Details (Healing)", "Healing Details", or "HPS Trend Graph".
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Player Name: Write what appears in the name field. It is written the same way and has the same items as "Player Name" in the [DPS Meter](DPS-Meter) (default: `{PlayerName}[{Psych}] - {Spec} ({PowerLevel}-{SeasonStrength})`).
- Filter settings: Choose the players to show from "Show all", "Party members only", "Non-party members", and "Self only" (default: "Show all"). It has no effect while a history entry is open.
- Self highlight: Shows the name in your own row in yellow (default: on).

### Class Colors

Choose the bar color for each class (including transformations and unknown). You can change how dark the bars are with "Class Colors Opacity" (default: 100%).

## Notes

- The amount of healing is the HP actually gained. Healing on people at full HP and the amount beyond full HP (overhealing) are not counted.
- Healing potions, cuisine, and natural recovery also count as healing by the person who drank or ate them. They may not be counted, for example, when other healing or damage happened at the same moment.
- Cuisine and natural recovery of people who were around you before the app started are not counted until you approach them again (the circle in the window title is yellow).
- Healing by things you summoned or placed counts toward the person who summoned or placed them.
- Enemy healing and shields are not counted. Enemy healing appears in the [Timeline](Timeline).
- The per-second value is the total divided by the battle's elapsed time. The elapsed time uses the same clock as the [DPS Meter](DPS-Meter).
- The share is relative to "Total". If you narrow down the people with the filter settings, it becomes the share of the total of the people shown.
- Even when the aggregation is split by changing maps, entering a boss room, and so on, the previous result stays shown until the next battle starts. When you click "Reset" or "Benchmark", the meter becomes empty immediately.
- By default, nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While the window is pinned, the header is hidden by default. You can still use "Benchmark" and "Reset" from "[Aggregation](Aggregation)" or with hotkeys.
- How numbers are written (such as 1.23M) can be changed with "Number" in Settings (default: "KMBT (Western)").
- While a history entry is open, the values of the opened battle are shown ("[Aggregation](Aggregation)").
