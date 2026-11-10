![Window](images/en/WidgetSettings1.png)

These are the window rules and the settings window shared by all widgets. The settings of each widget are on its own page.

## Widget windows

- Header: The top row of the window. From the left, it has the status circle, the title, and the close button (×). Some widgets have action buttons (such as "Benchmark" in the DPS Meter) to the left of the close button.
- Footer: The bottom row of the window. The Player List, Entity List, DPS Meter, HPS Meter, and Damage Taken Log have one.
- To move a window: Drag it by the area around the title in the header.
- To resize a window: Drag its edge.
- The position and size are saved, and the window appears in the same place the next time it opens. The size of Player Info and the height of Equipment Info are not saved; they open at the default size every time.
- In Status, Buff & Debuff Group, Damage Details, Healing Details, Damage Taken Details, and Entity Info, the height of the window changes to fit the content when the number of rows changes.
- Click the close button to close that window. A widget whose windows are all closed is stopped.

## Status circle

The circle on the left of the window title. Its color shows whether the information the widget shows is complete.

| Color | Status |
|---|---|
| Green | No missing information has been found. |
| Yellow | Some information is missing. It is fixed by opening the party screen, clicking the player, approaching again, and so on. |
| Red | Information cannot be received until you change maps or log in again (it is also red when data cannot be read). |

When it is yellow or red, hover over the circle or the title to see the reason.

| Reason | Color |
|---|---|
| Cannot read the data | Red |
| Cannot receive information until you change maps or log in again | Red |
| Cannot receive past chat until you log in again | Yellow |
| Cannot receive some information until you open the party screen | Yellow |
| Cannot receive some information until you click the player | Yellow |
| Cannot receive some information until you approach the player again | Yellow |
| Cannot receive some information until you approach the enemy again | Yellow |

- You can choose whether to show the circle with "Widget Status" in [Settings](Settings).
- If you start the app while the game is already running, the circle may be red or yellow until you change maps.

## Pin and click-through

Both are switched with the card buttons in "[Widgets](Widgets)" and apply to all windows of that widget.

- Pin: The window can no longer be moved or resized. Clicking the window does not make it active, so it does not interfere with playing the game. You can hide the header and footer with "Window" below.
- Click-through: The window does not take mouse input and passes it straight to the screen below (such as the game). The window's buttons and scrolling cannot be used either.
- On top: By default, widget windows are always on top. If you select "On Top Only While Pinned" in "Widget Windows" in Settings, only pinned windows are on top.

## Settings window

Open it with the settings button (gear) on the card in "Widgets".

- Click an item in the list on the left to scroll to that section. The sections differ by widget, and every widget has "Window" and "Theme".
- Changes appear in the widget's windows immediately. Settings are per widget and apply to all windows of the same widget.
- "Save" confirms the changes and closes the window. If you close the window without saving, a confirmation window appears, and "Yes" reverts the changes.
- If you click "Reset" and then "Yes" in the confirmation window, all items in this window return to their initial values. The change is confirmed when you click "Save".

## Window

- Hide Header While Pinned: While the window is pinned, hides the header and passes clicks in that area to the screen below (default: on).
- Hide Footer While Pinned: Only for widgets that have a footer. While the window is pinned, hides the footer in the same way (default: off).
- Window Opacity: How dark the window background is. Text, numbers, bars, and icons do not become lighter. While you click the window to make it active, it is shown at 50% even if the setting is lower than 50% (default: 50%).

![Theme](images/en/WidgetSettings2.png)

## Theme

- Window Color: The color of the window background. The card in "Widgets" also has this color (default: black).
- Background Image: Shows an image as the window background. Choose it with "Select Image". While an image is shown, no window color is shown as selected. Choosing a color removes the image (default: none).

## Choosing colors

The window color and color sections such as Class Colors all work the same way.

- Click one of the color circles shown to use that color.
- Click "Select color" to open the color picker. A color chosen with "OK" is added to the right of the row (up to 5; beyond that, the leftmost color is removed).
- In the color picker, choose brightness and saturation in the square and the hue with the vertical bar. You can also enter a six-digit color code starting with #. You can also choose from "Recent colors".
