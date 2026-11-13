// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using BinaryItem = StarResonanceDps.Core.Protocols.Game.Binary.Item;
using BinaryItemPackage = StarResonanceDps.Core.Protocols.Game.Binary.ItemPackage;
using BinaryMod = StarResonanceDps.Core.Protocols.Game.Binary.Mod;
using BinaryPackage = StarResonanceDps.Core.Protocols.Game.Binary.Package;
using StarResonanceDps.Core.Protocols.Game.Binary;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>モジュールの効果1つ。<paramref name="LinkCount"/> はリンク数で、届いていなければ <c>null</c>。</summary>
public sealed record SelfModuleEffect(int EffectId, int? LinkCount);

/// <summary>自分のモジュール1つ。効果はゲームの並びのまま。</summary>
public sealed record SelfModule(long Uuid, int ConfigId, int Quality, IReadOnlyList<SelfModuleEffect> Effects);

/// <summary>自分のモジュールの全部(uuid の順)と、装着中の枠(枠の番号 → uuid)。</summary>
public sealed record SelfModuleInventory(IReadOnlyList<SelfModule> Modules, IReadOnlyDictionary<int, long> EquippedUuidBySlot);

/// <summary>
/// 自分のモジュールの置き場。全体コンテナで作り直し、差分コンテナを当てる。
///
/// <para>
/// モジュールの中身は持ち物のモジュールの袋にあり、装着中の枠(<c>CharSerialize.mod</c>)が持つのは「枠 → uuid」だけ。
/// リンク数は効果ごとに <c>Item.modPartLevel</c> で届く。付け替えは枠の差分、入手は袋への追加で丸ごと、分解は袋からの削除で届く。
/// 全体コンテナに袋や枠が無いのは、持っていない・着けていないということ(空の項目は送られない)。
/// </para>
///
/// <para>
/// 全体コンテナが一度も来ていない間(途中起動)は中身が揃わないので、差分を当てず、値も返さない。
/// </para>
/// </summary>
public static class SelfModuleStore
{
    /// <summary>持ち物のモジュールの袋。</summary>
    private const int ModulePackageId = 5;

    private static readonly object StateLock = new();
    private static readonly Dictionary<int, long> SlotItems = [];
    private static readonly Dictionary<long, ItemState> Items = [];
    private static bool _hasState;

    /// <summary>
    /// 装着中の枠のモジュールの、効果ごとのリンク数の和(<see cref="GetEquippedEffectLinks"/>)。中身が変わったら捨て、次に読むときに作り直す。
    /// </summary>
    private static IReadOnlyList<SelfModuleEffect>? _equippedEffectLinks;

    /// <summary>自分のモジュールが変わった。パケット処理のスレッドから呼ばれる。</summary>
    public static event Action? Changed;

