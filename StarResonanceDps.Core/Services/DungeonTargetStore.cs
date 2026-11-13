// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.Protocols.Game.Binary;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// ダンジョンの目標の控え。届いた目標の番号 → 完了したか(<c>DungeonSyncData.target.targetData</c>)。
/// 得点を求めているか(<see cref="DungeonScoreStore"/>)と、予告のバーを消す時機(未完了の目標が完了する・目標から消える)に使う。
///
/// <para>
/// 入場の後に届くダンジョンの全体の同期で丸ごと置き換え、差分では足す・消す・更新を当てる。
/// 更新に完了の値が無ければ前の値のまま(控えに無い目標なら未完了として足す)。
/// </para>
///
/// <para>
/// マップに付く値なので、マップ切替・キャプチャの停止・ログアウトで消す。
/// </para>
/// </summary>
public sealed class DungeonTargetStore
{
    private static readonly Lazy<DungeonTargetStore> LazyInstance = new(() => new DungeonTargetStore());

    private readonly object _sync = new();

    /// <summary>届いた目標の番号 → 完了したか。</summary>
    private readonly Dictionary<int, bool> _isCompleteByTargetId = [];

    private DungeonTargetStore()
    {
    }

    public static DungeonTargetStore Instance => LazyInstance.Value;

    /// <summary>控えが変わったとき。</summary>
    public event EventHandler? Changed;

    /// <returns>当てる前に未完了だった目標のうち、当てた後に完了したもの・無くなったものの番号。</returns>
    public IReadOnlyList<int> ApplyFull(Zproto.DungeonSyncData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return Apply(() =>
        {
            _isCompleteByTargetId.Clear();
            if (data.Target is { } target)
            {
                foreach (var (targetId, targetData) in target.TargetData)
                {
                    _isCompleteByTargetId[targetId] = targetData.Complete != 0;
                }
            }
        });
    }

    /// <returns>当てる前に未完了だった目標のうち、当てた後に完了したもの・無くなったものの番号。</returns>
    public IReadOnlyList<int> ApplyDirty(DungeonDirtyData dirty)
    {
        ArgumentNullException.ThrowIfNull(dirty);

        if (dirty.Target?.TargetChanges is not { } changes)
        {
            return [];
        }

        return Apply(() =>
        {
            if (changes.ReplacesExisting)
            {
                _isCompleteByTargetId.Clear();
            }

            foreach (var (targetId, targetData) in changes.Added)
            {
                _isCompleteByTargetId[targetId] = targetData.Complete is int complete && complete != 0;
            }

            foreach (var targetId in changes.Removed)
            {
                _isCompleteByTargetId.Remove(targetId);
            }

            foreach (var (targetId, targetData) in changes.Updated)
            {
                if (targetData.Complete is int complete)
                {
                    _isCompleteByTargetId[targetId] = complete != 0;
                }
                else
                {
                    _isCompleteByTargetId.TryAdd(targetId, false);
                }
            }
        });
    }

    /// <summary>条件に合う目標のうち、未完了のものが届いているか。</summary>
    public bool HasIncompleteTarget(Func<int, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        lock (_sync)
        {
            return _isCompleteByTargetId.Any(pair => !pair.Value && predicate(pair.Key));
        }
    }

    public void Clear()
    {
        Apply(_isCompleteByTargetId.Clear);
    }

    private IReadOnlyList<int> Apply(Action update)
    {
        List<int> progressedTargetIds;
        bool changed;
        lock (_sync)
        {
            var before = new Dictionary<int, bool>(_isCompleteByTargetId);
            update();

            progressedTargetIds = before
                .Where(pair => !pair.Value
                    && (!_isCompleteByTargetId.TryGetValue(pair.Key, out var isComplete) || isComplete))
                .Select(pair => pair.Key)
                .ToList();
            changed = before.Count != _isCompleteByTargetId.Count
                || before.Any(pair => !_isCompleteByTargetId.TryGetValue(pair.Key, out var isComplete) || isComplete != pair.Value);
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return progressedTargetIds;
    }
}
