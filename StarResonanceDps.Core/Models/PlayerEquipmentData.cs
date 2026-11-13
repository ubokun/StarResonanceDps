// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;

namespace StarResonanceDps.Core.Models;

public enum PlayerEquipmentDataState
{
    Missing,
    Invalid,
    Available
}

public readonly record struct PlayerEquipmentItem(int Slot, int EquipmentId);

public sealed class PlayerEquipmentData : IEquatable<PlayerEquipmentData>
{
    private readonly PlayerEquipmentItem[] _items;
    private readonly ReadOnlyCollection<PlayerEquipmentItem> _readOnlyItems;

    private PlayerEquipmentData(PlayerEquipmentDataState state, IEnumerable<PlayerEquipmentItem> items)
    {
        State = state;
        _items = items.ToArray();
        _readOnlyItems = Array.AsReadOnly(_items);
    }

    public PlayerEquipmentDataState State { get; }

    public IReadOnlyList<PlayerEquipmentItem> Items => _readOnlyItems;

    public static PlayerEquipmentData Invalid { get; } = new(PlayerEquipmentDataState.Invalid, Array.Empty<PlayerEquipmentItem>());

    public static PlayerEquipmentData Create(IEnumerable<PlayerEquipmentItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new PlayerEquipmentData(PlayerEquipmentDataState.Available, items);
    }

    public bool Equals(PlayerEquipmentData? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && State == other.State
            && _items.AsSpan().SequenceEqual(other._items);
    }

    public override bool Equals(object? obj)
    {
        return obj is PlayerEquipmentData other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(State);

        foreach (var item in _items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
