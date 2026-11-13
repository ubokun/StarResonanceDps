// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.Models;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>顔写真・名刺の半身写真の状態。写真はゲームの審査を通ったものだけ使う。</summary>
/// <param name="AvatarId">
/// 顔の種類。0 か 1 はアップロードした写真、それ以外はゲームの用意したアイコン(プロフィールの絵の表の種類1の番号。<see cref="CombatDataCatalog.GetAvatarPresetImage"/>)。
/// </param>
public sealed record SocialAvatarState(
    int AvatarId,
    string ProfileUrl,
    bool IsProfileReviewed,
    string HalfBodyUrl,
    bool IsHalfBodyReviewed);

/// <summary>名刺照会の応答から控えた、相手ごとの値。届いていない値は空(名前は空文字、数値は 0、装備と写真は null)。</summary>
public sealed record SocialProfile(
    string Name,
    int Level,
    int SeasonLevel,
    int ProfessionId,
    int CombatPower,
    int SeasonStrength,
    PlayerEquipmentData? EquipmentData,
    SocialAvatarState? Avatar)
{
    public static SocialProfile Empty { get; } = new(string.Empty, 0, 0, 0, 0, 0, null, null);
}

/// <summary>
/// 相手ごとの名刺の値(<see cref="SocialProfile"/>)。名刺照会の応答の値を全部控える(<see cref="ApplySocialData"/>)。
/// 顔と名刺の写真は、パーティ情報と自分宛てのソーシャルデータからも控える(<see cref="Apply"/>。ほかの値はパーティの補完と自分の実体が持つ)。
///
/// <para>
/// 名刺照会はゲームが送ったものしか届かないので、相手によっては何も届かない。
/// <b>応答に入っていない部分では前の値を消さない</b>(照会の種類ごとに入る部分が違う)。
/// </para>
/// </summary>
public static class SocialDataStore
{
    private static readonly object StateLock = new();
    private static readonly Dictionary<long, SocialProfile> Profiles = [];

    /// <summary>その相手の値が変わった。引数はキャラID。受信のスレッドから上がる。</summary>
    public static event Action<long>? Changed;

    /// <summary>その相手の名刺の値。まだ何も届いていなければ null。</summary>
    public static SocialProfile? GetProfile(long characterId)
    {
        lock (StateLock)
        {
            return Profiles.GetValueOrDefault(characterId);
        }
    }

    /// <summary>その相手の顔と名刺。まだ届いていなければ null。</summary>
    public static SocialAvatarState? GetAvatar(long characterId)
    {
        return GetProfile(characterId)?.Avatar;
    }

    /// <summary>その相手の名前(名刺照会の応答の <c>basicData.name</c>)。まだ届いていなければ空。</summary>
    public static string GetName(long characterId)
    {
        return GetProfile(characterId)?.Name ?? string.Empty;
    }

    /// <summary>顔と名刺の写真だけを控える(パーティ情報と自分宛てのソーシャルデータ)。</summary>
    /// <param name="avatarInfo">無ければ前の値のまま。</param>
    public static void Apply(long characterId, AvatarInfo? avatarInfo)
    {
        if (characterId <= 0 || avatarInfo is null)
        {
            return;
        }

        var avatar = ToAvatarState(avatarInfo);
        Update(characterId, existing => existing with { Avatar = avatar });
    }

    /// <summary>名刺照会の応答の値を全部控える。応答に入っている部分だけ置き換える。</summary>
    /// <returns>控えの値が変わったか。</returns>
    public static bool ApplySocialData(SocialData socialData)
    {
        if (socialData.CharId <= 0)
        {
            return false;
        }

        var values = SocialDataValues.Read(socialData);
        var avatar = socialData.AvatarInfo is null ? null : ToAvatarState(socialData.AvatarInfo);
        return Update(socialData.CharId, existing => existing with
        {
            Name = values.Name.Length > 0 ? values.Name : existing.Name,
            Level = values.Level > 0 ? values.Level : existing.Level,
            SeasonLevel = values.SeasonLevel > 0 ? values.SeasonLevel : existing.SeasonLevel,
            ProfessionId = values.ProfessionId > 0 ? values.ProfessionId : existing.ProfessionId,
            CombatPower = values.CombatPower > 0 ? values.CombatPower : existing.CombatPower,
            SeasonStrength = values.SeasonStrength > 0 ? values.SeasonStrength : existing.SeasonStrength,
            EquipmentData = values.EquipmentData ?? existing.EquipmentData,
            Avatar = avatar ?? existing.Avatar
        });
    }

    private static bool Update(long characterId, Func<SocialProfile, SocialProfile> update)
    {
        bool changed;
        lock (StateLock)
        {
            var existing = Profiles.GetValueOrDefault(characterId) ?? SocialProfile.Empty;
            var next = update(existing);
            changed = !Profiles.ContainsKey(characterId) || existing != next;
            Profiles[characterId] = next;
        }

        if (changed)
        {
            Changed?.Invoke(characterId);
        }

        return changed;
    }

    /// <summary>
    /// 控えを全部消した(キャプチャの停止とログアウト)。表示側はこれを受けて、相手を問わず写真を引き直す。
    /// 停止かログアウトのスレッドから上がる。
    /// </summary>
    public static event Action? Cleared;

    /// <summary>起動時の値に戻す(キャプチャの停止とログアウト)。</summary>
    public static void ResetToStartup()
    {
        lock (StateLock)
        {
            Profiles.Clear();
        }

        Cleared?.Invoke();
    }

    private static SocialAvatarState ToAvatarState(AvatarInfo avatarInfo)
    {
        return new SocialAvatarState(
            avatarInfo.AvatarId,
            avatarInfo.Profile?.Url ?? string.Empty,
            IsReviewed(avatarInfo.Profile),
            avatarInfo.HalfBody?.Url ?? string.Empty,
            IsReviewed(avatarInfo.HalfBody));
    }

    private static bool IsReviewed(PictureInfo? picture)
    {
        return picture?.Verify?.ReviewStartTime == (uint)EPictureReviewType.EpictureReviewed;
    }
}
