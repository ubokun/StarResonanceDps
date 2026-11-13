// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// 警告の技(戦闘画面に警告が出る技)の開始の控え。予兆技の通知が読む。
///
/// <para>
/// タイムラインの詠唱の行とは別に持つ。詠唱の行は記録の門(計測の窓・非戦闘エリアでの集計の停止)で止まるが、通知は止めないため。
/// 詠唱者の決め方(召喚体は大元の召喚者か、名前のある召喚体自身)と、どの開始を控えるかは詠唱の行と同じ(<c>Encounter.AddSkillCast</c>)。
/// ダンジョンが終わった後(<c>BattleStateMachine.IsDungeonFinished</c>)の開始は、詠唱の行と同じく控えない。
/// 詠唱者は控えた時点の実体から決めておく(後でエンカウンターが作り直されても名前が変わらないように)。
/// </para>
///
/// <para>
/// 読み手は通し番号で続きから読む。番号は消しても戻さない。マップに付くので、マップ切替・キャプチャの停止・ログアウトで消す。
/// </para>
/// </summary>
public sealed class WarningSkillCastStore
{
    private static readonly Lazy<WarningSkillCastStore> LazyInstance = new(() => new WarningSkillCastStore());

    private readonly object _sync = new();
    private readonly List<WarningSkillCast> _casts = [];
    private long _lastSequence;

    private WarningSkillCastStore()
    {
    }

    public static WarningSkillCastStore Instance => LazyInstance.Value;

    /// <param name="Sequence">控えた順の通し番号(1から)。</param>
    /// <param name="Caster">詠唱者(タイムラインの詠唱の行と同じ決め方)。</param>
    /// <param name="SkillId">技ID。</param>
    /// <param name="ArrivalUtc">開始が届いたメッセージの到着時刻。</param>
    public readonly record struct WarningSkillCast(long Sequence, CombatEventLogParty Caster, int SkillId, DateTime ArrivalUtc);

    public void Add(CombatEventLogParty caster, int skillId, DateTime arrivalUtc)
    {
        lock (_sync)
        {
            _lastSequence++;
            _casts.Add(new WarningSkillCast(_lastSequence, caster, skillId, arrivalUtc));
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _casts.Clear();
        }
    }

    /// <summary>これまでに控えた最後の通し番号。読み始めの位置に使う(それより前は読まない)。</summary>
    public long LastSequence
    {
        get
        {
            lock (_sync)
            {
                return _lastSequence;
            }
        }
    }

    /// <summary><paramref name="afterSequence"/> より後に控えた開始。</summary>
    public IReadOnlyList<WarningSkillCast> GetAfter(long afterSequence)
    {
        lock (_sync)
        {
            return _casts
                .Where(cast => cast.Sequence > afterSequence)
                .ToList();
        }
    }
}
