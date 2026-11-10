namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One buff (or debuff) on an entity, as the host received it.
/// The host's buff list rules (priority, overrides, grouping the same buff) are not applied.
/// </summary>
/// <param name="BuffUuid">The buff instance UUID.</param>
/// <param name="BuffId">The buff ID. Use <see cref="IPluginNameService.GetBuffName"/> for its name.</param>
/// <param name="Level">The buff level.</param>
/// <param name="Layer">The number of stacks.</param>
/// <param name="DurationSeconds">The total duration (extended when the buff is extended), or <c>null</c> when the buff has no duration.</param>
/// <param name="EndUtc">When the buff ends, or <c>null</c> when it has no duration or the remaining time is unknown.</param>
/// <param name="IsRemainingUnknown">
/// The host received the buff without knowing how much time had already passed, so the remaining time is unknown.
/// </param>
/// <param name="CasterEntityUuid">The UUID of the entity that applied the buff, or 0 when unknown.</param>
/// <param name="CasterCharacterId">The character ID of the caster when the caster is a player.</param>
public sealed record PluginBuff(
    long BuffUuid,
    int BuffId,
    int Level,
    int Layer,
    double? DurationSeconds,
    DateTime? EndUtc,
    bool IsRemainingUnknown,
    long CasterEntityUuid,
    long? CasterCharacterId);
