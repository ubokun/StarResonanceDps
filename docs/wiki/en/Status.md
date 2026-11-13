![Status](images/en/PlayerStatus1.png)

This widget lists your own stats in one column. Only your own stats are shown, because the game does not send the stats of other players.

## How to open

- Click the start button on the "Status" card in "[Widgets](Widgets)".
- You can also open it by right-clicking your own row in the [Player List](Player-List) or the [Revive List](Revive-List) and choosing "Status".
- There is only one window.

## What it shows

- Row: From the left, the icon, the item name, and the value. Items that have both a percentage and a raw value are shown in the form "12.4%(3820)".
- When a value changes, the value is shown in the "Value Increase" or "Value Decrease" color for 2 seconds, and ▲ or ▼ appears to the left of the value. This does not happen for changes caused by changing maps.
- For sets that are used differently by class, such as Strength, Intellect, and Agility, or ATK and MATK, only the one your class uses is shown (the same applies to penetration and refined attack).
- Attack SPD and Cast Speed are both shown for every class. Which one applies is decided per skill rather than per class, and some classes use both.
- Move Speed is shown on one line if walking, running, and sprinting have the same value, and on three lines if they differ.
- Values that have not arrived are 0 (because the game does not send stats whose value is 0).

## Controls

- Drag a row to reorder it. Sets that are used differently by class move together. If the settings window is closed, the order is saved immediately; if it is open, the order is confirmed with "Save" in the settings window.
- Hovering over a row highlights it. Clicking does nothing.
- When the window is small, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Hide Inactive Status Effects: Does not show the rows whose value is 0 among PHY Boost, MAG Boost, the all-element and element bonuses, and the all-element and element resistances (default: on).
- Show Actual Values: Shows the raw value in parentheses for items that have both a percentage and a raw value. When it is off, only the percentage is shown (default: on).

### Status

Use the switches to choose the items to show, and drag them to reorder. The order you set on the widget is also reflected in this list. "Reset" also returns the order to the default.

Items that are on by default: PHY Boost, MAG Boost, the element bonuses, the element resistances, ATK and MATK, Strength, Intellect, and Agility, Crit, Crit DMG, Haste, Attack SPD and Cast Speed, Luck, Lucky Strike DMG Multiplier, Mastery, Versatility, Versatility Enhancement, Versatility Resistance, Block, and Block DMG Reduction.

### Text Colors

- Value Increase and Value Decrease: The colors of the value and ▲▼ when a value changes.
- The following rows, one per item: The colors of the icon and item name. The value stays white. The order is the same as in "Status".

## Notes

- If you start the app while the game is already running, values that have not arrived show "Unknown" until you change maps, and the circle in the window title turns red.
- Effects that do not arrive as stats, such as the cuisine effects on damage to and from Elite and above targets, are not shown.
- Even while a history entry is open, the current values are shown.
- The height of the window changes to fit the number of rows (up to the bottom of the screen). A scroll bar appears only when you make the window smaller by hand.
