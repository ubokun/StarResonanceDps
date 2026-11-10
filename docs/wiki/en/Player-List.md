![Player List](images/en/PlayerList1.png)

This widget lists yourself, your party members, and the players around you, one per row. It shows their class, HP, and the state of their Imagines and role skills.

## How to open

Click the start button on the "Player List" card in "[Widgets](Widgets)".

## What it shows

- Who is listed: Yourself, your party members, and the players around you (including helper NPCs). People who appear in the records of the current battle (DPS Meter, HPS Meter, Timeline, and Damage Taken Log) remain even after they are no longer around you.
- Row: From the left, the class icon, the Deep-Slumber Psychoscope type, the player name and HP, and the skill slots.
  - Class icon: The class spec (such as Iaido Slash) appears below it. It shows "Class I" if the player has no talents equipped, "Unknown" until it is known, and the transformation name while the player is transformed.
  - Deep-Slumber Psychoscope type: The image at the lower right of the class icon. Only the frame is shown if it is not enabled, and a question mark until it is known.
  - Upper line: The party number (party members only), the combat state icon (only for people in combat), and the player name. Your own name is shown in yellow ("Self highlight" in the display settings).
  - Lower line: The HP bar. Green is HP, the light gray part on the right is the shield, and the numbers are "HP+shield/max HP" ("HP value" in the display settings). While the player is defeated and cannot be revived, it changes to "No Revive (seconds)".
  - Skill slots: Two Imagines and the role skills. They show the cooldown (seconds) after use and, for skills with charges, the remaining charges. The debuff applied by the skill appears at the upper right, and the buff icon and its remaining seconds at the lower right.
- Rows with a gray HP bar: People who are neither around you nor in your party. The values are from when they were last seen. The rows of helper NPCs also have a gray HP bar.
- Order: You come first, followed by the other party members in order of party number. After them come the people outside the party, in the order of people who are neither around you nor in your party, then people in combat, then everyone else.
- Footer: The left shows the current map (with the channel in the form "ch12" if there is one), and the right shows the number of people "In Combat" and "Nearby". "Nearby" counts the people around you (including yourself), and "In Combat" counts those of them who are in combat.

## Controls

- Click a row to open that person's [Player Info](Player-Info).
- Right-click a row to choose and open that person's "Player Info", "Status" (your own row only), "Equipment Info", "Talent Info", "Deep-Slumber Psychoscope Info", "Module Info", "Buff List", or "Debuff List".
- Hover to see a tooltip: the skill name for a skill slot, the type name for the Deep-Slumber Psychoscope type, "In Combat" for the combat state icon, and the name for buff and debuff icons.
- If the rows do not fit in the window, scroll with the mouse wheel or the scroll bar on the right.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Player Name: Write what appears in the name field. Choose an item in the drop-down list and click "Insert" to insert the text for that item. You can check it in the preview below the input field (default: `{PlayerName}({PowerLevel}-{SeasonStrength})`).

| Item | Text | What appears |
|---|---|---|
| Player Name | `{PlayerName}` | The name. `*****` when hidden with "Player name" in Settings. |
| Class Spec | `{Spec}` | The class spec (such as Iaido Slash). "Unknown" until it is known, and "Class I" if the player has no talents equipped. |
| Deep-Slumber Psychoscope | `{Psych}` | The Deep-Slumber Psychoscope type. "Not Enabled" if it is not enabled, and "Unknown" until it is known. |
| Ability Score | `{PowerLevel}` | The ability score. |
| Season Strength | `{SeasonStrength}` | The season strength. |
| Season Level | `{SeasonLevel}` | The season level. |
| Player UID | `{Uid}` | The player UID. |

- HP value: "Add shield amount" ("HP+shield/max HP") or "Show shield separately" ("HP(shield)/max HP"; the parentheses are not shown when there is no shield) (default: "Add shield amount").
- Filter settings: Choose who is shown from "Show all", "Party members only", "Non-party members", and "Self only" (default: "Show all"). "Party members only" includes yourself, and "Non-party members" does not.
- Sort Order: Choose the order of people outside the party from "Discovery Order" and "Name Order" (default: "Discovery Order").
- Self highlight: Shows the name in your own row in yellow (default: on).
- Show Deep-Slumber Psychoscope: Shows the Deep-Slumber Psychoscope type image. When it is off, the space reserved for the image is also removed (default: on).
- Combat State: Choose how the combat state icon is shown from "Show", "Hide", and "Show Self Only" (default: "Show").

### Role skills

Use the switches to choose the role skills shown in the rows of other players. Thunderfall Grasp, Infernal Thrust, Blessing of Life, and Renewal Prayer are on by default. This does not apply to your own row (for yourself, the four slots of your action bar are shown as they are).

### Icon Colors

Choose the colors of the class icons and the Deep-Slumber Psychoscope type images for each class (including transformations and unknown) and each type (including unknown).

## Notes

- For other players, the role skills shown are the ones they have learned, not the ones they have equipped, because the game does not send which role skills are equipped. For Imagines, the two equipped ones are shown.
- The cooldowns of other players are estimated from the time their skill use was seen. Your own cooldowns are the game's values.
- The combat state and shields are known only for yourself and the people around you.
- The class spec, Deep-Slumber Psychoscope, and skill grades of other players become known when they come near you. Until then, they show "Unknown", and the circle in the window title turns yellow.
- The HP of party members who are not around you arrives through party information, so it may change with a delay. Their cooldowns and buff and debuff icons are not shown.
- In a 5-player party, the party numbers follow the order in which members joined. In a 20-player party, if you start the app while the game is already running, the party numbers show "?" until you open the party screen.
- While a history entry is open, the people who were in that battle are added as rows with a gray HP bar (HP is shown at its maximum). The HP, skills, and buffs in the other rows stay at their current values.
