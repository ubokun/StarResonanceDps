// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

public sealed class ProxyId(uint serviceId, uint methodId)
{
    public uint ServiceId { get; } = serviceId;

    public uint MethodId { get; } = methodId;

    public override bool Equals(object? obj)
    {
        return obj is ProxyId other
            && ServiceId == other.ServiceId
            && MethodId == other.MethodId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ServiceId, MethodId);
    }
}
