![Buff & Debuff Group](images/en/BuffDebuffGroup1.png)

This widget shows only the buffs and debuffs you choose from your own, in a fixed order. Those that are not applied are also shown, as "No effect".

## How to open

Click the start button on the "Buff & Debuff Group" card in "[Widgets](Widgets)". It is a window for yourself only, and only one window opens.

## What it shows

- Title: "Buff & Debuff Group".
- Rows: The rows turned on in "Buffs & Debuffs" in the settings, in that order. From the left, the icon, the name, and the remaining time. When there are 2 or more stacks, the number is shown over the icon.
- Rows that are applied: Show the remaining time and a gauge. The gauge color is the Buff Colors for buff rows and the Debuff Colors for debuff rows. The remaining time and the gauge are read the same way as in the [Buff List](Buff-List).
- Rows that are not applied: Show "No effect" at the right end and no gauge. They also show "No effect" when the time runs out or the effect is removed.
- The cuisine and potion rows are single "Cuisine" and "Potion" rows, whichever cuisine or potion is applied.
- When the same buff is applied by several people, the one with the longest remaining time is shown. The name of the person who applied it is not shown.
- Among the default rows, the ones other than cuisine and potions are debuffs that are applied after Imagine skills such as the "Arcane!" ones. While they are applied, you cannot do the following.

| Row | While it is applied |
|---|---|
| Time Stasis | You cannot receive the effect of "Arcane! Time Decree" again. |
| Mechanical Failure | You cannot activate "Arcane! Superconductor Surge" again. |
| Element Stasis | You cannot receive the effect of "Arcane! Fatal Spiral" again. |
| Exhausted: Flame Devour | You cannot use "Arcane! Heart of Flame" again. |
| Weakened: Wish Sealed | "Arcane! Blessing of Life" and the role skill "Renewal Prayer" cannot be cast on you again. |

## Controls

- Click a row to open the [Buff & Debuff Card](Buff-&-Debuff-Card) for that row. It also opens from rows that are not applied.
- Right-click a row to choose one of the following items.
  - "Buff Card" ("Debuff Card" for debuff rows): The same as clicking the row.
  - "Remove This Buff from Group" ("Remove This Debuff from Group" for debuff rows): Removes the row. Default rows are turned off and remain in the list in the settings, and rows you added are removed from the list.
  - "Add This Buff to Notification Targets" and "Remove This Buff from Notification Targets" (the matching debuff items for debuff rows): Switch whether it is a notification target.
- Changes made with the right-click menu are saved immediately.
- Drag a row to reorder it. If you release it without moving, the card opens. Release it outside the window or press Esc to cancel.
- To add a row, right-click a row in your own [Buff List](Buff-List) or [Debuff List](Debuff-List), or your own Buff & Debuff Card, and choose "Add to Group". The added row goes to the bottom.
- When the window is small, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Gauge length: Choose from "Auto", "10s", "20s", and "30s". "Auto" uses the buff's duration (default: "Auto").

### Buffs & Debuffs

The rows to show. Turn them on and off with the switches, and reorder the rows by dragging (you can also reorder them by dragging the rows in the window). The defaults are the following seven rows.

| Row | Default |
|---|---|
| Cuisine | On |
| Potion | On |
| Time Stasis | On |
| Mechanical Failure | On |
| Element Stasis | On |
| Exhausted: Flame Devour | On |
| Weakened: Wish Sealed | Off |

Rows you added are removed when you turn them off and save. Default rows remain in the list even when they are off.

### Notification Targets

The buffs and debuffs whose expiry is notified by "Buff & Debuff expired" in [Notifications](Notifications) while the window is open. The default rows and their on/off states are the same as in "Buffs & Debuffs". You can add one with "Add This Buff to Notification Targets" on the right-click menu of a row. Rows that are off in "Buffs & Debuffs" are not notified.

### Buff Colors

The colors of the left and right sides of the gauge for buff rows (it changes from left to right) and the Buff Colors Opacity (default: 100%).

### Debuff Colors

The colors of the left and right sides of the gauge for debuff rows and the Debuff Colors Opacity (default: 100%).

## Notes

- If you start the app while the game is already running, buffs and debuffs that were applied before the app started may be missing until you change maps (the circle in the window title is red).
- Even while a history entry is open, the current buffs and debuffs are shown.
- They are read again every second.
- The height of the window changes to fit the number of rows.
