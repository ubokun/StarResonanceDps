// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using BinaryCultivateAreaData = StarResonanceDps.Core.Protocols.Game.Binary.CultivateAreaData;
using BinaryCultivateLineData = StarResonanceDps.Core.Protocols.Game.Binary.CultivateLineData;
using BinaryCultivateLineSubTypeData = StarResonanceDps.Core.Protocols.Game.Binary.CultivateLineSubTypeData;
using BinarySeasonCultivateLineData = StarResonanceDps.Core.Protocols.Game.Binary.SeasonCultivateLineData;
using StarResonanceDps.Core.Protocols.Game.Binary;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>自分の因子の枠1つ。<paramref name="SlotId"/> は木の節(因子の枠)の番号、<paramref name="ItemId"/> は付けている因子のアイテム。</summary>
public sealed record SelfSeasonFactor(int SlotId, int ItemId);

/// <summary>
/// 自分の心相晶の育成データの置き場。全体コンテナ(<c>CharSerialize.seasonCultivateLineData</c>)で作り直し、差分コンテナを当てる。
///
/// <para>
/// 入れ物は「シーズン → 機能(800522 潜在心相晶 / 800523 滅妄心相晶)→ 型 → 因子の枠(木の節)→ 付けている因子のアイテム」。
/// 型ごとに有効か(<c>isActive</c>)も持つ。差分は変わった項目だけが入れ子の形で届くので、届いた項目だけを重ねる。
/// 使うのは因子だけで、ノードと絆スロットは控えない(読み進めるためだけに読む)。
/// </para>
///
/// <para>
/// 全体コンテナが一度も来ていない間(途中起動)は中身が揃わないので、差分を当てず、値も返さない。
/// </para>
/// </summary>
public static class SelfSeasonCultivateStore
{
    /// <summary>潜在心相晶の機能の番号(<c>FunctionTable</c>)。滅妄心相晶(800523)は出さない。</summary>
    private const int PsychoscopeFunctionId = 800522;

    private static readonly object StateLock = new();

    /// <summary>シーズン → 機能 → 型 → その型の控え。</summary>
    private static readonly Dictionary<int, Dictionary<int, Dictionary<int, TemplateState>>> Seasons = [];

    private static bool _hasState;

    /// <summary>自分の育成データが変わった。パケット処理のスレッドから呼ばれる。</summary>
    public static event Action? Changed;

    /// <summary>全体コンテナの育成データで作り直す。項目が無いのは、育成データが無いということ(空の項目は送られない)。</summary>
    public static void ReplaceSelf(Zproto.SeasonCultivateLineData? data)
    {
        lock (StateLock)
        {
            Seasons.Clear();
            if (data is not null)
            {
                foreach (var (seasonId, line) in data.SeasonCultivateLineMap)
                {
                    var functions = new Dictionary<int, Dictionary<int, TemplateState>>();
                    foreach (var (functionId, subType) in line.CultivateLineMap)
                    {
                        var templates = new Dictionary<int, TemplateState>();
                        foreach (var (templateId, area) in subType.CultivateLineDataMap)
                        {
                            templates[templateId] = TemplateState.From(area);
                        }

                        functions[functionId] = templates;
                    }

                    Seasons[seasonId] = functions;
                }
            }

            _hasState = true;
        }

        Changed?.Invoke();
    }

