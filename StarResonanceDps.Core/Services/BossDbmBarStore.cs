// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Services;

/// <summary>
/// ボス大技の予告のバーの控え。鍵は予告の番号(<c>DbmTable.Id</c>)。予兆技の通知がバーの消える時刻に通知するのと、
/// タイムラインがバーの終わりの行を残すの(<c>EncounterManager.RecordEndedAnnouncementBars</c>)に使う。
///
/// <para>
/// ゲームと同じ決め方をする。バーの終わりは予告の通知の到着時刻 + 持続の秒数(持続が 0 なら予告の表の <c>CountCDTime</c>)。
/// 同じ番号の予告が来たら上書きし、番号・持続・insertion が全部 0 の通知で全部消す。0 になっても技とは結び付けない。
/// </para>
///
/// <para>
/// マップに付くバーなので、マップ切替・計測の停止・ログアウトで消す。全滅でも消す(ゲームは全滅でも予告を消す通知を送らない)。ダンジョンの終了でも消し、終わった後に届いた予告は控えない(<c>BattleStateMachine.IsDungeonFinished</c>)。
/// ダンジョンの未完了の目標が完了する・目標から消えるとき(ボスの撃破と段の切り替わり)も消す(<see cref="DungeonTargetStore"/>。ゲームはそのときも予告を消す通知を送らない)。
/// それ以外のエンカウンターの作り直しでは消さない(区切りをまたいでバーが続く)。
/// </para>
/// </summary>
public sealed class BossDbmBarStore
{
    private static readonly Lazy<BossDbmBarStore> LazyInstance = new(() => new BossDbmBarStore());

    private readonly object _sync = new();
    private readonly Dictionary<int, BossDbmBar> _barByDbmId = [];

    private BossDbmBarStore()
    {
    }

    public static BossDbmBarStore Instance => LazyInstance.Value;

    /// <param name="DbmId">予告の番号。</param>
    /// <param name="SkillId">予告の番号から引いた技ID。引けなければ 0。</param>
    /// <param name="OwnerMonsterId">予告が届いた時点で周囲にいる、その技を持つモンスターの種別ID。見つからなければ 0(タイムラインの予告の行と同じ決め方)。</param>
    /// <param name="EndTimeUtc">バーが消える時刻(UTC)。</param>
    public readonly record struct BossDbmBar(int DbmId, int SkillId, int OwnerMonsterId, DateTime EndTimeUtc);

    public void Set(BossDbmBar bar)
    {
        lock (_sync)
        {
            _barByDbmId[bar.DbmId] = bar;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _barByDbmId.Clear();
        }
    }

    /// <summary><paramref name="afterUtc"/> より後、<paramref name="untilUtc"/> まで(含む)に消えるバー。</summary>
    public IReadOnlyList<BossDbmBar> GetEndedBetween(DateTime afterUtc, DateTime untilUtc)
    {
        lock (_sync)
        {
            return _barByDbmId.Values
                .Where(bar => bar.EndTimeUtc > afterUtc && bar.EndTimeUtc <= untilUtc)
                .OrderBy(bar => bar.EndTimeUtc)
                .ToList();
        }
    }
}
