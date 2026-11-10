namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The totals of one encounter for every player, with the same numbers as the host's meters.
/// </summary>
/// <param name="IsHistory">The encounter is a history encounter the user opened in the host.</param>
/// <param name="StartUtc">When the combat clock started, or <c>null</c> before the first combat event.</param>
/// <param name="Elapsed">The combat time (paused intervals excluded).</param>
/// <param name="IsClosed">The encounter has ended.</param>
/// <param name="Players">Players with any total above 0, in no particular order. Not filtered by party.</param>
public sealed record PluginEncounterSnapshot(
    bool IsHistory,
    DateTime? StartUtc,
    TimeSpan Elapsed,
    bool IsClosed,
    IReadOnlyList<PluginEncounterPlayer> Players);

/// <summary>
/// One player's totals in an encounter.
/// </summary>
/// <param name="CharacterId">The character ID (the same as <see cref="PluginPlayer.CharacterId"/>).</param>
/// <param name="Damage">Damage dealt.</param>
/// <param name="Healing">Healing done.</param>
/// <param name="DamageTaken">Damage taken.</param>
public sealed record PluginEncounterPlayer(
    long CharacterId,
    PluginCombatTotals Damage,
    PluginCombatTotals Healing,
    PluginCombatTotals DamageTaken);

/// <summary>
/// Totals of damage, healing or damage taken.
/// </summary>
/// <param name="Total">The total. Damage and healing equal the host's meter rows.</param>
/// <param name="PerSecond">The total per second of the encounter's combat time (the host's meters).</param>
/// <param name="ActivePerSecond">The total per second of the player's active time (the host's skill detail).</param>
/// <param name="Hits">The number of hits.</param>
/// <param name="CritTotal">The total of critical hits.</param>
/// <param name="CritCount">The number of critical hits.</param>
/// <param name="LuckyTotal">The total of lucky hits.</param>
/// <param name="LuckyCount">The number of lucky hits.</param>
public sealed record PluginCombatTotals(
    ulong Total,
    double PerSecond,
    double ActivePerSecond,
    ulong Hits,
    ulong CritTotal,
    uint CritCount,
    ulong LuckyTotal,
    uint LuckyCount);

/// <summary>
/// Which totals to read.
/// </summary>
public enum PluginCombatKind
{
    /// <summary>Damage dealt.</summary>
    Damage = 0,

    /// <summary>Healing done.</summary>
    Healing
}

/// <summary>
/// One row of a player's skill breakdown (the same rows as the host's Skill Detail widget).
/// </summary>
/// <param name="RowKey">The row key, <c>"id:branch"</c>.</param>
/// <param name="Name">
/// The row name in the current language, without internal names or IDs, or <c>null</c> when the row has no name.
/// The name depends on how the host grouped the row, so it cannot be looked up from the key alone.
/// </param>
/// <param name="Total">The total.</param>
/// <param name="PerSecond">The total per second of the encounter's combat time (the host's meters).</param>
/// <param name="ActivePerSecond">The total per second of the player's active time (the host's skill detail).</param>
/// <param name="Hits">The number of hits.</param>
/// <param name="CritRate">The critical rate, in percent.</param>
/// <param name="Share">The share of the player's total, in percent.</param>
public sealed record PluginSkillTotals(
    string RowKey,
    string? Name,
    ulong Total,
    double PerSecond,
    double ActivePerSecond,
    ulong Hits,
    double CritRate,
    double Share);

/// <summary>
/// The result of an encounter that has just closed: the totals and every player's skill breakdown.
/// </summary>
/// <param name="Totals">The totals of the closed encounter (<see cref="PluginEncounterSnapshot.IsHistory"/> is <c>false</c>).</param>
/// <param name="Skills">Character ID to the player's skill breakdown, for every player in <see cref="PluginEncounterSnapshot.Players"/>.</param>
public sealed record PluginEncounterResult(
    PluginEncounterSnapshot Totals,
    IReadOnlyDictionary<long, PluginPlayerSkills> Skills);

/// <summary>
/// One player's skill breakdown.
/// </summary>
/// <param name="Damage">The damage rows, largest first.</param>
/// <param name="Healing">The healing rows, largest first.</param>
public sealed record PluginPlayerSkills(
    IReadOnlyList<PluginSkillTotals> Damage,
    IReadOnlyList<PluginSkillTotals> Healing);
