// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// マップに出す位置の控え。周りのプレイヤーとモンスター(自分を含む)の位置(<c>AttrPos</c>、52)と向き(<c>AttrDir</c>、50)、
/// プレイヤーのいるエリア(<c>AttrSceneAreaId</c>、231)、パーティの同期(<c>TeamMemberFastSyncData</c>)の位置を持つ。
///
/// <para>
/// 名簿(<see cref="PlayerRosterStore"/>)と周りの実体の一覧(<see cref="NearbyEntityStore"/>)の行には入れない。
/// 位置と向きは動いている間およそ 0.1 秒ごとに届き、行に入れるとそのたびに一覧の知らせが出て、位置を使わない画面まで当て直すことになる。
/// ここは知らせを出さず、位置を出す画面が自分の更新のときに読む。
/// </para>
///
/// <para>
/// エンカウンターとは別に持つ。動かない実体の位置は出現時の一覧でしか届かず、回を作り直すとプレイヤー以外の属性は運ばれないことがある
/// (<see cref="NearbyMonsterIndex"/> と同じ理由)。
/// </para>
///
/// <para>
/// 周りの分は消える通知で外し、マップ移動・キャプチャの停止・ログアウトで全部消す。持つのは周りにいるプレイヤーとモンスターの数まで。
/// </para>
///
/// <para>
/// パーティの分は自分以外の今のメンバーだけを持つ(手放し方は <see cref="PartyMemberCache"/> と同じ)。
/// パーティの編成が変わるたびに、メンバーでないと分かった人の分を外す(<see cref="PrunePartyMembers"/>)。
/// 編成が確定していない間(マップ切り替えの直後など)は外さず、書くのも止めない。
/// ほかに、抜けた知らせ・パーティが無くなったとき・キャプチャの停止・ログアウトでも外す。マップ移動では消さない
/// (位置はシーンの番号つきで、自分の移動と関係なく届く)。持つのはパーティの人数まで。
/// </para>
/// </summary>
public sealed class EntityPositionStore
{
    private static readonly Lazy<EntityPositionStore> LazyInstance = new(() => new EntityPositionStore());

    private readonly object _sync = new();
    private readonly Dictionary<long, WorldPosition> _positionsByEntity = [];
    private readonly Dictionary<long, int> _rawDirectionsByEntity = [];
    private readonly Dictionary<long, int> _sceneAreaIdsByEntity = [];
    private readonly Dictionary<long, PartyMemberLocation> _partyLocationsByCharacter = [];

    private EntityPositionStore()
    {
        PartyStateStore.Instance.Changed += (_, _) => PrunePartyMembers();
    }

    public static EntityPositionStore Instance => LazyInstance.Value;

    /// <summary>世界の位置。水平は <c>X</c> と <c>Z</c>、<c>Y</c> は高さ。</summary>
    public readonly record struct WorldPosition(float X, float Y, float Z);

    /// <param name="SceneId">その人がいるシーンの番号。位置はこのシーンの中の位置。</param>
    /// <param name="SceneAreaId">その人がいるエリアの番号(<c>SceneAreaTable</c>)。届いていなければ 0。</param>
    public readonly record struct PartyMemberLocation(int SceneId, int SceneAreaId, WorldPosition Position);

    /// <summary>周りの実体の位置を置き換える。プレイヤーとモンスター以外(弾・仮想体・ペットなど)は控えない。</summary>
    public void SetPosition(long entityUuid, float x, float y, float z)
    {
        if (!IsTracked(entityUuid))
        {
            return;
        }

        lock (_sync)
        {
            _positionsByEntity[entityUuid] = new WorldPosition(x, y, z);
        }
    }

    public bool TryGetPosition(long entityUuid, out WorldPosition position)
    {
        lock (_sync)
        {
            return _positionsByEntity.TryGetValue(entityUuid, out position);
        }
    }

    /// <summary>
    /// 周りの実体の向きを置き換える。位置と同じく、プレイヤーとモンスター以外は控えない。
    /// 通信の値は 1/100 度(1〜35,998)で、北(<c>+Z</c>)が 0 の時計回り。
    /// </summary>
    public void SetDirection(long entityUuid, int rawDirection)
    {
        if (!IsTracked(entityUuid))
        {
            return;
        }

        lock (_sync)
        {
            _rawDirectionsByEntity[entityUuid] = rawDirection;
        }
    }

    /// <summary>向き(度)。北が 0 の時計回り。届いていなければ false。</summary>
    public bool TryGetFacingDegrees(long entityUuid, out double degrees)
    {
        lock (_sync)
        {
            if (!_rawDirectionsByEntity.TryGetValue(entityUuid, out var rawDirection))
            {
                degrees = 0d;
                return false;
            }

            degrees = rawDirection / 100d;
            return true;
        }
    }

