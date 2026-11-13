// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime.DataTypes;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZLinq;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public static class Utils
{
    public static string BytesToString<T>(T number)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        double value = Convert.ToDouble(number);
        if (value == 0)
        {
            return "0" + suf[0];
        }

        double absoluteValue = Math.Abs(value);
        int place = Convert.ToInt32(Math.Floor(Math.Log(absoluteValue, 1024)));
        double shortNumber = Math.Round(absoluteValue / Math.Pow(1024, place), 2);

        string fmt = "";
        if (place > 0)
        {
            fmt = "N2";
        }
        return $"{(Math.Sign(value) * shortNumber).ToString(fmt)}{suf[place]}";
    }

    public static ulong CreateEncounterTeamId(Encounter encounter)
    {
        var hash = new XxHash64();
        var playerIds = encounter.Entities.AsValueEnumerable()
            .Where(x => x.Value.EntityType == EEntityType.EntChar)
            .Select(x => x.Value.UUID)
            .Order();

        foreach (var id in playerIds)
        {
            hash.Append(MemoryMarshal.Cast<long, byte>([id]));
        }

        var hashUlong = hash.GetCurrentHashAsUInt64();

        return hashUlong;
    }

    public static string DamagePropertyToString(EDamageProperty damageElement)
    {
        switch (damageElement)
        {
            case EDamageProperty.General:
                return "General";
            case EDamageProperty.Fire:
                return "Fire";
            case EDamageProperty.Water:
                return "Ice";
            case EDamageProperty.Electricity:
                return "Lightning";
            case EDamageProperty.Wood:
                return "Forest";
            case EDamageProperty.Wind:
                return "Wind";
            case EDamageProperty.Rock:
                return "Earth";
            case EDamageProperty.Light:
                return "Light";
            case EDamageProperty.Dark:
                return "Dark";
            default:
                return "";
        }
    }

    public enum EEntityType_Lua
    {
        EntErrType = 0,
        EntMonster = 1,
        EntNpc = 2,
        EntSceneObject = 3,
        EntZone = 5,
        EntBullet = 6,
        EntClientBullet = 7,
        EntPet = 8,
        EntChar = 10,
        EntDummy = 11,
        EntDrop = 12,
        EntField = 14,
        EntTrap = 15,
        EntCollection = 16,
        EntStaticObject = 18,
        EntVehicle = 19,
        EntToy = 19,
        EntCommunityHouse = 21,
        EntHouseItem = 22,
        EntCount = 23
    }

    public static long GetCurrentShield(Entity entity)
    {
        if (entity.GetAttrKV("AttrShieldList") is not IEnumerable<ShieldInfo> shields)
        {
            return 0L;
        }

        long total = 0L;
        foreach (var shield in shields)
        {
            total += shield.Value;
        }

        return total;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UuidToEntityId(long uuid) => uuid >> 16;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long EntityIdToUuid(long uid, long entityType, bool isSummon, bool isClient) => uid << 16 | ((isSummon ? 1L : 0L) << 15) | ((isClient ? 1L : 0L) << 14) | (entityType << 6);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UuidToEntityType(long uuid) => (uuid >> 6) & 31;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSummonByUuid(long uuid) => ((uuid >> 15) & 1) == 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsClientByUuid(long uuid) => ((uuid >> 14) & 1) == 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CheckIsAiByEntityId(long uid) => ((uid >> 10) & 1) != 0;

    public static string GameCapturePreferenceToName(EGameCapturePreference pref)
    {
        var gamePrefName = pref switch
        {
            EGameCapturePreference.Auto => "Auto",
            EGameCapturePreference.Steam => "Steam",
            EGameCapturePreference.Standalone => "Standalone",
            EGameCapturePreference.Epic => "Epic",
            EGameCapturePreference.HaoPlaySea => "HaoPlay SEA",
            EGameCapturePreference.XDG => "XDG",
            EGameCapturePreference.HaoPlaySeaSteam => "HaoPlay SEA Steam",
            EGameCapturePreference.XDGSteam => "XDG Steam",
            EGameCapturePreference.WeGame => "WeGame",
            EGameCapturePreference.Custom => "Custom",
            _ => throw new ArgumentOutOfRangeException(nameof(pref), pref, null)
        };

        return gamePrefName;
    }

    public static string[] GameCapturePreferenceToExeNames(EGameCapturePreference pref)
    {
        return GameCapturePreferenceToExeNames(pref, CombatRuntimeSettings.GameCaptureCustomExeName);
    }

    public static string[] GameCapturePreferenceToExeNames(EGameCapturePreference pref, string customExeName)
    {
        string[] exeNameToCapture = pref switch
        {
            EGameCapturePreference.Auto => ["BPSR", "BPSR_STEAM", "BPSR_EPIC", "StarSEA", "StarASIA", "StarSEA_STEAM", "StarASIA_STEAM", "Star"],
            EGameCapturePreference.Steam => ["BPSR_STEAM"],
            EGameCapturePreference.Standalone => ["BPSR"],
            EGameCapturePreference.Epic => ["BPSR_EPIC"],
            EGameCapturePreference.HaoPlaySea => ["StarSEA"],
            EGameCapturePreference.XDG => ["StarASIA"],
            EGameCapturePreference.HaoPlaySeaSteam => ["StarSEA_STEAM"],
            EGameCapturePreference.XDGSteam => ["StarASIA_STEAM"],
            EGameCapturePreference.WeGame => ["Star"],
            EGameCapturePreference.Custom => [customExeName],
            _ => throw new ArgumentOutOfRangeException(nameof(pref), pref, null)
        };

        return exeNameToCapture;
    }

    /// <summary>ゲームの実行ファイル名(上の一覧の名前)のサーバー。一覧に無い名前(カスタム)は分からない。</summary>
    public static Models.GameServer GameServerFromExeName(string exeName)
    {
        return exeName switch
        {
            "StarASIA" or "StarASIA_STEAM" => Models.GameServer.Asia,
            "Star" => Models.GameServer.China,
            "BPSR" or "BPSR_STEAM" or "BPSR_EPIC" => Models.GameServer.Global,
            "StarSEA" or "StarSEA_STEAM" => Models.GameServer.SoutheastAsia,
            _ => Models.GameServer.Unknown
        };
    }
}
