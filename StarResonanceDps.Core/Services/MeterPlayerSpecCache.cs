// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// AOI外へ出てもメーター・被ダメログ・タイムラインに残り続けるプレイヤーの、職業特化だけを保持する。
///
/// <para>
/// AOIから消えたプレイヤーは <see cref="PlayerRosterProjection"/> が
/// <c>Entity.SetSubProfessionUnknown()</c> で特化を落とす。落とすこと自体は必要で、
/// 次にAOIへ現れたときの全バフスナップショットを権威にするためのリセットになっている
/// (スナップショット側は既存の特化を消さないので、落としておかないと
/// 離れている間にアビリティを外した人が古い特化のまま固定される)。
/// </para>
///
/// <para>
/// ただし <b>エンティティ自体はエンカウンターに残り、メーターには出続ける</b>。
/// パーティメンバーは <see cref="PartyMemberCache"/> が補完するので表に出なかったが、
/// パーティ外は補完先が無く、メーターだけ特化が消えた行になっていた。ここがその補完先。
/// </para>
///
/// <para>
/// <b>寿命はマップ/チャンネル切替まで。</b> パーティ在籍とは無関係なので
/// <see cref="PartyMemberCache"/> とは別の入れ物にしてある。中身も特化だけで、
/// イマジン/ロールスキル(<c>AttrSkillLevelIdList</c>)は持たない。あれはエンティティ属性として
/// 残り、エンカウンター作り直しでも運ばれるので補完が要らない。
/// </para>
///
/// <para>自分は対象外。自分は常にライブ値が取れる。</para>
/// </summary>
public sealed class MeterPlayerSpecCache
{
    private static readonly Lazy<MeterPlayerSpecCache> LazyInstance = new(() => new MeterPlayerSpecCache());

    private readonly object _sync = new();
    private readonly Dictionary<long, CacheEntry> _entriesByCharacterId = [];

    /// <summary>シーズンタレントの型。特化と同じ扱いで、寿命も同じ(マップ/チャンネル切替まで)。</summary>
    private readonly Dictionary<long, SeasonTalentEntry> _seasonTalentsByCharacterId = [];

    private MeterPlayerSpecCache()
    {
    }

    public static MeterPlayerSpecCache Instance => LazyInstance.Value;

    /// <summary>確定した特化を記録する。</summary>
    public void SetSubProfession(long characterId, int subProfessionId)
    {
        if (subProfessionId <= 0 || !IsCacheable(characterId))
        {
            return;
        }

        lock (_sync)
        {
            _entriesByCharacterId[characterId] = new CacheEntry(subProfessionId, false);
        }
    }

    /// <summary>
    /// アビリティ未装着(クラスR1)が確定したことを記録する。
    /// 「観測できていない」ではなく「無いことを確認した」ときだけ呼ぶこと。
    /// </summary>
    public void SetSpecAbilityUnequipped(long characterId)
    {
        if (!IsCacheable(characterId))
        {
            return;
        }

        lock (_sync)
        {
            _entriesByCharacterId[characterId] = new CacheEntry(0, true);
        }
    }

    /// <summary>
    /// 記録を捨てる。特化も未装着も確定していない状態を控えるときに使う。
    /// 書かずに素通りさせると、古い記録が残り続ける。
    /// </summary>
    public void Remove(long characterId)
    {
        if (characterId <= 0)
        {
            return;
        }

        lock (_sync)
        {
            _entriesByCharacterId.Remove(characterId);
        }
    }

    public bool TryGetSubProfession(long characterId, out int subProfessionId)
    {
        subProfessionId = 0;
        if (!IsCacheable(characterId))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_entriesByCharacterId.TryGetValue(characterId, out var entry)
                || entry.SubProfessionId <= 0)
            {
                return false;
            }

            subProfessionId = entry.SubProfessionId;
            return true;
        }
    }

    public bool IsSpecAbilityUnequipped(long characterId)
    {
        if (!IsCacheable(characterId))
        {
            return false;
        }

        lock (_sync)
        {
            return _entriesByCharacterId.TryGetValue(characterId, out var entry)
                && entry.SpecAbilityUnequipped;
        }
    }

    /// <summary>
    /// 確定したシーズンタレントの型(根ノードのバフID)を記録する。特化と同じ扱いで、特化の記録とは別に持つ
    /// (特化の書き込みは記録を丸ごと置き換えるため)。
    /// </summary>
    public void SetSeasonTalent(long characterId, int rootBuffId)
    {
        if (rootBuffId <= 0 || !IsCacheable(characterId))
        {
            return;
        }

        lock (_sync)
        {
            _seasonTalentsByCharacterId[characterId] = new SeasonTalentEntry(rootBuffId, false);
        }
    }

    /// <summary>
    /// シーズンタレントの型が無効と確定したことを記録する。「観測できていない」ではなく「無いことを確認した」ときだけ呼ぶこと。
    /// </summary>
    public void SetSeasonTalentInactive(long characterId)
    {
        if (!IsCacheable(characterId))
        {
            return;
        }

        lock (_sync)
        {
            _seasonTalentsByCharacterId[characterId] = new SeasonTalentEntry(0, true);
        }
    }

    /// <summary>シーズンタレントの記録を捨てる。型も無効も確定していない状態を控えるときに使う。</summary>
    public void RemoveSeasonTalent(long characterId)
    {
        if (characterId <= 0)
        {
            return;
        }

        lock (_sync)
        {
            _seasonTalentsByCharacterId.Remove(characterId);
        }
    }

    public bool TryGetSeasonTalent(long characterId, out int rootBuffId)
    {
        rootBuffId = 0;
        if (!IsCacheable(characterId))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_seasonTalentsByCharacterId.TryGetValue(characterId, out var entry)
                || entry.RootBuffId <= 0)
            {
                return false;
            }

            rootBuffId = entry.RootBuffId;
            return true;
        }
    }

    public bool IsSeasonTalentInactive(long characterId)
    {
        if (!IsCacheable(characterId))
        {
            return false;
        }

        lock (_sync)
        {
            return _seasonTalentsByCharacterId.TryGetValue(characterId, out var entry)
                && entry.Inactive;
        }
    }

    /// <summary>マップ/チャンネル切替で全部捨てる。</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _entriesByCharacterId.Clear();
            _seasonTalentsByCharacterId.Clear();
        }
    }

    /// <summary>
    /// 対象かどうか。自分だけ弾く。読みと書きの両方でこれを通すので、
    /// 自分の情報が入ることも出ることもない。
    /// </summary>
    private static bool IsCacheable(long characterId)
    {
        return characterId > 0 && characterId != AppState.PlayerUID;
    }

    private readonly record struct CacheEntry(int SubProfessionId, bool SpecAbilityUnequipped);

    private readonly record struct SeasonTalentEntry(int RootBuffId, bool Inactive);
}
