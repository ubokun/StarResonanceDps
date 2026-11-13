// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes.Skills;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 自分以外のパーティメンバーについて、ライブ経路が存在しない情報だけを保持する。
///
/// <para>
/// キャッシュしてよいのは「他に取得手段が無いもの」だけに限る。名前・レベル・戦闘力・職業・
/// シーズン値・装備・HPはパーティのsocial data(<see cref="PartyMemberSupplement"/>)で
/// AOI外でもライブに届くため、ここには持たない。冗長なコピーを持つと、それがライブ値を
/// 追い越して更新を握り潰す事故になる(旧 EntityCache がまさにそれだった)。
/// </para>
///
/// <para>保持するのは次の3種類だけ。どれもAOI同期でしか届かず、相手がAOI外に出ると取れなくなる:</para>
/// <list type="bullet">
///   <item>
///     <b>習得スキル一覧(AttrSkillLevelIdList)</b> — イマジン/ロールスキルの表示元。
///   </item>
///   <item>
///     <b>職業特化</b> — 特化マーカーバフの観測結果。「特化が確定した」と
///     「アビリティ未装着が確定した」の両方を持つ。
///   </item>
///   <item>
///     <b>シーズンタレントの型</b> — 根ノードのバフの観測結果。「型が確定した」と「無効が確定した」の両方を持つ。
///     特化と同じ扱い。
///   </item>
/// </list>
///
/// <para>
/// 特化キャッシュはスキル一覧と同じ扱い。観測したら入れ、AOI外で観測できない間だけ補完に使い、
/// パーティから外れたら捨てる。以前スキルIDから推定していた頃と違い、いま入るのは
/// マーカーバフで確定した値だけなので、推定値が居座る問題は起きない。
/// アビリティを外したことを観測したときは「未装着」として上書きするので、古い特化も残らない。
/// </para>
///
/// <para>
/// 対象は自分以外のパーティメンバーのみ。自分は常に完全なライブ値が取れるので一切キャッシュしない。
/// パーティから外れたメンバーの分は破棄する。
/// </para>
///
/// <para>
/// 各エントリは観測時刻を持つ。表示には使わないが、古い値を掴んでいないかを追えるようにしておく。
/// </para>
/// </summary>
public sealed class PartyMemberCache
{
    private static readonly Lazy<PartyMemberCache> LazyInstance = new(() => new PartyMemberCache());

    private readonly object _sync = new();
    private readonly Dictionary<long, CacheEntry> _entriesByCharacterId = [];

    private PartyMemberCache()
    {
        PartyStateStore.Instance.Changed += (_, _) => PruneToCurrentParty();
    }

    public static PartyMemberCache Instance => LazyInstance.Value;

