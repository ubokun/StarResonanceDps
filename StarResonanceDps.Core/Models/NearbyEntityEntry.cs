// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using Zproto;
using MonsterClassification = StarResonanceDps.Core.CombatRuntime.EMonsterType;

namespace StarResonanceDps.Core.Models;

/// <param name="HasHpBar">
/// ゲーム内でプレイヤーに見える HP バーを持つ実体か。エンティティリストは、これが true なら名前が無くても載せる。
/// 表で判定できない実体(<c>AttrId</c> が未着・表に無い番号)は true。
/// </param>
/// <param name="Score">
/// 倒すとダンジョンの得点(空間調査)に入る点数(<see cref="CombatDataCatalog.GetMonsterScore"/>)。
/// 敵だけで、<c>AttrId</c> が未着・表に無い番号は 0。
/// </param>
/// <param name="IsInCombat">いま戦闘状態か(実体の属性 <c>AttrCombatState</c>、104)。出現時の一覧と周りの差分で届く。</param>
/// <param name="TargetCharacterId">
/// 今狙っている相手のキャラ番号(実体の属性 <c>AttrTargetUuid</c>、450)。相手がプレイヤーでない・いないときは 0。
/// ヘイトの1位とは限らない。
/// </param>
/// <param name="HateRanking">ヘイトの一覧(実体の属性 <c>AttrHateList</c>、474)のプレイヤーだけを、値の大きい順に全員。</param>
/// <param name="Cast">
/// 始めた詠唱(詠唱バーを持つ技)。詠唱していない・始まりの時刻が分からない(出現したときにすでに詠唱していた)ときは null。
/// 全体秒数が過ぎても技が続く間は残るので、詠唱が終わったかは表示側が時刻で決める。
/// </param>
public sealed record NearbyEntityEntry(
    long EntityUuid,
    long EntityId,
    string Name,
    EEntityType EntityType,
    MonsterClassification MonsterType,
    int Level,
    long CurrentHp,
    long MaxHp,
    int CurrentBreak,
    int MaxBreak,
    bool IsInvulnerable,
    bool IsBreakLocked,
    EntityCampRelation CampRelation,
    long CurrentShield,
    bool HasHpBar,
    int Score,
    bool IsInCombat,
    long TargetCharacterId,
    NearbyEntityHateRanking HateRanking,
    NearbyEntityCast? Cast,
    IReadOnlyList<long>? TargetingCharacterIds = null,
    IReadOnlyList<TargetDamageRank>? DamageRanking = null);

/// <summary>
/// 詠唱1回。技の開始(<c>AttrSkillId</c> に値が入った周りの差分)の到着時刻と、技の表の詠唱バーの全体秒数。
/// 同じ技を続けて唱えても始まりの時刻が違うので、別の値になる。
/// </summary>
public readonly record struct NearbyEntityCast(int SkillId, DateTime StartedAtUtc, double TotalSeconds);

/// <summary>ヘイトの一覧の1人。</summary>
public readonly record struct NearbyEntityHateRank(long CharacterId, long HateValue);

/// <summary>
/// ヘイトの一覧(プレイヤーだけ、値の大きい順、全員)。何人まで出すかは画面の設定が決める。
/// 中身が同じなら等しい(行の値が変わらないときに通知を出さないため)。
/// </summary>
public sealed class NearbyEntityHateRanking : IEquatable<NearbyEntityHateRanking>
{
    private readonly NearbyEntityHateRank[] _ranks;

    private NearbyEntityHateRanking(NearbyEntityHateRank[] ranks)
    {
        _ranks = ranks;
    }

    public static NearbyEntityHateRanking Empty { get; } = new([]);

    public int Count => _ranks.Length;

    public NearbyEntityHateRank this[int index] => _ranks[index];

    /// <param name="ranks">値の大きい順に並べた、プレイヤーだけの一覧。</param>
    public static NearbyEntityHateRanking Create(IEnumerable<NearbyEntityHateRank> ranks)
    {
        ArgumentNullException.ThrowIfNull(ranks);

        var items = ranks.ToArray();
        return items.Length == 0 ? Empty : new NearbyEntityHateRanking(items);
    }

    public bool Equals(NearbyEntityHateRanking? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null && _ranks.AsSpan().SequenceEqual(other._ranks);
    }

    public override bool Equals(object? obj)
    {
        return obj is NearbyEntityHateRanking other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var rank in _ranks)
        {
            hash.Add(rank);
        }

        return hash.ToHashCode();
    }
}

public enum EntityCampRelation
{
    NonHostile,
    Friendly,
    Hostile
}
