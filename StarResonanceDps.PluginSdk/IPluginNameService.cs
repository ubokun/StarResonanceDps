namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Returns the names the app shows for game IDs, in the current UI culture
/// (<see cref="IPluginLocalizationService.CurrentCulture"/>), or in Chinese (<c>zh-CN</c>) when that culture has no name.
/// Each method returns <c>null</c> when there is no name. Internal names and IDs are never added, whatever the host's display settings.
/// Safe to call from any thread.
/// </summary>
public interface IPluginNameService
{
    /// <summary>The name of a skill.</summary>
    string? GetSkillName(int skillId);

    /// <summary>The name of a buff. For a food or potion buff, the name of the item that gives it (several items are joined with <c> / </c>).</summary>
    string? GetBuffName(int buffId);

    /// <summary>The name of a monster.</summary>
    string? GetMonsterName(int monsterId);

    /// <summary>
    /// The name of a spec. The ID is the spec ID (profession ID × 10000 + spec number).
    /// Class R1, transformations and unknown specs have no name.
    /// </summary>
    string? GetClassSpecName(int specId);

    /// <summary>
    /// The name of a scene (map). When there is a difficulty name, it is added after a hyphen: the name of <paramref name="dungeonDifficulty"/>
    /// when it is above 0 and has one, otherwise the difficulty of the dungeon itself (for dungeons that have one ID per difficulty), also with 0.
    /// </summary>
    string? GetSceneName(long sceneId, int dungeonDifficulty = 0);

    /// <summary>The name of an item. Equipment IDs are item IDs.</summary>
    string? GetItemName(int itemId);
}
