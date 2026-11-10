namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One player of the list the Player List widget is built from (<see cref="IPluginGameDataService.Players"/>).
/// </summary>
/// <param name="CharacterId">The character ID. Use it to identify the player.</param>
/// <param name="Name">
/// The display name. It is <c>*****</c> while the user hides player names in the host's display settings,
/// and can be empty while the name is unknown.
/// </param>
/// <param name="ProfessionId">The class (profession) ID, or 0 when unknown.</param>
/// <param name="SpecState">Whether the specialization is known.</param>
/// <param name="SpecId">
/// The specialization ID when <see cref="SpecState"/> is <see cref="PluginSpecState.Specialized"/>; otherwise <c>null</c>.
/// Use <see cref="IPluginNameService.GetClassSpecName"/> for its name.
/// </param>
/// <param name="Level">The level, or 0 when unknown.</param>
/// <param name="AbilityScore">The ability score, or 0 when unknown.</param>
/// <param name="SeasonStrength">The season strength, or 0 when unknown.</param>
/// <param name="CurrentHp">
/// The current HP. For a party member who is not nearby, the value the party sync sends; for another player who is not nearby,
/// the last value the host received. For a row that comes from the opened history encounter
/// or from the previous encounter the meters keep showing, it equals <see cref="MaxHp"/>.
/// </param>
/// <param name="MaxHp">The maximum HP.</param>
/// <param name="CurrentShield">The shield amount. Known only for the local player and nearby players; otherwise 0.</param>
/// <param name="PartyNumber">The number in the party, or <c>null</c> when not in the party or unknown.</param>
/// <param name="IsSelf">The local player.</param>
/// <param name="IsNpc">An NPC helper.</param>
/// <param name="IsPartyMember">A party member.</param>
/// <param name="IsNearby">The host currently receives the player's values from the surroundings (the player is on the same map).</param>
/// <param name="IsInCombat">The player is in the combat state. Known only for the local player and nearby players.</param>
/// <param name="IsIncapacitated">
/// The player is incapacitated. Always <c>false</c> for a row that comes from the opened history encounter
/// or from the previous encounter the meters keep showing.
/// </param>
/// <param name="PsychoscopeRootBuffId">
/// The root buff ID of the active Deep-Slumber Psychoscope type, or <c>null</c> when unknown or inactive.
/// </param>
/// <param name="IsPsychoscopeInactive">The player has no active Deep-Slumber Psychoscope type.</param>
/// <param name="IsFromRecord">
/// The row comes from the opened history encounter (a player who is only in the record), not from live data.
/// </param>
public sealed record PluginPlayer(
    long CharacterId,
    string Name,
    int ProfessionId,
    PluginSpecState SpecState,
    int? SpecId,
    int Level,
    int AbilityScore,
    int SeasonStrength,
    long CurrentHp,
    long MaxHp,
    long CurrentShield,
    int? PartyNumber,
    bool IsSelf,
    bool IsNpc,
    bool IsPartyMember,
    bool IsNearby,
    bool IsInCombat,
    bool IsIncapacitated,
    int? PsychoscopeRootBuffId,
    bool IsPsychoscopeInactive,
    bool IsFromRecord);

/// <summary>
/// What is known about a player's specialization.
/// </summary>
public enum PluginSpecState
{
    /// <summary>Not received yet.</summary>
    Unknown = 0,

    /// <summary>No specialization ability is equipped (Class R1).</summary>
    NotEquipped,

    /// <summary>The specialization is known (<see cref="PluginPlayer.SpecId"/>).</summary>
    Specialized,

    /// <summary>The player is transformed (a transformation class, or a transformation by an imagine or a quest).</summary>
    Transformation
}
