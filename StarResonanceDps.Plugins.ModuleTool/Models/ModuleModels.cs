// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Text.Json.Serialization;

namespace StarResonanceDps.Plugins.ModuleTool.Models;

/// <summary>効果の段階1つ。<see cref="Links"/> はその段階になる効果のリンク数の合計。</summary>
internal sealed record ModuleEffectLevel(int Links, int FightValue);

/// <summary>効果1つの定義。段階は 0 から順。</summary>
internal sealed record ModuleEffectDefinition(int EffectId, string Icon, IReadOnlyList<ModuleEffectLevel> Levels)
{
    public int MaxLevel => Levels.Count - 1;

    /// <summary>リンク数の合計がなる段階。</summary>
    public int GetLevel(int links)
    {
        var level = 0;
        for (var index = 1; index < Levels.Count; index++)
        {
            if (Levels[index].Links <= links)
            {
                level = index;
            }
        }

        return level;
    }
}

/// <summary>モジュール1種類(アイテムID)の定義。</summary>
internal sealed record ModuleDefinition(int ConfigId, string Icon, int Quality);

/// <summary>優先する効果1つの条件。<see cref="MinLevel"/> は 1 から効果の最後の段階まで(Lv1 はその効果が入っていること)。</summary>
internal sealed record ModulePriorityEffect(int EffectId, int MinLevel);

/// <summary>検索に使うモジュール1つ。効果は (効果の添字, リンク数)。</summary>
internal sealed record ModuleCandidate(long Uuid, int ConfigId, IReadOnlyList<(int EffectIndex, int Links)> Effects)
{
    public int TotalLinks { get; } = Effects.Sum(effect => effect.Links);
}

/// <summary>見つけた組1つ。モジュールは表示に使う代表(装着中を先、次に uuid の小さい順)。</summary>
internal sealed record ModuleCombination(int FightValue, int TotalLinks, IReadOnlyList<ModuleCandidate> Modules);

/// <summary>検索の結果。<see cref="MatchingCount"/> は条件を満たす組の数(<see cref="Combinations"/> と同じ単位)。</summary>
internal sealed record ModuleSearchResult(IReadOnlyList<ModuleCombination> Combinations, long MatchingCount);

/// <summary>保存する設定(<c>module_config.json</c>)。</summary>
internal sealed class ModuleToolConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("priority_effects")]
    public List<ModulePriorityEffectConfig> PriorityEffects { get; set; } = [];

    /// <summary>検索に入れない品質。</summary>
    [JsonPropertyName("excluded_qualities")]
    public List<int> ExcludedQualities { get; set; } = [];
}

internal sealed class ModulePriorityEffectConfig
{
    [JsonPropertyName("effect_id")]
    public int EffectId { get; set; }

    [JsonPropertyName("min_level")]
    public int MinLevel { get; set; }
}

internal sealed class ModuleTablesFile
{
    public int SlotCount { get; set; }

    public Dictionary<string, ModuleEffectFile> Effects { get; set; } = [];

    public Dictionary<string, ModuleFile> Modules { get; set; } = [];

    public Dictionary<string, int[]> LinkFightValues { get; set; } = [];
}

internal sealed class ModuleEffectFile
{
    public string Icon { get; set; } = string.Empty;

    public List<ModuleEffectLevelFile> Levels { get; set; } = [];
}

internal sealed class ModuleEffectLevelFile
{
    public int Links { get; set; }

    public int FightValue { get; set; }
}

internal sealed class ModuleFile
{
    public string Icon { get; set; } = string.Empty;

    public int Quality { get; set; }
}

internal sealed class ModuleNamesFile
{
    public Dictionary<string, Dictionary<string, string>> Effects { get; set; } = [];

    public Dictionary<string, Dictionary<string, string>> Modules { get; set; } = [];
}
