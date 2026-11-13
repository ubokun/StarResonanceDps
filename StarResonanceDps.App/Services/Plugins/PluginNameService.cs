// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 番号から、本体が画面に出すのと同じ名前(内部IDの注記なし)を返す。名前が無ければ null。全部のプラグインで1つ。
/// </summary>
internal sealed class PluginNameService : IPluginNameService
{
    private PluginNameService()
    {
    }

    public static PluginNameService Instance { get; } = new();

    public string? GetSkillName(int skillId)
    {
        return NullIfEmpty(CombatDataCatalog.GetSkillNameWithoutInternalId(skillId));
    }

    public string? GetBuffName(int buffId)
    {
        return NullIfEmpty(CombatDataCatalog.GetBuffNameWithoutInternalId(buffId));
    }

    public string? GetMonsterName(int monsterId)
    {
        return NullIfEmpty(CombatDataCatalog.GetEntityNameWithoutInternalId(Zproto.EEntityType.EntMonster, monsterId));
    }

    public string? GetClassSpecName(int specId)
    {
        return NullIfEmpty(CombatDataCatalog.GetClassSpecName(specId));
    }

    public string? GetSceneName(long sceneId, int dungeonDifficulty = 0)
    {
        return NullIfEmpty(CombatDataCatalog.GetSceneNameWithoutInternalId(sceneId, dungeonDifficulty));
    }

    public string? GetItemName(int itemId)
    {
        return NullIfEmpty(CombatDataCatalog.GetItemName(itemId));
    }

    private static string? NullIfEmpty(string name)
    {
        return string.IsNullOrEmpty(name) ? null : name;
    }
}
