// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知の対象。エンティティ詠唱はスキル、プレイヤー出現はプレイヤー、エンティティ出現はエンティティで、通知タブの「対象〇〇名」と「対象〇〇ID」(どちらも複数を並べられる)。
/// IDが1つでもあればIDだけで比べ、どれかに一致すれば当たる(名前は見ない)。IDが無ければ名前を前後の空白を除いて、大文字小文字を区別せずに完全一致で比べ、
/// どれかに一致すれば当たる(部分一致だと別の名前まで当たる)。
/// 中身(名前とIDの並び)が同じなら等しい(見張りが、対象を変えたときだけ控え直すため)。
/// </summary>
public sealed class NotificationWatchTarget : IEquatable<NotificationWatchTarget>
{
    private NotificationWatchTarget(IReadOnlyList<string> names, IReadOnlyList<long> ids)
    {
        Names = names;
        Ids = ids;
    }

    /// <summary>対象の名前(前後の空白を除いたもの)。IDで比べるときは使わない。</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>対象のID。空ならIDは入力されていない。</summary>
    public IReadOnlyList<long> Ids { get; }

    /// <summary>項目の対象。オフか、名前もIDも無ければ null(判定を動かさない)。</summary>
    public static NotificationWatchTarget? From(NotificationItemConfig item)
    {
        var names = item.TargetNames
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .ToArray();
        return item.IsEnabled && (item.TargetIds.Count > 0 || names.Length > 0)
            ? new NotificationWatchTarget(names, [.. item.TargetIds])
            : null;
    }

    /// <param name="getId">相手のID。</param>
    /// <param name="getName">相手の名前。IDで比べるときは引かない。名前が無ければ空。</param>
    public bool Matches<T>(T item, Func<T, long> getId, Func<T, string> getName)
    {
        if (Ids.Count > 0)
        {
            return Ids.Contains(getId(item));
        }

        var name = getName(item).Trim();
        return Names.Any(targetName => string.Equals(name, targetName, StringComparison.OrdinalIgnoreCase));
    }

    public bool Equals(NotificationWatchTarget? other)
    {
        return other is not null
            && Names.SequenceEqual(other.Names, StringComparer.Ordinal)
            && Ids.SequenceEqual(other.Ids);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as NotificationWatchTarget);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var name in Names)
        {
            hash.Add(name, StringComparer.Ordinal);
        }

        foreach (var id in Ids)
        {
            hash.Add(id);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(NotificationWatchTarget? left, NotificationWatchTarget? right)
    {
        return left is null ? right is null : left.Equals(right);
    }

    public static bool operator !=(NotificationWatchTarget? left, NotificationWatchTarget? right)
    {
        return !(left == right);
    }
}
