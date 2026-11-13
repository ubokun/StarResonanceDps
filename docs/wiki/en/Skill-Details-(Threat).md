![Skill Details (Threat)](images/en/HateSkillDetail1.png)

This widget lists the threat built up by one player by skill, from most to least. It shows the total, the per-second value, the share, the hit count, and the crit rate for each skill.

## How to open

- On the "Skill Details (Threat)" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The list shows the people in the [Threat Meter](Threat-Meter), following the Threat Meter's "Filter settings" (the default is yourself).
- You can also open it by clicking a row in the [Threat Meter](Threat-Meter), or by right-clicking a row and choosing "Skill Details (Threat)".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

It works the same way as [Skill Details (Damage)](Skill-Details-(Damage)), and the values are threat.

- Title: "Skill Details (Threat) - target name".
- Row: From the left, the rank, the element icon, the name ("Skill Name" in the display settings), the total, the per-second value (in parentheses), and the share. The share is relative to that person's total threat.
- Taunts: Threat gained from a taunt goes into the row of the skill that taunted (for example, the taunt of Sandward goes into the Sandward row).
- "Initial Threat" row: Threat gained when an enemy finds the person. This includes being found again after dropping off once.
- "Other" row: Threat that increased at a moment with neither an attack nor a taunt.
- The hit count and the crit rate are counted from the attacks the threat was divided among. For taunts, Initial Threat, and Other, the number of increases is the hit count, and crits are not counted.
- Bar: Its length is relative to the first place, and its color is the color of the element. Rows without damage (rows with only taunts, Initial Threat, and Other) use the color for no element.

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

- The increase in threat is divided among that person's attacks at the same moment by the ratio of their damage. If an attack hits at the same time as a skill whose threat multiplier differs from others (such as Countercrush), part of that skill's threat goes into the rows of the other skills. The total per person is the same as in the [Threat Meter](Threat-Meter).
- The breakdown by element and type is the breakdown of the same skill's damage. Rows without damage are no element and unclassified.
- The per-second value is the row's total divided by the battle's elapsed time. The elapsed time uses the same clock as the [DPS Meter](DPS-Meter).
- For rows that have no name, you can choose blank or the internal name (Chinese) with "Unnamed skills and buffs" in Settings (default: "Show internal name"). With "Internal ID" in Settings, you can add the number after the name.
- While a benchmark is running, only your own data is recorded. Windows for other people are empty.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While the target is not in the battle, the window is empty.
- While a history entry is open, the values of the opened battle are shown ("[Aggregation](Aggregation)").
