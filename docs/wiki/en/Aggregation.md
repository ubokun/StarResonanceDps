![Aggregation](images/en/Aggregation1.png)

This tab lets you split the combat aggregation or aggregate for a set length of time. You can also open saved battles (history) and review them later.

## What it shows

- Top left: The current state.

| Display | State |
|---|---|
| Aggregating | The current battle is being aggregated. |
| Benchmarking | A benchmark is running (from when you click "Benchmark" until you click "Stop Benchmark"). |
| Viewing History | A battle from the history is open. |

- "Benchmark" and "Reset": Actions on the current battle. While a benchmark is running, "Benchmark" changes to "Stop Benchmark".
- History list: Saved battles. The newest is at the top, and each row shows the date and time the battle started and the location. Fields have the channel (such as ch3), and dungeons have the difficulty.
- "(Benchmark)": Marks a battle recorded by a benchmark.
- "Viewing" and "Loading": Mark the battle that is open and the battle that is being loaded.

## Controls

- "Reset": Splits the current aggregation and starts aggregating again. The split aggregation is saved to the history.
- "Benchmark": Aggregates for a set length of time.
  - When you click it, the aggregation is split and the app waits. From the moment you deal damage or your own healing increases HP (excluding healing potions and natural recovery), it aggregates for the set length of time (default: 180 seconds).
  - While a benchmark is running, only your own data is aggregated. Other players do not appear in the meters and other widgets.
  - By default, only damage to the first enemy you attack is aggregated.
  - When the time is up, aggregation stops and "[Benchmark Complete]" appears in the footer of the DPS Meter and HPS Meter. The result remains until you click "Stop Benchmark".
  - When you click "Stop Benchmark", the benchmark ends, and the record is saved to the history with the "(Benchmark)" mark. It also ends if you change maps or log out after the benchmark has started. Before it starts, it keeps waiting even if you change maps.
  - If you click "Reset" during a benchmark, the benchmark ends if it has started, or waits again if it has not started.
- History rows: Click a row to open that battle, and click it again to return to the current battle.

![When a history entry is open](images/en/Aggregation2.png)

- When a history entry is open, the following widgets show that battle: [DPS Meter](DPS-Meter), [HPS Meter](HPS-Meter), [Timeline](Timeline), [Damage Taken Log](Damage-Taken-Log), [Skill Details (Damage)](Skill-Details-(Damage)), [Skill Details (Healing)](Skill-Details-(Healing)), [Damage Details](Damage-Details), [Healing Details](Healing-Details), [Damage Taken Details](Damage-Taken-Details), [DPS Trend Graph](DPS-Trend-Graph), and [HPS Trend Graph](HPS-Trend-Graph).
- The players who were in that battle are added to the [Player List](Player-List) as gray rows.
- While a history entry is open, the app automatically returns to the current battle when damage, healing, or a hit taken occurs in the current battle, or when the aggregation is split.

## Notes

- "Benchmark" and "Reset" can also be used from the header of the DPS Meter and HPS Meter and with [Hotkeys](Hotkeys) (defaults: Ctrl+F10 for Benchmark and Ctrl+F9 for Reset). It is the same action wherever you use it.
- Only battles with records such as damage, healing, or hits taken are saved to the history. When the number of entries that can be saved (default: 99) is exceeded, the oldest are deleted first.
- Even when a history entry is open, widgets that show the current state, such as the Buff List and Entity List, stay as they are.
- The benchmark duration, whether to aggregate only the first enemy attacked, whether to exit the history view automatically, and the number of entries that can be saved can be changed in "Aggregation" in [Settings](Settings).
- If a history entry cannot be opened, the "History load error" window appears. The data for that history entry was not found or is damaged.
