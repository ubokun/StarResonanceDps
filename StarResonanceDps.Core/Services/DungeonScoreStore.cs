// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Protocols.Game.Binary;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// ダンジョンの得点(空間調査)の控え。今までに得た得点(<c>DungeonScore.totalScore</c>)と、
/// 今の場所が得点を求めているか(得点の目標が未完了で届いているか。目標は <see cref="DungeonTargetStore"/>)。
/// エンティティリストが、今の場所が得点を求めているかと今の得点を出すのに使う。
///
/// <para>
/// 入場の後に届くダンジョンの全体の同期で置き換え、差分では届いた分だけを重ねる。
/// 全体の同期に得点が無ければ 0(値が 0 の項目は送られない)、差分に得点が無ければ前の値のまま。
/// 途中起動で全体の同期を取っていない間は、得点が届くまで分からない(null)。
/// 目標が変わったら(<see cref="DungeonTargetStore.Changed"/>)、得点を求めているかを決め直す。
/// </para>
///
/// <para>
/// マップに付く値なので、マップ切替・キャプチャの停止・ログアウトで消す。
/// </para>
/// </summary>
public sealed class DungeonScoreStore
{
    private static readonly Lazy<DungeonScoreStore> LazyInstance = new(() => new DungeonScoreStore());

    private readonly object _sync = new();

    private int? _totalScore;
    private DungeonScoreSnapshot _current = DungeonScoreSnapshot.Empty;

    private DungeonScoreStore()
    {
        DungeonTargetStore.Instance.Changed += (_, _) => PublishIfChanged(() => { });
        PublishIfChanged(() => { });
    }

    public static DungeonScoreStore Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public DungeonScoreSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public void ApplyFull(Zproto.DungeonSyncData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        PublishIfChanged(() => _totalScore = data.DungeonScore?.TotalScore ?? 0);
    }

    public void ApplyDirty(DungeonDirtyData dirty)
    {
        ArgumentNullException.ThrowIfNull(dirty);

        if (dirty.Score?.TotalScore is not int totalScore)
        {
            return;
        }

        PublishIfChanged(() => _totalScore = totalScore);
    }

    public void Clear()
    {
        PublishIfChanged(() => _totalScore = null);
    }

    private void PublishIfChanged(Action update)
    {
        lock (_sync)
        {
            update();

            var next = new DungeonScoreSnapshot(
                DungeonTargetStore.Instance.HasIncompleteTarget(CombatDataCatalog.IsDungeonScoreTarget),
                _totalScore);
            if (next == _current)
            {
                return;
            }

            _current = next;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <param name="IsScoreRequested">今の場所が得点を求めているか(得点の目標が未完了で届いている)。</param>
/// <param name="TotalScore">今までに得た得点。分からなければ null。</param>
public sealed record DungeonScoreSnapshot(bool IsScoreRequested, int? TotalScore)
{
    public static DungeonScoreSnapshot Empty { get; } = new(false, null);
}
