// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Models;

/// <summary>根のバフの種類。表は <c>Data/Generated/RootBuffs.json</c>(<c>DataTools/Core/gen_root_buffs.py</c>)。</summary>
public enum RootBuffKind
{
    /// <summary>タレント(アビリティ情報。特化型を除く)。</summary>
    Talent,

    /// <summary>潜在心相晶の木のノード(型の根とローグライク専用の型の木を除く)。</summary>
    SeasonTalentNode,

    /// <summary>潜在心相晶の上級(型の専用効果を除く)。絵は型ごとに違う。</summary>
    SeasonTalentAdvanced,

    /// <summary>因子。</summary>
    Factor,

    /// <summary>モジュール効果。</summary>
    ModuleEffect
}

/// <summary>
/// プレイヤーに乗っている根のバフ(<see cref="RootBuffKind"/> のどれかに当たるバフ)の番号。昇順・重なりなし。
/// 値で比べる。名簿の行は作り直しのたびに新しく作られるので、中身が同じなら同じ値として扱う。
/// </summary>
public sealed class PlayerRootBuffData : IEquatable<PlayerRootBuffData>
{
    private readonly int[] _buffIds;

    /// <param name="buffIds">昇順・重なりなしの番号。<b>書き換えない配列を渡す</b>(写さずに持つ)。</param>
    private PlayerRootBuffData(int[] buffIds)
    {
        _buffIds = buffIds;
    }

    public IReadOnlyList<int> BuffIds => _buffIds;

    /// <summary>実体の一覧(<c>Entity.RootBuffIds</c>)から作る。実体は一覧を書き換えず差し替えるので、写さずに持つ。</summary>
    public static PlayerRootBuffData FromSorted(int[] buffIds)
    {
        ArgumentNullException.ThrowIfNull(buffIds);
        return new PlayerRootBuffData(buffIds);
    }

    public bool Equals(PlayerRootBuffData? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && (ReferenceEquals(_buffIds, other._buffIds) || _buffIds.AsSpan().SequenceEqual(other._buffIds));
    }

    public override bool Equals(object? obj)
    {
        return obj is PlayerRootBuffData other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var buffId in _buffIds)
        {
            hash.Add(buffId);
        }

        return hash.ToHashCode();
    }
}