    /// <summary>差分コンテナの育成データを当てる。全体コンテナが来る前は当てない。</summary>
    public static void ApplySelfChanges(BinarySeasonCultivateLineData? changes)
    {
        if (changes?.SeasonChanges is not { } seasonChanges)
        {
            return;
        }

        lock (StateLock)
        {
            if (!_hasState)
            {
                return;
            }

            ApplyMap(Seasons, seasonChanges, ApplyLine);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// キャプチャを止めたときとログイン画面へ戻ったとき。起動直後と同じく値を持たない状態にする。
    /// 止めている間の差分は届かないので、控えを残すと古い付け方を今の付け方として出し続ける。
    /// </summary>
    public static void ResetSelfToStartup()
    {
        lock (StateLock)
        {
            Seasons.Clear();
            _hasState = false;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// そのシーズンの潜在心相晶の、有効な型に付けている因子(型の番号の順、その中は枠の番号の順。空きの枠は入れない)。
    /// 有効な型が無ければ空。全体コンテナがまだなら <c>null</c>。
    /// </summary>
    public static IReadOnlyList<SelfSeasonFactor>? GetActiveFactors(int seasonId)
    {
        lock (StateLock)
        {
            if (!_hasState)
            {
                return null;
            }

            if (!Seasons.TryGetValue(seasonId, out var functions)
                || !functions.TryGetValue(PsychoscopeFunctionId, out var templates))
            {
                return [];
            }

            return templates
                .Where(pair => pair.Value.IsActive)
                .OrderBy(pair => pair.Key)
                .SelectMany(pair => pair.Value.FactorItems
                    .Where(slot => slot.Value != 0)
                    .OrderBy(slot => slot.Key)
                    .Select(slot => new SelfSeasonFactor(slot.Key, slot.Value)))
                .ToList();
        }
    }

    private static void ApplyLine(Dictionary<int, Dictionary<int, TemplateState>> functions, BinaryCultivateLineData changes)
    {
        if (changes.FunctionChanges is { } functionChanges)
        {
            ApplyMap(functions, functionChanges, ApplySubType);
        }
    }

    private static void ApplySubType(Dictionary<int, TemplateState> templates, BinaryCultivateLineSubTypeData changes)
    {
        if (changes.TemplateChanges is { } templateChanges)
        {
            ApplyMap(templates, templateChanges, (state, area) => state.Apply(area));
        }
    }

    /// <summary>
    /// 地図の差分を当てる。追加は丸ごと届くので作り直し、更新は変わった項目だけなので控えに重ねる(控えに無ければ空から作る)。
    /// </summary>
    private static void ApplyMap<TState, TChange>(
        Dictionary<int, TState> states,
        BlobHashMapDelta<int, TChange> changes,
        Action<TState, TChange> apply)
        where TState : new()
    {
        if (changes.ReplacesExisting)
        {
            states.Clear();
        }

        foreach (var key in changes.Removed)
        {
            states.Remove(key);
        }

        foreach (var (key, change) in changes.Added)
        {
            var state = new TState();
            apply(state, change);
            states[key] = state;
        }

        foreach (var (key, change) in changes.Updated)
        {
            if (!states.TryGetValue(key, out var state))
            {
                state = new TState();
                states[key] = state;
            }

            apply(state, change);
        }
    }

    /// <summary>1つの型の控え。使う項目だけ持つ。</summary>
    private sealed class TemplateState
    {
        public bool IsActive { get; private set; }

        /// <summary>因子の枠の番号 → 付けている因子のアイテム(0 は空き)。</summary>
        public Dictionary<int, int> FactorItems { get; } = [];

        public static TemplateState From(Zproto.CultivateAreaData area)
        {
            var state = new TemplateState { IsActive = area.IsActive };
            foreach (var (slotId, middle) in area.CultivateMiddleNodeMap)
            {
                state.FactorItems[slotId] = middle.ItemId;
            }

            return state;
        }

        /// <summary>届いた項目だけを重ねる。</summary>
        public void Apply(BinaryCultivateAreaData area)
        {
            if (area.IsActive is { } isActive)
            {
                IsActive = isActive;
            }

            if (area.MiddleNodeChanges is not { } middleChanges)
            {
                return;
            }

            if (middleChanges.ReplacesExisting)
            {
                FactorItems.Clear();
            }

            foreach (var slotId in middleChanges.Removed)
            {
                FactorItems.Remove(slotId);
            }

            foreach (var (slotId, middle) in middleChanges.Added.Concat(middleChanges.Updated))
            {
                if (middle.ItemId is { } itemId)
                {
                    FactorItems[slotId] = itemId;
                }
            }
        }
    }
}
