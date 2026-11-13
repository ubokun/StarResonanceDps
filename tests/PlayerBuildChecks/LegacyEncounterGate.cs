using System.Reflection;
namespace StarResonanceDps.Core.CombatRuntime;
internal static class LegacyEncounterGate
{
    private static readonly MethodInfo Gate=typeof(Encounter).GetMethod("IsRecorded",BindingFlags.Instance|BindingFlags.NonPublic)!;
    public static bool IsRecorded(Encounter encounter,DateTime utc,long? player)=>(bool)Gate.Invoke(encounter,[utc,player])!;
}