    /// <summary>
    /// マーカーバフで確定した特化を記録する。
    /// </summary>
    public void SetSubProfession(long characterId, int subProfessionId)
    {
        if (subProfessionId <= 0 || !IsCacheableMember(characterId))
        {
            return;
        }

        lock (_sync)
        {
            var entry = GetOrCreateNoLock(characterId);
            entry.SubProfessionId = subProfessionId;
            entry.SpecAbilityUnequipped = false;
            entry.SubProfessionObservedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// アビリティ未装着(クラスR1)が確定したことを記録する。
    ///
    /// <para>
    /// 「マーカーが観測できていない」ではなく「マーカーが無いことを確認した」ときだけ呼ぶこと。
    /// 全バフスナップショットにマーカーが1つも無かった場合と、マーカーの除去を見届けた場合の2つ。
    /// </para>
    /// </summary>
    public void SetSpecAbilityUnequipped(long characterId)
    {
        if (!IsCacheableMember(characterId))
        {
            return;
        }

        lock (_sync)
        {
            var entry = GetOrCreateNoLock(characterId);
            entry.SubProfessionId = 0;
            entry.SpecAbilityUnequipped = true;
            entry.SubProfessionObservedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// 職業が変わったなど、記録している特化が無効になったときに捨てる。
    /// 「観測できなくなった」だけのときは呼ばない(それはAOI外の通常状態で、補完すべき場面そのもの)。
    /// </summary>
    public void ClearSubProfession(long characterId)
    {
        if (characterId <= 0)
        {
            return;
        }

        lock (_sync)
        {
            if (_entriesByCharacterId.TryGetValue(characterId, out var entry))
            {
                entry.SubProfessionId = 0;
                entry.SpecAbilityUnequipped = false;
                entry.SubProfessionObservedAt = default;
            }
        }
    }

    public bool TryGetSubProfession(long characterId, out int subProfessionId)
    {
        subProfessionId = 0;
        if (!IsCacheableMember(characterId))
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
        if (!IsCacheableMember(characterId))
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
    /// 根ノードのバフで確定した、有効化しているシーズンタレントの型を記録する(鍵は根ノードのバフID)。
    /// </summary>
    public void SetSeasonTalent(long characterId, int rootBuffId)
    {
        if (rootBuffId <= 0 || !IsCacheableMember(characterId))
        {
            return;
        }

        lock (_sync)
        {
            var entry = GetOrCreateNoLock(characterId);
            entry.SeasonTalentBuffId = rootBuffId;
            entry.SeasonTalentInactive = false;
            entry.SeasonTalentObservedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// シーズンタレントの型が無効(どの型も有効化していない)と確定したことを記録する。
    /// 全バフスナップショットに根ノードのバフが無かった場合と、その除去を見届けた場合だけ呼ぶ。
    /// </summary>
    public void SetSeasonTalentInactive(long characterId)
    {
        if (!IsCacheableMember(characterId))
        {
            return;
        }

        lock (_sync)
        {
            var entry = GetOrCreateNoLock(characterId);
            entry.SeasonTalentBuffId = 0;
            entry.SeasonTalentInactive = true;
            entry.SeasonTalentObservedAt = DateTime.Now;
        }
    }

    public bool TryGetSeasonTalent(long characterId, out int rootBuffId)
    {
        rootBuffId = 0;
        if (!IsCacheableMember(characterId))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_entriesByCharacterId.TryGetValue(characterId, out var entry)
                || entry.SeasonTalentBuffId <= 0)
            {
                return false;
            }

            rootBuffId = entry.SeasonTalentBuffId;
            return true;
        }
    }

    public bool IsSeasonTalentInactive(long characterId)
    {
        if (!IsCacheableMember(characterId))
        {
            return false;
        }

        lock (_sync)
        {
            return _entriesByCharacterId.TryGetValue(characterId, out var entry)
                && entry.SeasonTalentInactive;
        }
    }

    public void SetSkillLevels(long characterId, IReadOnlyList<SkillLevelInfo> skillLevels)
    {
        if (skillLevels is null || skillLevels.Count == 0 || !IsCacheableMember(characterId))
        {
            return;
        }

        lock (_sync)
        {
            var entry = GetOrCreateNoLock(characterId);
            entry.SkillLevels = [.. skillLevels];
            entry.SkillLevelsObservedAt = DateTime.Now;
        }
    }

    public bool TryGetSkillLevels(long characterId, out IReadOnlyList<SkillLevelInfo> skillLevels)
    {
        skillLevels = Array.Empty<SkillLevelInfo>();
        if (!IsCacheableMember(characterId))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_entriesByCharacterId.TryGetValue(characterId, out var entry)
                || entry.SkillLevels.Count == 0)
            {
                return false;
            }

            skillLevels = entry.SkillLevels;
            return true;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entriesByCharacterId.Clear();
        }
    }

    /// <summary>
    /// パーティから外れたメンバーの分を破棄する。
    /// 編成が確定しているときだけ判断する。マップ切替などで一時的に「不明」になっている間に
    /// 破棄すると、本当は在籍しているメンバーの補完値まで失われる。
    /// </summary>
    public void PruneToCurrentParty()
    {
        var party = PartyStateStore.Instance.Current;
        if (!party.HasCompleteMembership)
        {
            return;
        }

        var selfCharacterId = AppState.PlayerUID;

        lock (_sync)
        {
            var staleCharacterIds = _entriesByCharacterId.Keys
                .Where(characterId => characterId == selfCharacterId
                    || party.GetMembership(characterId) == PartyMembershipState.NonMember)
                .ToArray();
            foreach (var characterId in staleCharacterIds)
            {
                _entriesByCharacterId.Remove(characterId);
            }
        }
    }


    /// <summary>
    /// キャッシュ対象かどうか。自分は対象外、パーティメンバー以外も対象外。
    /// 読み書きの両方でこれを通すので、パーティ外の情報が入ることも出ることもない。
    /// </summary>
    private static bool IsCacheableMember(long characterId)
    {
        if (characterId <= 0 || characterId == AppState.PlayerUID)
        {
            return false;
        }

        // 「パーティ外と分かっている」場合だけ弾く。
        // マップ切替直後などで編成が未確定(Unknown)の間は、在籍している可能性があるので通す。
        // ここで弾くと、補完が必要なまさにその場面で読み書きの両方が止まる。
        return PartyStateStore.Instance.Current.GetMembership(characterId)
            != PartyMembershipState.NonMember;
    }

    private CacheEntry GetOrCreateNoLock(long characterId)
    {
        if (!_entriesByCharacterId.TryGetValue(characterId, out var entry))
        {
            entry = new CacheEntry();
            _entriesByCharacterId.Add(characterId, entry);
        }

        return entry;
    }

    private sealed class CacheEntry
    {
        public int SubProfessionId { get; set; }

        public bool SpecAbilityUnequipped { get; set; }

        public DateTime SubProfessionObservedAt { get; set; }

        public int SeasonTalentBuffId { get; set; }

        public bool SeasonTalentInactive { get; set; }

        public DateTime SeasonTalentObservedAt { get; set; }

        public IReadOnlyList<SkillLevelInfo> SkillLevels { get; set; } = Array.Empty<SkillLevelInfo>();

        public DateTime SkillLevelsObservedAt { get; set; }
    }
}
