This page explains how to read game data with a plugin. What every plugin needs is described in [Plugin Development](Plugin-Development).

| `IPluginContext` member | Contents |
|---|---|
| `GameData` (`IPluginGameDataService`) | Values the app builds from the game's traffic (the connection and map, players, nearby enemies, buffs, battle statistics, and your own modules) and events when they change |
| `Names` (`IPluginNameService`) | Looks up names from the IDs of skills, buffs, monsters, class specs, maps, and items |
| `Packets` (`IPluginPacketService`) | The game messages themselves |

## Game data (`GameData`)

The following example writes everyone's total damage to the log when a battle ends.

```csharp
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(MyPlugin.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "私のプラグイン")]
[assembly: PluginAuthor("YourName")]
[assembly: PluginVersion("1.0.0")]

namespace MyPlugin;

public sealed class Plugin : IStarResonancePlugin
{
    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        context.GameData.EncounterClosed += OnEncounterClosed;
    }

    private void OnEncounterClosed(object? sender, PluginEncounterResult result)
    {
        foreach (PluginEncounterPlayer player in result.Totals.Players)
        {
            _context?.Logger.Info($"{player.CharacterId}: {player.Damage.Total}");
        }
    }

    public void Shutdown()
    {
        if (_context is not null)
        {
            _context.GameData.EncounterClosed -= OnEncounterClosed;
        }
    }
}
```

| Member | Contents | Event when it changes |
|---|---|---|
| `CurrentServer` | The game server currently being read (`Asia`, `China`, `Global`, or `SoutheastAsia` of `PluginGameServer`). `Unknown` if it is not known | None |
| `Session` | The state of the connection and map (`PluginSessionState`) | `SessionChanged` |
| `Players` | The list of players (`PluginPlayer`) | `PlayersChanged` |
| `Entities` | The list of nearby monsters (`PluginEntity`) | `EntitiesChanged` |
| `SelfModules` | Your owned modules and equipped slots (`PluginModuleInventory`). `null` if they have not arrived yet | `SelfModulesChanged` |
| `GetPlayerBuffs(characterId)` | The buffs and debuffs currently on that player (`PluginBuff`) | None (read again) |
| `GetEntityBuffs(entityUuid)` | The buffs and debuffs currently on that entity | None (read again) |
| `GetEncounter()` | The statistics of everyone in the battle shown in "[DPS Meter](DPS-Meter)" (`PluginEncounterSnapshot`) | None (read again) |
| `GetSkillTotals(characterId, kind)` | That player's breakdown by skill in the same battle (`PluginSkillTotals`). `kind` is damage (`Damage`) or healing (`Healing`) | None (read again) |

| Event | When it arrives |
|---|---|
| `EncounterStarted` | When the app creates a new battle |
| `EncounterClosed` | When a battle with records is closed. The statistics of the closed battle and everyone's breakdown by skill (`PluginEncounterResult`) are passed |
| `MapEntered` | When you move to another map or channel |
| `LoggedOut` | When the game returns to the login screen |

- The values can be read from any thread. All events arrive on the UI thread.
- `SessionChanged`, `PlayersChanged`, `EntitiesChanged`, and `SelfModulesChanged` combine changes within a short time into one and report the values at that time. The other four arrive every time they happen.
- The values are read-only copies and are not updated later. Read again to get new values. If you read two values one after another, they may be from different moments.
- `Players`, `Entities`, `SelfModules`, and the result of `EncounterClosed` are the same objects passed to every plugin. Do not cast `IReadOnlyList` and similar types to their original types to modify them.
- Event handlers run on the UI thread, the same thread as the app's screen. Heavy work also stops the widgets.
- An exception in an event handler stops the plugin.

### Connection and map (`Session`)

| Property | Contents |
|---|---|
| `SelfCharacterId` | Your character ID. `null` until it is known and after logging out |
| `SceneId` | The ID of the current map. Pass it to `Names.GetSceneName` |
| `SeasonId` | The ID of the current season |
| `DungeonState` | Outside a dungeon (`NotInDungeon`), inside (`InDungeon`), or after it ends (`Finished`) |
| `IsMidLaunch` | The app started reading the game's traffic partway through, and your information is not complete yet (such as when the app is started while the game is running). It becomes `false` when you change maps |

- If the app is started while the game is running, your character ID and owned modules may not be known until you change maps.

### Players (`Players`)

- It includes yourself, your party members, the players around you, and the people with records in the battle shown in DPS Meter. It is the underlying list of "[Player List](Player-List)", without the Player List filter or sorting by name.
- The order is yourself, your party members (in party number order), people who are neither around you nor in your party, people in combat, and then everyone else.
- Names follow "Player name" in Settings. Hidden names are `*****`. A name may be empty when it is not known.

