![Damage Taken Log](images/en/DamageTakenLog1.png)

This widget lists the damage taken by players, with the newest at the top. It shows who took how much damage from which skill of which enemy.

## How to open

Click the start button on the "Damage Taken Log" card in "[Widgets](Widgets)". Only one window opens.

## What it shows

- By default, there is one row per person, showing only the last damage that person took ("Show latest hit only" in the display settings). The people who took damage most recently are at the top. When it is off, all damage taken is shown one hit per row, and the newest hit is at the top.
- Row: From the left, the time, the class icon, the player name, the skill name, the damage taken, and the element icon.
  - Time: The battle's elapsed time (in the form "00:01:23"). It uses the same clock as the elapsed time of the [DPS Meter](DPS-Meter).
  - Player name: The content of "Player Name" in the display settings. In your own row, the name is shown in yellow ("Self highlight" in the display settings).
  - Skill name: The content of "Skill Name" in the display settings. The default is "enemy name-skill name(type)". If the name and the skill name do not fit, they are cut off with "…".
  - Damage taken: The value of that damage. Damage absorbed entirely by a shield is also shown.
  - Rows where the damage defeated the player show a red "(Defeated)" to the right of the value ("Show cause of defeat" in the display settings).
- Bar: It extends from the right of the row. Its length is relative to the largest damage in the list, and its color is the color of the element.
- Header: Click "Show All" to show all damage, and "Latest Only" to return to one row per person. The button text shows what happens when you click it.
- Footer: The battle's elapsed time.

## Controls

- Click a row to open that person's [Damage Taken Details](Damage-Taken-Details). You can also right-click a row and choose "Damage Taken Details".
- Hover over a row to see the element name (such as "Fire") in a tooltip.
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right. While you are viewing the top, the view follows new rows.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Player Name: Write what appears in the name field. It is written the same way and has the same items as "Player Name" in the [DPS Meter](DPS-Meter) (default: `{PlayerName} - {Spec} ({PowerLevel}-{SeasonStrength})`).
- Skill Name: Write what appears in the skill name field. Choose an item in the drop-down list and click "Insert" to insert the text for that item. You can check it in the preview below the input field (default: `{EntityName}-{SkillName}({Type})`).

| Item | Text | What appears |
|---|---|---|
| Skill Name | `{SkillName}` | The name of the skill that hit. |
| Entity Name | `{EntityName}` | The name of whatever dealt the damage. |
| Type | `{Type}` | Physical, Magic, or Unclassified. |

- Filter settings: Choose from "Show all" and "Hide self-damage & friendly fire". "Hide self-damage & friendly fire" does not show damage recorded as taken from a player (damage from buffs you applied, falling, attacks by other players, and so on). It also applies while a history entry is open (default: "Hide self-damage & friendly fire").
- Show latest hit only: Shows only the last damage taken, one row per person. You can also switch it with the header button (default: on).
- Self highlight: Shows the name in your own row in yellow (default: on).
- Show cause of defeat: Adds "(Defeated)" to the row of the damage that defeated the player (default: on).

### Icon Colors

Choose the color of the class icon for each class (including transformations and unknown). The defaults are the same colors as in the [Player List](Player-List).

### Element Colors

Choose the bar color for each element. You can change how dark the bars are with "Element Colors Opacity" (default: 100%).

## Notes

- Damage taken by players whose value is not 0 is shown. Damage that reduced neither HP nor shields and defeats without damage (such as those caused by mechanics) are not shown.
- Enemy names: Enemies with no name whose HP bar is visible are shown as "Enemy", enemies that cannot be identified (such as enemies that were there before the app started) as "Unknown enemy", and mechanics whose summoner cannot be traced as "System". Attacks by unnamed summons are shown as attacks by the enemy that summoned them.
- Skill names: For damage from a buff, it is the name of the skill that applied the buff (or the buff's name if that is not known). Falling is shown as "Fall". For skills that have no name, you can choose blank (shown as "Attack") or the internal name (Chinese) with "Unnamed skills and buffs" in Settings (default: "Show internal name").
- Damage from a mechanic may arrive as damage taken from a player (such as some mechanics in Field of Forgotten Illusions). In that case, it is not shown with the default filter settings.
- The damage values are plain numbers without digit grouping, regardless of "Number" in Settings.
- While a benchmark is running, only the damage you take is recorded.
- When "Stop Aggregation in Non-Combat Areas" in Settings is on (the default), nothing is recorded in Asterleeds, Homestead Courtyard, and Homestead House.
- Even when the aggregation is split by changing maps, entering a boss room, and so on, the previous rows stay shown until the next battle starts.
- When you open a history entry, the damage taken in that battle is shown. The records are saved together with the battle.
- While the window is pinned, the header is hidden by default, and you cannot click the switch button. Switch it with "Show latest hit only" in the settings window.
