// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Plugins.ModuleTool.Models;

namespace StarResonanceDps.Plugins.ModuleTool.Services;

/// <summary>
/// 組の戦闘力と、優先する効果の条件。効果は添字(<see cref="Effects"/> の並び)で扱う。
/// 戦闘力はゲームの式で、効果ごとのリンク数の合計がなる段階の戦闘力の和 ＋ リンクの総和の戦闘力(表の最後を超えた総和は最後の行)。
/// </summary>
internal sealed class ModuleScorer
{
    private readonly int[][] _fightValueByLinks;
    private readonly int[][] _levelByLinks;
    private readonly IReadOnlyList<int> _linkFightValues;
    private readonly (int EffectIndex, int MinLevel)[] _requirements;

    public ModuleScorer(
        IReadOnlyList<ModuleEffectDefinition> effects,
        IReadOnlyList<int> linkFightValues,
        IReadOnlyList<ModulePriorityEffect> priorityEffects)
    {
        Effects = effects;
        _linkFightValues = linkFightValues.Count > 0
            ? linkFightValues
            : throw new ArgumentException("The link fight value table is empty.", nameof(linkFightValues));
        EffectIndexById = effects
            .Select((effect, index) => (effect.EffectId, index))
            .ToDictionary(pair => pair.EffectId, pair => pair.index);

        // リンク数 → 段階・戦闘力を先に引いておく。最後の段階に必要なリンク数を超えた分は同じ値。
        _levelByLinks = new int[effects.Count][];
        _fightValueByLinks = new int[effects.Count][];
        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            var cap = effect.Levels[^1].Links;
            _levelByLinks[index] = new int[cap + 1];
            _fightValueByLinks[index] = new int[cap + 1];
            for (var links = 0; links <= cap; links++)
            {
                var level = effect.GetLevel(links);
                _levelByLinks[index][links] = level;
                _fightValueByLinks[index][links] = effect.Levels[level].FightValue;
            }
        }

        // 優先する効果は全部が条件。段階は 1(その効果が入っていること)から効果の最後の段階まで。
        var requirements = new List<(int EffectIndex, int MinLevel)>();
        foreach (var priority in priorityEffects)
        {
            if (!EffectIndexById.TryGetValue(priority.EffectId, out var index))
            {
                throw new ArgumentException($"Unknown priority effect {priority.EffectId}.", nameof(priorityEffects));
            }

            if (priority.MinLevel < 1 || priority.MinLevel > effects[index].MaxLevel)
            {
                throw new ArgumentException(
                    $"The minimum level {priority.MinLevel} of priority effect {priority.EffectId} is out of range.",
                    nameof(priorityEffects));
            }

            requirements.Add((index, priority.MinLevel));
        }

        _requirements = [.. requirements];
        RequiredLinks = requirements
            .Select(requirement => (requirement.EffectIndex, effects[requirement.EffectIndex].Levels[requirement.MinLevel].Links))
            .ToArray();
    }

    public IReadOnlyList<ModuleEffectDefinition> Effects { get; }

    public IReadOnlyDictionary<int, int> EffectIndexById { get; }

    /// <summary>優先する効果ごとの、条件の段階になるリンク数の合計。</summary>
    public IReadOnlyList<(int EffectIndex, int Links)> RequiredLinks { get; }

    public int GetLevel(int effectIndex, int links)
    {
        var table = _levelByLinks[effectIndex];
        return table[Math.Min(links, table.Length - 1)];
    }

    public int GetEffectFightValue(int effectIndex, int links)
    {
        var table = _fightValueByLinks[effectIndex];
        return table[Math.Min(links, table.Length - 1)];
    }

    public int GetLinkFightValue(int totalLinks)
    {
        return _linkFightValues[Math.Min(totalLinks, _linkFightValues.Count - 1)];
    }

    /// <summary>ゲームの式の戦闘力。</summary>
    public int GetFightValue(int[] links, int totalLinks)
    {
        var value = GetLinkFightValue(totalLinks);
        for (var index = 0; index < links.Length; index++)
        {
            value += GetEffectFightValue(index, links[index]);
        }

        return value;
    }

    /// <summary>優先する効果の条件を全部満たすか。</summary>
    public bool MeetsRequirements(int[] links)
    {
        foreach (var (effectIndex, minLevel) in _requirements)
        {
            if (GetLevel(effectIndex, links[effectIndex]) < minLevel)
            {
                return false;
            }
        }

        return true;
    }
}
