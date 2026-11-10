![Entity List](images/en/EntityList1.png)

This widget lists the enemies around you, one per row. It shows their HP, chanting, the player they are targeting, and the aggro ranking.

## How to open

Click the start button on the "Entity List" card in "[Widgets](Widgets)".

## What it shows

- Row: From the left, the type icon, the name and HP, and the aggro slots.
  - Type icon: Monster, Elite, or Boss. Enemies whose type is unknown show a question mark. The type name appears below it. In dungeons where you collect points through the spatial investigation, the enemy's points (such as "60pt") appear in the center of the icon.
  - Upper line: The combat state icon (only for enemies in combat) and the name ("Entity Name" in the display settings).
  - HP bar: Red is HP (green for allies), the white part is the shield, and the numbers are "HP+shield/max HP" ("HP value" in the display settings).
  - Third line: Shown for enemies that have a Super Armor Gauge ("current/max"). For enemies that are chanting, it is replaced by the chanting gauge ("elapsed/total" in seconds).
  - Aggro slots: Class icons and player names. The leftmost slot is the player the enemy is targeting now, and an orange crosshair mark appears on its icon. The players with the most aggro follow in order, and the black mark to the right of each icon is the aggro gauge (it fills with orange from the bottom).
- Order: Enemies in combat are at the top and other enemies are below. Within each group, the order is Boss → Elite → Monster → others, and within the same type, the order follows "Sort Order" in the display settings.
- Footer: The left shows the current map, and the right shows the number of enemies "In Combat" and "Nearby". "Nearby" counts the enemies in the list, and "In Combat" counts those of them that are in combat. While you are collecting points through the spatial investigation, it changes to "In Combat" (the total points of the enemies in combat) and "Current" (the current points).

## Controls

- Click a row to open that enemy's [Entity Info](Entity-Info).
- Right-click a row to choose and open that enemy's "Entity Info", "Buff List", or "Debuff List".
- Hover to see a tooltip: "In Combat" for the combat state icon, and the class spec followed by "(Targeted)" or "(Aggro: value)" for an aggro slot.
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Entity Name: Write what appears in the name field. Choose an item in the drop-down list and click "Insert" to insert the text for that item. You can check it in the preview below the input field (default: `Lv.{Level} {EntityName}`).

| Item | Text | What appears |
|---|---|---|
| Entity Name | `{EntityName}` | The enemy's name. |
| Level | `{Level}` | The enemy's level. |

- HP value: "Add shield amount" ("HP+shield/max HP") or "Show shield separately" ("HP(shield)/max HP"; the parentheses are not shown when there is no shield) (default: "Add shield amount").
- Filter settings: Choose from "Show all" and "Hide objects". "Hide objects" does not show enemies whose HP bar is not visible in the game (default: "Hide objects").
- Sort Order: Choose from "Discovery Order" and "Name Order" (default: "Name Order").
- Combat State: Choose how the combat state icon is shown from "Show" and "Hide" (default: "Show").
- Targeted Player: Whether the player the enemy is targeting now is shown at the left end of the aggro slots (default: "Show").
- Aggro: Choose how many people are shown in the aggro slots from "Show Top 5", "Show Top 4", "Show Top 3", "Show Top 2", and "Show Top Aggro Only" (default: "Show Top 5").

### Icon Colors

Choose the colors of the type icons and of the class icons in the aggro slots (including transformations and unknown).

## Notes

- Enemies with a name and enemies whose HP bar is visible in the game are shown. Enemies with no name whose HP bar is visible are shown as "Enemy" ("Ally" for allies).
- Enemies that were around you before the app started are shown as "Unknown enemy", and their max HP and level may be 0. Approaching them again reveals them.
- If you start the app while the game is already running, allies and enemies cannot be told apart until you change maps, and all HP bars are red.
- No chanting gauge is shown for a skill that was already being chanted when the enemy appeared nearby. The chanting gauge appears not only for magic but also for charged and channeled skills.
- The player the enemy is targeting now is not always the one with the most aggro.
- Points are shown only while you are collecting points through the spatial investigation, and only for enemies whose HP bar is visible in the game.
- Even while a history entry is open, the enemies currently around you are shown.
- "Entity appeared", "Entity chanting", and "Targeted Player" in "[Notifications](Notifications)" notify even when this window is not open.
