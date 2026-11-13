// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Zproto;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// エンティティの窓とカードの選択が相手として持つ個体の値。
/// エンティティリストの行(<see cref="EntityListEntry"/>)は位置に結びついていて別の個体に替わるので、行そのものを持たない。
/// </summary>
/// <param name="EntityUuid">実体のID。再起動で変わる。</param>
/// <param name="EntityId">種別ID(<c>AttrId</c>)。再起動をまたいでも同じ値。</param>
/// <param name="EntityType">実体の種類。<paramref name="EntityId"/> がどの表の番号かを決める。</param>
/// <param name="Name">書式を通していない素の名前(<see cref="EntityListEntry.Name"/>)。</param>
/// <param name="Level">レベル。</param>
public sealed record EntityTargetInfo(long EntityUuid, long EntityId, EEntityType EntityType, string Name, int Level)
{
    /// <summary>種別ID(<c>AttrId</c>)が分からない(アプリの起動前から居た)実体か。</summary>
    public bool IsTypeUnknown => EntityId == 0;
}
