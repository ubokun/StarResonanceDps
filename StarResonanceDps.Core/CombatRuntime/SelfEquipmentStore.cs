// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using BinaryEquipAttr = StarResonanceDps.Core.Protocols.Game.Binary.EquipAttr;
using BinaryEquipAttrSet = StarResonanceDps.Core.Protocols.Game.Binary.EquipAttrSet;
using BinaryEquipList = StarResonanceDps.Core.Protocols.Game.Binary.EquipList;
using BinaryItem = StarResonanceDps.Core.Protocols.Game.Binary.Item;
using BinaryItemPackage = StarResonanceDps.Core.Protocols.Game.Binary.ItemPackage;
using BinaryPackage = StarResonanceDps.Core.Protocols.Game.Binary.Package;
using StarResonanceDps.Core.Protocols.Game.Binary;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 自分の装備の値。部位ごとに、着けている装備の装備ID・突破の段階・区分ごとの「属性庫の行ID → r」。
/// <see cref="SelfEquipmentStore.TryGetSlot"/> が返す。
/// </summary>
/// <param name="Plain">
/// 素の項目の行(<c>EquipAttr</c> の basicAttr / advanceAttr / recastAttr / rareQualityAttr)。
/// 表は装備の表のその区分の属性庫の型で引く。
/// </param>
/// <param name="Set">特化ごとの行(<c>EquipAttr.equipAttrSet</c>)。表は型2 の属性庫で引く。</param>
public sealed record SelfEquipmentSlot(
    int Slot,
    int EquipmentId,
    int BreakThroughTime,
    SelfEquipmentAttrs Plain,
    SelfEquipmentAttrs Set);

/// <summary>区分ごとの「属性庫の行ID → r」。r は完成度と同じ値で、値は <c>floor(r × (最大 − 最小) / 100 + 最小)</c>。</summary>
public sealed record SelfEquipmentAttrs(
    IReadOnlyDictionary<int, int> Basic,
    IReadOnlyDictionary<int, int> Advance,
    IReadOnlyDictionary<int, int> Recast,
    IReadOnlyDictionary<int, int> Rare);

/// <summary>
/// 自分の装備の値の置き場。全体コンテナで作り直し、差分コンテナを当てる。
///
/// <para>
/// 装備スロットが持つのは「部位 → 装備の uuid」だけで、装備の中身は持ち物の装備の袋にある。
/// 付け替えの差分は uuid しか運ばないので、<b>着けていない装備も含めて装備の袋を丸ごと控える</b>。
/// 特化・職業の切り替えではサーバーが着けている装備の特化ごとの行を今の特化の行に書き換え、
/// 入手は袋への追加で丸ごと、改鋳・突破は変わった項目だけが届く。
/// </para>
///
/// <para>
/// 全体コンテナが一度も来ていない間(途中起動)は中身が揃わないので、差分を当てず、値も返さない。
/// </para>
/// </summary>
public static class SelfEquipmentStore
{
    /// <summary>持ち物の装備の袋。</summary>
    private const int EquipPackageId = 2;

    private static readonly object StateLock = new();
    private static readonly Dictionary<int, long> SlotItems = [];
    private static readonly Dictionary<long, ItemState> Items = [];
    private static bool _hasState;

    /// <summary>自分の装備の値が変わった。パケット処理のスレッドから呼ばれる。</summary>
    public static event Action? Changed;

    /// <summary>自分の装備の具体値(全体コンテナの装備スロットと持ち物)を持っているか。ウィジェットの状態の印が読む。</summary>
    public static bool HasState
    {
        get
        {
            lock (StateLock)
            {
                return _hasState;
            }
        }
    }