    /// <summary>プレイヤーのいるエリアを置き換える。プレイヤー以外は控えない。</summary>
    public void SetSceneAreaId(long entityUuid, int sceneAreaId)
    {
        if (Utils.UuidToEntityType(entityUuid) != (long)EEntityType.EntChar)
        {
            return;
        }

        lock (_sync)
        {
            _sceneAreaIdsByEntity[entityUuid] = sceneAreaId;
        }
    }

    /// <summary>プレイヤーのいるエリアの番号。届いていなければ 0。</summary>
    public int GetSceneAreaId(long entityUuid)
    {
        lock (_sync)
        {
            return _sceneAreaIdsByEntity.TryGetValue(entityUuid, out var sceneAreaId) ? sceneAreaId : 0;
        }
    }

    /// <summary>周りから消えた実体を外す。</summary>
    public void RemoveEntity(long entityUuid)
    {
        lock (_sync)
        {
            _positionsByEntity.Remove(entityUuid);
            _rawDirectionsByEntity.Remove(entityUuid);
            _sceneAreaIdsByEntity.Remove(entityUuid);
        }
    }

    /// <summary>周りの実体の分(自分を含む)を全部消す。マップ移動で呼ぶ。</summary>
    public void ClearNearby()
    {
        lock (_sync)
        {
            _positionsByEntity.Clear();
            _rawDirectionsByEntity.Clear();
            _sceneAreaIdsByEntity.Clear();
        }
    }

    /// <summary>
    /// パーティの同期で届いた、その人のいるシーンと位置を置き換える。
    /// 自分と、メンバーでないと分かっている人の分は書かない(マップは自分の分をパーティの同期から読まない)。
    /// </summary>
    public void SetPartyMemberLocation(long characterId, int sceneId, int sceneAreaId, float x, float y, float z)
    {
        if (!IsPartyLocationHeld(characterId, PartyStateStore.Instance.Current))
        {
            return;
        }

        lock (_sync)
        {
            _partyLocationsByCharacter[characterId] = new PartyMemberLocation(sceneId, sceneAreaId, new WorldPosition(x, y, z));
        }
    }

    public bool TryGetPartyMemberLocation(long characterId, out PartyMemberLocation location)
    {
        lock (_sync)
        {
            return _partyLocationsByCharacter.TryGetValue(characterId, out location);
        }
    }

    /// <summary>パーティを抜けた人を外す。</summary>
    public void RemovePartyMember(long characterId)
    {
        lock (_sync)
        {
            _partyLocationsByCharacter.Remove(characterId);
        }
    }

    /// <summary>パーティの分を全部消す。パーティが無くなったときに呼ぶ。</summary>
    public void ClearPartyMembers()
    {
        lock (_sync)
        {
            _partyLocationsByCharacter.Clear();
        }
    }

    /// <summary>全部消す。キャプチャの停止とログアウトで呼ぶ。</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _positionsByEntity.Clear();
            _rawDirectionsByEntity.Clear();
            _sceneAreaIdsByEntity.Clear();
            _partyLocationsByCharacter.Clear();
        }
    }

    /// <summary>
    /// パーティの分から、自分とメンバーでないと分かった人を外す。パーティの編成が変わったときに呼ばれる
    /// (HP などの同期では編成の知らせが出ないので、編成が変わったときだけ動く)。
    /// 編成が確定していない間は外さない(<see cref="PartyMemberCache.PruneToCurrentParty"/> と同じ決まり)。
    /// </summary>
    private void PrunePartyMembers()
    {
        var party = PartyStateStore.Instance.Current;
        if (!party.HasCompleteMembership)
        {
            return;
        }

        lock (_sync)
        {
            if (_partyLocationsByCharacter.Count == 0)
            {
                return;
            }

            var staleCharacterIds = new List<long>();
            foreach (var characterId in _partyLocationsByCharacter.Keys)
            {
                if (!IsPartyLocationHeld(characterId, party))
                {
                    staleCharacterIds.Add(characterId);
                }
            }

            foreach (var characterId in staleCharacterIds)
            {
                _partyLocationsByCharacter.Remove(characterId);
            }
        }
    }

    /// <summary>パーティの分に持つ人か。自分は持たない。メンバーでないと分かっている人も持たない(編成が未確定なら持つ)。</summary>
    private static bool IsPartyLocationHeld(long characterId, PartyStateSnapshot party)
    {
        return characterId > 0
            && characterId != AppState.PlayerUID
            && party.GetMembership(characterId) != PartyMembershipState.NonMember;
    }

    private static bool IsTracked(long entityUuid)
    {
        return Utils.UuidToEntityType(entityUuid) is (long)EEntityType.EntChar or (long)EEntityType.EntMonster;
    }
}
