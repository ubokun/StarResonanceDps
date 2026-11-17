// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 1つの対象の1つのバフの通知(バフ・デバフ効果切れ・薬剤・料理効果切れ)の判定。
/// 開いているバフ・デバフカードとバフ・デバフグループと通知の見張り(自分の料理・薬剤)が、1秒ごとの更新で見つけたバフを渡す。
/// 同じ対象とバフを見ているものは、<see cref="BuffNotificationRegistry"/> を通してこの判定を1つ共有する
/// (先に気づいたものが通知し、後のものは同じ状態を見るので通知しない)。
///
/// <para>
/// <b>バフ・デバフ効果切れ</b>は、窓が見ている間だけ、通知タブの通知条件で決めた時機に出す。持続の無いバフと、残りが分からないバフは出さない。
/// 付与者プレイヤー名は、最後の観測の付与者。
/// </para>
///
/// <para>
/// 通知条件が「切れるとき」(0秒)なら、前の観測で残りが1秒以下だったバフが、次の観測で消えていたら出す。
/// バフが消える通知に時間切れか解除かの理由は無いので、残り時間で決める。1秒はカードとグループの更新間隔。
/// 残りがあるのに消えた(解除・死亡・マップ移動・周りから外れた)バフは出さない。
/// </para>
///
/// <para>
/// 「切れる1〜3秒前」(N秒)なら、観測のたびに切れる予定の時刻(今＋残り)を控え、残りがN秒より長い観測で構える。
/// 観測は1秒ごとなので、予定の時刻のN秒前を過ぎたかは <see cref="BuffNotificationRegistry"/> が短い間隔で確かめ(<see cref="PollExpiryLead"/>)、過ぎたら1回出す。
/// 延長・掛け直しで残りがN秒より長く戻れば構え直す。残りがN秒より長いのを一度も見なかったバフ(持続がN秒以下・見始めたときに既に切っていた)には出さない。
/// 消えたときは何も出さない(予定の時刻の前に消えたなら解除なので出さず、後なら既に出している)。
/// 通知条件を変えたら、出さずに次の観測の残りから構えを決め直す。
/// </para>
///
/// <para>
/// <b>薬剤・料理効果切れ</b>: 今のバフが料理・薬剤のとき、残りが通知条件の分数(通知タブで選ぶ)を上から下回った観測で1回出す。
/// その分数を超えたら次も出す。見始めた時点で既に下回っていれば出さない。通知条件を変えたら、出さずにその時点の残りから見直す。
/// </para>
/// </summary>
public sealed class BuffNotificationTracker
{
    private const double ExpiryWindowSeconds = 1.0;

    private bool _hasLastBuff;
    private double? _lastRemainingSeconds;
    private string _lastBuffName = string.Empty;
    private string _lastTargetName = string.Empty;
    private string _lastCasterName = string.Empty;
    private bool _isCuisinePotionArmed;

    /// <summary>前の観測の通知条件の秒数。今の観測と違えば(通知条件を変えた)、出さずに構えを決め直す。</summary>
    private double _cuisinePotionArmedSeconds;

    /// <summary>効果切れの N秒前を待っているか(残りが N秒より長いのを見た後で、まだ出していない)。</summary>
    private bool _isExpiryLeadArmed;

    /// <summary>構えたときの通知条件の秒数。今の設定と違えば出さない(次の観測で構えを決め直す)。</summary>
    private int _expiryLeadArmedSeconds;

    /// <summary>最後の観測で控えた、切れる予定の時刻(UTC)。</summary>
    private DateTime _expiresAtUtc;

    /// <summary>効果切れの N秒前を待っているか。<see cref="BuffNotificationRegistry"/> が、待っている判定がある間だけ確かめる。</summary>
    public bool IsExpiryLeadArmed => _isExpiryLeadArmed;

