![ただのモジュールツール](images/en/ModuleTool1.png)

This is a bundled plugin that searches the modules you own for combinations of five that give a high ability score. You can search with effects and levels as conditions.

## How to open

In "[Plugins](Plugins)", click the "ただのモジュールツール" row. There is only one window, and if it is already open, it is brought to the front.

## What it shows

### Left column

- Owned Modules: The number of modules you own and the number equipped (in the form "13 modules (Equipped 5/5)"), the ability score of the equipped set, and the time they were last received.
  - If they have not been received yet, "Modules have not been loaded" appears. They are loaded when you change maps or log in again.
  - If there are modules whose link counts are not known or modules that are not supported, they are left out of the search and their number is shown.
- Conditions: The effects and levels that the combinations to search for must meet. The condition is the selected level or higher, and Lv1 means "the effect is included".
- Quality: The quality of the modules included in the search (Basic, Advanced, and Excellent). The buttons for qualities that are turned off have faded text.
- "Search" and "Reset": Buttons fixed at the bottom of the column. "Search" cannot be clicked before your owned modules have been received, or when there are no modules to include in the search (such as when all qualities are turned off).

### Results

- To the right of the heading: The number of combinations that meet the conditions, the number shown, and the seconds it took (in the form "Top 10 of 177 (0.012s)"; for 10 or fewer, only the number and the seconds). During a search, the heading changes to "Searching", and the progress and seconds appear.
- Below that: The conditions used for the search. If your owned modules change after the search, "(Your modules have changed. Please search again.)" is added.
- Cards: The top card is the set you have equipped now ("Equipped"), followed by "#1" to "#10" in order from the highest ability score.
  - First line: The ability score and its difference from the equipped set (green when it increases and red when it decreases), and on the right, "Effects" (the total link count of the five modules).
  - Second line: The total link count and level of each effect in the set. Lv5 is shown in blue and Lv6 in yellow. The effects in the conditions come first.
  - Third line: The five modules in the set. Modules you have equipped now have an "Equipped" band.
- When no combination meets the conditions, "0 found" and only the equipped card are shown.

## Controls

- Adding a condition: Choose an effect with "Add Effect" to add a row at Lv1. Choose the level with the row's drop-down list, and remove the row with "✕".
- Quality: Each click on a button switches it on or off.
- "Search": Click it to start searching. During a search, it changes to "Cancel", and clicking it stops the search and returns to the previous results.
- Even if you change the conditions or quality, the results do not change until you search again.
- "Reset": Removes all conditions and turns all qualities back on. The results do not change.
- Hover over a module image to see the module name and the link count of each effect in a tooltip.
- Scroll the left column and the results separately with the mouse wheel or their scroll bars.

## Settings

There is no settings window. The conditions and quality are saved each time you change them and remain the next time you open the window.

## Notes

- All your owned modules arrive when you log in and when you change maps. Changing, obtaining, and dismantling modules are reflected immediately. If you start the app while the game is already running, they cannot be loaded until you change maps.
- They are received even when the window is not open. Even after you log out, the last received modules remain shown. If you log in with another character, they change to that character's modules.
- The search goes through all combinations in order from the highest ability score. Modules with exactly the same effects and link counts are counted as one.
- The ability score is the value for the modules only, calculated the same way as in the game.
- When you have many modules, the search may take a few seconds.
- When you close the window, the search stops and the results are cleared. The window size and position are not saved.
- If the search fails, the "Search Error" window appears. Search again. If the settings file cannot be read, the "Settings load error" window appears, and the tool opens with the default conditions. If the settings cannot be saved, the "Settings Save Error" window appears.
- This plugin is updated together with the app. If you delete it, it does not come back even when you update the app ([Plugins](Plugins)).
