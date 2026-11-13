// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public sealed class BlobHashMapDelta<TKey, TValue> where TKey : notnull
{
    public BlobHashMapDelta(
        bool replacesExisting,
        Dictionary<TKey, TValue> added,
        List<TKey> removed,
        Dictionary<TKey, TValue> updated)
    {
        ReplacesExisting = replacesExisting;
        Added = added;
        Removed = removed;
        Updated = updated;
    }

    public bool ReplacesExisting { get; }

    public Dictionary<TKey, TValue> Added { get; }

    public List<TKey> Removed { get; }

    public Dictionary<TKey, TValue> Updated { get; }
}
