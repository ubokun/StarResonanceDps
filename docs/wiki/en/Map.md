![Map](images/en/Map1.png)

This widget shows where you, the players and enemies around you, and your party members are on the map you are on.

## How to open

- Click the start button on the "Map" card in "[Widgets](Widgets)". There is only one window.
- When the location in [Player Info](Player-Info) is underlined, click it to open the window centered on that player. For a party member on another map, that map is shown.
- Click a shared position link in [Chat](Chat) to open the window and place a position marker at that spot.

## What it shows

- Map: The map you are on, centered on you. When you change maps, it switches to the map you moved to. On maps with layers, the map of the layer you are on is shown. On maps without a map image, only the icons are shown.
- Players: Shown with their class icons. Players in combat are shown as small circles with a white border. You are shown as a triangle with a white border that points in the direction you are facing ("Simplify Players In Combat" in the display settings). With "Always Simplify Self" in the display settings, you are shown as the triangle even when you are not in combat.
- Enemies: Bosses are shown with the boss icon, and other enemies as small circles without a border. All of them use the color of "Entity" in the icon colors.
- Points: In dungeons where you collect points through the spatial investigation, the enemy's points (such as "60pt") are shown above the circles of enemies that are not in combat. A boss's points are shown in the center of its icon.
- Names: Shown below the icons. Choose whose names are shown in the display settings. Hover over an icon to see the name in a tooltip, even when the name is not shown.
- Markers: The markers 1 to 6 that the captain places in dungeons ("Marker 1" to "Marker 6") are shown with the same images as the game, regardless of the layer.
- Position marker: Shown at a spot shared with a link in chat.
- Footer: The left shows the name of the map being shown (with the channel in the form "ch12" if there is one), and the right shows the number of "Entities" and "Players". The numbers count those on that map after the display settings filter them, including those outside the window. While you are collecting points through the spatial investigation, it changes to "In Combat" (the total points of the enemies in combat) and "Current" (the current points).

![While collecting points through the spatial investigation](images/en/Map2.png)

## Controls

- Mouse wheel: Zooms the map in and out. At the closest zoom, the shorter side of the window covers 20m, and at the farthest zoom, the whole map fits. It opens at 200m, and the zoom is not saved.
- Drag the map to change the spot that is shown (only in the directions in which the map extends beyond the window; on maps without a map image, in any direction). When you move, it returns to centering on you.
- Double-click where there is no icon to center on you right away.
- Click a player's icon to open that player's [Player Info](Player-Info). Click an enemy's icon to show that enemy in [Target Info](Target-Info).
- While another map, or another layer shared with a link, is shown, "Back to My Map" appears in the header. Click it to return to the map and layer you are on.
- Right-click to choose "Back to My Map" (while another map or layer is shown) and "Clear Position Marker" (when there is a position marker).
- While another map or layer shared with a link is shown, the map keeps showing that spot even when you move, drag the map, or double-click. It returns only when you click "Back to My Map" or change maps.

## Settings

The window settings and theme are described in [Common Widget Settings](Common-Widget-Settings).

### Display

- Players: Choose which players are shown from "Show all", "Party members only", "Non-party members", and "Self only" (default: "Show all").
- Entities: Choose from "Show all" and "Hide objects". "Hide objects" does not show enemies whose HP bar is not visible in the game (default: "Hide objects").
- Player Names: Choose whose names are shown from "Show all", "Show Party Members Only", "Show Non-Party Members", and "Show Self Only". Players whose names are not shown have only their icons (default: "Show Non-Party Members").
- Entity Names: Choose "Show" or "Hide" for the enemies' names (default: "Hide").
- Hide Names In Combat: Choose whose names are not shown while they are in combat from "All", "Players Only", "Entities Only", and "Don't Hide" (default: "All").
- Simplify Players In Combat: Shows players in combat as small circles with a white border (a triangle for you). When it is off, players in combat are also shown with their class icons (default: on).
- Always Simplify Self: Shows you as the triangle with a white border even when you are not in combat (default: on).
- Map Opacity: Choose the opacity of the map image from 0 to 100%. The map image is shown at this opacity regardless of the window opacity (default: 100%).

### Icon Colors

"Entity" in the first row is the color of the enemies' icons and circles (the default is the orange in the second slot). The class rows that follow (including transformations) and "Unknown" (players whose class cannot be determined) are the colors of the players' icons and circles.

## Notes

- Players and enemies around you are shown only when they are near you (within about 90m).
- Party members are shown even when they are far away, as long as they are on the same map. Whether they are in combat can be determined only while they are near you, so party members who are far away are always shown as not in combat.
- Markers are shown only while the player who placed them is near you. They disappear when you change maps.
- The position marker disappears when you click a new link, choose "Clear Position Marker", or log out. It does not disappear when you change maps.
- If you start the app while the game is already running, nothing may be shown until you move.
- Even while a history entry is open, the current positions are shown.
