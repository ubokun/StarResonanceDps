// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Services;

/// <summary>バフ1つの由来。<see cref="Landing"/> は付与元をたどって着いた先(着かなければ無し)。</summary>
public sealed record BuffOrigin(SourceLanding Landing);

/// <summary>
/// 表に名前の無いバフの由来(<see cref="BuffEvent.Origin"/>)を、付与の通知の最後に決める。
/// 名前は <c>CombatDataCatalog.GetBuffDisplayName</c> がこの由来から引く。
///
/// <para>
/// 付与(<c>BuffInfo</c> を伴う通知)を受けたら一覧に入れ、通知1件(周りの差分・自分宛ての差分・出現)の処理を終えたところで
/// まとめてたどる(<see cref="ResolvePending"/>)。同じ通知の中で親のバフが子より後ろに届いても、たどる時点では親が索引に入っている。
/// 結果はそのバフに持たせ、画面の更新ごとにはたどらない。
/// </para>
///
/// <para>
/// 一覧は通知1件の中だけで使い、たどれば空になる。ログアウト・キャプチャの停止・マップ移動でも空にする(<see cref="Clear"/>)。
/// </para>
/// </summary>
public sealed class BuffOriginResolver
{
    private static readonly Lazy<BuffOriginResolver> LazyInstance = new(() => new BuffOriginResolver());

    private readonly object _sync = new();
    private readonly List<(BuffEvent BuffEvent, DateTime ArrivalTime)> _pending = [];

    private BuffOriginResolver()
    {
    }

    public static BuffOriginResolver Instance => LazyInstance.Value;

    /// <summary>
    /// 付与を受けたバフを一覧に入れる。前の由来は消す(付与のたびに付与元が変わりうる)。
    /// 表に名前のあるバフはたどらない(名前に由来を使わない)。
    /// </summary>
    public void Enqueue(BuffEvent buffEvent, DateTime arrivalTime)
    {
        buffEvent.SetOrigin(null);
        if (buffEvent.BaseId <= 0
            || !string.IsNullOrEmpty(CombatDataCatalog.GetBuffNameWithoutInternalId(buffEvent.BaseId)))
        {
            return;
        }

        lock (_sync)
        {
            _pending.Add((buffEvent, arrivalTime));
        }
    }

    /// <summary>一覧のバフの由来をたどる。通知1件の処理の最後に呼ぶ。到着時刻は付与を受けた時刻を使う。</summary>
    public void ResolvePending()
    {
        (BuffEvent BuffEvent, DateTime ArrivalTime)[] items;
        lock (_sync)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            items = [.. _pending];
            _pending.Clear();
        }

        foreach (var (buffEvent, arrivalTime) in items)
        {
            var landing = SourceLandingResolver.Instance.ResolveBuffOrigin(
                buffEvent.BaseId,
                buffEvent.FightSourceType,
                buffEvent.SourceConfigId,
                buffEvent.FireUuid,
                arrivalTime);
            buffEvent.SetOrigin(new BuffOrigin(landing));
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _pending.Clear();
        }
    }
}