    /// <param name="snapshot">見つけたバフ。消えていれば null。</param>
    /// <param name="targetName">対象の名前。</param>
    /// <param name="notifiesExpiry">効果切れを出すか(窓が見ているか)。</param>
    public void Observe(PlayerBuffSnapshot? snapshot, string targetName, bool notifiesExpiry)
    {
        var expiryLeadSeconds = GetBuffExpiredLeadSeconds();
        if (snapshot is null)
        {
            if (expiryLeadSeconds == 0
                && notifiesExpiry
                && _hasLastBuff
                && _lastRemainingSeconds is { } remaining
                && remaining <= ExpiryWindowSeconds)
            {
                Notify(NotificationKind.BuffExpired, _lastTargetName, _lastBuffName, _lastCasterName);
            }

            Reset();
            return;
        }

        var remainingSeconds = snapshot.IsRemainingUnknown ? null : snapshot.RemainingSeconds;
        var buffName = BuffDebuffCardContent.GetCardBuffName(snapshot);
        var casterName = BuffDebuffCardContent.GetCasterName(snapshot.Caster);

        if (CombatDataCatalog.GetBuffGroup(snapshot.BaseId) is not (BuffGroup.Cuisine or BuffGroup.Potion))
        {
            _isCuisinePotionArmed = false;
        }
        else if (remainingSeconds is { } cuisinePotionRemaining)
        {
            var thresholdSeconds = GetCuisinePotionThresholdSeconds();
            if (cuisinePotionRemaining > thresholdSeconds)
            {
                _isCuisinePotionArmed = true;
            }
            else if (_isCuisinePotionArmed && thresholdSeconds == _cuisinePotionArmedSeconds)
            {
                _isCuisinePotionArmed = false;
                Notify(NotificationKind.CuisinePotionLow, targetName, buffName, casterName);
            }
            else
            {
                _isCuisinePotionArmed = false;
            }

            _cuisinePotionArmedSeconds = thresholdSeconds;
        }

        _hasLastBuff = true;
        _lastRemainingSeconds = remainingSeconds;
        _lastBuffName = buffName;
        _lastTargetName = targetName;
        _lastCasterName = casterName;

        ObserveExpiryLead(remainingSeconds, notifiesExpiry, expiryLeadSeconds);
    }

    /// <summary>
    /// 効果切れの N秒前を過ぎていれば1回出す。待っていないとき・窓が見ていないとき(<paramref name="notifiesExpiry"/>)・
    /// 構えた後に通知条件が変わったときは出さない。
    /// </summary>
    public void PollExpiryLead(DateTime nowUtc, bool notifiesExpiry)
    {
        if (!_isExpiryLeadArmed
            || !notifiesExpiry
            || _expiryLeadArmedSeconds != GetBuffExpiredLeadSeconds()
            || nowUtc < _expiresAtUtc - TimeSpan.FromSeconds(_expiryLeadArmedSeconds))
        {
            return;
        }

        _isExpiryLeadArmed = false;
        Notify(NotificationKind.BuffExpired, _lastTargetName, _lastBuffName, _lastCasterName);
    }

    /// <summary>
    /// 切れる予定の時刻を控え、効果切れの N秒前の構えを決める。残りが N秒より長ければ構える。N秒を切った観測では構えを変えず、構えていればその場で確かめる。
    /// 通知条件を変えたときは構えを解いてから、この観測の残りで決め直す。
    /// </summary>
    private void ObserveExpiryLead(double? remainingSeconds, bool notifiesExpiry, int expiryLeadSeconds)
    {
        if (expiryLeadSeconds == 0 || !notifiesExpiry || remainingSeconds is not { } remaining)
        {
            _isExpiryLeadArmed = false;
            return;
        }

        var nowUtc = DateTime.UtcNow;
        _expiresAtUtc = nowUtc + TimeSpan.FromSeconds(remaining);
        if (_expiryLeadArmedSeconds != expiryLeadSeconds)
        {
            _isExpiryLeadArmed = false;
            _expiryLeadArmedSeconds = expiryLeadSeconds;
        }

        if (remaining > expiryLeadSeconds)
        {
            _isExpiryLeadArmed = true;
        }

        PollExpiryLead(nowUtc, notifiesExpiry);
    }

    private void Reset()
    {
        _hasLastBuff = false;
        _lastRemainingSeconds = null;
        _lastBuffName = string.Empty;
        _lastTargetName = string.Empty;
        _lastCasterName = string.Empty;
        _isCuisinePotionArmed = false;
        _isExpiryLeadArmed = false;
    }

    private static int GetBuffExpiredLeadSeconds()
    {
        var index = ConfigManager.Instance.GetNotificationItem(NotificationKind.BuffExpired).BuffExpiredConditionIndex;
        return AppConfigDefaults.BuffExpiredLeadSeconds[index];
    }

    private static double GetCuisinePotionThresholdSeconds()
    {
        var index = ConfigManager.Instance.GetNotificationItem(NotificationKind.CuisinePotionLow).CuisinePotionConditionIndex;
        return AppConfigDefaults.CuisinePotionLowMinutes[index] * 60.0;
    }

    private static void Notify(NotificationKind kind, string targetName, string buffName, string casterName)
    {
        NotificationService.Instance.Notify(
            kind,
            NotificationTextFormatter.FormatBuff(NotificationTextFormatter.ResolveFormat(kind), targetName, buffName, casterName));
    }
}
