![Skill Details (Healing)](images/en/HealingSkillDetail1.png)

This widget lists the healing done by one player by skill, from most to least. It shows the total, the per-second value, the share, the hit count, and the crit rate for each skill.

## How to open

- On the "Skill Details (Healing)" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The list shows the people in the [HPS Meter](HPS-Meter), following the HPS Meter's "Filter settings" (the default is yourself).
- You can also open it by clicking a row in the [HPS Meter](HPS-Meter), or by right-clicking a row and choosing "Skill Details (Healing)".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

It works the same way as [Skill Details (Damage)](Skill-Details-(Damage)), and the values are the amount healed.

- Title: "Skill Details (Healing) - target name".
- Row: From the left, the rank, the element icon, the name ("Skill Name" in the display settings), the total, the per-second value (in parentheses), and the share. The share is relative to that person's total healing.
- Healing potions, cuisine, and natural recovery also become rows. Their names are "Healing Potion", "Natural Recovery", and the name of the cuisine. Their element is No element, and their type is Unclassified.
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

- The amount of healing is the HP actually gained. Healing on people at full HP is counted in neither the healing nor the hit count.
- Healing potions, cuisine, and natural recovery count as healing by the person who drank or ate them. They may not be counted, for example, when other healing or damage happened at the same moment.
- Cuisine and natural recovery of people who were around you before the app started are not counted until you approach them again (the circle in the window title is yellow).
- Healing by things you summoned or placed goes into the rows of the person who summoned or placed them.
- The per-second value is the row's total divided by the battle's elapsed time. The elapsed time uses the same clock as the [DPS Meter](DPS-Meter).
- For rows that have no name, you can choose blank or the internal name (Chinese) with "Unnamed skills and buffs" in Settings (default: "Show internal name"). With "Internal ID" in Settings, you can add the number after the name.
- While a benchmark is running, only your own data is recorded. Windows for other people are empty.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While the target is not in the battle, the window is empty.
- While a history entry is open, the values of the opened battle are shown ("[Aggregation](Aggregation)").