| Property | Contents |
|---|---|
| `CharacterId` | The character ID. It is the real value even when names are hidden |
| `Name` | The name |
| `ProfessionId` | The class ID. 0 if it is not known |
| `SpecState`, `SpecId` | The class spec state (`Unknown`, `NotEquipped` (Class I), `Specialized`, or `Transformation`). `SpecId` holds an ID only when it is `Specialized` (pass it to `Names.GetClassSpecName`) |
| `Level`, `AbilityScore`, `SeasonStrength` | The level, ability score, and season strength. 0 if they are not known |
| `CurrentHp`, `MaxHp`, `CurrentShield` | The amounts of HP and shield. The shield is known only for yourself and the people around you |
| `PartyNumber` | The party number. `null` outside the party |
| `IsSelf`, `IsNpc`, `IsPartyMember`, `IsNearby` | Whether this is you, a helper NPC, a party member, or someone around you |
| `IsInCombat`, `IsIncapacitated` | In combat, and defeated. The combat state is known only for yourself and the people around you |
| `PsychoscopeRootBuffId`, `IsPsychoscopeInactive` | The Deep-Slumber Psychoscope type (the ID of its root buff), and whether it is not enabled |
| `IsFromRecord` | The person appears only in the opened history of Aggregation |

### Nearby enemies (`Entities`)

- It includes only nearby entities of the monster type. It is the underlying list of "[Entity List](Entity-List)", without the Entity List filter or sorting by name.
- The order is entities in combat first, then the rest, and within each, bosses, elites, normal enemies, and others.

| Property | Contents |
|---|---|
| `EntityUuid` | The UUID of the entity. Pass it to `GetEntityBuffs` |
| `MonsterId` | The monster ID (pass it to `Names.GetMonsterName`). For a monster that was around before the app started, it is `null` until the monster leaves your surroundings once and appears again |
| `CurrentHp`, `MaxHp` | HP |
| `IsInCombat` | The combat state |
| `Cast` | The skill being cast (`SkillId`, `StartedAtUtc`, and `TotalSeconds`). It is set when a skill with a cast time starts and remains until the skill changes next. It can remain after `TotalSeconds` has passed, so decide the end by time |
| `TargetCharacterId` | The character ID of the player being targeted. It is not necessarily the top of the aggro list |
| `HateRanking` | The aggro list (players only, highest first) |

### Buffs (`GetPlayerBuffs`, `GetEntityBuffs`)

- All the buffs currently applied are included. The filtering and grouping of what to show, as in "[Buff List](Buff-List)", are not applied.
- The lists are empty for people and enemies that are not around you. Everything is cleared when you change maps.
- Even when the history of Aggregation is open, these are the current values.

| Property | Contents |
|---|---|
| `BuffUuid` | The UUID of each buff |
| `BuffId` | The buff ID (pass it to `Names.GetBuffName`) |
| `Level`, `Layer` | The level and the layers (stack count) |
| `DurationSeconds` | The total duration (it grows when the buff is extended). `null` if there is no duration |
| `EndUtc` | The time the buff ends (UTC). `null` if there is no duration or the remaining time is not known |
| `IsRemainingUnknown` | The remaining time is not known |
| `CasterEntityUuid`, `CasterCharacterId` | The UUID of the entity that applied the buff (0 if not known), and its character ID if it was a player |

### Battle statistics (`GetEncounter`, `GetSkillTotals`)

- They read the battle shown in DPS Meter: the opened battle when the history of Aggregation is open, or the previous battle when it is shown because of "Hold Results Until Next Event" in Settings.

| `PluginEncounterSnapshot` property | Contents |
|---|---|
| `IsHistory` | The history of Aggregation is open |
| `StartUtc` | The time the battle clock started (UTC). `null` if it has not started yet |
| `Elapsed` | The elapsed time of the battle (excluding Auto Pause) |
| `IsClosed` | The battle has ended |
| `Players` | Players who have any damage, healing, or damage taken (`PluginEncounterPlayer`). Not filtered by party, and in no particular order |

- `PluginEncounterPlayer` has `CharacterId` and the totals (`PluginCombatTotals`) of damage (`Damage`), healing (`Healing`), and damage taken (`DamageTaken`).
- `PluginCombatTotals` has the total (`Total`), the per-second value (`PerSecond`, the same as DPS Meter), the per-second value over that player's active time (`ActivePerSecond`, the same as the skill details), the hit count (`Hits`), and the totals and counts of crits and lucky hits.
- `PluginSkillTotals` has the same rows as the skill details: the row key (`RowKey`), the row name (`Name`, `null` if there is none), the total, the per-second value, the hit count, the crit rate (`CritRate`), and the share of that player's total (`Share`). They are in order from the largest total.
- `EncounterStarted` arrives on a map change, at the start of a dungeon, at a split by "Auto Reset by Progress", on a manual reset, when a benchmark starts or stops, and on logout. It can arrive several times for one map change.
- There is no event when the battle clock starts. Read `GetEncounter().StartUtc`.
- `EncounterClosed` arrives when a battle with records is closed and the next battle is created. It does not arrive for the last battle when the app is closed. Keep the result you receive right away to have the values of the closed battle.

