![Skill Details (Damage)](images/en/DamageSkillDetail1.png)

This widget lists the damage dealt by one player by skill, from most to least. It shows the total, the per-second value, the share, the hit count, and the crit rate for each skill.

## How to open

- On the "Skill Details (Damage)" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The list shows the people in the [DPS Meter](DPS-Meter), following the DPS Meter's "Filter settings" (the default is yourself).
- You can also open it by clicking a row in the [DPS Meter](DPS-Meter), or by right-clicking a row and choosing "Skill Details (Damage)".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

- Title: "Skill Details (Damage) - target name".
- Row: From the left, the rank, the element icon, the name ("Skill Name" in the display settings), the total, the per-second value (in parentheses), and the share.
  - Name: There is one row per source of damage, such as a skill or a buff. Damage that comes from the same skill (such as projectiles fired by the skill and damage from buffs the skill applied) is combined into one row.
  - Element icon: The element of that row. For rows that mix two or more elements, the two largest are shown in left and right halves.
  - Share: The share of that person's total.
- Bar: Its length is relative to the first place, and its color is the color of the element. For rows that mix two or more elements, the colors are blended by the proportion of the amounts.

## Controls

- Hover over a row to see the breakdown by element and type in a tooltip (such as "Rock100.00%, Physical100.00%").
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Skill Name: Write what appears in the name field of the rows. Choose an item in the drop-down list and click "Insert" to insert the text for that item. You can check it in the preview below the input field (default: `{SkillName} - {Type} ({Hits}hits-CRT{CritRate})`).

| Item | Text | What appears |
|---|---|---|
| Skill Name | `{SkillName}` | The row name. |
| Element | `{Element}` | The element name. If there are two or more, they are joined with "/". |
| Type | `{Type}` | Physical, Magic, or Unclassified. If there are two or more, they are joined with "/". |
| Hit Count | `{Hits}` | The number of hits. |
| Crit Rate | `{CritRate}` | The share of hits that were critical (in the form "12.34%"). |

Parentheses and hyphens left with nothing inside are removed.

### Element Colors

Choose the bar color for each element. You can change how dark the bars are with "Element Colors Opacity" (default: 100%).

## Notes

- Damage dealt to enemies is counted. Damage to yourself or other players (self-damage and friendly fire) is not included.
- Damage dealt by things you summoned or placed goes into the rows of the person who summoned or placed them.
- Damage caused by an ally's effect may appear in the rows of the person who received that effect (such as "Encore" of the Soul Musician).
- Misses and attacks that were nullified are counted in neither the total nor the hit count.
- The per-second value is the row's total divided by the battle's elapsed time. The elapsed time uses the same clock as the [DPS Meter](DPS-Meter).
- For rows that have no name, you can choose blank or the internal name (Chinese) with "Unnamed skills and buffs" in Settings (default: "Show internal name"). With "Internal ID" in Settings, you can add the number after the name.
- While a benchmark is running, only your own data is recorded. Windows for other people are empty.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is aggregated in Asterleeds, Homestead Courtyard, and Homestead House.
- While the target is not in the battle, the window is empty.
- While a history entry is open, the values of the opened battle are shown ("[Aggregation](Aggregation)").
