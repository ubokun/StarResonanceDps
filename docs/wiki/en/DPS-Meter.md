![DPS Meter](images/en/DpsMeter1.png)

This widget lists players by the damage they dealt, from most to least. It shows the total, the per-second value (DPS), and each player's share of the whole.

## How to open

Click the start button on the "DPS Meter" card in "[Widgets](Widgets)".

## What it shows

- Row: From the left, the rank, the class icon, the player name, the total damage dealt, the per-second value (in parentheses), and the share.
  - The length of the bar is relative to the first place, and the color of the bar is the color of the class.
  - In your own row, the name is shown in yellow ("Self highlight" in the display settings).
- Header: "Benchmark" and "Reset" do the same as the same buttons in "[Aggregation](Aggregation)". While a benchmark is running, "Benchmark" changes to "Stop Benchmark".
- Footer: The left shows the battle's elapsed time, and the right shows "DPS" (the total of the per-second values of the people shown) and "Total" (the total damage dealt). While a benchmark is running, "[Benchmarking]" appears to the right of the elapsed time, and "[Benchmark Complete]" appears when the time is up.

## Controls

- Click a row to open that player's [Skill Details (Damage)](Skill-Details-(Damage)).
- Right-click a row to choose and open that player's "Skill Details (Damage)", "Damage Details", or "DPS Trend Graph".
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Player Name: Write what appears in the name field. Choose an item in the drop-down list and click "Insert" to insert the text for that item. You can check it in the preview below the input field (default: `{PlayerName}[{Psych}] - {Spec} ({PowerLevel}-{SeasonStrength})`).

| Item | Text | What appears |
|---|---|---|
| Player Name | `{PlayerName}` | The name. `*****` when hidden with "Player name" in Settings. |
| Class Spec | `{Spec}` | The class spec (such as Iaido Slash). "Unknown" until it is known, and "Class I" if the player has no talents equipped. |
| Deep-Slumber Psychoscope | `{Psych}` | The Deep-Slumber Psychoscope type. "Not Enabled" if it is not enabled, and "Unknown" until it is known. |
| Ability Score | `{PowerLevel}` | The ability score. |
| Season Strength | `{SeasonStrength}` | The season strength. |
| Season Level | `{SeasonLevel}` | The season level. |
| Player UID | `{Uid}` | The player UID. |

- Values that are not known yet show 0.
- Filter settings: Choose the players to show from "Show all", "Party members only", "Non-party members", and "Self only" (default: "Show all"). It has no effect while a history entry is open.
- Self highlight: Shows the name in your own row in yellow (default: on).

### Class Colors

Choose the bar color for each class (including transformations and unknown). You can change how dark the bars are with "Class Colors Opacity" (default: 100%).

## Notes

- Damage dealt by things you summoned or placed counts toward the person who summoned or placed them.
- The per-second value is the total divided by the battle's elapsed time. The elapsed time counts from the first combat, stops a set number of seconds (default: 10 seconds) after the last combat, and continues counting in the next combat.
- The share is relative to "Total". If you narrow down the people with the filter settings, it becomes the share of the total of the people shown.
- Even when the aggregation is split by changing maps, entering a boss room, and so on, the previous result stays shown until the next battle starts. When you click "Reset" or "Benchmark", the meter becomes empty immediately.
- By default, nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While the window is pinned, the header is hidden by default. You can still use "Benchmark" and "Reset" from "[Aggregation](Aggregation)" or with hotkeys.
- How numbers are written (such as 1.23M) can be changed with "Number" in Settings (default: "KMBT (Western)").
- While a history entry is open, the values of the opened battle are shown ("[Aggregation](Aggregation)").
- How the aggregation is split, the number of seconds before pausing, whether to keep the previous result, and the places where nothing is aggregated can be changed in "Aggregation" in [Settings](Settings).
