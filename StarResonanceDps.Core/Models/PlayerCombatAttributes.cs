// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Models;

public readonly record struct PlayerCombatAttributes(
    int PhysicalAttack = 0,
    int MagicalAttack = 0,
    int Strength = 0,
    int Dexterity = 0,
    int Intelligence = 0,
    int Endurance = 0,
    int Armor = 0,
    int Resistance = 0,
    int Critical = 0,
    int CriticalPercent = 0,
    int Haste = 0,
    int HastePercent = 0,
    int Luck = 0,
    int LuckPercent = 0,
    int Mastery = 0,
    int MasteryPercent = 0,
    int Versatility = 0,
    int VersatilityPercent = 0,
    int BlockPercent = 0);
