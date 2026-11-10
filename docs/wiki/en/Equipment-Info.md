![Equipment Info](images/en/PlayerEquipment1.png)

This widget lists one player's equipment, one row per slot.

## How to open

- On the "Equipment Info" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The default target is yourself.
- You can also open it by right-clicking a row in the [Player List](Player-List) or the [Revive List](Revive-List) and choosing "Equipment Info".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

- Title: "Equipment Info - player name".
- Row: From the left, the slot icon, the equipment name ("Equipment Name" in the display settings), and the equipment level. The slots are in the order Weapon, Helmet, Armor, Gauntlets, Boots, Earrings, Necklace, Ring, Bracelet (L), Bracelet (R), and Charm.
- The color of the name is the color for its rarity (Text Colors).
- Equipment level: In the form "Lv.260". For other players' equipment that has breakthroughs, the breakthrough stage is not known, so it is shown as a range such as "Lv.260~270".
- Hover over a row to see the equipment's stats in a tooltip. For other players, the values are ranges such as "Strength 412~528"; for yourself, they are the actual values.
- Slots with nothing equipped show "No Equipment", and slots that are not known show "Unknown".

## Controls

- Hover over a row to show the tooltip. Clicking does not close it.
- When the window is small, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Equipment Name: Write what appears as the row name. Choose an item in the drop-down list and click "Insert" to insert the text for that item. You can check it in the preview below the input field (default: `{EquipName}({MainStat})`).

| Item | Text | What appears |
|---|---|---|
| Equipment Name | `{EquipName}` | The equipment's name. |
| Equipment Level | `{EquipLevel}` | The same text as the equipment level on the right (also when it is a range). |
| Main Stat | `{MainStat}` | Strength, Intellect, or Agility. Empty for equipment that has none. |

### Text Colors

Choose the color of equipment names for each rarity from 1 to 6.

## Notes

- What is known differs between yourself and other players. For other players, the game only sends which equipment is in which slot, so the values are shown as ranges from the equipment tables.

| | Yourself | Other players |
|---|---|---|
| Equipment name, rarity, and main stat | Shown | Shown |
| Equipment level | Actual value | A range for equipment with breakthroughs (such as "Lv.260~270") |
| Basic Attributes and Advanced Attributes | Actual values | Ranges (such as "Strength 412~528"); rows that are randomly added to each piece of equipment are not shown |
| Reforge and rare attributes | Actual values | Not shown |
| Refining, gems, sigils, and set effects | Not shown | Not shown |

- Your own equipment shows actual values from the data that arrives when you log in or change maps. If you start the app while the game is already running, it is shown the same way as for other players until you change maps.
- Items that depend on the class spec are shown with the values for that person's class spec. If the class spec is not known, the values for the first class spec of that class are used.
- The season strength of equipment from a previous season is grayed out with "(Expired)" in the tooltip.
- Other players' equipment arrives when they come near you, through party information, and when you open their namecard in the game.
- For people who have left the Player List, the last known equipment remains. It is cleared when you log out.
- While a history entry is open, the equipment from that time is shown for people who were in that battle and are not around you now.
- The window height is not saved, and the window opens at the default height every time. The width and position are saved.
