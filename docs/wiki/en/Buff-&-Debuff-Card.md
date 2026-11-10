![Buff & Debuff Card](images/en/BuffDebuffCard1.png)

This widget shows one buff or debuff on a player or an enemy, with its name, icon, and remaining time in large size.

## How to open

- On the "Buff & Debuff Card" card in "[Widgets](Widgets)", choose an item from the drop-down list in the name field and click the start button. The items are listed in the following order.
  1. Rows turned on in "Shortcuts" in the settings (the target is yourself)
  2. The buffs of the target selected in the name field of the "Buff List" card
  3. The debuffs of the target selected in the name field of the "Debuff List" card
  4. Open windows that are not listed above
- The default item is the first shortcut. With the default settings, it is "Buff Card (Cuisine) - your name".
- When there is nothing to list, the name field shows only "Buff & Debuff Card". If you click the start button then, the card for the first row of your own Buff List (or Debuff List if there is none) opens. If there is none either, an empty window opens and becomes the card for whatever is applied first.
- Click a row in the [Buff List](Buff-List) or [Debuff List](Debuff-List), or right-click a row and choose "Buff Card" or "Debuff Card", to open the card for that buff or debuff.
- You can also open it from the right-click menu of a slot in [Entity Info](Entity-Info) or a row in the [Buff & Debuff Group](Buff-&-Debuff-Group). Clicking a row in the group also opens it.
- A window is opened per pair of target and buff. If a window for the same pair is already open, that window is brought to the front.

## What it shows

- Title: "Buff Card (buff name) - target name" or "Debuff Card (debuff name) - target name". For cuisine and potions, it is "Buff Card (Cuisine)" or "Buff Card (Potion)".
- Body: From the top, the name line ("Buff Name" in the display settings), the icon, and the remaining time. When there are 2 or more stacks, the number is shown over the icon. The remaining time is shown the same way as in the Buff List.
- Cuisine and potions are followed by type. Even if you eat again and the cuisine or potion changes to a different one, it is shown in the same window.
- When the same buff is applied by several people, the one with the longest remaining time is shown.
- When the effect ends, only the name line remains. When the same buff is applied again, it is shown again.
- Header: There are "Shrink" and "Enlarge" buttons to the left of the close button.

## Controls

- Shrink and Enlarge: Change the size of the body in steps of 25%, from 100% to 1000% (default: 200%). The window size does not change. The scale is saved for each card (by type for cuisine and potions, and by enemy type for enemies) and is used the next time you open the same card.
- Right-click to choose one of the following items. If there is no item to show, the menu does not open.
  - "Add to Group": Adds the buff to the [Buff & Debuff Group](Buff-&-Debuff-Group). It appears on your own cards, for buffs that are not in the group yet.
  - "Add to Buff & Debuff Card Shortcuts": Adds the buff to the shortcuts. It appears on your own cards, for buffs that are not in the shortcuts yet.
  - "Add This Buff to Notification Targets" and "Remove This Buff from Notification Targets" (the matching debuff items for debuffs): Switch whether it is a notification target. They appear on every card.
- Changes made with the right-click menu are saved immediately.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Buff Name: Write what appears in the name line. Choose an item in the drop-down list and click "Insert" to insert that text (default: `{BuffName}({TargetName})`).

| Item | Text | What appears |
|---|---|---|
| Buff Name | `{BuffName}` | The buff's name. "Cuisine" or "Potion" for cuisine and potions. |
| Target Name | `{TargetName}` | The name of the card's target. |
| Caster Player Name | `{CasterName}` | The name of the player who applied the buff being shown. Empty when it was applied by something other than a player. |
| Level | `{Level}` | The level of the card's target. |
| Line Break | `{NewLine}` | A line break. |

Parentheses left with nothing inside are removed. The same text is used for all cards.

### Shortcuts

The rows shown at the top of the drop-down list in the card's name field. Turn them on and off with the switches, and reorder the rows by dragging. The defaults are the following seven rows.

| Row | Default |
|---|---|
| Cuisine | On |
| Potion | On |
| Time Stasis | On |
| Mechanical Failure | On |
| Element Stasis | On |
| Exhausted: Flame Devour | On |
| Weakened: Wish Sealed | Off |

You can add one with "Add to Buff & Debuff Card Shortcuts" on the right-click menu of a row in your own Buff List or Debuff List or of your own card. Rows you added are removed when you turn them off and save.

### Notification Targets

The buffs and debuffs whose expiry is notified by "Buff & Debuff expired" in [Notifications](Notifications) while the card is open. The default rows and their on/off states are the same as in Shortcuts. You can add one with "Add This Buff to Notification Targets" on the right-click menu of a card. All cards use the same list.

## Notes

- A card for another player shows only the name line while that person is not around you. A card for an enemy shows only the name line when that enemy is defeated or moves away.
- The Hide List of the Buff List does not apply to cards.
- Even while a history entry is open, the current buffs are shown.
- The buffs are read again every second.
- Cards that were running when you closed the app open again with the same target and buff the next time you start the app. A card for an enemy shows that enemy when an enemy of the same type appears.
