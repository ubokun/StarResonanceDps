![Debuff List](images/en/DebuffList1.png)

This widget lists the debuffs on one player or one enemy, one per row, with the remaining time and a gauge. It works the same way as the [Buff List](Buff-List), and its settings are kept separately from the Buff List.

## How to open

- On the "Debuff List" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The order is the same as in the Buff List (the default is yourself).
- You can also open it by right-clicking a row in the [Player List](Player-List), the [Revive List](Revive-List), or the [Entity List](Entity-List) and choosing "Debuff List". It also opens when you click a debuff slot in [Entity Info](Entity-Info).
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

- Title: "Debuff List - target name".
- The rows are read the same way as in the [Buff List](Buff-List). In enemy windows, by default, debuffs applied by players are shown one row per player who applied them, with that player's name added (the image shows debuffs that two players applied to a boss).
- Order: Debuffs turned on in the Priority List come first (in the order of the list), and the rest follow in the order they were applied (newest at the bottom).

## Controls

- Click a row to open the [Buff & Debuff Card](Buff-&-Debuff-Card) for that debuff.
- Right-click a row to choose "Debuff Card", "Prioritize This Debuff" ("Stop Prioritizing This Debuff" while it is prioritized), or "Hide This Debuff". In your own window, "Add to Group" and "Add to Buff & Debuff Card Shortcuts" also appear. They work the same way as in the Buff List.
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

The items and the text you can insert are the same as in the [Buff List](Buff-List). The defaults are `{BuffName}({CasterName})` for Buff Name, "30s" for Gauge length, "Group Players Only" for Same Buffs, and "Applied by Others" for Caster Player Name.

### Priority List

The debuffs that are shown first. Turn them on and off with the switches, and reorder the rows by dragging. The defaults are the following six rows.

| Row | Default |
|---|---|
| Time Stasis | Off |
| Mechanical Failure | Off |
| Element Stasis | Off |
| Wound | On |
| Healing Prohibition (from "Arcane! Infernal Thrust") | On |
| Healing Prohibition (from the role skill "Infernal Thrust") | On |

You can add one with "Prioritize This Debuff" on the right-click menu of a row. Rows you added are removed when you turn them off and save.

### Hide List

The debuffs that are not shown. Add one with "Hide This Debuff" on the right-click menu of a row. If you turn its switch off and save, it is removed from the list and is shown again.

### Debuff Colors

The colors of the left and right sides of the gauge (it changes from left to right) and the Debuff Colors Opacity (default: 100%).

## Notes

- The notes are the same as in the [Buff List](Buff-List).
- Prioritizing and hiding are based on the debuff's number. Debuffs with the same name but different numbers (such as the two "Healing Prohibition" debuffs) are handled separately.
