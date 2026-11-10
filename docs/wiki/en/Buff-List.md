![Buff List](images/en/BuffList1.png)

This widget lists the buffs on one player or one enemy, one per row, with the remaining time and a gauge.

## How to open

- On the "Buff List" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The list shows yourself, party members, the other people in the Player List, and then the enemies in the [Entity List](Entity-List) (the default is yourself).
- You can also open it by right-clicking a row in the [Player List](Player-List) or the [Revive List](Revive-List) and choosing "Buff List".
- If you right-click a row in the [Entity List](Entity-List) and choose "Buff List", the window for that enemy opens. It also opens when you click a buff slot in [Entity Info](Entity-Info).
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

- Title: "Buff List - target name".
- Row: From the left, the buff icon, the name ("Buff Name" in the display settings), and the remaining time. When there are 2 or more stacks, the number is shown over the icon. Buffs whose icon file is missing show a question mark.
- Remaining time: In the form "12s", rounded up to the second. Buffs whose remaining time is not known show "??s", and buffs with no duration show nothing.
- Gauge: While the remaining time is longer than "Gauge length" in the display settings, the gauge is full; once it becomes shorter, the gauge shrinks with the remaining time. For buffs whose remaining time is not known and buffs with no duration, the gauge stays full.
- Name: For cuisine and potions, it is the name of the cuisine or potion that applies the buff. For buffs applied by a player other than the target, the name of the player who applied it is added after the buff name (for buffs applied by a summon, the name of the player who summoned it).
- Buffs shown: The buffs shown in the buff area of the game screen, plus buffs that the app adds (such as equipment effects and factor effects). Effects that are always active, such as talents, the Deep-Slumber Psychoscope, and modules, are shown in [Talent Info](Talent-Info), [Deep-Slumber Psychoscope Info](Deep-Slumber-Psychoscope-Info), and [Module Info](Module-Info).
- Order: Buffs turned on in the Priority List come first (in the order of the list), and the rest follow in the order they were applied (newest at the bottom).

## Controls

- Click a row to open the [Buff & Debuff Card](Buff-&-Debuff-Card) for that buff. Cuisine and potion rows open the cuisine or potion card.
- Right-click a row to choose one of the following items.
  - "Buff Card": The same as clicking the row.
  - "Prioritize This Buff": Adds the buff to the Priority List. While it is prioritized, this changes to "Stop Prioritizing This Buff".
  - "Hide This Buff": Adds the buff to the Hide List.
  - "Add to Group": Adds the buff to the [Buff & Debuff Group](Buff-&-Debuff-Group). It appears only in your own window, for buffs that are not in the group yet.
  - "Add to Buff & Debuff Card Shortcuts": Adds the buff to the shortcuts of the Buff & Debuff Card. It appears under the same conditions as "Add to Group".
- Changes made with the right-click menu are saved immediately.
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Buff Name: Write what appears as the row name. Choose an item in the drop-down list and click "Insert" to insert that text (default: `{BuffName}({CasterName})`).

| Item | Text | What appears |
|---|---|---|
| Buff Name | `{BuffName}` | The buff's name. |
| Target Name | `{TargetName}` | The name of the window's target. |
| Caster Player Name | `{CasterName}` | The name of the player who applied the buff. Empty when it was applied by something other than a player, and when it is not shown by "Caster Player Name" below. |
| Level | `{Level}` | The level of the window's target. |

- Gauge length: Choose from "Auto", "10s", "20s", and "30s". "Auto" uses the buff's duration. When the buff is extended, the gauge length grows together with the remaining time (default: "10s").
- Same Buffs: Chooses whether to combine the same buff applied by several players into one row. The choices are "Don't Group", "Group Players Only", "Group Entities Only", and "Group All". "Group Players Only" groups only in player windows, and "Group Entities Only" groups only in enemy windows. A grouped row shows the one with the longest remaining time. Rows that are not grouped are shown one per player who applied them (default: "Group Players Only").
- Caster Player Name: Choose from "Show", "Hide", and "Applied by Others". "Applied by Others" does not add a name to buffs that the target applied to themselves (default: "Applied by Others").

Parentheses left with nothing inside are removed.

### Priority List

The buffs that are shown first. Turn them on and off with the switches, and reorder the rows by dragging. The defaults are "Lower CD", "Mechanical Power", and "Highland Blood", all turned off. You can add one with "Prioritize This Buff" on the right-click menu of a row. Rows you added are removed when you turn them off and save.

### Hide List

The buffs that are not shown. Add one with "Hide This Buff" on the right-click menu of a row. If you turn its switch off and save, it is removed from the list and is shown again.

### Buff Colors

The colors of the left and right sides of the gauge (it changes from left to right) and the Buff Colors Opacity (default: 100%).

## Notes

- Other players' buffs are shown only while they are around you. Even for party members, the window is empty if they are not around you.
- An enemy window follows the one enemy it was opened for. When that enemy is defeated or moves away, the window becomes empty.
- When you change maps, the window becomes empty once and returns when the game applies the buffs again. In Homestead Courtyard, the game does not apply buffs other than cuisine and potions.
- The buffs of people and enemies that were around you before the app started may be incomplete until you approach them again (the circle in the window title is yellow). If you start the app while the game is already running, your own buffs may be incomplete until you change maps (the circle is red).
- Even while a history entry is open, the current buffs are shown.
- The buffs are read again every second.