    /// <summary>全体コンテナの装備スロットと持ち物で作り直す。どちらかが無ければ値を持たない状態にする。</summary>
    public static void ReplaceSelf(Zproto.EquipList? equipList, Zproto.ItemPackage? itemPackage)
    {
        lock (StateLock)
        {
            SlotItems.Clear();
            Items.Clear();
            _hasState = false;

            Zproto.Package? package = null;
            if (equipList is null || itemPackage is null || !itemPackage.Packages.TryGetValue(EquipPackageId, out package))
            {
                Serilog.Log.Warning(
                    "The full container has no equipment slots or equipment package; self equipment values are unavailable (slots={HasSlots} package={HasPackage})",
                    equipList is not null,
                    package is not null);
            }
            else
            {
                foreach (var (uuid, item) in package.Items)
                {
                    Items[uuid] = ItemState.From(item);
                }

                foreach (var (slot, info) in equipList.EquipList_)
                {
                    if (info.ItemUuid != 0)
                    {
                        SlotItems[slot] = (long)info.ItemUuid;
                    }
                }

                _hasState = true;
            }
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// 差分コンテナの装備スロットと持ち物の変化を当てる。全体コンテナが来る前は当てない。
    /// 装備の袋と部位に変化が無い差分(ほかの袋の個数・セット効果だけ など)では知らせない。
    /// </summary>
    public static void ApplySelfChanges(BinaryEquipList? equipList, BinaryItemPackage? itemPackage)
    {
        var changed = false;
        lock (StateLock)
        {
            if (!_hasState)
            {
                return;
            }

            if (itemPackage?.PackageChanges is { } packageChanges && TouchesEquipPackage(packageChanges))
            {
                ApplyPackageChanges(packageChanges);
                changed = true;
            }

            if (equipList?.EquipInfoChanges is { } slotChanges)
            {
                ApplySlotChanges(slotChanges);
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// キャプチャを止めたときとログイン画面へ戻ったとき。起動直後と同じく値を持たない状態にする。
    /// 止めている間の差分は届かないので、控えを残すと古い値を具体値として出し続ける。
    /// </summary>
    public static void ResetSelfToStartup()
    {
        lock (StateLock)
        {
            SlotItems.Clear();
            Items.Clear();
            _hasState = false;
        }

        Changed?.Invoke();
    }

    /// <summary>部位に着けている装備の値。値を持っていない・その部位に着けていない・袋に無ければ <c>false</c>。</summary>
    public static bool TryGetSlot(int slot, out SelfEquipmentSlot detail)
    {
        lock (StateLock)
        {
            if (_hasState
                && SlotItems.TryGetValue(slot, out var uuid)
                && Items.TryGetValue(uuid, out var item))
            {
                detail = item.ToSlot(slot);
                return true;
            }
        }

        detail = null!;
        return false;
    }

    private static bool TouchesEquipPackage(BlobHashMapDelta<int, BinaryPackage> changes)
    {
        return changes.ReplacesExisting
            || changes.Removed.Contains(EquipPackageId)
            || changes.Added.ContainsKey(EquipPackageId)
            || changes.Updated.ContainsKey(EquipPackageId);
    }

    private static void ApplyPackageChanges(BlobHashMapDelta<int, BinaryPackage> changes)
    {
        if (changes.ReplacesExisting || changes.Removed.Contains(EquipPackageId))
        {
            Items.Clear();
        }

        if (changes.Added.TryGetValue(EquipPackageId, out var added) && added.ItemChanges is { } addedItems)
        {
            Items.Clear();
            ApplyItemChanges(addedItems);
        }

        if (changes.Updated.TryGetValue(EquipPackageId, out var updated) && updated.ItemChanges is { } updatedItems)
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
        foreach (var (uuid, item) in changes.Added)
        {
            var state = new ItemState();
            state.Apply(item);
            Items[uuid] = state;
        }

        foreach (var (uuid, item) in changes.Updated)
        {
            if (!Items.TryGetValue(uuid, out var state))
            {
                state = new ItemState();
                Items[uuid] = state;
            }

            state.Apply(item);
        }
    }

    private static void ApplySlotChanges(BlobHashMapDelta<int, EquipInfo> changes)
    {
        if (changes.ReplacesExisting)
        {
            SlotItems.Clear();
        }

        foreach (var slot in changes.Removed)
        {
            SlotItems.Remove(slot);
        }

        foreach (var (slot, info) in changes.Added.Concat(changes.Updated))
        {
            if (info.ItemUuid is not { } uuid)
            {
                continue;
            }

            if (uuid == 0)
            {
                SlotItems.Remove(slot);
            }
            else
            {
                SlotItems[slot] = (long)uuid;
            }
        }
    }

    private static void ApplyMapChanges(Dictionary<int, int> target, BlobHashMapDelta<int, int>? changes)
    {
        if (changes is null)
        {
            return;
        }

        if (changes.ReplacesExisting)
        {
            target.Clear();
        }

        foreach (var key in changes.Removed)
        {
            target.Remove(key);
        }

        // 既にある鍵への追加は上書き。
        foreach (var (key, value) in changes.Added)
        {
            target[key] = value;
        }

        foreach (var (key, value) in changes.Updated)
        {
            target[key] = value;
        }
    }

    /// <summary>装備の袋のアイテム1つの控え。使う項目だけ持つ。</summary>
    private sealed class ItemState
    {
        private int _configId;
        private int _breakThroughTime;
        private readonly AttrMaps _plain = new();
        private readonly AttrMaps _set = new();

        public static ItemState From(Zproto.Item item)
        {
            var state = new ItemState { _configId = item.ConfigId };
            var equipAttr = item.EquipAttr;
            if (equipAttr is not null)
            {
                state._breakThroughTime = equipAttr.BreakThroughTime;
                state._plain.Replace(equipAttr.BasicAttr, equipAttr.AdvanceAttr, equipAttr.RecastAttr, equipAttr.RareQualityAttr);
                if (equipAttr.EquipAttrSet is { } set)
                {
                    state._set.Replace(set.BasicAttr, set.AdvanceAttr, set.RecastAttr, set.RareQualityAttr);
                }
            }

            return state;
        }

        /// <summary>届いた項目だけを重ねる。</summary>
        public void Apply(BinaryItem item)
        {
            if (item.ConfigId is { } configId)
            {
                _configId = configId;
            }

            if (item.EquipAttr is { } equipAttr)
            {
                Apply(equipAttr);
            }
        }

        private void Apply(BinaryEquipAttr equipAttr)
        {
            if (equipAttr.BreakThroughTime is { } breakThroughTime)
            {
                _breakThroughTime = breakThroughTime;
            }

            ApplyMapChanges(_plain.Basic, equipAttr.BasicAttrChanges);
            ApplyMapChanges(_plain.Advance, equipAttr.AdvanceAttrChanges);
            ApplyMapChanges(_plain.Recast, equipAttr.RecastAttrChanges);
            ApplyMapChanges(_plain.Rare, equipAttr.RareQualityAttrChanges);

            if (equipAttr.EquipAttrSet is { } set)
            {
                Apply(set);
            }
        }

        private void Apply(BinaryEquipAttrSet set)
        {
            ApplyMapChanges(_set.Basic, set.BasicAttrChanges);
            ApplyMapChanges(_set.Advance, set.AdvanceAttrChanges);
            ApplyMapChanges(_set.Recast, set.RecastAttrChanges);
            ApplyMapChanges(_set.Rare, set.RareQualityAttrChanges);
        }

        public SelfEquipmentSlot ToSlot(int slot)
        {
            return new SelfEquipmentSlot(slot, _configId, _breakThroughTime, _plain.Snapshot(), _set.Snapshot());
        }
    }

    private sealed class AttrMaps
    {
        public Dictionary<int, int> Basic { get; } = [];
        public Dictionary<int, int> Advance { get; } = [];
        public Dictionary<int, int> Recast { get; } = [];
        public Dictionary<int, int> Rare { get; } = [];

        public void Replace(
            IEnumerable<KeyValuePair<int, int>> basic,
            IEnumerable<KeyValuePair<int, int>> advance,
            IEnumerable<KeyValuePair<int, int>> recast,
            IEnumerable<KeyValuePair<int, int>> rare)
        {
            Copy(Basic, basic);
            Copy(Advance, advance);
            Copy(Recast, recast);
            Copy(Rare, rare);
        }

        public SelfEquipmentAttrs Snapshot()
        {
            return new SelfEquipmentAttrs(
                new Dictionary<int, int>(Basic),
                new Dictionary<int, int>(Advance),
                new Dictionary<int, int>(Recast),
                new Dictionary<int, int>(Rare));
        }

        private static void Copy(Dictionary<int, int> target, IEnumerable<KeyValuePair<int, int>> source)
        {
            target.Clear();
            foreach (var (key, value) in source)
            {
                target[key] = value;
            }
        }
    }
}
