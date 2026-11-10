![Revive List](images/en/ReviveList1.png)

This widget lists defeated players and shows the time remaining until they can be revived.

## How to open

Click the start button on the "Revive List" card in "[Widgets](Widgets)".

## What it shows

- Row: From the left, the class icon, the player name ("Player Name" in the display settings), and the state.
- State: "Revivable" for people who can be revived, the remaining seconds (such as "12s") for people who cannot be revived because they have the debuff "Weakened: Wish Sealed", and "??s" for people whose state is not known. While "Weakened: Wish Sealed" is applied, "Arcane! Blessing of Life" and the role skill "Renewal Prayer" cannot be cast on that person again.
- Rows with remaining seconds show a gauge that shrinks with the remaining time.
- Order: People who are revivable → in order of who can be revived soonest → people whose state is not known.
- In your own row, the name is shown in yellow ("Self highlight" in the display settings).

## Controls

- Click a row to open that person's [Player Info](Player-Info).
- Right-click a row to choose and open that person's "Player Info", "Status" (your own row only), "Equipment Info", "Talent Info", "Deep-Slumber Psychoscope Info", "Module Info", "Buff List", or "Debuff List", the same as in the [Player List](Player-List).
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Player Name: Write what appears in the name field. It is written the same way and has the same items as "Player Name" in the [Player List](Player-List) (default: `{PlayerName} - {Spec} ({PowerLevel}-{SeasonStrength})`).
- Self highlight: Shows the name in your own row in yellow (default: on).
- Gauge length: Choose from "Auto", "10s", "20s", and "30s". "Auto" uses the duration of "Weakened: Wish Sealed". While the remaining time is longer than this length, the gauge is full (default: "Auto").

### Debuff Colors

The colors of the left and right sides of the gauge and the Debuff Colors Opacity (default: 100%).

### Icon Colors

Choose the color of the class icon for each class (including transformations and unknown).

### Text Colors

The color of the "Revivable" text.

## Notes

- Whether a person is defeated is determined by whether their HP is 0 for people around you, and by party information for party members who are not around you.
- "Revivable" can be shown only when all of the person's buffs have arrived. Party members who are not around you and people who were around you before the app started show "??s".
- People who are neither around you nor in your party, and people who come from a history record, are not shown. Even while a history entry is open, the current state is shown.
