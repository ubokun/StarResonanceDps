// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

public class NetCapConfig
{
    public string CaptureDeviceName { get; set; } = string.Empty;
    public string[] ExeNames { get; set; } = ["BPSR", "BPSR_STEAM"];
}
