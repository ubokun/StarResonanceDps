// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 自分のターゲットの控え。自分が最後に技を向けた敵の実体の UUID。ターゲット情報が映す敵を決めるのに使う。
///
/// <para>
/// ゲームのロックオンは通信に出ない。届くのは自分の <c>AttrTargetId</c>(30)だけで、技を出した瞬間に、技を向けた敵か空(相手の無い技)で変わる(notes/205)。
/// 空と敵以外の値ではターゲットを変えず、最後の敵のままにする(相手の無い技を出すたびにターゲットが外れないように)。
/// </para>
///
/// <para>
/// ゲームはこの値を、敵が倒れた・離れた・戦闘が終わったでは変えない。ターゲットの敵が周りから消えたら(倒れた・離れた)このターゲットを消す(<see cref="ClearIfTarget"/>)。
/// 敵が周りにいる間は、戦闘が終わっても消さない。
/// ゲームは値が変わったときだけ送るので、消した後に同じ敵へ技を出しても値は届かない。自分の技の開始ごとに、自分の実体が持つ今の値を
/// <see cref="ApplyTargetId"/> へ渡し直して戻す(<c>MessageManager.ProcessAttrs</c>)。
/// </para>
///
/// <para>
/// 実体の UUID はマップごとなので、マップ移動・キャプチャの停止・ログアウトで消す。持つのは値1つ。
/// 値が変わったときだけ <see cref="Changed"/> を上げる(パケットのスレッド、ロックの外)。
/// </para>
/// </summary>
public sealed class SelfTargetStore
{
    private static readonly Lazy<SelfTargetStore> LazyInstance = new(() => new SelfTargetStore());

    private readonly object _sync = new();
    private long _targetUuid;

    private SelfTargetStore()
    {
    }

    public static SelfTargetStore Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    /// <summary>自分が最後に技を向けた敵の実体の UUID。まだ無ければ 0。</summary>
    public long TargetUuid
    {
        get
        {
            lock (_sync)
            {
                return _targetUuid;
            }
        }
    }

    /// <summary>自分の <c>AttrTargetId</c> の値を受ける。敵の値だけを採る。</summary>
    public void ApplyTargetId(long targetUuid)
    {
        if (targetUuid == 0 || Utils.UuidToEntityType(targetUuid) != (long)EEntityType.EntMonster)
        {
            return;
        }

        Set(targetUuid);
    }

    public void Clear()
    {
        Set(0);
    }

    /// <summary>周りから消えた実体を受ける。今のターゲットならターゲットを消す。</summary>
    public void ClearIfTarget(long entityUuid)
    {
        lock (_sync)
        {
            if (_targetUuid != entityUuid || entityUuid == 0)
            {
                return;
            }

            _targetUuid = 0;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Set(long targetUuid)
    {
        lock (_sync)
        {
            if (_targetUuid == targetUuid)
            {
                return;
            }

            _targetUuid = targetUuid;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
