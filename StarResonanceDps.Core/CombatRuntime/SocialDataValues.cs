// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.Models;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// ソーシャルデータから取り出したプレイヤーの値。名刺照会の応答・自分宛てのソーシャルデータ(<see cref="SocialData"/>)と
/// パーティ情報(<see cref="TeamMemberSocialData"/>)は、同じ部品(基本・職業・能力値・装備)で届くので、取り出しはここ1つにする。
///
/// <para>
/// 照会の種類(mask)によって入っている部分が違う。入っていない部分は空(名前は空文字、数値は 0、装備は null、NPC の印は null)で返し、
/// 前の値を残すかは受け取る側が決める。
/// </para>
/// </summary>
internal readonly record struct SocialDataValues(
    string Name,
    int Level,
    int SeasonLevel,
    int ProfessionId,
    int CombatPower,
    int SeasonStrength,
    PlayerEquipmentData? EquipmentData,
    bool? IsNpc)
{
    public static SocialDataValues Read(SocialData socialData)
    {
        return Read(socialData.BasicData, socialData.ProfessionData, socialData.UserAttrData, socialData.EquipData);
    }

    public static SocialDataValues Read(TeamMemberSocialData socialData)
    {
        return Read(socialData.BasicData, socialData.ProfessionData, socialData.UserAttrData, socialData.EquipData);
    }

    private static SocialDataValues Read(
        BasicData? basicData,
        ProfessionData? professionData,
        UserAttrData? userAttrData,
        EquipData? equipData)
    {
        var equipInfos = equipData?.EquipInfos;
        return new SocialDataValues(
            basicData?.Name ?? string.Empty,
            basicData?.Level > 0 ? basicData.Level : 0,
            basicData?.SeasonLevel > 0 ? basicData.SeasonLevel : 0,
            professionData?.ProfessionId > 0 ? professionData.ProfessionId : 0,
            userAttrData?.FightPoint > 0 ? ToInt32Saturating(userAttrData.FightPoint) : 0,
            userAttrData?.SeasonStrength > 0 ? userAttrData.SeasonStrength : 0,
            equipInfos is { Count: > 0 }
                ? PlayerEquipmentData.Create(equipInfos.Select(item => new PlayerEquipmentItem(item.Slot, item.EquipID)))
                : null,
            basicData is null ? null : basicData.BotAiId > 0);
    }

    private static int ToInt32Saturating(long value)
    {
        return value > int.MaxValue ? int.MaxValue : (int)value;
    }
}
