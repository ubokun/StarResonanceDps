![Entity Info](images/en/EntityInfo1.png)

This widget shows one enemy from the Entity List in one place: its HP, chanting, the player it is targeting and aggro, and its buffs and debuffs.

## How to open

- Click a row in the [Entity List](Entity-List), or right-click a row and choose "Entity Info".
- You can also open it from the "Entity Info" card in "[Widgets](Widgets)". The drop-down list in the name field shows the same enemies in the same order as the Entity List (the default is the first enemy). If there are no enemies, an empty window that waits for an enemy to appear opens and shows the enemy that appears.
- A window is opened per enemy. If a window for the same enemy is already open, that window is brought to the front.

## What it shows

- Title: "Entity Info - enemy name".
- First line: The same as the row in the Entity List (points are not shown). While the enemy is chanting, the skill name appears to the right of the name, and the chanting gauge appears at the right end. The length of the HP bar changes with the width of the window.
- Second line: The player the enemy is targeting now and the players in order of aggro. The crosshair marks and gauges are read the same way as the aggro slots in the Entity List. If they do not fit, they wrap.
- Below that: The buffs on the enemy, and then the debuffs on a new line. The name ("Buff Name" in the display settings) appears above each slot, the number of stacks (when 2 or more) over the icon, and the remaining seconds below the icon.
- Order of buffs and debuffs: Those turned on in the Priority List come first (in the order of the list), and the rest follow in the order they were applied.
- While the enemy is not around you (defeated or moved away), nothing is shown. If the same enemy returns, it is shown again.

## Controls

- Click a buff slot to open the enemy's [Buff List](Buff-List), or click a debuff slot to open its [Debuff List](Debuff-List).
- Right-click a slot to choose "Buff List", "Buff Card", "Prioritize This Buff" ("Stop Prioritizing This Buff" while it is prioritized), or "Hide This Buff" (debuff slots have the matching debuff items, such as "Debuff List", "Debuff Card", and "Hide This Debuff"). Prioritizing and hiding are saved immediately.
- Hover to see a tooltip: the name for buff and debuff slots, "In Combat" for the combat state icon, and the class spec followed by "(Targeted)" or "(Aggro: value)" for an aggro slot.
- When the window is small, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Entity Name: Write what appears in the name field. It is written the same way as "Entity Name" in the [Entity List](Entity-List), but the value is kept separately (default: `Lv.{Level} {EntityName}`).
- Buff Name: Write what appears as the name of buff and debuff slots (default: `{BuffName}({CasterName})`).

| Item | Text | What appears |
|---|---|---|
| Buff Name | `{BuffName}` | The buff's name. |
| Target Name | `{TargetName}` | The enemy's name. |
| Caster Player Name | `{CasterName}` | The name of the player who applied the buff. `*****` when hidden with "Player name" in Settings. |
| Level | `{Level}` | The enemy's level. |

- HP value: Choose from "Add shield amount" and "Show shield separately" (default: "Add shield amount").
- Combat State: Choose how the combat state icon is shown from "Show" and "Hide" (default: "Show").
- Targeted Player: Whether the player the enemy is targeting now is shown at the left end of the second line (default: "Show").
- Aggro: Choose how many people are shown on the second line from "Show All", "Show Top 5", "Show Top 4", "Show Top 3", "Show Top 2", and "Show Top Aggro Only" (default: "Show All").
- Same Buffs: Choose from "Don't Group" and "Group". "Don't Group" shows buffs applied by players in a separate slot for each player who applied them (default: "Don't Group").
- Caster Player Name: Choose from "Show", "Hide", and "Applied by Others" (default: "Applied by Others").

### Priority List

The buffs and debuffs that are shown first. Turn them on and off with the switches, and reorder the rows by dragging. The defaults are "Wound" and two "Healing Prohibition" entries, all turned on. You can add one with "Prioritize This Buff" on the right-click menu of a slot. Rows you added are removed when you turn them off and save.

### Hide List

The buffs and debuffs that are not shown. Add one with "Hide This Buff" on the right-click menu of a slot. If you turn its switch off and save, it is removed from the list and is shown again.

### Icon Colors

Choose the colors of the type icons and of the class icons in the aggro slots. The rows and default colors are the same as in the Entity List.

## Notes

- The window follows the one enemy it was opened for.
- Buffs and debuffs are read again every second.
- The circle in the window title turns yellow for enemies that were around you before the app started, or while information about the enemy is still missing. Approaching the enemy again fixes it.
- The height of the window changes to fit the number of rows.
- Even while a history entry is open, the current enemy is shown.
