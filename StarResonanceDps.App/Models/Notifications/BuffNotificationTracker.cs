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
/// <b>バフ・デバフ効果切れ</b>: 窓が見ている間だけ、前の観測で残りが1秒以下だったバフが、次の観測で消えていたら出す。
/// バフが消える通知に時間切れか解除かの理由は無いので、残り時間で決める。1秒はカードとグループの更新間隔。
/// 残りがあるのに消えた(解除・死亡・マップ移動・周りから外れた)、持続の無いバフ、残りが分からないバフは出さない。
/// 付与者プレイヤー名は、消える前の最後の観測の付与者。
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

    /// <param name="snapshot">見つけたバフ。消えていれば null。</param>
    /// <param name="targetName">対象の名前。</param>
    /// <param name="notifiesExpiry">効果切れを出すか(窓が見ているか)。</param>
    public void Observe(PlayerBuffSnapshot? snapshot, string targetName, bool notifiesExpiry)
    {
        if (snapshot is null)
        {
            if (notifiesExpiry && _hasLastBuff && _lastRemainingSeconds is { } remaining && remaining <= ExpiryWindowSeconds)
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
    }

    private void Reset()
    {
        _hasLastBuff = false;
        _lastRemainingSeconds = null;
        _lastBuffName = string.Empty;
        _lastTargetName = string.Empty;
        _lastCasterName = string.Empty;
        _isCuisinePotionArmed = false;
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
