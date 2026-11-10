![Talent Info](images/en/PlayerTalent1.png)

This widget draws one player's talent tree in the same shape as the game's talent screen and colors the talents they have taken.

## How to open

- On the "Talent Info" card in "[Widgets](Widgets)", choose the target from the drop-down list in the name field and click the start button. The default target is yourself.
- You can also open it by right-clicking a row in the [Player List](Player-List) or the [Revive List](Revive-List) and choosing "Talent Info".
- A window is opened per target. If a window for the same target is already open, that window is brought to the front.

## What it shows

- Title: "Talent Info - player name".
- Tree shown: If the class spec is known, the tree of that class spec. For people who have no talents equipped (Class I), the Class I tree of their class. Nothing is shown when the class spec is not known or while the player is transformed.
- Top node: The class spec (such as Block Spec).
- Colored nodes: Talents that have been taken. The color shows the role: blue for Guard, green for Support, and red for Attack.
- Gray nodes: Talents for which it is not known whether they have been taken.
- Small dots: Stat nodes and talents that have not been taken.
- The tree can differ between game servers, and the tree of the server you are connected to is shown.

## Controls

- Hover over a node to see the talent's name in a tooltip (the name followed by "(Activation unknown)" for gray nodes). Clicking does not close it.
- If the tree does not fit in the window, scroll with the mouse wheel or the scroll bars on the right and bottom.

## Settings

There are only the window settings and theme ([Common Widget Settings](Common-Widget-Settings)).

## Notes

- What is known differs between yourself and other players. The talents that other players have taken are not sent by the game, so they are determined from the effects that the talents apply and the skills the person used.

| | Yourself | Other players |
|---|---|---|
| Talents taken | All known | Only talents that apply an effect, and talents that replace a skill (determined from the skill the person used last); those that cannot be determined are gray |

- The talents you have taken arrive when you log in, change maps, or change talents. If you start the app while the game is already running, they are determined the same way as for other players until you change maps.
- Other players' data arrives while they are around you. It does not arrive through party information.
- For people who have left the Player List, the last known values remain. They are cleared when you log out.
- While a history entry is open, the values from that time are shown for the people who were in that battle.
- At the default size, the tree does not fit vertically. Make the window taller or scroll to see it.
