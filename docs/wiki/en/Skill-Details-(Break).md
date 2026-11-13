![Skill Details (Break)](images/en/BreakSkillDetail1.png)

This widget lists how much of the enemies' Super Armor Gauge one player depleted (Resilience Break) by skill, from most to least. It shows the total, the per-second value, the share, the hit count, and the crit rate for each skill.

## How to open

- On the "Skill Details (Break)" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The list shows the people in the [Break Meter](Break-Meter), following the Break Meter's "Filter settings" (the default is yourself).
- You can also open it by clicking a row in the [Break Meter](Break-Meter), or by right-clicking a row and choosing "Skill Details (Break)".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

It works the same way as [Skill Details (Damage)](Skill-Details-(Damage)), and the values are Resilience Break.

- Title: "Skill Details (Break) - target name".
- Row: From the left, the rank, the element icon, the name ("Skill Name" in the display settings), the total, the per-second value (in parentheses), and the share. The share is relative to that person's total Resilience Break.
- The hit count and the crit rate are counted from the attacks that depleted the Super Armor Gauge.
- Bar: Its length is relative to the first place, and its color is the color of the element.

## Controls

- Hover over a row to see the breakdown by element and type in a tooltip.
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Skill Name: Write what appears in the name field of the rows. It is written the same way and has the same items as "Skill Name" in [Skill Details (Damage)](Skill-Details-(Damage)) (default: `{SkillName} - {Type} ({Hits}hits-CRT{CritRate})`).

### Element Colors

Choose the bar color for each element. You can change how dark the bars are with "Element Colors Opacity" (default: 100%).

## Notes

- When several attacks hit at the same moment, the amount the Super Armor Gauge went down is divided by the ratio of each skill's base break amount. Bonuses such as Resilience Break Efficiency are not used for the division. The total per person is the same as in the [Break Meter](Break-Meter).
- Only enemies that have a Super Armor Gauge are counted.
- The breakdown by element and type is the breakdown of the same skill's damage.
- The per-second value is the row's total divided by the battle's elapsed time. The elapsed time uses the same clock as the [DPS Meter](DPS-Meter).
- For rows that have no name, you can choose blank or the internal name (Chinese) with "Unnamed skills and buffs" in Settings (default: "Show internal name"). With "Internal ID" in Settings, you can add the number after the name.
- While a benchmark is running, only your own data is recorded. Windows for other people are empty.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While the target is not in the battle, the window is empty.
- While a history entry is open, the values of the opened battle are shown ("[Aggregation](Aggregation)").
