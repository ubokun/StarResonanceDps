namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Provides game state that only the host can receive from the game connection.
/// Values are read-only snapshots identified by game IDs.
///
/// <para>
/// Properties and methods can be read from any thread. Events are raised on the UI thread.
/// <see cref="SessionChanged"/>, <see cref="PlayersChanged"/>, <see cref="EntitiesChanged"/> and <see cref="SelfModulesChanged"/>
/// are combined into one call per UI update; the other events are raised once for each occurrence.
/// Buffs and encounter totals have no change event; read them again when needed, as the host's widgets do.
/// </para>
/// </summary>
public interface IPluginGameDataService
{
    /// <summary>
    /// The game server of the client currently being captured.
    /// </summary>
    PluginGameServer CurrentServer { get; }

    /// <summary>
    /// The local player's modules, or <c>null</c> while the host has not received them
    /// (for example, the capture started after login and no map change has happened yet).
    /// It is also <c>null</c> after logging out, and after the capture restarts because the network adapter or game executable setting changed,
    /// until the next map change.
    /// </summary>
    PluginModuleInventory? SelfModules { get; }

    /// <summary>
    /// Raised on the UI thread after <see cref="SelfModules"/> has been replaced: each time the player enters a map (even when nothing changed),
    /// when modules are obtained, dismantled or equipped, and when it becomes <c>null</c>.
    /// </summary>
    event EventHandler? SelfModulesChanged;

    /// <summary>
    /// The connection and map state.
    /// </summary>
    PluginSessionState Session { get; }

    /// <summary>
    /// Raised after <see cref="Session"/> has changed.
    /// </summary>
    event EventHandler? SessionChanged;

    /// <summary>
    /// The players of the list the Player List widget is built from: the local player, party members, nearby players,
    /// and the players with records (damage, healing, damage taken or timeline records) in the encounter the meters show.
    /// The widget's filter and name sorting are not applied. The order is the local player, party members (by party number),
    /// players who are neither nearby nor in the party, players in combat, and the others.
    /// </summary>
    IReadOnlyList<PluginPlayer> Players { get; }

    /// <summary>
    /// Raised after <see cref="Players"/> has changed (including HP changes).
    /// </summary>
    event EventHandler? PlayersChanged;

    /// <summary>
    /// The monsters around the player, from the list the Entity List widget is built from. The widget's filter and name sorting are not applied.
    /// Monsters in combat come first, then the others; within each group, bosses, elites, normal monsters and the rest.
    /// </summary>
    IReadOnlyList<PluginEntity> Entities { get; }

    /// <summary>
    /// Raised after <see cref="Entities"/> has changed (including HP changes).
    /// </summary>
    event EventHandler? EntitiesChanged;

    /// <summary>
    /// The buffs on a player, in the order they were applied. Empty when the host has no buffs for the player,
    /// and for another player who is not around.
    /// Always live, even while a history encounter is open.
    /// </summary>
    IReadOnlyList<PluginBuff> GetPlayerBuffs(long characterId);

    /// <summary>
    /// The buffs on an entity (for example <see cref="PluginEntity.EntityUuid"/>), in the order they were applied.
    /// Empty when the entity is no longer around.
    /// Always live, even while a history encounter is open.
    /// </summary>
    IReadOnlyList<PluginBuff> GetEntityBuffs(long entityUuid);

    /// <summary>
    /// The totals of the encounter the host's meters show: the opened history encounter, the previous encounter the meters keep showing,
    /// or the current one.
    /// </summary>
    PluginEncounterSnapshot GetEncounter();

    /// <summary>
    /// A player's skill breakdown in the encounter <see cref="GetEncounter"/> reads, largest first.
    /// Empty when the player has no total of that kind in the encounter.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for a value that is not a <see cref="PluginCombatKind"/>.
    /// </summary>
    IReadOnlyList<PluginSkillTotals> GetSkillTotals(long characterId, PluginCombatKind kind);

    /// <summary>
    /// Raised after the host has created a new encounter: on a map change (possibly several times for one map change),
    /// at the start of a dungeon, at a split of the progress-based reset (a boss room or a wipe), on a manual reset,
    /// when a benchmark starts or stops, and on logout. The combat clock starts later; read <see cref="PluginEncounterSnapshot.StartUtc"/>.
    /// </summary>
    event EventHandler? EncounterStarted;

    /// <summary>
    /// Raised when the host closes an encounter that has records and creates the next one, with the closed encounter's final result.
    /// Not raised for the last encounter when the app exits.
    /// Keep what you need from the result: <see cref="GetEncounter"/> reads the closed encounter only while the meters keep showing it.
    /// </summary>
    event EventHandler<PluginEncounterResult>? EncounterClosed;

    /// <summary>
    /// Raised after the player has moved to another map or channel. Not raised for the first map after the app starts or after logging in again.
    /// <see cref="Players"/> and <see cref="Entities"/> are rebuilt for the new map.
    /// </summary>
    event EventHandler? MapEntered;

    /// <summary>
    /// Raised after the player has returned to the login screen. The host has cleared what it received for the character.
    /// </summary>
    event EventHandler? LoggedOut;
}