    /// <summary>全体コンテナの装着中の枠と持ち物で作り直す。</summary>
    public static void ReplaceSelf(Zproto.Mod? mod, Zproto.ItemPackage? itemPackage)
    {
        lock (StateLock)
        {
            SlotItems.Clear();
            Items.Clear();

            if (itemPackage is not null && itemPackage.Packages.TryGetValue(ModulePackageId, out var package))
            {
                foreach (var (uuid, item) in package.Items)
                {
                    Items[uuid] = ItemState.From(item);
                }
            }

            if (mod is not null)
            {
                foreach (var (slot, uuid) in mod.ModSlots)
                {
                    if (uuid != 0)
                    {
                        SlotItems[slot] = uuid;
                    }
                }
            }

            _hasState = true;
            _equippedEffectLinks = null;
            WarnMissingLinkCounts(Items);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// 差分コンテナの装着中の枠と持ち物の変化を当てる。全体コンテナが来る前は当てない。
    /// モジュールの袋と枠に変化が無い差分では知らせない。
    /// </summary>
    public static void ApplySelfChanges(BinaryMod? mod, BinaryItemPackage? itemPackage)
    {
        var changed = false;
        lock (StateLock)
        {
            if (!_hasState)
            {
                return;
            }

            if (itemPackage?.PackageChanges is { } packageChanges && TouchesModulePackage(packageChanges))
            {
                ApplyPackageChanges(packageChanges);
                changed = true;
            }

            if (mod?.SlotChanges is { } slotChanges)
            {
                ApplySlotChanges(slotChanges);
                changed = true;
            }

            if (changed)
            {
                _equippedEffectLinks = null;
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// キャプチャを止めたときとログイン画面へ戻ったとき。起動直後と同じく値を持たない状態にする。
    /// 止めている間の差分は届かないので、控えを残すと古い所持品を今の所持品として出し続ける。
    /// </summary>
    public static void ResetSelfToStartup()
    {
        lock (StateLock)
        {
            SlotItems.Clear();
            Items.Clear();
            _hasState = false;
            _equippedEffectLinks = null;
        }

        Changed?.Invoke();
    }

    /// <summary>今の所持品と装着中の枠。全体コンテナがまだなら <c>null</c>。</summary>
    public static SelfModuleInventory? GetSnapshot()
    {
        lock (StateLock)
        {
            if (!_hasState)
            {
                return null;
            }

            var modules = Items
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value.ToModule(pair.Key))
                .ToList();
            return new SelfModuleInventory(modules, new Dictionary<int, long>(SlotItems));
        }
    }

    /// <summary>
    /// 装着中の枠のモジュールの、効果ごとのリンク数の和(効果の番号の順)。和に入るリンク数が1つでも届いていない効果は <c>null</c>。
    /// 全体コンテナがまだなら <c>null</c>。中身が変わるまでは同じ一覧を返す。
    /// </summary>
    public static IReadOnlyList<SelfModuleEffect>? GetEquippedEffectLinks()
    {
        lock (StateLock)
        {
            if (!_hasState)
            {
                return null;
            }

            return _equippedEffectLinks ??= SumEquippedEffectLinks();
        }
    }

    private static IReadOnlyList<SelfModuleEffect> SumEquippedEffectLinks()
    {
        var totals = new SortedDictionary<int, int?>();
        foreach (var (slot, uuid) in SlotItems)
        {
            if (!Items.TryGetValue(uuid, out var state))
            {
                Serilog.Log.Warning(
                    "Equipped module {Uuid} in slot {Slot} is not in the module bag; its effects are not counted",
                    uuid,
                    slot);
                continue;
            }

            foreach (var effect in state.ToModule(uuid).Effects)
            {
                totals[effect.EffectId] = totals.TryGetValue(effect.EffectId, out var total)
                    ? total + effect.LinkCount
                    : effect.LinkCount;
            }
        }

        return totals.Select(pair => new SelfModuleEffect(pair.Key, pair.Value)).ToList();
    }

    private static bool TouchesModulePackage(BlobHashMapDelta<int, BinaryPackage> changes)
    {
        return changes.ReplacesExisting
            || changes.Removed.Contains(ModulePackageId)
            || changes.Added.ContainsKey(ModulePackageId)
            || changes.Updated.ContainsKey(ModulePackageId);
    }

    private static void ApplyPackageChanges(BlobHashMapDelta<int, BinaryPackage> changes)
    {
        if (changes.ReplacesExisting || changes.Removed.Contains(ModulePackageId))
        {
            Items.Clear();
        }

        if (changes.Added.TryGetValue(ModulePackageId, out var added) && added.ItemChanges is { } addedItems)
        {
            Items.Clear();
            ApplyItemChanges(addedItems);
        }

        if (changes.Updated.TryGetValue(ModulePackageId, out var updated) && updated.ItemChanges is { } updatedItems)
        {
            ApplyItemChanges(updatedItems);
        }
    }

    private static void ApplyItemChanges(BlobHashMapDelta<long, BinaryItem> changes)
    {
        if (changes.ReplacesExisting)
        {
            Items.Clear();
        }

        foreach (var uuid in changes.Removed)
        {
            Items.Remove(uuid);
        }

        // 追加は丸ごと届く。更新は変わった項目だけなので、控えている値に重ねる。
        var touched = new Dictionary<long, ItemState>();
        foreach (var (uuid, item) in changes.Added)
        {
            var state = new ItemState();
            state.Apply(item);
            Items[uuid] = state;
            touched[uuid] = state;
        }

        foreach (var (uuid, item) in changes.Updated)
        {
            if (!Items.TryGetValue(uuid, out var state))
            {
                state = new ItemState();
                Items[uuid] = state;
            }

            state.Apply(item);
            touched[uuid] = state;
        }

        WarnMissingLinkCounts(touched);
    }

    private static void ApplySlotChanges(BlobHashMapDelta<int, long> changes)
    {
        if (changes.ReplacesExisting)
        {
            SlotItems.Clear();
        }

        foreach (var slot in changes.Removed)
        {
            SlotItems.Remove(slot);
        }

        foreach (var (slot, uuid) in changes.Added.Concat(changes.Updated))
        {
            if (uuid == 0)
            {
                SlotItems.Remove(slot);
            }
            else
            {
                SlotItems[slot] = uuid;
            }
        }
    }

    /// <summary>リンク数の届いていない効果を持つモジュールを記録する。値は埋めず、使う側が計算から外す。</summary>
    private static void WarnMissingLinkCounts(IReadOnlyDictionary<long, ItemState> items)
    {
        foreach (var (uuid, state) in items)
        {
            if (state.HasMissingLinkCount)
            {
                Serilog.Log.Warning(
                    "Module {Uuid} (config {ConfigId}) has effects without link counts; it is excluded from module calculations",
                    uuid,
                    state.ConfigId);
            }
        }
    }

    /// <summary>モジュールの袋のアイテム1つの控え。使う項目だけ持つ。</summary>
    private sealed class ItemState
    {
        private int _quality;
        private List<int> _effectIds = [];

        /// <summary>効果の番号 → リンク数。一度も届いていなければ <c>null</c>。</summary>
        private Dictionary<int, int>? _linkCounts;

        public int ConfigId { get; private set; }

        public bool HasMissingLinkCount =>
            _linkCounts is null || _effectIds.Any(effectId => !_linkCounts.ContainsKey(effectId));

        public static ItemState From(Zproto.Item item)
        {
            var state = new ItemState
            {
                ConfigId = item.ConfigId,
                _quality = item.Quality,
            };

            if (item.ModNewAttr is { } modNewAttr)
            {
                state._effectIds = [.. modNewAttr.ModParts];
            }

            if (item.ModPartLevel is { } modPartLevel)
            {
                state._linkCounts = new Dictionary<int, int>(modPartLevel.Levels);
            }

            return state;
        }

        /// <summary>届いた項目だけを重ねる。</summary>
        public void Apply(BinaryItem item)
        {
            if (item.ConfigId is { } configId)
            {
                ConfigId = configId;
            }

            if (item.Quality is { } quality)
            {
                _quality = quality;
            }

            if (item.ModNewAttr?.ModParts is { } effectIds)
            {
                _effectIds = effectIds;
            }

            if (item.ModPartLevel?.LevelChanges is { } levelChanges)
            {
                _linkCounts ??= [];
                if (levelChanges.ReplacesExisting)
                {
                    _linkCounts.Clear();
                }

                foreach (var effectId in levelChanges.Removed)
                {
                    _linkCounts.Remove(effectId);
                }

                foreach (var (effectId, linkCount) in levelChanges.Added.Concat(levelChanges.Updated))
                {
                    _linkCounts[effectId] = linkCount;
                }
            }
        }

        public SelfModule ToModule(long uuid)
        {
            var effects = _effectIds
                .Select(effectId => new SelfModuleEffect(
                    effectId,
                    _linkCounts is not null && _linkCounts.TryGetValue(effectId, out var linkCount) ? linkCount : null))
                .ToList();
            return new SelfModule(uuid, ConfigId, _quality, effects);
        }
    }
}