### Your modules (`SelfModules`)

- `Modules` holds your owned modules (`PluginModuleItem`: `Uuid`, the item ID `ConfigId`, `Quality`, and the effects `Effects`), and `EquippedUuidBySlot` holds the equipped slot numbers and the `Uuid` of each module.
- An effect (`PluginModuleEffect`) has the effect ID (`EffectId`) and the link count (`LinkCount`, `null` if it has not arrived).
- `SelfModulesChanged` arrives every time you enter a map, even if the contents are the same.
- After logging out, and after changing "Network Adapter" or "Game Executable" in Settings, it is `null` until you next change maps.

## Looking up names (`Names`)

| Method | Name it looks up |
|---|---|
| `GetSkillName(skillId)` | Skill |
| `GetBuffName(buffId)` | Buff |
| `GetMonsterName(monsterId)` | Monster |
| `GetClassSpecName(specId)` | Class spec (`PluginPlayer.SpecId`) |
| `GetSceneName(sceneId, dungeonDifficulty)` | Map. If there is a difficulty name, "map name-difficulty name". `dungeonDifficulty` can be omitted |
| `GetItemName(itemId)` | Item (including equipment) |

- The names are in the app's display language. If there is no name in that language, the Chinese name is returned, and `null` if there is neither.
- Unlike the app's screens, internal names and internal IDs are not added. "Internal ID" and "Unnamed skills and buffs" in Settings do not apply either.
- There are no names for Class I or transformations, so `GetClassSpecName` returns `null` for them.
- There are no methods to look up the names of classes, Deep-Slumber Psychoscope types, seasons, or module effects.
- They can be called from any thread. When the display language changes, look the names up again in `Localization.CultureChanged`.

## Game messages (`Packets`)

Pass the types to receive to `ReadAsync`, and read the arriving messages with `await foreach`.

```csharp
private static async Task ReadMessagesAsync(IPluginContext context, IReadOnlyCollection<PluginPacketType> types)
{
    await foreach (PluginPacketMessage message in context.Packets.ReadAsync(types).ConfigureAwait(false))
    {
        context.Logger.Info($"{message.Kind} {message.ServiceId}/{message.MethodId}: {message.Payload.Length} bytes");
    }
}
```

- Start it in `Initialize` without awaiting it, as in `_ = ReadMessagesAsync(context, types);`.
- A type to receive (`PluginPacketType`) is a combination of the kind (`Kind`), the service ID, and the method ID. Pass one or more. Only messages that match all three arrive.
- The kinds are notifications from the server (`Notify`), calls from the game to the server (`Call`), and responses to calls (`Return`). The IDs of a `Return` are those of the call it answers.
- Messages that arrive after `await foreach` starts are passed in the order they arrive. Earlier ones are not passed. Leaving the loop stops receiving.
- The contents (`Payload`) are protobuf bytes. `Notify` and `Return` are decompressed, and `Return` stays in the form that wraps the response. `Call` is not decompressed.
- `Payload` is a copy made by the app, so you can keep it as long as you like.
- If the total of contents not yet taken exceeds 64 MB for one plugin, the plugin is stopped and its row in "[Plugins](Plugins)" shows "Error" (the "Plugin Error" window appears, and the plugin is loaded again at the next startup). Narrow down the types you receive, and take what arrives right away.
- `Initialize` is called on the UI thread, so without `ConfigureAwait(false)` the body of the loop runs on the UI thread.
- When the app stops the plugin, the loop ends. If you did not pass `Stopping`, it ends without an exception; if you passed it, it ends with `OperationCanceledException`.
- This wiki does not describe the IDs and content formats of game messages.

## Rules for reading data

- Remove the events you subscribed to in the content of a window or in the body and footer of a widget in `Dispose`. If you do not, the content of a closed window keeps receiving events and stays in memory.
- Remove the events you subscribed to in `Initialize` in `Shutdown`. The app also removes them before `Shutdown`, so your handlers are not called after the plugin is stopped.
- Buffs and battle statistics have no change events, so read them again with a timer.

## What is not provided

- Equipment, attributes, talents, skill slots, cooldowns, dungeon objectives and scores, telegraphed skills, chat, and so on are not provided. If you need them, read the game messages.

## Samples

| Sample | What it shows |
|---|---|
| [SampleGameData](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleGameData) | Shows all the values and events of `GameData` in a window. Rereading every second, keeping the result of `EncounterClosed`, and removing events in `Dispose` |
| [SamplePackets](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SamplePackets) | A plugin without a window that reads game messages and writes their sizes to the log |
| [SampleNotification](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleNotification) | In `MapEntered`, looks up the map name from `Session.SceneId` with `GetSceneName` |
| [SampleWidget](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleWidget) | Builds the list of targets for a per-target widget from `Players` |
