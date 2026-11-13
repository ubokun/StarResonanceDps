![Widgets](images/en/Widgets1.png)

This tab lets you start, stop, and pin the windows (widgets) that are shown over the game screen. This screen appears first when you open the app.

## What it shows

- Top row: From the left, the search box, the sort menu, and the three-dot menu.
- Cards: There is one card per widget. The circle on the left shows the status, and the buttons on the right are, from the left, favorite, pin, click-through, settings, and start/stop.

| Circle color | Status |
|---|---|
| Gray | The window is not open (stopped). |
| Green | The window is open, and no missing information has been found. |
| Yellow | The window is open, and some information is missing. |
| Red | The window is open, and it cannot receive information until you change maps or log in again (it is also red when data cannot be read). |

- When two or more windows are open, the worst color is shown. You can check the reason with the circle on the left of each window's title ([Common Widget Settings](Common-Widget-Settings)).
- For widgets that open a window per target (such as Player Info), the number of open windows appears instead of the circle when one or more windows are open.
- For widgets that open a window per target, the name field becomes a drop-down list for choosing the target when there are targets to choose from. The field shows the title of the window that opens for the selected target.
- Card order ("Default"): [Player List](Player-List), [Entity List](Entity-List), [Player Info](Player-Info), [Entity Info](Entity-Info), [Status](Status), [Equipment Info](Equipment-Info), [Talent Info](Talent-Info), [Deep-Slumber Psychoscope Info](Deep-Slumber-Psychoscope-Info), [Module Info](Module-Info), [Buff List](Buff-List), [Debuff List](Debuff-List), [Buff & Debuff Card](Buff-&-Debuff-Card), [Buff & Debuff Group](Buff-&-Debuff-Group), [Revive List](Revive-List), [Timeline](Timeline), [DPS Meter](DPS-Meter), [HPS Meter](HPS-Meter), [Threat Meter](Threat-Meter), [Break Meter](Break-Meter), [Damage Taken Log](Damage-Taken-Log), [Skill Details (Damage)](Skill-Details-(Damage)), [Damage Details](Damage-Details), [DPS Trend Graph](DPS-Trend-Graph), [Skill Details (Healing)](Skill-Details-(Healing)), [Healing Details](Healing-Details), [HPS Trend Graph](HPS-Trend-Graph), [Skill Details (Threat)](Skill-Details-(Threat)), [TPS Trend Graph](TPS-Trend-Graph), [Skill Details (Break)](Skill-Details-(Break)), [BPS Trend Graph](BPS-Trend-Graph), [Damage Taken Details](Damage-Taken-Details), [Chat](Chat). Widgets added by plugins come after these.

## Controls

- Search box: Shows only the cards whose name field contains the text you enter. It is not case-sensitive.
- Sort menu: "Default", "Name", "Favorite", "Status", "Pinned", or "Click-Through" (default: "Default"). With "Favorite" through "Click-Through", the cards that have that setting come to the top.
- Three-dot menu: "Start favorites", "Stop all", "Pin running", "Unpin all", "Make pinned click-through", and "Disable all click-through".
- Card buttons
  - Favorite: Adds the widget to your favorites. Favorite widgets are the ones started together by "Start favorites" in the three-dot menu.
  - Pin: The window can no longer be moved or resized. Clicking the window does not make it active, so it does not interfere with playing the game. By default, the header is hidden while the window is pinned.
  - Click-through: The window does not take mouse input and passes it straight to the screen below (such as the game).
  - Settings: Opens the settings window of the widget.
  - Start/stop: Opens the window, or closes it if it is open. For widgets that open a window per target, only the window for the selected target is opened or closed.
- Drop-down list in the name field: The target you choose appears in the field. The window opens when you click the start button.

## Notes

- Favorite, pin, and click-through are saved. The search text, the sort order, and the target chosen in the drop-down list are not saved.
- Widgets that were running when you closed the app open again the next time you start the app. Widgets that you closed with the window's × or the stop button are stopped.
- In a click-through window, the window's buttons and scrolling cannot be used either. To turn it off, use the card button, the three-dot menu, or [Hotkeys](Hotkeys).
- The three-dot menu actions can also be performed with [Hotkeys](Hotkeys) (defaults: Ctrl+F8, Ctrl+F7, and Ctrl+F6). When hotkeys are available, the key is shown after each menu item.
- You can choose whether windows are always on top or only pinned windows are on top with "Widget Windows" in [Settings](Settings).
- How to move and resize windows and the common items of settings windows are described in [Common Widget Settings](Common-Widget-Settings).
