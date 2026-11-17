// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Zproto;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// ダンジョンで隊長が置くフィールドマーク(技 1101〜1106「フィールドマーク01〜06」)の控え。マップが番号の印で出す。
///
/// <para>
/// マークは持ち主(置いた隊長)の実体のパッシブの技として届く。置くと差分の <c>PassiveSkillInfos</c> に技の番号と位置(<c>TarPos</c>)、
/// 外すと <c>PassiveSkillEndInfos</c> に置いたときのパッシブの技の番号が届く。
/// 出現の一覧(<c>Entity.PassiveSkillInfos</c>)は、その実体の今のパッシブの技の全部として扱い、その持ち主の分を置き換える。
/// </para>
///
/// <para>
/// 持ち主と番号(1〜6)ごとに1つ持つ(ゲームの画面も番号ごとに置いたかを持つ)。同じ番号の新しい情報が来たら置き換える。
/// 持ち主が周りから消えたら、その持ち主の分を外す(外した知らせを受け取れなくなるため。周りに戻れば出現の一覧で戻る)。
/// マップ移動・キャプチャの停止・ログアウトで全部消す。持つのは周りにいる隊長の数 × 6 まで。
/// </para>
///
/// <para>
/// 知らせは出さない。マップが自分の更新のときに読む(<see cref="EntityPositionStore"/> と同じ)。
/// </para>
/// </summary>
public sealed class FieldMarkStore
{
    private const int FirstSkillId = 1101;
    private const int LastSkillId = 1106;

    private static readonly Lazy<FieldMarkStore> LazyInstance = new(() => new FieldMarkStore());

    private readonly object _sync = new();
    private readonly Dictionary<(long OwnerUuid, int Number), FieldMark> _marks = [];
    private readonly List<(long OwnerUuid, int Number)> _removingKeys = [];

    private FieldMarkStore()
    {
    }

    public static FieldMarkStore Instance => LazyInstance.Value;

    /// <param name="OwnerUuid">置いた人(隊長)の実体。</param>
    /// <param name="Number">マークの番号(1〜6)。</param>
    /// <param name="PassiveUuid">そのマークのパッシブの技の番号(外したときの知らせで引く)。</param>
    /// <param name="Position">置いた位置。</param>
    public readonly record struct FieldMark(long OwnerUuid, int Number, long PassiveUuid, EntityPositionStore.WorldPosition Position);

    /// <summary>差分のパッシブの技の情報(置いた)と終わり(外した)を当てる。持ち主は差分の実体。</summary>
    public void ApplyDelta(long ownerUuid, SeqPassiveSkillInfo? infos, SeqPassiveSkillEndInfo? ends)
    {
        if (infos is null && ends is null)
        {
            return;
        }

        lock (_sync)
        {
            if (infos is not null)
            {
                foreach (var info in infos.PassiveInfos)
                {
                    TryPut(ownerUuid, info);
                }
            }

            if (ends is not null)
            {
                foreach (var passiveUuid in ends.Uuids)
                {
                    RemoveLocked(ownerUuid, passiveUuid);
                }
            }
        }
    }

    /// <summary>出現の一覧。その持ち主の分を、一覧にあるマークで置き換える(無ければ消す)。</summary>
    public void ApplyAppear(long ownerUuid, SeqPassiveSkillInfo? infos)
    {
        lock (_sync)
        {
            RemoveLocked(ownerUuid, passiveUuid: null);
            if (infos is null)
            {
                return;
            }

            foreach (var info in infos.PassiveInfos)
            {
                TryPut(ownerUuid, info);
            }
        }
    }

    /// <summary>周りから消えた実体の分を外す。</summary>
    public void RemoveOwner(long ownerUuid)
    {
        lock (_sync)
        {
            RemoveLocked(ownerUuid, passiveUuid: null);
        }
    }

    /// <summary>全部消す。マップ移動・キャプチャの停止・ログアウトで呼ぶ。</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _marks.Clear();
        }
    }

    /// <summary>今のマークを <paramref name="target"/> に写す(中身は入れ替える)。呼び手の一覧を使い回し、読むたびに新しく作らない。</summary>
    public void CopyTo(List<FieldMark> target)
    {
        target.Clear();
        lock (_sync)
        {
            foreach (var mark in _marks.Values)
            {
                target.Add(mark);
            }
        }
    }

    private void TryPut(long ownerUuid, PassiveSkillInfo info)
    {
        if (info.SkillId is < FirstSkillId or > LastSkillId || info.TarPos is not { } position)
        {
            return;
        }

        var number = info.SkillId - FirstSkillId + 1;
        _marks[(ownerUuid, number)] = new FieldMark(
            ownerUuid,
            number,
            info.Uuid,
            new EntityPositionStore.WorldPosition(position.X, position.Y, position.Z));
    }

    /// <summary>その持ち主の分を外す。<paramref name="passiveUuid"/> があれば、そのパッシブの技のマークだけ。</summary>
    private void RemoveLocked(long ownerUuid, long? passiveUuid)
    {
        if (_marks.Count == 0)
        {
            return;
        }

        _removingKeys.Clear();
        foreach (var (key, mark) in _marks)
        {
            if (mark.OwnerUuid == ownerUuid && (passiveUuid is null || mark.PassiveUuid == passiveUuid))
            {
                _removingKeys.Add(key);
            }
        }

        foreach (var key in _removingKeys)
        {
            _marks.Remove(key);
        }
    }
}
